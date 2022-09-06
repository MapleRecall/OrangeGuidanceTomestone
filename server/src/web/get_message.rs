use std::sync::Arc;

use anyhow::Context;
use uuid::Uuid;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::message::RetrievedMessageTerritory;
use crate::State;
use crate::web::{AnyhowRejection, WebError};

pub fn get_message(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::get()
        .and(warp::path("messages"))
        .and(warp::path::param())
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and_then(move |message_id: Uuid, (id, _)| logic(Arc::clone(&state), id, message_id))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, message_id: Uuid) -> Result<impl Reply, Rejection> {
    let message_id = message_id.simple().to_string();
    let message = sqlx::query_as!(
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
                   v2.vote                                   as user_vote,
                   m.glyph,
                   m.created
            from messages m
                     left join votes v on m.id = v.message
                     left join votes v2 on m.id = v2.message and v2.user = ?
            where m.id = ?
            group by m.id"#,
        id,
        message_id,
    )
        .fetch_optional(&state.db)
        .await
        .context("could not get message from database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    let message = message.ok_or_else(|| warp::reject::custom(WebError::NoSuchMessage))?;
    Ok(warp::reply::json(&message))
}
