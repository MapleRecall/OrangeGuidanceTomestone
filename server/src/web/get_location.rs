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
        .and(super::get_id(Arc::clone(&state)))
        .and_then(move |location: u32, (id, _)| logic(Arc::clone(&state), id, location))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, location: u32) -> Result<impl Reply, Rejection> {
    // TODO: when we're not just returning all results, make sure own messages are always present
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
                   m.glyph,
                   m.created,
                   m.user,
                   cast((julianday(current_timestamp) - julianday(created)) * 1440 as int) as last_seen_minutes
            from messages m
                     left join votes v on m.id = v.message
                     left join votes v2 on m.id = v2.message and v2.user = ?
                     inner join users u on m.user = u.id
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
    filter_messages(&mut messages, id);
    Ok(warp::reply::json(&messages))
}

fn filter_messages(messages: &mut Vec<RetrievedMessage>, id: i64) {
    // FIXME: make a migration to fix this, smh I'm dumb
    let id_str = id.to_string();

    // just count nearby messages. this is O(n^2) but alternatives are hard
    let mut ids = Vec::with_capacity(messages.len());
    for a in messages.iter() {
        if a.user == id_str {
            // always include own messages
            ids.push(a.id.clone());
            continue;
        }

        if a.last_seen_minutes >= 35 {
            continue;
        }

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

        let (numerator, denominator) = if nearby <= 2 {
            // no need to do calculations for groups of three or fewer
            (17, 20)
        } else {
            let time_since_creation = a.created.signed_duration_since(Utc::now().naive_utc());
            let brand_new = time_since_creation < Duration::minutes(30);
            let new = time_since_creation < Duration::hours(2);

            let mut numerator = 1;
            if brand_new {
                numerator = nearby;
            } else if new {
                numerator += (nearby / 3).min(1);
            }

            let score = (a.positive_votes - a.negative_votes).max(0);
            if score > 0 {
                let pad = score as f32 / nearby as f32;
                let rounded = pad.round() as u32;
                numerator += rounded.max(nearby / 2);
            }

            nearby *= 2;

            if numerator * 20 > nearby * 17 {
                numerator = 17;
                nearby = 20;
            }

            (numerator, nearby)
        };

        if rand::thread_rng().gen_ratio(numerator.min(denominator), denominator) {
            ids.push(a.id.clone());
        }
    }

    messages.drain_filter(|msg| !ids.contains(&msg.id));
}
