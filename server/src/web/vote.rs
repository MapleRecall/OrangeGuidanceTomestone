use std::sync::Arc;

use anyhow::Context;
use uuid::Uuid;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::State;
use crate::web::AnyhowRejection;

pub fn vote(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::patch()
        .and(warp::path("messages"))
        .and(warp::path::param())
        .and(warp::path("votes"))
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and(warp::body::content_length_limit(3))
        .and(warp::body::json())
        .and_then(move |message_id: Uuid, (id, _), vote: i8| logic(Arc::clone(&state), id, message_id, vote))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, message_id: Uuid, vote: i8) -> Result<impl Reply, Rejection> {
    let message_id = message_id.simple().to_string();
    let vote = match vote.signum() {
        -1 => -1,
        0 => 1,
        1 => 1,
        _ => unreachable!(),
    };
    sqlx::query!(
        // language=sqlite
        "insert into votes (user, message, vote) values (?, ?, ?) on conflict do update set vote = ?",
        id,
        message_id,
        vote,
        vote,
    )
        .execute(&state.db)
        .await
        .context("could not insert vote into database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    Ok(warp::reply())
}
