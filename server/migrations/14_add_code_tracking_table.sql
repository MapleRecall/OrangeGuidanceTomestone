create table used_codes (
    id text not null references extra_codes (id) on delete cascade,
    user integer not null references users (id) on delete cascade
);
