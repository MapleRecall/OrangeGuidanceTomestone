use std::sync::Arc;

use anyhow::Context;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::State;
use crate::web::AnyhowRejection;

pub fn unregister(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::delete()
        .and(warp::path("account"))
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and_then(move |(id, _)| logic(Arc::clone(&state), id))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64) -> Result<impl Reply, Rejection> {
    sqlx::query!(
        // language=sqlite
        "delete from users where id = ?",
        id,
    )
        .execute(&state.db)
        .await
        .context("could not delete user from database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    Ok(warp::reply())
}
