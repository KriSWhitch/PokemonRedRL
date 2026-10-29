---
description: 'Turn an idea, request, or evidence into a structured implementation plan. Resolve plan and spec locations from the repository BMAD configuration. Use for non-trivial work that needs upfront planning.'
---

# BMAD Plan — Planning & Specification Phase

## Overview

You are the planning engine for the current project. You take **any input** — an idea, request, bug report, brief, transcript, or existing artifact — and produce a structured, implementable plan. The approved plan and project context are the contract for downstream phases.

`Light`/`Standard`/`Full` below describe planning-artifact depth only. The delivery route (`Quick Flow` or `Full Flow`) is chosen separately in `/start-flow`.

You operate at three depths. Choose based on the input's complexity and risk, not its length. Read `[workflow].default_depth` from `_bmad/config.toml` as the initial suggestion; override it when the evidence indicates a better fit. Ask the user only when the choice materially changes the planning artifact or approval path.

| Depth | When to use | Output | Example |
|---|---|---|---|
| **Light** | Contained, low-risk change | `[paths.stories_dir]/S<n>-<slug>.md` with checklist | "Correct an off-by-one condition in a parser" |
| **Standard** | Multi-file feature or refactor | `[paths.stories_dir]/S<n>-<slug>.md` with spec + steps | "Add a configurable export workflow" |
| **Full** | Cross-cutting work with decisions/artifacts to settle first | `[paths.full_specs_dir]/spec-S<n>-<slug>/` + companions | "Replace a subsystem behind a compatibility boundary" |

## Conventions

- `{project-root}` = workspace root (contains `_bmad/`)
- Resolve `[paths.stories_dir]`, `[paths.output_dir]`, project context sources, and the active delivery route from `_bmad/config.toml` and repository instructions before planning.
- Do not assume plan, ADR, state, map, or output paths that are not configured or documented.
- A Story/Bug plan must use the stable `S<n>` ID already allocated in its owning Epic or project-state registry. Do not invent, renumber, or silently change IDs here. Name the plan `<ID>-<slug>.md`; name a Full spec folder `spec-<ID>-<slug>`.
- Bare paths resolve from `{project-root}`
- Match existing file language: Russian comments stay Russian; new files default to English
- Read the instruction file configured by `[paths].instructions_file` for project conventions before planning

## On Activation

### Step 1: Load Context
Read the repository's root/IDE instructions and `_bmad/config.toml`, then read `[paths].work_items_file` for the active hierarchy, parent, status, and dependencies. Load the relevant Initiative/Epic and plan; use Project Spec and other mapped context sources for technical overview, codebase map, build/test commands, risks, and ADRs.

### Step 2: Assess the Input
Classify the input:
- **Structured** (a PRD, a detailed spec, a well-written bug report) → trust the structure, extract directly
- **Mixed** (brain dump, chat transcript, rough notes) → sort and distill yourself
- **Sparse** (one sentence, vague idea) → elicit more detail before planning; suggest `bmad-brainstorm` if the idea is too raw

Before eliciting, inspect relevant repository and parent-work evidence. Distinguish one cohesive outcome from multiple independently shippable outcomes; multiple files, layers, or verbs do not by themselves mean the work should split. If independent outcomes exist, recommend `/bmad-decompose` or ask the user to select/scope them; never silently drop one. Identify user-visible intent gaps, irreversible actions, and implementation footprint. Resolve gaps from evidence first, then record unresolved material choices as open questions; include irreversible actions and safeguards in risks/steps and identify affected files/contracts in the implementation plan.

### Step 3: Choose Depth
Based on complexity and risk, choose light/standard/full. Confirm with user if ambiguous.

### Step 4: Load Persona (if needed)
For standard/full depth, adopt the appropriate persona from `[paths].agents_dir`. Choose based on the input type:

| Input type | Persona | File |
|---|---|---|
| Feature request, user story, requirements, scope definition | **John (PM)** 📋 | `john-pm.md` |
| Architecture design, component layout, data flow, ADRs | **Winston (Architect)** 🏗️ | `winston-architect.md` |
| Research-heavy, competitive analysis, pre-mortem, alternatives | **Mary (Analyst)** 📊 | `mary-analyst.md` |
| Backlog prioritization, scope tradeoffs, acceptance decisions | **Pat (PO)** 📌 | `pat-po.md` |
| User-interface or interaction requirements | **Sally (UX)** 🎨 | `sally-ux.md` |

