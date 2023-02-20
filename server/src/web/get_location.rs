use std::sync::Arc;

use anyhow::Context;
use chrono::{Duration, Utc};
use rand::Rng;
use rand::seq::SliceRandom;
use serde::Deserialize;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::message::RetrievedMessage;
use crate::State;
use crate::util::HOUSING_ZONES;
use crate::web::{AnyhowRejection, WebError};

pub fn get_location(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::get()
        .and(warp::path("messages"))
        .and(warp::path::param())
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and(warp::query::<GetLocationQuery>())
        .and_then(move |location: u32, (id, _), query| logic(Arc::clone(&state), id, location, query))
        .boxed()
}

#[derive(Deserialize)]
pub struct GetLocationQuery {
    #[serde(default)]
    ward: Option<u32>,
}

async fn logic(state: Arc<State>, id: i64, location: u32, query: GetLocationQuery) -> Result<impl Reply, Rejection> {
    let housing = HOUSING_ZONES.contains(&location);
    if housing && query.ward.is_none() {
        return Err(warp::reject::custom(WebError::MissingWard));
    }

    if !housing && query.ward.is_some() {
        return Err(warp::reject::custom(WebError::UnnecessaryWard));
    }

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
                   cast((julianday(current_timestamp) - julianday(u.last_seen)) * 1440 as int) as last_seen_minutes
            from messages m
                     left join votes v on m.id = v.message
                     left join votes v2 on m.id = v2.message and v2.user = ?
                     inner join users u on m.user = u.id
            where m.territory = ? and m.ward is ?
            group by m.id"#,
        id,
        location,
        query.ward,
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
    // remove messages where the user has been offline for over 35 minutes
    // also remove messages with low score (that aren't the from the user)
    messages.drain_filter(|msg| msg.last_seen_minutes >= 35 || (msg.user != id && (msg.positive_votes - msg.negative_votes) < crate::consts::VOTE_THRESHOLD_HIDE));

    // shuffle messages since we'll be excluding later based on messages
    // that have already been included, so this will be more fair
    messages.shuffle(&mut rand::thread_rng());

    // just count nearby messages. this is O(n^2) but alternatives are hard
    let mut ids = Vec::with_capacity(messages.len());
    for a in messages.iter() {
        if a.user == id {
            // always include own messages
            ids.push(a.id.clone());
            continue;
        }

        let mut nearby_ids = Vec::new();
        for b in messages.iter() {
            if a.id == b.id {
                continue;
            }

            let distance = (a.x - b.x).powi(2)
                + (a.y - b.y).powi(2)
                + (a.z - b.z).powi(2);
            // 10 squared
            if distance >= 100.0 {
                continue;
            }

            nearby_ids.push(&b.id);
        }

        let mut nearby = nearby_ids.len() as u32;
        let (numerator, denominator) = if nearby <= 2 {
            // no need to do calculations for groups of three or fewer
            (17, 20)
        } else {
            let already_visible = nearby_ids.iter()
                .filter(|id| ids.contains(id))
                .count();
            if already_visible >= 3 {
                continue;
            }

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
                let rounded = pad.floor() as u32;
                numerator += rounded.max(nearby / 2);
            }

            nearby *= 2;

            if numerator * 5 > nearby * 4 {
                numerator = 4;
                nearby = 5;
            }

            (numerator, nearby)
        };

        if rand::thread_rng().gen_ratio(numerator.min(denominator), denominator) {
            ids.push(a.id.clone());
        }
    }

    messages.drain_filter(|msg| !ids.contains(&msg.id));
}
