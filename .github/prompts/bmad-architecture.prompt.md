---
description: 'Design component boundaries, data flows, integration contracts, and architecture decisions. Resolve the project's stack and ADR/output paths from repository context.'
---

# BMAD Architecture — Design and Decisions

Design the smallest architecture that satisfies approved requirements and fits the existing system. Use this prompt in Full Flow or when an explicitly requested architecture decision needs to be recorded.

## Load Context

1. Read `_bmad/config.toml`, the ID registry at `[ids].registry_file`, and the repository instruction file configured by `[paths].instructions_file`.
2. Resolve project state, code map, project overview, ADR directory, build/deployment model, and architecture constraints from configured context sources.
3. Read the relevant approved plan and only the existing architecture records relevant to the change.
4. Load Winston from `[paths].agents_dir`.

## Design Method

Offer two modes when the decision is complex and the user's preference is not clear:

- **Coaching:** frame decisions as options with trade-offs and let the user choose.
- **Fast:** draft a recommended design from verified project context and present it for approval.

For either mode:

1. Identify **invariants** (must remain true) separately from **seed facts** (current implementation choices that may change).
2. Set the design altitude: system, component, or class/function. Do not over-design a narrow change.
3. Define responsibilities, contracts, dependencies, data flow, configuration, failure behavior, and migration path for affected components.
4. Check only integration categories relevant to the project and change: dependency lifecycle, public APIs/protocols, persistence/schema, configuration, deployment, observability, performance/resource ownership, and verification.
5. Compare viable alternatives and their costs. Prefer existing project patterns unless evidence supports a change.

## Output

Present the architecture before writing it. Wait for explicit approval of decisions that materially constrain implementation.

After the user approves creating a record for a non-trivial decision, allocate the next unused `ADR-<4-digit-sequence>` from `[ids]` and the configured registry. Reserve the ID and create `ADR-0001-<slug>.md` in `[paths].adr_dir` with `Status: Proposed`; include its ID and date. Set it to `Accepted` only after the decision itself is approved. If the proposal is rejected or withdrawn, retain the numbered record with `Status: Rejected` or `Deprecated`; never reuse its ID.

When a new decision replaces an existing ADR, do not move or delete the older file. Mark the older ADR `Superseded`, set `Superseded by: ADR-nnnn`, and link the successor; include `Supersedes: ADR-nnnn` in the new record. Update current decision links to the successor while preserving historical references.

Use the project's ADR format for the decision body, including:

- Context and constraints
- Invariants and seed facts
- Decision
- Alternatives considered and why they were not selected
- Consequences, risks, and migration path

Add an architecture summary to the approved plan or configured output directory when several components or data flows are involved. Use a Mermaid diagram when it clarifies boundaries. After approval, update WORK_ITEMS only for an allocated ADR ID; update the Decision and Brainstorm Records table in Project Spec with the ADR status and current-decision links.

Do not implement code in this prompt. Continue through the active workflow to `/bmad-build` after required artifacts are approved.