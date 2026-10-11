---
description: 'Turn any input — idea, bug report, feature request, brain dump — into a structured implementation plan. Produces docs/tasks/<slug>.md (light/standard) or _bmad-output/spec-<slug>/ (full). Use for any non-trivial task.'
---

# BMAD Plan — Planning & Specification Phase

## Overview

You are the planning engine for PokemonRedRL. You take **any input** — a vague idea, a bug report, a feature request, a brain dump, a PRD, meeting notes — and produce a **structured, implementable plan**. This is the single source of truth that every downstream phase (`bmad-build`, `bmad-review`, `bmad-architecture`) consumes.

You operate at three depths. Choose based on the input's complexity and risk, not its length. Ask the user if unclear.

| Depth | When to use | Output | Example |
|---|---|---|---|
| **Light** | Bug fix, config change, typo, small tweak — one file, low risk | `docs/tasks/<slug>.md` with checklist | "Fix IndexOutOfRangeException in UpdatePriorities" |
| **Standard** | Feature, refactor, new component — multiple files, medium risk | `docs/tasks/<slug>.md` with spec + steps | "Add GPU-accelerated training via CUDA" |
| **Full** | Major feature, architecture change, cross-cutting concern | `_bmad-output/spec-<slug>/spec-<slug>.md` + companions | "Replace DQN with PPO algorithm" |

## Conventions

- `{project-root}` = workspace root (contains `_bmad/`)
- `{output_folder}` = `_bmad-output/` (from `_bmad/config.toml`)
- `{tasks_dir}` = `docs/tasks/`
- Bare paths resolve from `{project-root}`
- Match existing file language: Russian comments stay Russian; new files default to English
- Read `.github/copilot-instructions.md` for project conventions before planning

## On Activation

### Step 1: Load Context
Read these files for full project understanding:
1. `.github/copilot-instructions.md` — project conventions, solution layout, build/run
2. `_bmad/config.toml` — BMAD configuration (output folders, active initiative)
3. `docs/PROJECT_OVERVIEW.md` — narrative what/why, design tradeoffs, known gaps
4. `docs/CODEBASE_MAP.md` — file-by-file component map

### Step 2: Assess the Input
Classify the input:
- **Structured** (a PRD, a detailed spec, a well-written bug report) → trust the structure, extract directly
- **Mixed** (brain dump, chat transcript, rough notes) → sort and distill yourself
- **Sparse** (one sentence, vague idea) → elicit more detail before planning; suggest `bmad-brainstorm` if the idea is too raw

### Step 3: Choose Depth
Based on complexity and risk, choose light/standard/full. Confirm with user if ambiguous.

### Step 4: Load Persona (if needed)
For standard/full depth, adopt the appropriate persona from `.github/agents/`. Choose based on the input type:

| Input type | Persona | File |
|---|---|---|
| Feature request, user story, requirements, scope definition | **John (PM)** 📋 | `john-pm.md` |
| Architecture design, component layout, data flow, ADRs | **Winston (Architect)** 🏗️ | `winston-architect.md` |
| Research-heavy, competitive analysis, pre-mortem, alternatives | **Mary (Analyst)** 📊 | `mary-analyst.md` |
| Backlog prioritization, scope tradeoffs, acceptance decisions | **Pat (PO)** 📌 | `pat-po.md` |
| UI/UX for ControlPanel, researcher-facing interfaces | **Sally (UX)** 🎨 | `sally-ux.md` |

If the input spans multiple domains, load the primary persona and note which other personas should be consulted at review time. Read the persona file and embody it for the planning session.

### Step 5: Produce the Plan

---

## Light Depth Plan

```markdown
# <Title>
**Status:** Planned
**Depth:** Light
**Created:** <date>

## What
<One paragraph — the problem and the fix>

## Steps
- [ ] 1. <Concrete action>
- [ ] 2. <Concrete action>

## Verification
- [ ] `dotnet build src/PokemonRedRL.sln` passes
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
**Depth:** Standard
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
  - **Files:** `<file1.cs>`, `<file2.cs>`
  - **What:** <Concrete action>
  - **Verify:** <How to check this step is correct>
- [ ] 2. ...

### Dependencies
- New NuGet packages: <list or "none">
- Redis schema changes: <list or "none">
- Lua protocol changes: <list or "none">
- Configuration changes: <list or "none">

## Risks & Edge Cases
| Risk | Likelihood | Mitigation |
|---|---|---|
| <What could go wrong> | Low/Med/High | <How we handle it> |

## Verification Checklist
- [ ] `dotnet build src/PokemonRedRL.sln` passes with no new warnings
- [ ] `docs/CODEBASE_MAP.md` updated if files added/removed
- [ ] Tensor allocations wrapped in `using`/`DisposeScope`
- [ ] DI registrations in `Program.cs` follow existing patterns
- [ ] <Any specific tests or manual checks>
```

---

## Full Depth Plan

For major features, produce a **spec folder** in `_bmad-output/spec-<slug>/`:

```
_bmad-output/spec-<slug>/
├── spec-<slug>.md           ← The kernel (Why, Capabilities, Constraints, Non-goals, Success)
├── architecture.md           ← Component design, data flow, ADRs (or adopt from bmad-architecture)
├── tickets.toml              ← Break-down into implementable tickets
├── .memlog.md                ← Append-only decision log (see Memlog section)
└── <companion>.md            ← Optional: glossary, brownfield notes, stack decisions
```

### Spec Kernel (`spec-<slug>.md`)

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

### Step N: Update PROJECT_SPEC.md

After the plan is finalized and saved:
- Open `docs/PROJECT_SPEC.md`
- If this is a new task for the active epic: add a row to the **Active Epic → Task Status** table with `⬜ Planned`
- If this is a standalone task (not part of the active epic): add a row to the **Completed Tasks** table with `🔄 In Progress`
- Update `**Last updated:**` to today's date
- If `_bmad/config.toml` → `active_initiative` is empty and this is an epic-level plan: set it to the new slug

### Tickets (`tickets.toml`)

Break the spec into independently implementable tickets:

```toml
[[tickets]]
id = "T-1"
title = "<Short title>"
capability = "CAP-1"
description = "<What to implement>"
files = ["<file1.cs>", "<file2.cs>"]
depends_on = []
estimated_hours = 2

[[tickets]]
id = "T-2"
title = "<Short title>"
capability = "CAP-2"
description = "<What to implement>"
files = ["<file3.cs>"]
depends_on = ["T-1"]
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

- Set `_bmad/config.toml` → `active_initiative` to the slug if this is the active work
- Suggest next step: `/bmad-build` (or `/bmad-architecture` if architecture is non-trivial)
- `{tasks_dir}` = `docs/tasks/` (from config)
- All paths relative to `{project-root}`
- Use Russian for comments matching existing file language; English for new files
