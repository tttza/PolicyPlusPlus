# ADR 0012: Target folder layering (Core / Application / Infrastructure)

- Status: Accepted
- Date: 2026-01-14

## Context

The ongoing refactor is intentionally splitting large facades into smaller components.

Over time, we accumulated infrastructure-heavy code under locations that read as “Core”:

- `PolicyPlusCore/Core/Caching/*` contains SQLite/FTS, file enumeration, and other environment branching.
- `PolicyPlusCore/IO/*` and `PolicyPlusCore/Helpers/*` are also infrastructure concerns.

This creates avoidable ambiguity:

- “Core” may be read as domain-ish (external-dependency free), but `Core/Caching` is not.
- Thin public entry points (e.g., `AdmxCache.cs`) are not Domain Services; they act as an Application boundary.

We want the folder structure to communicate dependency direction and reduce accidental coupling.

## Decision

Adopt a target folder layering model for `PolicyPlusCore`:

- `PolicyPlusCore/Core/` — Domain-ish semantics
  - Value objects, normalization/validation rules, evaluation logic
  - Must not depend on SQLite/FTS, filesystem enumeration, registry IO, or environment-variable branching

- `PolicyPlusCore/Infrastructure/` — concrete IO and persistence
  - SQLite/FTS cache storage, scan/search pipelines, file enumeration
  - File formats/loaders, registry IO, OS/PInvoke helpers

- `PolicyPlusCore/Application/` — optional; use cases and public facades
  - Treat public entry points (“thin facades”) as the Application boundary
  - Coordinates Domain + Infrastructure while keeping public APIs stable

Migration stance:

- This ADR does not require immediate file moves.
- When code physically remains in legacy locations (e.g., `Core/Caching/*`), treat it as Infrastructure-equivalent and keep Core free of dependencies on it.
- New infrastructure-heavy code should prefer the `Infrastructure/*` target locations.

## Rationale

- Makes dependency direction obvious: Infrastructure may depend on Core (domain), but Core must not depend on Infrastructure.
- Reduces confusion about what “Core” means during and after the refactor.
- Provides a stable vocabulary for ADRs and reviews: Domain vs Infrastructure vs Application boundary.

## Consequences

### Positive

- Clearer ownership boundaries for new extractions.
- Lower risk of Domain semantics accidentally taking an IO dependency.
- Easier to explain and enforce “thin facade” intent.

### Negative / Risks

- Existing files and ADRs reference legacy paths (`Core/Caching/*`).
- Future physical moves may cause namespace churn.

### Mitigations

- Prefer describing responsibilities and layer intent over hardcoding paths in new docs.
- When needed, add a short “path note” in ADRs stating that paths reflect the layout at the time of extraction.
- Migrate incrementally with ADR-sized steps and test coverage.

## Compatibility

- Public API impact: None.
- Behavior changes: None.
- Migration steps:
  1. Update refactor docs to use the target layering vocabulary.
  2. Prefer `Infrastructure/*` for new infrastructure-heavy types.
  3. Optionally, move legacy `Core/Caching/*` into `Infrastructure/Caching/*` in a follow-up refactor step.

## Test Plan

Doc-only change.

- New tests: None.
- Updated tests: None.

## Notes

- This ADR intentionally leaves the exact subfolder names inside `Infrastructure/` flexible (e.g., `OS/` vs `Helpers/`).
