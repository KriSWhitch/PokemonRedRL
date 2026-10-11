# BMAD Migration Guide — PokemonRedRL

## What Changed

The project workflow has been migrated from custom prompts (`/plan-task`, `/review-plan`, `/implement-task`, `/review-changes`, `/commit-changes`) to a **BMAD-adapted workflow**.

## New Prompts (use these now)

| Old Prompt | New BMAD Prompt | Notes |
|---|---|---|
| `/plan-task` | `/bmad-plan` | Now supports 3 depth levels (light/standard/full) |
| `/review-plan` | *(part of `/bmad-plan`)* | Review is built into the plan phase |
| `/implement-task` | `/bmad-build` | Now loads Amelia persona automatically |
| `/review-changes` | `/bmad-review` | Now uses 6 review lenses |
| `/commit-changes` | `/bmad-commit` | Same behavior, new name |
| — | `/bmad-brainstorm` | NEW: structured brainstorming |
| — | `/bmad-architecture` | NEW: architecture design + ADRs |
| — | `/bmad-code-review` | NEW: standalone code review |
| — | `/bmad-retrospective` | NEW: learn & adjust phase |

## Existing Tasks

Your existing task plans in `docs/tasks/` are **fully compatible**. They already follow a similar format (Status field, step checklist). To migrate an existing task:

1. The task file stays in `docs/tasks/<slug>.md`
2. Use `/bmad-build` to implement remaining steps
3. Use `/bmad-review` instead of `/review-changes`
4. Use `/bmad-commit` instead of `/commit-changes`

No file format changes needed — the existing `Status:` field and `- [ ]` checklists work with BMAD prompts.

## New Files Created

```
_bmad/
├── config.toml              ← BMAD project configuration
├── custom/                  ← your personal overrides (edit freely)
_bmad-output/                ← future BMAD artifacts go here
.github/
├── agents/
│   ├── amelia-dev.md        ← Developer persona (💻)
│   ├── john-pm.md           ← PM persona (📋)
│   ├── winston-architect.md ← Architect persona (🏗️)
│   ├── mary-analyst.md      ← Analyst persona (📊)
│   ├── sally-ux.md          ← UX Designer persona (🎨)
│   ├── pat-po.md            ← Product Owner persona (📌) — NEW
│   └── quinn-qa.md          ← QA persona (🧪) — NEW
├── prompts/
│   ├── bmad-plan.prompt.md         ← Planning (3 depths, Spec Law, memlog)
│   ├── bmad-build.prompt.md        ← Implementation (oneshot/full routes)
│   ├── bmad-review.prompt.md       ← Multi-lens review (6 lenses)
│   ├── bmad-commit.prompt.md       ← Conventional commits + push gate
│   ├── bmad-brainstorm.prompt.md   ← Ideation (3 stances, 7 techniques)
│   ├── bmad-architecture.prompt.md ← Architecture (spine, ADRs, Coaching/Fast)
│   ├── bmad-code-review.prompt.md  ← Standalone code review (5 dimensions)
│   └── bmad-retrospective.prompt.md ← Learning (5 dimensions, evidence-based)
```

## Agent Quality (v2 — BMAD-depth)

All 5 agents have been rewritten with real BMAD agent architecture:

| Feature | v1 (basic) | v2 (BMAD-depth) |
|---|---|---|
| Activation protocol | 1 line | 6-8 step protocol |
| Menu system | ❌ | ✅ Code + Description + Prompt dispatch |
| Persistent facts | ❌ | ✅ Project-specific facts loaded on activation |
| Icon prefix | ❌ | ✅ 💻 📋 🏗️ 📊 🎨 |
| Principles | 5 generic | 6-7 project-specific, actionable |
| Communication format | Free text | Structured: `icon file:line — what — why` |
| Sally (UX Designer) | ❌ | ✅ New agent for ControlPanel UI/UX |
| Pat (Product Owner) | ❌ | ✅ New agent for backlog/scope/priority decisions |
| Quinn (QA) | ❌ | ✅ New agent for quality verification and test design |

## Prompt Quality (v2 — BMAD-depth)

All 8 prompts have been rewritten with real BMAD methodology depth (100-300+ lines each):

| Feature | v1 (basic) | v2 (BMAD-depth) |
|---|---|---|
| Spec kernel (5-field) | ❌ | ✅ Why/Capabilities/Constraints/Non-goals/Success |
| Memlog (append-only decision log) | ❌ | ✅ `.memlog.md` with typed entries |
| Spec Law (8 self-validation rules) | ❌ | ✅ Coherence + Preservation passes |
| Route selection (oneshot/full) | ❌ | ✅ Build route; Coaching/Fast for architecture |
| Review lenses (parallel) | ❌ | ✅ 6 independent lenses with severity classification |
| Pre-mortem | Basic | ✅ Applied to every top candidate |
| Brainstorming stances | ❌ | ✅ Facilitator/Creative Partner/Ideate for Me |
| Architecture spine | ❌ | ✅ Invariants vs seed facts, altitude concept |
| Evidence-based retro | ❌ | ✅ Every finding linked to commit/file/event |
| Output contracts | Free text | ✅ Structured templates with required fields |
| Self-validation | ❌ | ✅ Reviewer gate, Spec Law sweep, pre-flight check |
| Headless/resume support | ❌ | ✅ Config-driven, active_initiative tracking |

## Quick Start

1. **Simple bug fix:**
   ```
   /bmad-plan light "Fix IndexOutOfRangeException in ExplorationAgent.UpdatePriorities"
   ```
   Then: `/bmad-build` → `/bmad-review` → `/bmad-commit`

2. **New feature:**
   ```
   /bmad-plan standard "Add GPU-accelerated training via CUDA"
   ```
   Then: `/bmad-architecture` (optional) → `/bmad-build` → `/bmad-review` → `/bmad-commit`

3. **Brainstorm + full cycle:**
   ```
   /bmad-brainstorm "How to improve reward shaping for exploration"
   ```
   Then: `/bmad-plan full` → `/bmad-architecture` → `/bmad-build` → `/bmad-review` → `/bmad-commit` → `/bmad-retrospective`

## No Dependencies

This is a **pure file-based adaptation**. No `npm install`, no `npx`, no Python/`uv`, no external tools. Everything works through GitHub Copilot's native `.github/prompts/` mechanism.
