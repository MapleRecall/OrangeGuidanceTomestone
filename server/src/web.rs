use std::convert::Infallible;
use std::error::Error;
use std::sync::Arc;

use warp::{Filter, Rejection, Reply};
use warp::body::BodyDeserializeError;
use warp::filters::BoxedFilter;
use warp::http::StatusCode;
use warp::reject::{MethodNotAllowed, Reject};

use crate::State;

mod register;
mod unregister;
mod write;
mod erase;
mod get_location;
mod vote;
mod get_mine;
mod get_message;
mod claim;
mod ping;
mod packs;

pub fn routes(state: Arc<State>) -> BoxedFilter<(impl Reply, )> {
    register::register(Arc::clone(&state))
        .or(unregister::unregister(Arc::clone(&state)))
        .or(write::write(Arc::clone(&state)))
        .or(erase::erase(Arc::clone(&state)))
        .or(vote::vote(Arc::clone(&state)))
        .or(get_message::get_message(Arc::clone(&state)))
        .or(get_location::get_location(Arc::clone(&state)))
        .or(get_mine::get_mine(Arc::clone(&state)))
        .or(claim::claim(Arc::clone(&state)))
        .or(ping::ping(Arc::clone(&state)))
        .or(packs::packs(Arc::clone(&state)))
        .recover(handle_rejection)
        .boxed()
}

pub fn get_id(state: Arc<State>) -> BoxedFilter<((i64, i64), )> {
    warp::cookie("access_token")
        .or(warp::header("x-api-key"))
        .unify()
        .and_then(move |access_token: String| {
            let state = Arc::clone(&state);
            async move {
                let hashed = crate::util::hash(&access_token);
                let id = sqlx::query!(
                    // language=sqlite
                    "select id, extra from users where auth = ?",
                    hashed,
                )
                    .fetch_optional(&state.db)
                    .await;
                match id {
                    Ok(Some(i)) => Ok((i.id, i.extra)),
                    Ok(None) => Err(warp::reject::custom(WebError::InvalidAuthToken)),
                    Err(e) => Err(warp::reject::custom(AnyhowRejection(e.into()))),
                }
            }
        })
        .boxed()
}

#[derive(Debug)]
pub enum WebError {
    InvalidAuthToken,
    InvalidPackId,
    InvalidIndex,
    TooManyMessages,
    NoSuchMessage,
    InvalidExtraCode,
}

impl Reject for WebError {}

#[derive(Debug)]
pub struct AnyhowRejection(anyhow::Error);

impl Reject for AnyhowRejection {}

async fn handle_rejection(err: Rejection) -> Result<impl Reply, Infallible> {
    let (status, name, desc) = if err.is_not_found() {
        (StatusCode::NOT_FOUND, "not_found", "route was unknown to the server")
    } else if let Some(e) = err.find::<BodyDeserializeError>() {
        (StatusCode::BAD_REQUEST, "invalid_body", format!("invalid body: {}", e))
    } else if let Some(_) = err.find::<MethodNotAllowed>() {
        (StatusCode::METHOD_NOT_ALLOWED, "method_not_allowed", "that http method is not allowed on that route")
    } else if let Some(AnyhowRejection(e)) = err.find::<AnyhowRejection>() {
        eprintln!("{:#?}", e);
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            "internal_error",
            "an internal logic error occured",
        )
    } else if let Some(e) = err.find::<WebError>() {
        match e {
            WebError::InvalidAuthToken => (StatusCode::BAD_REQUEST, "invalid_auth_token", "the auth token was not valid"),
            WebError::InvalidPackId => (StatusCode::NOT_FOUND, "invalid_pack_id", "the server does not have a pack registered with that id"),
            WebError::InvalidIndex => (StatusCode::NOT_FOUND, "invalid_index", "one of the provided indices was out of range"),
            WebError::TooManyMessages => (StatusCode::BAD_REQUEST, "too_many_messages", "you have run out of messages - delete one and try again"),
            WebError::NoSuchMessage => (StatusCode::NOT_FOUND, "no_such_message", "no message with that id was found"),
            WebError::InvalidExtraCode => (StatusCode::BAD_REQUEST, "invalid_extra_code", "that extra code was not found"),
        }
    } else {
        eprintln!("{:#?}", err);
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            "internal_error",
            "an unhandled error was encountered",
        )
    };

    #[derive(serde::Serialize)]
    struct ErrorMessage {
        code: &'static str,
        message: &'static str,
    }

    let message = ErrorMessage {
        code: name,
        message: desc,
    };

    Ok(warp::reply::with_status(warp::reply::json(&message), status))
}
