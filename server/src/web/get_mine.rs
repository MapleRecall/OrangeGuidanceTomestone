use std::collections::HashMap;
use std::sync::Arc;

use anyhow::Context;
use warp::{Filter, Rejection, Reply};
use warp::filters::BoxedFilter;

use crate::message::OwnMessage;
use crate::State;
use crate::web::AnyhowRejection;

pub fn get_mine(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    warp::get()
        .and(warp::path("messages"))
        .and(warp::path::end())
        .and(super::get_id(Arc::clone(&state)))
        .and(warp::query())
        .and_then(move |(id, extra), query: HashMap<String, String>| logic(Arc::clone(&state), id, extra, query))
        .boxed()
}

async fn logic(state: Arc<State>, id: i64, extra: i64, mut query: HashMap<String, String>) -> Result<impl Reply, Rejection> {
    let version = query.remove("v")
        .unwrap_or_else(|| "1".to_string())
        .parse::<u8>()
        .unwrap_or(1);

    let mut messages = sqlx::query_as!(
        OwnMessage,
        // language=sqlite
        r#"
            select m.id,
                   m.territory,
                   m.world,
                   m.ward,
                   m.plot,
                   m.x,
                   m.y,
                   m.z,
                   m.yaw,
                   m.message,
                   coalesce(sum(v.vote between 0 and 1), 0)  as positive_votes,
                   coalesce(sum(v.vote between -1 and 0), 0) as negative_votes,
                   v2.vote                                   as user_vote,
                   m.glyph,
                   m.created,
                   0 as "is_hidden: bool"
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

    messages.sort_by_key(|msg| msg.created);
    messages.reverse();

    for msg in &mut messages {
        msg.is_hidden = msg.positive_votes - msg.negative_votes < crate::consts::VOTE_THRESHOLD_HIDE;
    }

    if version == 1 {
        return Ok(warp::reply::json(&messages));
    }

    #[derive(serde::Serialize)]
    struct Mine {
        messages: Vec<OwnMessage>,
        extra: i64,
    }

    let mine = Mine {
        messages,
        extra,
    };

    Ok(warp::reply::json(&mine))
}
