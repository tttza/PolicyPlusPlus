# ADR 0005: AdmxCache Initialization Split and Writer Gate

- Status: Accepted
- Date: 2026-01-13

## Context

`PolicyPlusCore/Core/AdmxCache.cs` has been incrementally refactored into a facade that delegates to internal services and stores (ADR 0001–0004).

Even after ADR 0004, `AdmxCache.InitializeAsync(...)` remains a mixed-responsibility entry point:

- Cross-process writer serialization (skipped in fast mode)
- Store initialization (SQLite open/PRAGMA/schema)
- Auxiliary schema ensure (e.g., FileUsage)
- WAL checkpoint hygiene

Additionally, the writer-lock acquisition pattern exists in multiple locations (write/apply pipeline, stale purges, initialization) and is easy to accidentally drift:

- retry budgets
- delays
- fast-mode behavior
- exception swallowing boundaries

This makes further behavior-preserving refactors riskier and keeps `AdmxCache.cs` larger than necessary.

## Decision

### 1) Keep public API stable

- No changes to `IAdmxCache`.
- No changes to the public signature or intended behavior of `AdmxCache.InitializeAsync`.

`AdmxCache` remains a facade.

### 2) Extract initialization orchestration behind an internal service

Introduce an internal service under `PolicyPlusCore/Core/Caching/`:

- `AdmxCacheInitializationService`
  - Owns the orchestration currently inside `AdmxCache.InitializeAsync`.
  - Calls existing store and auxiliary stores to perform initialization steps.
  - Preserves try/catch swallowing boundaries.

The facade will delegate to the service.

### 3) Introduce a shared writer gate abstraction (internal)

Introduce a small internal helper under `PolicyPlusCore/Core/Caching/` (name may evolve):

- `AdmxCacheWriterGate`
  - Encapsulates the current "try acquire writer lock with retries" behavior.
  - Centralizes fast-mode behavior (`POLICYPLUS_CACHE_FAST=1`).
  - Uses the existing lock primitive (`AdmxCacheRuntime.TryAcquireWriterLock`).

Services/stores that need writer serialization should call into this gate rather than re-implementing retry loops.

## Rationale

- Initialization is a natural seam: it is a lifecycle boundary and is easy to delegate without changing public semantics.
- A shared writer gate reduces the risk of subtle drift in retry behavior across the codebase.
- This continues the established pattern (AdmxCache as facade, internals under `Core/Caching`).

## Consequences

### Positive

- `AdmxCache.cs` becomes smaller and easier to read.
- Cross-process writer serialization rules become centralized.
- Follow-on refactors reduce risk because lock/fast-mode semantics are less scattered.

### Negative / Risks

- Any change to exception swallowing boundaries can unintentionally change behavior.
- A shared writer gate makes it easy to overuse serialization and reduce concurrency if applied too broadly.

### Mitigations

- Lift-and-shift only: keep retry counts, delays, and fast-mode checks identical.
- Keep the same exception swallowing boundaries at the same layers.
- Prefer narrow adoption: start with initialization and one other call site, then expand.

## Compatibility

- Public API impact: none.
- Behavior changes: none intended.
- Migration steps:
  1. Add `AdmxCacheInitializationService` and route `AdmxCache.InitializeAsync` through it.
  2. Add `AdmxCacheWriterGate` and route initialization writer-lock acquisition through it.
  3. (Optional follow-up) Route other writer-serialized operations through the same gate, one at a time.

## Implementation

Completed:

- `PolicyPlusCore/Core/AdmxCache.cs`: `InitializeAsync` delegates to `AdmxCacheInitializationService`.
- `PolicyPlusCore/Core/Caching/AdmxCacheInitializationService.cs`: owns initialization orchestration.
- `PolicyPlusCore/Core/Caching/AdmxCacheWriterGate.cs`: centralizes writer-lock acquisition retries and fast-mode behavior.
- Follow-up adoption: apply pipeline, file-usage purge, and culture purge call sites route through `AdmxCacheWriterGate`.

## Test Plan

See `Docs/Architecture/Refactor/TestPlan.md`.

- New tests (only if needed):
  - Characterization: `InitializeAsync` remains idempotent and does not throw under typical conditions.
  - Characterization: fast-mode behavior does not acquire writer locks (best-effort, non-invasive).
- Updated tests: none required if existing suite covers initialization paths.

Run:

- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Notes

This ADR is intentionally scoped to structure-only refactoring.

It does not attempt to redefine the underlying lock primitive or environment-variable contracts.
