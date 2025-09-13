using Xunit;

// UI automation tests manipulate global desktop focus and send real keyboard input.
// Disable parallelization in this test assembly to avoid race conditions (interleaved keystrokes,
// focus stealing, transient popups) which cause flaky failures when tests run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
