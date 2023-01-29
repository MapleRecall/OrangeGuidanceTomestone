alter table messages
    add column ward int;
create index messages_ward_idx on messages (ward);
