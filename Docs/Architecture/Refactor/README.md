# Refactor Design Docs (Spec-driven)

This folder is the canonical place to document the ongoing refactor of **PolicyPlusCore/Core** and related layers.

We will use a **spec-driven** workflow:

1. Start with the **test plan** (what behaviors must not change).
2. Capture design intent in an **ADR** (what we decide and why).
3. Implement in small, reversible steps (facades/adapters; no breaking API changes).
4. Record notable changes in the **ChangeLog**.

## Documents

- [TestPlan.md](TestPlan.md) — the primary spec for refactor safety
- [Overview.md](Overview.md) — target architecture, boundaries, naming, migration strategy
- [ChangeLog.md](ChangeLog.md) — human-readable summary of refactor milestones
- [WorkingAssumptions.md](WorkingAssumptions.md) — living notes for changeable design assumptions
- [ADR/](ADR) — decision records (one decision per file)

Key structural decision:

- ADR 0012: target folder layering (Core / Application / Infrastructure)

## Conventions

### Scope

This refactor starts in **PolicyPlusCore/Core** and should keep the core UI-agnostic.

### Compatibility

- Prefer **behavior-preserving refactors**.
- Public surface should remain stable unless explicitly approved.
- If semantics must change, record it as an ADR and add tests that demonstrate the difference.

### How to propose a change

1. Add/extend a section in [TestPlan.md](TestPlan.md) that defines expected behavior.
2. Add an ADR in [ADR/](ADR) using the template.
3. Implement behind facades/adapters.
4. Add a ChangeLog entry.

## Status

Active; iterative. The goal is to reduce scattered branching and improve change safety.
