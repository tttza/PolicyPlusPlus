# ADR 0010: Core/Culture consolidation for culture name normalization and preference

- Status: Accepted
- Date: 2026-01-13

## Context

The refactor has introduced (and revealed) multiple places that handle “culture string” concerns:

- Scan/search/query paths normalize culture names before using them as identifiers.
- We want dependency direction to be natural: Search and other Core logic should not depend on cache-named types.
- There is duplication risk between culture normalization helpers and culture preference ordering helpers.

Long term, we want culture-related semantics to be represented as a Value Object (VO) and validated at construction time where appropriate. Short term, we must preserve the existing behavior (including tolerant fallback behavior in some call sites).

## Decision

Introduce a dedicated Core module for culture-related logic:

- Add `PolicyPlusCore/Core/Culture/` as the home for:
  - Culture name normalization (string → canonical culture name)
  - Culture preference ordering (ordered list of candidate cultures for queries/search)
  - Future internal Value Objects (e.g., `CultureName`) once behavior contracts are fully specified

During migration:

- Keep public APIs stable (methods in `AdmxCache` that accept `string culture` remain unchanged).
- Allow cache/search internals to call the new `Core/Culture` components.
- Keep compatibility shims (e.g., cache-scoped helper wrappers) only as temporary adapters.

## Rationale

- Culture name normalization and preference ordering are Core-domain concerns that apply beyond caching.
- Placing them under `Core/Culture` avoids awkward dependencies such as “Search depends on AdmxCache-named types”.
- A dedicated folder makes it easier to evolve toward VO-based validation without scattering helper functions across `Utilities` and `Core/Caching/*`.

Alternatives considered:

- Keep culture helpers in `Core/Caching/Culture/`:
  - Rejected because it implies cache ownership and encourages non-cache logic to depend on cache-scoped types.
- Keep culture helpers in `Utilities/`:
  - Rejected because the refactor is intentionally moving Core semantics into explicit modules and away from a catch-all bucket.

## Consequences

### Positive

- Clear ownership for culture semantics.
- Reduced duplication and improved dependency direction.
- Enables a clean path to a `CultureName` VO.

### Negative / Risks

- Risk of behavior drift if “normalization” semantics differ between call sites.
- Temporary adapter types may persist longer than desired.

### Mitigations

- Preserve current normalization behavior initially (lift-and-shift); only consolidate call sites after characterization tests exist.
- Keep normalization as a single canonical implementation and route all existing callers through it.

## Compatibility

- Public API impact: None (internal re-organization only).
- Behavior changes: None intended.
- Migration steps:
  1. Add `Core/Culture` folder.
  2. Move/introduce canonical normalization helper there.
  3. Move/introduce canonical preference ordering helper there.
  4. Update internal callers (Search/Cache) to reference `Core/Culture`.
  5. Remove or minimize cache-scoped wrappers once no longer referenced.
  6. (Optional) Introduce a `CultureName` VO internally after tests lock current behavior.

## Test Plan

Link: `Docs/Architecture/Refactor/TestPlan.md`

- New tests:
  - Characterization tests for culture normalization inputs (empty/whitespace/invalid) and expected outputs.
  - Equivalence tests proving Search/Scan/Query use the same normalization.
- Updated tests:
  - None required if behavior is preserved.
- Manual checks (if any):
  - Scan with non-default UI culture and verify search/query still resolve localized rows.

## Notes

- A future VO (`CultureName`) should be introduced only after we have an explicit contract about when to throw vs when to fall back.

## Implementation

- Phase 1: Introduce `PolicyPlusCore/Core/Culture/CultureNameNormalization.cs` and route internal call sites through it.
- `PolicyPlusCore/Utilities/CultureNameNormalization.cs` remains as a compatibility shim to avoid churn while migrating call sites.

### Phase 2 (Decision)

Do not move `PolicyPlusCore/Utilities/CulturePreference` wholesale into `Core/Culture`.

Observed usage distribution:

- `PolicyPlusCore/Utilities/CulturePreference` is used primarily by WinUI layer and tests that lock UI/slot semantics (placeholder second, slot roles).
- Search has its own internal `CulturePreference` type in `Core/Caching/Search/SearchModel.cs` that serves a different purpose (minimal normalization for search).

Therefore the safe next step is to clarify ownership and naming rather than relocating the entire utility:

- Keep slot/placeholder oriented logic (`CultureSlot`, `CultureRole`, `Utilities.CulturePreference.Build`) in `Utilities` (UI-adjacent semantics).
- Keep `Core/Culture` focused on pure culture semantics (normalization/validation/comparison, future VO).
- Optional follow-up: rename the search-internal `CulturePreference` to `SearchCulturePreference` to avoid confusion with the public `Utilities.CulturePreference`.
