alter table messages
    drop column glyph;

alter table messages
    add column glyph integer not null default 3;
