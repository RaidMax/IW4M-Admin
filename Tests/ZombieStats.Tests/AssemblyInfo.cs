// The host exposes process-wide event subscriptions; lifecycle fixtures must not overlap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
