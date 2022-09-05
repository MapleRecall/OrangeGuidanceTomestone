use std::sync::Arc;

use anyhow::Context;
use bytes::Bytes;
use uuid::Uuid;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::message::RetrievedMessageTerritory;
use crate::State;
use crate::web::{AnyhowRejection, WebError};

pub fn claim(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::post()
        .and(warp::path("claim"))
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and(warp::body::content_length_limit(256))
        .and(warp::body::bytes())
        .and_then(move |(id, _), code: Bytes| logic(Arc::clone(&state), id, code))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, bytes: Bytes) -> Result<impl Reply, Rejection> {
    let bytes: Vec<u8> = bytes.into_iter().collect();
    let code = String::from_utf8(bytes)
        .context("invalid utf8 for extra code")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    let code = sqlx::query!(
        // language=sqlite
        r#"delete from extra_tokens where id = ? returning extra as "extra!: i64""#,
        code,
    )
        .fetch_optional(&state.db)
        .await
        .context("could not get code from database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    if let Some(code) = code {
        sqlx::query!(
            // language=sqlite
            "update users set extra = ? where id = ?",
            code.extra,
            id,
        )
            .execute(&state.db)
            .await
            .context("could not update user")
            .map_err(AnyhowRejection)
            .map_err(warp::reject::custom)?;

        return Ok(code.extra.to_string());
    }

    Err(warp::reject::custom(WebError::InvalidExtraCode))
}
