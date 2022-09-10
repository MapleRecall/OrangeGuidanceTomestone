-- 7. Making Other Kinds Of Table Schema Changes
-- https://www.sqlite.org/lang_altertable.html

-- step 0. (we're already in a transaction!)
end transaction;

-- step 1.
pragma foreign_keys = off;

-- step 2.
begin transaction;

-- step 3 skipped (no code for that)

-- step 4.
-- fixing the user column to be an integer instead of string
create table new_messages
(
    id        text      not null primary key,
    user      integer   not null references users (id) on delete cascade,
    created   timestamp not null default current_timestamp,
    territory integer   not null,
    glyph     integer   not null,
    x         float     not null,
    y         float     not null,
    z         float     not null,
    yaw       float     not null,
    message   text      not null
);

-- step 5.
insert into new_messages
select id,
       cast(user as integer),
       created,
       territory,
       glyph,
       x,
       y,
       z,
       yaw,
       message
from messages;

-- step 6.
drop table messages;

-- step 7.
alter table new_messages
    rename to messages;

-- step 8.
-- actually adding a new index here, rather than recreating any old ones
-- only old ones were from the primary key, so don't need to remake
create index messages_user_idx on messages (user);
create index messages_territory_idx on messages (territory);

-- step 9 skipped (no views)

-- step 10.
pragma foreign_key_check;

-- step 11.
commit transaction;

-- step 12.
pragma foreign_keys = on;

-- step 13. (start the transaction from earlier!)
begin transaction;
