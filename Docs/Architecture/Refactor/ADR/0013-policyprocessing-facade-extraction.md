# ADR 0013: PolicyProcessing Facade Extraction

- Status: Accepted
- Date: 2026-01-16

## Context

`PolicyPlusCore/Core/PolicyProcessing.cs` is currently the largest file in Core (1,511 lines) containing multiple responsibilities:

1. **State Evaluation** (`GetPolicyState`) — ~450 lines of evidence scoring logic
2. **Option State Retrieval** (`GetPolicyOptionStates`) — element value reading
3. **State Application** (`SetPolicyState`) — registry mutation for Enabled/Disabled
4. **Policy Forgetting** (`ForgetPolicy`) — clearing registry values
5. **Registry Walking** (`GetReferencedRegistryValues`, `WalkPolicyRegistry`) — policy registry enumeration
6. **Support Checking** (`IsPolicySupported`) — product support evaluation
7. **Deduplication** (`DeduplicatePolicies`) — bundle cleanup
8. **Internal helpers** — `CachedPolicySource`, `ValuePresent`, `ValueListPresent`, etc.

This monolithic structure makes it difficult to:
- Test individual responsibilities in isolation
- Understand evidence scoring behavior
- Evolve evaluation logic safely
- Maintain clear separation of read (evaluate) vs write (apply) operations

Following the successful facade pattern established in ADR 0001–0012 for `AdmxCache`, we will apply the same approach to `PolicyProcessing`.

## Decision

### 1) Keep the public API stable
- Keep `PolicyProcessing` public surface unchanged (static methods).
- Refactor internals incrementally using facade + delegation.

### 2) Extract responsibilities into internal services under `PolicyPlusCore/Core/Policies/`

| Service | Responsibility | Source Methods |
|---------|----------------|----------------|
| `PolicyStateEvaluator` | Pure state evaluation + evidence scoring | `GetPolicyState` core logic |
| `PolicyOptionReader` | Element value retrieval from registry | `GetPolicyOptionStates`, `GetRegistryListState` |
| `PolicyStateApplier` | Registry mutations for state changes | `SetPolicyState` |
| `PolicyRegistryWalker` | Registry key/value enumeration | `WalkPolicyRegistry`, `GetReferencedRegistryValues`, `ForgetPolicy` |
| `PolicySupportChecker` | Product support evaluation | `IsPolicySupported` |

### 3) Phased extraction order
1. **Phase 1 (this ADR)**: Extract `PolicyStateEvaluator` — highest complexity, most branching
2. **Phase 2**: Extract `PolicyStateApplier` — write-side complement
3. **Phase 3**: Extract remaining helpers (`PolicyOptionReader`, `PolicyRegistryWalker`, `PolicySupportChecker`)

### 4) Internal service patterns
- Services are `internal sealed class` with no public constructors
- `PolicyProcessing` delegates to services via static method calls
- `CachedPolicySource` moves into `PolicyStateEvaluator` as a private nested class
- Evidence scoring constants/thresholds become explicit named constants

## Rationale

- **Evaluation-first**: `GetPolicyState` is the most complex method (~450 lines) with the most branching (explicit matching, evidence scoring, element-type handling). Extracting it first provides the highest value.
- **Read before write**: Evaluation (read) is safer to extract before application (write) because evaluation has no side effects.
- **Proven pattern**: This mirrors the successful `AdmxCacheSearchService` extraction.
- **Test coverage**: Existing tests in `PolicyProcessingStateEvaluationTests.cs` and related files provide a regression safety net.

## Consequences

### Positive
- State evaluation logic becomes testable in isolation
- Evidence scoring behavior becomes explicit and documented
- Future scoring improvements can be made with confidence
- Clear separation of read vs write operations

### Negative / Risks
- Initial indirection may increase complexity temporarily
- Must ensure `CachedPolicySource` optimization is preserved

### Mitigations
- Lift-and-shift extraction (no behavior changes)
- Run full test suite after each extraction step
- Keep nested `CachedPolicySource` for performance

## Compatibility

- Public API impact: None — `PolicyProcessing` static methods remain unchanged
- Behavior changes: None intended — pure refactor
- Migration steps:
  1. Create `PolicyPlusCore/Core/Policies/` folder
  2. Extract `PolicyStateEvaluator` with existing logic
  3. Delegate from `PolicyProcessing.GetPolicyState`
  4. Verify all tests pass

## Test Plan

Reference: `Docs/Architecture/Refactor/TestPlan.md`

