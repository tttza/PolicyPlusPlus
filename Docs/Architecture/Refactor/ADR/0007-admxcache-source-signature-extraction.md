# ADR 0007: AdmxCache Source Signature Extraction

- Status: Accepted
- Date: 2026-01-13

## Context

`PolicyPlusCore/Core/AdmxCache.cs` still contains `ComputeSourceSignatureAsync`, which builds a signature string for a culture by:

- Reading `POLICYPLUS_CACHE_ONLY_FILES` (optional filter list)
- Enumerating ADMX files and culture-specific ADML files
- Including file length + last write ticks in a stable text representation
- Hashing the resulting bytes (SHA-256 with a safe fallback)
- Swallowing exceptions and honoring cancellation in key loops

This method is used by the scan signature services to decide whether a culture can be skipped. Because it mixes environment branching, file system enumeration, hashing, and exception/cancellation semantics, it is easy to accidentally change behavior during follow-on refactors.

Keeping it inside the facade also keeps `AdmxCache.cs` larger than necessary.

## Decision

### 1) Keep public API stable

- No changes to `IAdmxCache`.
- No changes to the observable behavior of scan skip decisions.

### 2) Extract signature computation into an internal component

Introduce an internal component under `PolicyPlusCore/Core/Caching/`:

- `AdmxCacheSourceSignatureBuilder` (name may evolve)
  - Owns the logic currently in `AdmxCache.ComputeSourceSignatureAsync`.
  - Preserves exception swallowing and cancellation behavior.
  - Preserves the interpretation of `POLICYPLUS_CACHE_ONLY_FILES`.

Initial migration is lift-and-shift:

- `AdmxCache.ComputeSourceSignatureAsync` becomes a thin delegate to the builder.

Optional follow-up (separate step, still behavior-preserving):

- Route `AdmxCacheScanSignatureService` to call the builder directly (reducing the facade surface further).

## Rationale

- The signature computation is a pure-ish utility with complex edge-case behavior; extracting it reduces drift risk.
- It aligns with the current architecture direction: `AdmxCache` remains a facade; heavy logic lives in `Core/Caching/*`.

## Consequences

### Positive

- `AdmxCache.cs` becomes smaller.
- Signature behavior is centralized and easier to test in isolation.

### Negative / Risks

- Any subtle change to ordering, formatting, exception swallowing, or cancellation can change skip decisions.

### Mitigations

- Lift-and-shift first; avoid refactoring the algorithm during extraction.
- Run the full unit test suite after extraction.

## Compatibility

- Public API impact: none.
- Behavior changes: none intended.
- Migration steps:
  1. Add `AdmxCacheSourceSignatureBuilder`.
  2. Delegate `ComputeSourceSignatureAsync` to the builder.
  3. Run tests; add characterization tests only if coverage gaps are discovered.

## Test Plan

See `Docs/Architecture/Refactor/TestPlan.md`.

- New tests (only if needed):
  - Characterization: same inputs produce the same signature (including filtered file sets).
  - Characterization: cancellation is honored in enumeration loops.
- Updated tests: none required if existing scan characterization tests cover typical scenarios.

Run:

- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Implementation

- Added `PolicyPlusCore/Core/Caching/AdmxCacheSourceSignatureBuilder.cs`.
- `AdmxCache.ComputeSourceSignatureAsync` delegates to the builder.
