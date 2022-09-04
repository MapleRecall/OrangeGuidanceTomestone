use std::sync::Arc;

use anyhow::Context;
use uuid::Uuid;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::message::Message;
use crate::State;
use crate::web::{AnyhowRejection, WebError};

pub fn write(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::post()
        .and(warp::path("messages"))
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and(warp::body::json())
        .and_then(move |id: i64, message: Message| logic(Arc::clone(&state), id, message))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, message: Message) -> Result<impl Reply, Rejection> {
    let text = {
        let packs = state.packs.read().await;
        let pack = packs.get(&message.pack_id)
            .ok_or(WebError::InvalidPackId)
            .map_err(warp::reject::custom)?;
        pack.format(
            message.template_1,
            message.word_1_list.and_then(|list| message.word_1_word.map(|word| (list, word))),
            message.conjunction,
            message.template_2,
            message.word_2_list.and_then(|list| message.word_2_word.map(|word| (list, word))),
        )
            .ok_or(WebError::InvalidIndex)
            .map_err(warp::reject::custom)?
    };

    let existing = sqlx::query!(
        // language=sqlite
        "select count(*) as count from messages where user = ?",
        id
    )
        .fetch_one(&state.db)
        .await
        .context("could not get count of messages")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;

    if existing.count >= 10 {
        return Err(warp::reject::custom(WebError::TooManyMessages));
    }

    let message_id = Uuid::new_v4().simple().to_string();
    let territory = message.territory as i64;

    sqlx::query!(
        // language=sqlite
        "insert into messages (id, user, territory, x, y, z, yaw, message) values (?, ?, ?, ?, ?, ?, ?, ?)",
        message_id,
        id,
        territory,
        message.x,
        message.y,
        message.z,
        message.yaw,
        text,
    )
        .execute(&state.db)
        .await
        .context("could not insert message into database")
        .map_err(AnyhowRejection)
        .map_err(warp::reject::custom)?;
    Ok(message_id)
}
