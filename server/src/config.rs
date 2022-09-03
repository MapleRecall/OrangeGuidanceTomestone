use std::net::SocketAddr;
use std::path::PathBuf;

use serde::Deserialize;

#[derive(Debug, Deserialize)]
pub struct Config {
    pub address: SocketAddr,
    pub packs: PathBuf,
    pub database: String,
}
