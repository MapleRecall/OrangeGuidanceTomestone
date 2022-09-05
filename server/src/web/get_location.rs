use std::collections::HashMap;
use std::sync::Arc;

use anyhow::Context;
use chrono::{Duration, Utc};
use rand::Rng;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::message::RetrievedMessage;
use crate::State;
use crate::web::AnyhowRejection;

pub fn get_location(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::get()
        .and(warp::path("messages"))
        .and(warp::path::param())
        .and(warp::path::end())
        .and(warp::query::<HashMap<String, String>>())
        .and(super::get_id(Arc::clone(&state)))
        .and_then(move |location: u32, query: HashMap<String, String>, (id, _)| logic(Arc::clone(&state), id, location, query))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, location: u32, query: HashMap<String, String>) -> Result<impl Reply, Rejection> {
    // TODO: when we're not just returning all results, make sure own messages are always present
    let filter = query.contains_key("filter");
    let location = location as i64;
    let mut messages = sqlx::query_as!(
        RetrievedMessage,
        // language=sqlite
        r#"
            select m.id,
                   m.x,
                   m.y,
                   m.z,
                   m.yaw,
                   m.message,
                   coalesce(sum(v.vote between 0 and 1), 0)  as positive_votes,
                   coalesce(sum(v.vote between -1 and 0), 0) as negative_votes,
                   v2.vote                                   as user_vote,
                   m.created
            from messages m
                     left join votes v on m.id = v.message
                     left join votes v2 on m.id = v2.message and v2.user = ?
            where m.territory = ?
            group by m.id"#,
        id,
        location,
    )
        .fetch_all(&state.db)
        .await
        .context("could not get messages from database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    if filter {
        filter_messages(&mut messages);
    }
    Ok(warp::reply::json(&messages))
}

fn filter_messages(messages: &mut Vec<RetrievedMessage>) {
    // just count nearby messages. this is O(n^2) but alternatives are hard
    let mut ids = Vec::with_capacity(messages.len());
    for a in messages.iter() {
        let mut nearby = 0;

        for b in messages.iter() {
            if a.id == b.id {
                continue;
            }

            let distance = (a.x - b.x).powi(2)
                + (a.y - b.y).powi(2)
                + (a.z - b.z).powi(2);
            // 7.5 squared
            if distance >= 56.25 {
                continue;
            }

            nearby += 1;
        }

        println!("{} ({} nearby)", a.id, nearby);

        if nearby <= 2 {
            // always include groups of three or fewer
            ids.push(a.id.clone());
            continue;
        }

        let score = (a.positive_votes - a.negative_votes).max(0);
        let time_since_creation = Utc::now().naive_utc().signed_duration_since(a.created) - Duration::weeks(score as i64);
        println!("  time_since_creation: {}", Utc::now().naive_utc().signed_duration_since(a.created));
        println!("  modified: {}", time_since_creation);
        if time_since_creation > Duration::weeks(1) {
            continue;
        }

        // originally thresholds were 6 hours and 2 days
        let brand_new = time_since_creation < Duration::minutes(30);
        let new = time_since_creation < Duration::hours(2);

        let mut numerator = 1;
        if brand_new {
            numerator = nearby;
        } else if new {
            numerator += (nearby / 3).min(1);
        }

        if score > 0 {
            let pad = score as f32 / nearby as f32;
            let rounded = pad.round() as u32;
            numerator += rounded.max(nearby / 2);
        }

        println!("  chance: {}/{}", numerator, nearby * 2);
        if rand::thread_rng().gen_ratio(numerator.min(nearby), nearby * 2) {
            ids.push(a.id.clone());
        }
    }

    messages.drain_filter(|msg| !ids.contains(&msg.id));
}
