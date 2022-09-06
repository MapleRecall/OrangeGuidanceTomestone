use std::sync::Arc;

use anyhow::Context;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::State;
use crate::web::AnyhowRejection;

pub fn ping(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::post()
        .and(warp::path("ping"))
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and_then(move |(id, _)| logic(Arc::clone(&state), id))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64) -> Result<impl Reply, Rejection> {
    sqlx::query!(
        // language=sqlite
        "update users set last_seen = current_timestamp where id = ?",
        id,
    )
        .execute(&state.db)
        .await
        .context("could not update user")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    Ok(warp::reply())
}
