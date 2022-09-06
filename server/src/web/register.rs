use std::sync::Arc;

use anyhow::{Context, Result};
use rand::distributions::{Alphanumeric, DistString};
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::State;
use crate::web::AnyhowRejection;

pub fn register(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::post()
        .and(warp::path("account"))
        .and(warp::path::end())
        .and_then(move || logic(Arc::clone(&state)))
        .boxed()
}

async fn logic(state: Arc<State>) -> Result<impl Reply, Rejection> {
    let auth = Alphanumeric.sample_string(&mut rand::thread_rng(), 32);
    let hashed = crate::util::hash(&auth);
    sqlx::query!(
        // language=sqlite
        "insert into users (auth, last_seen) values (?, current_timestamp)",
        hashed,
    )
        .execute(&state.db)
        .await
        .context("could not insert user into database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    Ok(auth)
}
