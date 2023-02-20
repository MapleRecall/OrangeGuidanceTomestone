alter table messages
    add column world int;
create index messages_world_idx on messages (world);