If the input spans multiple domains, load the primary persona and note which other personas should be consulted at review time. Read the persona file and embody it for the planning session.

### Step 5: Produce the Plan

---

Resolve output paths and project-specific verification requirements from `_bmad/config.toml` and the repository context before using the templates below.

## Light Depth Plan

```markdown
# <Title>
**Status:** Planned
**ID:** <S-number>
**Depth:** Light
**Flow:** <Quick | Full>
**Initiative:** <I-number and slug, if applicable>
**Parent Epic:** <E-number and slug, or "standalone">
**Parent Branch:** <epic branch or configured base branch>
**Branch:** `story/s<number>-<slug>` or `bug/s<number>-<slug>`
**Created:** <date>

## What
<One paragraph — the problem and the fix>

## Steps
- [ ] 1. <Concrete action>
- [ ] 2. <Concrete action>

## Verification
- [ ] The project's documented build/check command passes, when applicable
- [ ] <Any specific test>

## Risks
<None / specific risk>
```

---

## Standard Depth Plan

Uses the **five-field kernel** adapted from BMAD Spec Law:

```markdown
# <Title>
**Status:** Planned
**ID:** <S-number>
**Depth:** Standard
**Flow:** <Quick | Full>
**Initiative:** <I-number and slug, if applicable>
**Parent Epic:** <E-number and slug, or "standalone">
**Parent Branch:** <epic branch or configured base branch>
**Branch:** `story/s<number>-<slug>` or `bug/s<number>-<slug>`
**Created:** <date>

## Why (Motivation)
<What problem does this solve? Why now? What's the impact if we don't do it?>

## What (Specification)

### Capabilities
<!-- Each capability has BOTH intent AND success signal -->
| ID | Intent (WHAT) | Success Signal (how to verify) |
|---|---|---|
| CAP-1 | <What the system will do> | <Concrete, testable outcome> |
| CAP-2 | ... | ... |

### Constraints
<!-- Only constraints that actually bend design decisions -->
- <Constraint 1>: <What it rules out / what it mandates>
- <Constraint 2>: ...

### Non-goals
<!-- Explicitly what we are NOT doing — at least one -->
- <Non-goal 1>
- <Non-goal 2>

### Success Signal
<One concrete, measurable outcome that says "this is done and working">

## How (Implementation Plan)

### Architecture Notes
<!-- Component diagram (mermaid), data flow, integration points -->
<Only if the change spans multiple components. Otherwise omit.>

### Steps
- [ ] 1. <Step title>
  - **Files:** `<path1>`, `<path2>`
  - **What:** <Concrete action>
  - **Verify:** <How to check this step is correct>
- [ ] 2. ...

### Dependencies
- New packages or external dependencies: <list or "none">
- Data-store, persistence, or protocol changes: <list or "none">
- Configuration, deployment, or migration changes: <list or "none">

## Risks & Edge Cases
| Risk | Likelihood | Mitigation |
|---|---|---|
| <What could go wrong> | Low/Med/High | <How we handle it> |

## Verification Checklist
- [ ] The project's documented build and test commands pass
- [ ] Configured codebase/architecture maps updated if structure changed
- [ ] Resource ownership, cleanup, and dependency lifetimes follow documented project rules
- [ ] <Any specific tests or manual checks>
```

---

## Full Depth Plan

For major features, produce a **spec folder** under `[paths.full_specs_dir]`, named `spec-S<n>-<slug>/`:

```
<configured-full-specs-dir>/spec-S<n>-<slug>/
├── spec-S<n>-<slug>.md      ← The kernel (Why, Capabilities, Constraints, Non-goals, Success)
├── architecture.md           ← Component design, data flow, ADRs (or adopt from bmad-architecture)
├── tickets.toml              ← Break-down into implementable tickets
├── .memlog.md                ← Append-only decision log (see Memlog section)
└── <companion>.md            ← Optional: glossary, brownfield notes, stack decisions
```

### Spec Kernel (`spec-S<n>-<slug>.md`)

