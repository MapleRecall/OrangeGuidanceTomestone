#![feature(let_chains)]
#![feature(drain_filter)]

use std::collections::HashMap;
use std::sync::Arc;

use anyhow::{Context, Result};
use sqlx::{Executor, Pool, Sqlite};
use sqlx::migrate::Migrator;
use sqlx::sqlite::{SqliteConnectOptions, SqlitePoolOptions};
use tokio::sync::RwLock;
use uuid::Uuid;

use crate::config::Config;
use crate::pack::Pack;

mod pack;
mod message;
mod web;
mod util;
mod config;
mod task;

static MIGRATOR: Migrator = sqlx::migrate!();

pub struct State {
    pub config: Config,
    pub db: Pool<Sqlite>,
    pub packs: RwLock<HashMap<Uuid, Pack>>,
}

impl State {
    pub async fn update_packs(&self) -> Result<()> {
        let mut packs = HashMap::new();

        let mut dir = tokio::fs::read_dir(&self.config.packs).await?;
        while let Ok(Some(entry)) = dir.next_entry().await {
            if !entry.path().is_file() {
                continue;
            }

            match entry.path().extension().and_then(|x| x.to_str()) {
                Some("yaml") | Some("yml") => {}
                _ => continue,
            }

            let text = match tokio::fs::read_to_string(entry.path()).await {
                Ok(t) => t,
                Err(e) => {
                    eprintln!("error reading pack: {:#?}", e);
                    continue;
                }
            };
            match serde_yaml::from_str::<Pack>(&text) {
                Ok(pack) => {
                    println!("added {}", pack.name);
                    packs.insert(pack.id, pack);
                }
                Err(e) => eprintln!("error parsing pack at {:?}: {:#?}", entry.path(), e),
            }
        }

        *self.packs.write().await = packs;

        Ok(())
    }
}

#[tokio::main]
async fn main() -> Result<()> {
    let args: Vec<String> = std::env::args().skip(1).collect();
    if args.is_empty() {
        eprintln!("usage: server [config]");
        return Ok(());
    }

    let config_str = tokio::fs::read_to_string(&args[0])
        .await
        .with_context(|| format!("could not read config file at {}", args[0]))?;
    let config: Config = toml::from_str(&config_str)
        .context("could not parse config file")?;

    let options = SqliteConnectOptions::new();
    // options.log_statements(LevelFilter::Debug);

    let pool = SqlitePoolOptions::new()
        .after_connect(|conn, _| Box::pin(async move {
            conn.execute(
                // language=sqlite
                "PRAGMA foreign_keys = ON;"
            ).await?;
            Ok(())
        }))
        .connect_with(options.filename(&config.database))
        .await
        .context("could not connect to database")?;
    MIGRATOR.run(&pool)
        .await
        .context("could not run database migrations")?;

    let state = Arc::new(State {
        config,
        db: pool,
        packs: Default::default(),
    });

    println!("adding packs");
    state.update_packs().await?;

    let address = state.config.address;
    println!("listening at {}", address);
    warp::serve(web::routes(state)).run(address).await;
    Ok(())
}
