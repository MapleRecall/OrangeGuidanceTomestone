use std::sync::Arc;

use anyhow::Context;
use uuid::Uuid;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::State;
use crate::web::AnyhowRejection;

pub fn erase(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::delete()
        .and(warp::path("messages"))
        .and(warp::path::param())
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and_then(move |post_id: Uuid, (id, _)| logic(Arc::clone(&state), id, post_id))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, post_id: Uuid) -> Result<impl Reply, Rejection> {
    let post_id = post_id.simple().to_string();
    sqlx::query!(
        // language=sqlite
        "delete from messages where id = ? and user = ?",
        post_id,
        id,
    )
        .execute(&state.db)
        .await
        .context("could not delete message from database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    Ok(warp::reply())
}
