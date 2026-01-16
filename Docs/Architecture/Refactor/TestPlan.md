# Refactor Test Plan (PolicyPlusCore/Core)

This test plan defines the **behavioral contract** that refactors must preserve.

The purpose is to reduce the risk of regressions while we restructure code (e.g., splitting responsibilities, introducing services, facades, adapters) in **PolicyPlusCore/Core**.

## Principles

- **Characterize before changing**: when behavior is unclear, write characterization tests first.
- **Prefer small, local tests**: unit tests over wide integration tests whenever possible.
- **No UI dependency**: tests here should validate core behaviors without WinUI.
- **Behavior first, structure second**: tests should assert outcomes, not implementation details.

## Target Areas

Initial focus (highest branching / highest risk):

- `PolicyPlusCore/Core/AdmxCache.cs` — scanning, culture selection, caching/search paths
- `PolicyPlusCore/Core/PolicyProcessing.cs` — policy evaluation / application logic
- `PolicyPlusCore/Core/PolicySavePipeline.cs` — save orchestration and IO wiring

## Invariants (Must Hold)

### Domain Model Invariants (when introducing Entities / Value Objects)

These invariants apply when we introduce or refactor toward domain types.

- Entity identity is stable and does not depend on mutable/display fields.
- Value Objects are immutable and use value-based equality.
- Normalization rules are explicit and tested (e.g., culture casing, registry path normalization).

### ADMX/ADML Loading

- Loading the same ADMX/ADML set produces the same semantic bundle representation.
- Culture selection is deterministic given the same inputs and preference rules.
- Cache invalidation does not cause missing/duplicate policies.

### Policy Evaluation

- Enabled/Disabled/NotConfigured semantics remain stable.
- Element value interpretation remains stable (lists, enums, numeric types, multi-line text).
- Registry operations derived from a policy remain stable (key/value names, types, data).

### Save Pipeline

- Save/apply produces the same persisted result (registry / POL / REG) for the same inputs.
- Error handling behavior remains stable (fail/partial apply rules), unless explicitly changed via ADR.

## Test Strategy

### 1) Characterization Tests (Safety Net)

Create tests that lock in the current behavior of complex/branchy logic.

Recommended pattern:

- Arrange: minimal synthetic structures / fixtures
- Act: call the existing public entry point
- Assert: compare outputs using stable projections (avoid depending on exact logging strings)

Sources:

- Prefer tests in `PolicyPlusModTests/` for core logic.
- When global singletons are involved, use the repo's isolation patterns/collections.

### 2) Unit Tests for New Extracted Components

When a new internal component is introduced (e.g., `*Service`, `*Evaluator`), add unit tests for:

- Happy path
- One edge case
- One error/invalid input path (where applicable)

### 3) Integration Tests (Only Where Necessary)

Use integration tests when behavior depends on:

- File system scanning
- Registry I/O (prefer mocking abstractions where available)
- POL/REG serialization formats

## Test Matrix (Initial)

### AdmxCache

- Scan respects culture preference ordering.
- Scan produces stable policy count and identity set.
- Search returns the same top results for a fixed query set.
- Search ranking tests MUST NOT rely on stable ordering for ties (equal or near-equal scores).
  - Prefer assertions on set membership within Top-K.
  - Assert ordering only when the score separation is meaningful (e.g., A.Score - B.Score >= Δ).
  - Where applicable, assert score bands/ranges rather than absolute score values.
- Cache invalidation triggers rebuild when inputs change.

### PolicyProcessing

- Evaluate state for representative policies:
  - Boolean policy with Enabled/Disabled
  - Enum policy
  - List policy
  - Multi-line text policy
- Applying state produces stable registry mutation set.

### PolicySavePipeline

- Given a set of policy changes, apply produces stable serialization output.
- Applying the same changes twice is idempotent where expected.

## Performance Checks (Non-blocking, but tracked)

- Large ADMX set: ensure search and scan do not regress noticeably.
- Avoid repeated parsing/allocations in hot paths.

## When the Plan Must Be Updated

- If we discover undefined behavior that differs across sources.
- If we intentionally change semantics.
- If new abstractions are introduced (new boundary = new test responsibilities).

## Definition of Done for a Refactor Step

- Tests updated/added per this plan.
- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64` passes.
- No new warnings; behavior preserved unless ADR says otherwise.
