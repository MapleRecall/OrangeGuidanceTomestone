drop table used_codes;
create table used_codes (
    id text not null references extra_tokens (id) on delete cascade,
    user integer not null references users (id) on delete cascade,

    primary key (id, user)
);
