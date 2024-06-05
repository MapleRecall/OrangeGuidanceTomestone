use std::path::PathBuf;

use serde::Deserialize;

#[derive(Debug, Deserialize)]
pub struct Config {
    pub address: String,
    pub packs: PathBuf,
    pub database: String,
    pub vote_threshold_hide: i32,
    pub max_messages: i32,
}
