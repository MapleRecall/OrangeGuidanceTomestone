use std::collections::HashMap;
use std::sync::Arc;

use anyhow::Context;
use rand::distributions::WeightedIndex;
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
        .and_then(move |location: u32, query: HashMap<String, String>, id: i64| logic(Arc::clone(&state), id, location, query))
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
                   v2.vote                                   as user_vote
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
    // let mut nearby = HashMap::with_capacity(messages.len());
    let mut weights = HashMap::with_capacity(messages.len());
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

            // *nearby.entry(&a.id).or_insert(0) += 1;
            nearby += 1;
        }

        if nearby <= 2 {
            // always include groups of three or fewer
            ids.push(a.id.clone());
            continue;
        }

        let score = (a.positive_votes - a.negative_votes).max(0);
        let raw_weight = score as f32 * (1.0 / nearby as f32);
        let weight = raw_weight.trunc() as i64;
        println!("{}: weight {} ({} nearby)", a.id, weight.max(1), nearby);
        weights.insert(a.id.clone(), weight.max(1));
    }

    if weights.is_empty() {
        return;
    }

    let max_weight = weights.values().map(|weight| *weight).max().unwrap();
    messages.drain_filter(|msg| {
        if ids.contains(&msg.id) {
            return false;
        }

        let weight = match weights.get(&msg.id) {
            Some(w) => *w,
            None => return true,
        };

        // weight / max_weight chance of being included (returning true means NOT included)
        !rand::thread_rng().gen_ratio(weight as u32, max_weight as u32)
    });
}
