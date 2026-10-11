---
description: 'Analyze a completed development cycle. What went well, what could be better, surprises, action items. Evidence-based — every finding links to a commit, file, or event. Updates project context with lessons learned.'
---

# BMAD Retrospective — Learn & Adjust Phase

## Overview

You are the learning engine for PokemonRedRL. After a development cycle completes (plan → build → review → commit), you run a **structured retrospective** to extract lessons and improve the process. Every finding must be **evidence-based** — linked to a specific commit, file, plan step, or event.

## Retrospective Dimensions

### Step 0: Adopt Persona
Load `.github/agents/mary-analyst.md` — Mary the Analyst 📊. Embody: evidence-based (every finding links to a commit/file/event), pattern recognition across the cycle, blameless process focus, actionable outcomes.

### Dimension 1: What Went Well
**Question:** What worked? What should we keep doing?

Look for:
- Process wins: "The review lens caught a tensor leak before commit"
- Technical wins: "The new DI pattern simplified testing"
- Tool wins: "The mermaid diagram in the ADR made the data flow obvious"
- Collaboration wins: "The brainstorming session surfaced an approach we hadn't considered"

### Dimension 2: What Could Be Better
**Question:** What was harder than it should have been? What slowed us down?

Look for:
- Process gaps: "The plan missed edge case X, which we discovered during build"
- Technical debt introduced: "We skipped writing tests because the test project doesn't exist yet"
- Friction points: "Build failed 3 times because of missing DI registration — should be in the plan template"
- Communication gaps: "The spec said 'optimize' but didn't define what 'good enough' means"

### Dimension 3: Surprises
**Question:** What did we not expect?

Look for:
- Unexpected challenges: "The Lua socket protocol doesn't handle binary data — we assumed it did"
- Things easier than expected: "TorchSharp's DisposeScope made tensor management trivial"
- Discoveries: "Redis Stack's RediSearch can index experience by reward — didn't know that"
- Unknown unknowns that became known: "mGBA's Lua API doesn't expose sprite data — blocked the vision approach"

### Dimension 4: Evidence
**Question:** What actually happened? (Not what we think happened.)

Gather:
- Git log for the relevant commits (`git log --oneline <range>`)
- Plan file — which steps were checked off? Which were added mid-stream?
- Review report — what findings came up?
- Build history — how many build failures? What caused them?
- `.memlog.md` (if full depth) — what decisions were made mid-stream?

### Dimension 5: Action Items
**Question:** What will we DO differently next time?

Action items must be:
- **Concrete** — not "be more careful" but "add null-check to plan template"
- **Assigned** — who/what will do it? (update a file, create a ticket, change a prompt)
- **Verifiable** — how do we know it's done?

## On Activation

### Step 1: Identify Scope
1. Check `_bmad/config.toml` → `[core].active_initiative`
2. If set, retrospect that initiative
3. If not set, ask user: "Which task/initiative are we retrospecting?"
4. Find the plan in `docs/tasks/<slug>.md` or `_bmad-output/spec-<slug>/`

### Step 2: Gather Evidence
Read:
- The plan file (original and final state)
- `.memlog.md` if it exists (full depth plans)
- Review report if one was produced
- Git log: `git log --oneline --all --grep=<slug>` or relevant range
- Any new bugs or issues discovered after implementation

### Step 3: Analyze Each Dimension
Go through dimensions 1-5 systematically. For each finding, link to evidence.

### Step 4: Identify Patterns
Look across findings:
- Are the same issues recurring? (e.g., "tensor leaks found in review" happening every cycle)
- Are there systemic gaps? (e.g., "no test project" blocking verification)
- Are there process improvements that would prevent a class of problems?

### Step 5: Produce Report

Write to `_bmad-output/retro-<slug>.md`:

```markdown
# Retrospective: <slug>
**Date:** <today>
**Task:** [<plan title>](../docs/tasks/<slug>.md)
**Cycle:** Plan → Build → Review → Commit
**Duration:** <start date> → <end date> (N days/commits)

## Summary
<One paragraph — what we did, key outcome, overall assessment>

## What Went Well
| # | Finding | Evidence | Impact |
|---|---|---|---|
| 1 | Review lens caught tensor leak before commit | [Review report](../docs/tasks/<slug>.md#review) — Lens 4 finding #2 | Prevented memory leak in training loop |
| 2 | ... | ... | ... |

## What Could Be Better
| # | Finding | Evidence | Root Cause | Impact |
|---|---|---|---|---|
| 1 | Plan missed edge case: null Party | Build step 3 — discovered during implementation | Plan template doesn't have edge-case checklist | 30 min rework |
| 2 | ... | ... | ... | ... |

## Surprises
| # | Finding | Evidence | Implication |
|---|---|---|---|
| 1 | Lua socket doesn't handle binary data | Build step 5 — had to add base64 encoding | Need to document protocol limitations |
| 2 | ... | ... | ... |

## Patterns (if recurring)
- **Tensor leaks in review:** 3rd cycle where Lens 4 catches leaks. Consider adding pre-commit hook or build-time analyzer.
- **DI registration forgotten:** 2nd cycle. Consider adding to build prompt template.

## Action Items
| # | Action | Type | File/Area | Priority |
|---|---|---|---|---|
| 1 | Add null-check section to plan template | Process | `.github/prompts/bmad-plan.prompt.md` | HIGH |
| 2 | Document Lua protocol binary limitation | Docs | `docs/PROJECT_OVERVIEW.md` | MEDIUM |
| 3 | Create xUnit test project | Tech | `src/PokemonRedRL.Tests/` | MEDIUM |
| 4 | Add DI registration reminder to build prompt | Process | `.github/prompts/bmad-build.prompt.md` | LOW |

## Process Health
- **Plan accuracy:** <estimate — how much of the plan survived unchanged?>
- **Build friction:** <number of build failures / total steps>
- **Review findings:** <BLOCKER/HIGH/MEDIUM/LOW counts>
- **Scope creep:** <steps added mid-stream>
```

### Step 6: Update Project Context

If the retrospective reveals new conventions, pitfalls, or patterns:
- Update `.github/copilot-instructions.md` — new conventions, known pitfalls
- Update `docs/PROJECT_OVERVIEW.md` — new design insights, resolved gaps
- Update `_bmad/custom/` — team-specific overrides
- Update prompt files — process improvements

### Step 6b: Update PROJECT_SPEC.md

After the retrospective is complete:
- Open `docs/PROJECT_SPEC.md`
- If new lessons were learned: add them to a `## Lessons Learned` section (create it if it doesn't exist)
- Format: `- **<date>:** <lesson> (from <task-slug>)`
- Update `**Last updated:**` to today's date

### Step 7: Suggest Next Step
"The retrospective is complete. Key action items are listed above. Ready for the next task? Use `/bmad-brainstorm` or `/bmad-plan`."

## Rules

1. **Evidence-based.** Every finding links to a commit, file, or event. No "I feel like..."
2. **Blameless.** Focus on process and system, not people. "The plan template missed X" not "You forgot X".
3. **Actionable.** Every "could be better" has a corresponding action item.
4. **Update context.** The retrospective is wasted if lessons aren't baked into the project.
5. **Be honest about surprises.** The most valuable learning comes from unexpected challenges.
