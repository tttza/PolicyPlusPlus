using Xunit;

// TODO: Enable parallelization when tests are refactored to avoid shared mutable state.

// Disabling parallelization: Many tests mutate global singletons (PolicySourceManager, PendingChangesService)
// and rely on predictable ordering of side effects. Parallel execution was causing race conditions leading
// to sporadic state mismatches (e.g., Enabled vs NotConfigured, unexpected queued diffs). Making tests run
// serially stabilizes shared-state interactions without a large refactor.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
