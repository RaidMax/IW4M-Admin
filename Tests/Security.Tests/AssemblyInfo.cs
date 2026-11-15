// Host-wide statics (plugin bundle registry, event subscriptions) mean fixtures must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
