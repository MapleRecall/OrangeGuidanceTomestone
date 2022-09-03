#![feature(let_chains)]

use std::collections::HashMap;
use std::net::SocketAddr;
use std::sync::Arc;

use anyhow::{Context, Result};
use sqlx::{Executor, Pool, Sqlite};
use sqlx::migrate::Migrator;
use sqlx::sqlite::{SqliteConnectOptions, SqlitePoolOptions};
use tokio::sync::RwLock;
use uuid::Uuid;

use crate::pack::Pack;

mod pack;
mod message;
mod web;
mod util;

static MIGRATOR: Migrator = sqlx::migrate!();

pub struct State {
    pub db: Pool<Sqlite>,
    pub packs: RwLock<HashMap<Uuid, Pack>>,
}

impl State {
    pub async fn update_packs(&self) -> Result<()> {
        let mut packs = HashMap::new();

        let mut dir = tokio::fs::read_dir("packs").await?;
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
        // .connect_with(options.filename(&config.database.path))
        .connect_with(options.filename("./database.sqlite"))
        .await
        .context("could not connect to database")?;
    MIGRATOR.run(&pool)
        .await
        .context("could not run database migrations")?;

    let state = Arc::new(State {
        db: pool,
        packs: Default::default(),
    });

    state.update_packs().await?;

    warp::serve(web::routes(state)).run("127.0.0.1:8080".parse::<SocketAddr>()?).await;
    Ok(())
}
