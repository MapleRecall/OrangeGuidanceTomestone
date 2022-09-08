use std::sync::Arc;

use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::{Pack, State};

pub fn packs(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::get()
        .and(warp::path("packs"))
        .and(warp::path::end())
        .and_then(move || logic(Arc::clone(&state)))
        .boxed()
}

async fn logic(state: Arc<State>) -> Result<impl Reply, Rejection> {
    let mut visible: Vec<Pack> = state.packs.read()
        .await
        .values()
        .filter(|pack| pack.visible)
        .map(|pack| pack.clone())
        .collect();
    visible.sort_unstable_by_key(|pack| pack.order);

    Ok(warp::reply::json(&visible))
}
