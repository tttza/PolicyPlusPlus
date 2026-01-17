# ADR 0011: AdmxCache scan orchestration extraction

- Status: Accepted
- Date: 2026-01-14

## Context

`PolicyPlusCore/Core/AdmxCache.cs` has been progressively slimmed down by delegating scan sub-steps into internal services (culture plan, gate, signature, bundle/usage, apply, meta, maintenance).

However, `AdmxCache.ScanAndUpdateCoreAsync(...)` still contains the main orchestration logic:

- Environment branching (maintenance disable, source root default)
- Culture plan + per-culture loop
- Cross-cutting tracing scopes (`AdmxCacheTrace.Scope(...)`)
- Wiring of delegates into the extracted services

To continue the “facade + internal services” migration strategy, the remaining orchestration should be extracted behind an internal service.

At the same time, we want to keep the dependency direction and file tree easy to understand:

- Scan orchestration lives under `Core/Caching/*`.
- `AdmxCacheTrace` remains referenced from `AdmxCache` (facade) only, so service code does not depend on cache tracing types by name.

Path note:

- `Core/Caching/*` is the layout at the time of extraction.
- Under the target layering (ADR 0012), these types belong under `Infrastructure/Caching/*`.

## Decision

Introduce a new internal orchestration service:

- Add `PolicyPlusCore/Core/Caching/AdmxCacheScanOrchestrationService.cs`.
- Move the body of `AdmxCache.ScanAndUpdateCoreAsync(...)` into the new service.

Keep `AdmxCacheTrace` in the facade:

- `AdmxCache` will continue to create trace scopes and pass a `scopeFactory` delegate into the orchestration service.
- The orchestration service will accept `Func<string, IDisposable>` (or equivalent) and use it to create scopes without directly referencing `AdmxCacheTrace`.

Scope of extraction (lift-and-shift):

- Preserve behavior and public API.
- Preserve trace scope names (e.g., `ScanAndUpdateAsync.Total`, `ScanAndUpdateAsync.Culture:<cul>`, `DiffAndApply:<cul>`).
- Preserve environment variable branching semantics and exception/cancellation behavior.

## Rationale

- Further slims `AdmxCache.cs` while preserving a stable public facade.
- Removes the last large scan method from the facade, reducing regression risk by localizing scan decisions.
- Keeps tracing dependency direction explicit and avoids “service code depends on facade-only types”.

Alternatives considered:

- Move `AdmxCacheTrace` into `Core/Caching` and let services reference it directly:
  - Rejected for now to keep dependencies simpler and the tree structure easier to reason about.

## Consequences

### Positive

- `AdmxCache.cs` becomes closer to a pure facade.
- Scan orchestration becomes unit-testable in isolation if needed.
- Easier to evolve scan wiring/options without growing the facade again.

### Negative / Risks

- Risk of subtle behavior drift during extraction (ordering, skip decisions, exception swallowing).
- Adds one more internal type.

### Mitigations

- Treat as lift-and-shift: minimize changes beyond method movement.
- Keep existing tests as safety net; add characterization tests only if a missing contract is discovered.

## Compatibility

- Public API impact: None.
- Behavior changes: None intended.
- Migration steps:
  1. Add `AdmxCacheScanOrchestrationService`.
  2. Move orchestration logic and keep trace scope creation via delegate.
  3. Update `AdmxCache.ScanAndUpdateCoreAsync` to delegate.
  4. Move remaining scan-only private helpers from facade to orchestration (e.g., filtered enumeration / ADML derivation / existing culture query) if appropriate.

## Test Plan

Link: `Docs/Architecture/Refactor/TestPlan.md`

- New tests: None required initially (lift-and-shift).
- Updated tests: None expected.
- Verification:
  - Run `PolicyPlusModTests` full suite.

## Notes

- The orchestration service should remain `internal` and accept dependencies explicitly (store + delegates) to avoid hidden couplings.

## Implementation

- Implemented `PolicyPlusCore/Core/Caching/AdmxCacheScanOrchestrationService.cs` and delegated `AdmxCache.ScanAndUpdateCoreAsync` to it.
- `AdmxCacheTrace` remains in the facade and is passed as a scope factory delegate.
