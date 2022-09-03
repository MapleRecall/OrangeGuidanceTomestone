create table users
(
    id   integer not null primary key autoincrement,
    auth text    not null
);

create table messages
(
    id        text      not null primary key,
    user      text      not null references users (id) on delete cascade,
    created   timestamp not null default current_timestamp,
    territory integer   not null,
    x         float     not null,
    y         float     not null,
    z         float     not null,
    message   text      not null
);

create table votes
(
    user    text    not null references users (id) on delete cascade,
    message text    not null references messages (id) on delete cascade,
    vote    tinyint not null,
    primary key (user, message)
);

create index votes_user_idx on votes (user);
