use std::sync::Arc;

use anyhow::Context;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::message::{RetrievedMessage, RetrievedMessageTerritory};
use crate::State;
use crate::web::AnyhowRejection;

pub fn get_mine(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::get()
        .and(warp::path("messages"))
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and_then(move |(id, _)| logic(Arc::clone(&state), id))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64) -> Result<impl Reply, Rejection> {
    let messages = sqlx::query_as!(
        RetrievedMessageTerritory,
        // language=sqlite
        r#"
            select m.id,
                   m.territory,
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
            where m.user = ?
            group by m.id"#,
        id,
        id,
    )
        .fetch_all(&state.db)
        .await
        .context("could not get messages from database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    Ok(warp::reply::json(&messages))
}