```markdown
---
companions: [architecture.md]
sources: []
---

# Spec: <Title>

## Why
<Motivation — the problem, the opportunity, the context>

## Capabilities
| ID | Intent | Success Signal |
|---|---|---|
| CAP-1 | ... | ... |

## Constraints
- ...

## Non-goals
- ...

## Success Signal
<One concrete, measurable outcome>

## Open Questions
- Q1: ...
- Q2: ...

## Assumptions
- A1: ...
```

### Memlog (`.memlog.md`)

The memlog is an **append-only, chronological record** of every decision, constraint, capability, assumption, open question, and user direction. It is the **canonical source** — the spec file is derived from it, never hand-edited after initial creation.

Format (one line per entry):
```
<timestamp> | <type> | <text>
```

Types: `decision`, `constraint`, `capability`, `assumption`, `question`, `direction`, `note`, `event`

Rules:
- **Never edit or reorder** existing entries — only append
- A later entry on the same topic **supersedes** an earlier one
- Terminal moments (spec finalized, review passed) are `event` entries
- The spec file is **re-derived from the memlog** on each update

### Step N: Update project state

After the plan is finalized and saved:
- Resolve `[paths].work_items_file` and record the allocated ID, owner/parent, dependencies, exact branch name, and `Planned` status there. Preserve the branch state already recorded by `/start-flow`; do not reset an active branch to `Not created`. Update the owning Epic's child list in the same approved operation when required by this workflow.
- For a standalone Story/Bug, add it to the appropriate WORK_ITEMS section as active work, not to completed history.
- Update `**Updated:**` in WORK_ITEMS and `**Last updated:**` in Project Spec only when their respective content changes.
- If this plan changes Initiative or Epic scope, use `/bmad-decompose` and update its configured parent record before implementation.

### Tickets (`tickets.toml`)

Break the spec into independently implementable tickets:

```toml
# Ticket IDs here are local to this spec; global work-item IDs remain I/E/S.
[[tickets]]
id = "ITEM-1"
title = "<Short title>"
capability = "CAP-1"
description = "<What to implement>"
files = ["<path1>", "<path2>"]
depends_on = []
estimated_hours = 2

[[tickets]]
id = "ITEM-2"
title = "<Short title>"
capability = "CAP-2"
description = "<What to implement>"
files = ["<path3>"]
depends_on = ["ITEM-1"]
estimated_hours = 3
```

---

## Spec Law (applies to standard and full depth)

Every plan must satisfy these rules. Self-validate before presenting.

1. **Each capability has both `intent` and `success`.** Missing either = not a capability.
2. **Intents describe WHAT, not HOW.** Implementation prescription belongs in Steps, not Capabilities.
3. **Constraints actually bend design decisions.** A "constraint" that rules nothing out is decoration — remove it.
4. **Non-goals are explicit.** At least one. Absence means downstream fills the vacuum.
5. **Success signal is concrete enough to test against.** "It works" doesn't qualify.
6. **Capability IDs are stable and unique.** Never reused, never renumbered.
7. **Preservation.** Every load-bearing claim from the input lands in the plan.
8. **Lean prose.** Every sentence carries load-bearing content. Cut decoration, hedges, backstory.

## Self-Validate

After producing the plan, sweep it in two passes:

### Pass 1 — Coherence
Check against Spec Law rules 1–6 and 8. Fix anything that fails. Gaps you can't fill become `Open Questions` or `Assumptions`.

### Pass 2 — Preservation
Walk the input claim by claim. Confirm each load-bearing claim landed in the plan. If something was intentionally dropped, note why.

## Output

Present the plan to the user. For light/standard: the plan file content. For full: the spec folder path and a summary (capability count, companions, open questions).

**Wait for user approval** before suggesting `/bmad-build`. The plan is a contract — don't build until it's signed off.

## After Approval

- Update the plan status to `**Status:** Approved` only after the user explicitly approves the plan.
- Set `_bmad/config.toml` → `active_initiative` to the full Initiative ID/slug (for example `I1-computer-vision-rl-overhaul`) if this is the active work; do not copy status rows into Project Spec
- Suggest next step: `/bmad-build` (or `/bmad-architecture` if architecture is non-trivial)
- `{stories_dir}` = `[paths.stories_dir]` from `_bmad/config.toml`
- All paths relative to `{project-root}`
- Use Russian for comments matching existing file language; English for new files