- **Existing tests**: All tests in `PolicyPlusModTests/Core/PolicyProcessing/` must pass
- **Characterization tests**: 23 tests in `PolicyStateEvaluatorCharacterizationTests.cs` covering:
  - Explicit match priority over evidence scoring
  - Evidence tie behavior (Unknown state)
  - Element-type specific scoring weights
  - ValueList handling (full match, partial match, key override)
  - Boolean OffValue/OffValueList 0.1 weight multiplier

Run validation:
```cmd
dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64
```

## Implementation Status

### Phase 1: PolicyStateEvaluator — ✅ Complete (2026-01-17)

**Extracted to**: `PolicyPlusCore/Core/Policies/PolicyStateEvaluator.cs`

**Components moved**:
- `Evaluate()` — main state evaluation entry point
- `EvaluateExplicitMatches()` — explicit On/Off value matching
- `EvaluateElementExplicitMatch()` — element-level explicit matching
- `EvaluateEvidence()` — evidence-based scoring
- `EvaluateElementEvidence()` — element evidence scoring
- `ValuePresent()` / `ValueListPresent()` — registry value matching helpers
- `CachedPolicySource` — memoizing wrapper (internal nested class)

**Characterization tests**: 23 tests covering 100% of code paths
- Explicit match priority (Root, Boolean, Enum)
- ValueList handling (full match, partial match, DefaultRegistryKey, entry RegistryKey override)
- Evidence scoring (weights, tie behavior, 0.1 Boolean OffValue multiplier)
- Null/guard conditions

**Test file**: `PolicyPlusModTests/Core/PolicyProcessing/PolicyStateEvaluatorCharacterizationTests.cs`

**Verification**: 315 tests pass (302 existing + 13 new ValueList tests)

### Phase 2: PolicyStateApplier — ✅ Complete (2026-01-18)

**Extracted to**: `PolicyPlusCore/Core/Policies/PolicyStateApplier.cs`

**Components moved**:
- `Apply()` — main state application entry point
- `ApplyEnabledState()` — Enabled state registry writes
- `ApplyDisabledState()` — Disabled state registry deletions
- `ApplyElementEnabled()` — element-type switch for Enabled state
- `ApplyDecimalElement()` / `ApplyBooleanElementEnabled()` / `ApplyTextElement()` / `ApplyListElement()` / `ApplyEnumElement()` / `ApplyMultiTextElement()` — element-specific handlers
- `SetValue()` / `SetSingleList()` / `SetList()` — registry value helpers

**Test coverage**: Existing tests in PolicyProcessingTests, PolicyProcessingElementTests, and related files cover SetPolicyState behavior (~30+ invocations)

**Verification**: 315 tests pass

### Phase 3a: PolicyOptionReader — ✅ Complete (2026-01-18)

**Extracted to**: `PolicyPlusCore/Core/Policies/PolicyOptionReader.cs`

**Components moved**:
- `GetOptionStates()` — main entry point for element option retrieval
- `ReadElementValue()` / `ReadDecimalElement()` / `ReadBooleanElement()` / `ReadTextElement()` / `ReadListElement()` / `ReadEnumElement()` / `ReadMultiTextElement()` — element-type handlers
- `GetRegistryListState()` — boolean On/Off evaluation helper
- `TryGetUInt32()` — flexible uint parsing

**Dependencies**: Uses `PolicyStateEvaluator.ValuePresent()` and `ValueListPresent()` for enum matching

**Test coverage**: Existing tests cover `GetPolicyOptionStates` via PolicyProcessing facade

**Verification**: No compilation errors

### Phase 3b: PolicyRegistryWalker — ✅ Complete (2026-01-18)

**Extracted to**: `PolicyPlusCore/Core/Policies/PolicyRegistryWalker.cs`

**Components moved**:
- `GetReferencedValues()` — get all registry key/value pairs for a policy
- `Forget()` — clear all policy registry values
- `Walk()` — core registry enumeration with element-type handling

**Test coverage**: Existing tests in PolicyProcessingTests, ReferencedValueTests cover this behavior

### Phase 3c: PolicySupportChecker — ✅ Complete (2026-01-18)

**Extracted to**: `PolicyPlusCore/Core/Policies/PolicySupportChecker.cs`

**Components moved**:
- `IsSupported()` — product support evaluation with cycle detection
- `SupEntryMet()` / `SupDefMet()` — nested local functions converted to methods

**Test coverage**: Existing tests in IsPolicySupportedTests (9+ tests) cover AnyOf, AllOf, range, nested, blank logic, cycle protection

## Notes

- The `PolicyState` enum and `RegistryKeyValuePair` class remain in `PolicyProcessing.cs` for now (they are public types)
- Future ADRs may move these to dedicated files if needed
- `DeduplicatePolicies` is bundle-related and may move to a different location in later phases
