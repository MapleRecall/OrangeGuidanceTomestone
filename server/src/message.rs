use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Deserialize)]
pub struct Message {
    pub territory: u32,
    pub x: f32,
    pub y: f32,
    pub z: f32,

    pub pack_id: Uuid,
    pub template_1: usize,
    pub word_1_list: Option<usize>,
    pub word_1_word: Option<usize>,
    pub conjunction: Option<usize>,
    pub template_2: Option<usize>,
    pub word_2_list: Option<usize>,
    pub word_2_word: Option<usize>,
}

#[derive(Debug, Serialize)]
pub struct RetrievedMessage {
    pub id: String,
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub message: String,
    pub positive_votes: i32,
    pub negative_votes: i32,
    pub user_vote: i64,
}

#[derive(Debug, Serialize)]
pub struct RetrievedMessageTerritory {
    pub id: String,
    pub territory: i64,
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub message: String,
    pub positive_votes: i32,
    pub negative_votes: i32,
    pub user_vote: i64,
}
