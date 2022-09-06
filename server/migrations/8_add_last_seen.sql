alter table users
    add column last_seen timestamp not null default 0;

-- noinspection SqlWithoutWhere
update users
set last_seen = current_timestamp;
