alter table messages
    add column plot int;
create index messages_plot_idx on messages (plot);
