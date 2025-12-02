using Xunit;

// Allow assembly-level parallel execution; sensitive tests opt into serial collections when needed.
[assembly: CollectionBehavior(DisableTestParallelization = false, MaxParallelThreads = -1)]
