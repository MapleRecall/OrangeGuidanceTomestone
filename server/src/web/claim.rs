use std::sync::Arc;

use anyhow::Context;
use bytes::Bytes;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

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

    let mut t = state.db.begin()
        .await
        .context("could not start transaction")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    let rec = sqlx::query!(
        // language="sqlite"
        r#"update extra_tokens set uses = case uses when -1 then -1 else max(0, uses - 1) end where id = ? returning extra as "extra!: i64""#,
        code,
    )
        .fetch_optional(&mut *t)
        .await
        .context("could not update code in database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    sqlx::query!(
        // language=sqlite
        "delete from extra_tokens where id = ? and uses = 0",
        code,
    )
        .execute(&mut *t)
        .await
        .context("could not attempt to delete expended code")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    t.commit()
        .await
        .context("could not commit transaction")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    if let Some(code) = rec {
        let result = sqlx::query!(
            // language=sqlite
            r#"update users set extra = extra + ? where id = ? returning extra as "extra!: i64""#,
            code.extra,
            id,
        )
            .fetch_one(&state.db)
            .await
            .context("could not update user")
            .map_err(AnyhowRejection)
            .map_err(warp::reject::custom)?;

        return Ok(result.extra.to_string());
    }

    Err(warp::reject::custom(WebError::InvalidExtraCode))
}
