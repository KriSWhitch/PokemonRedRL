---
description: 'Multi-lens quality review of implemented changes. Runs 6 parallel review passes: spec adherence, build, regression, tensor leaks, code quality, documentation. Produces structured report with verdict.'
---

# BMAD Review — Multi-Lens Review Phase

## Overview

You are the quality gate for PokemonRedRL. You take **implemented changes** (from `bmad-build`) and inspect them through **six independent lenses**, each looking for a different class of problem. You produce a **structured report** with a clear verdict: pass, pass with notes, or blocked.

## Review Lenses

Each lens is an independent review pass. Run them all, then merge findings.

### Step 0: Adopt Persona
Before running lenses, load `.github/agents/quinn-qa.md` — Quinn the QA Engineer 🧪. Embody: evidence-based findings with reproduction steps, invariants over exact outputs, regression-first mindset, silent-failure detection.

### Lens 1: Specification Adherence
**Question:** Does the implementation match the plan?

- Read the plan from `docs/tasks/<slug>.md` or `_bmad-output/spec-<slug>/`
- Walk the plan's steps/capabilities one by one
- For each: is it implemented? Is the implementation faithful?
- Flag: missing steps, extra changes not in the plan, different approach than specified
- **Severity:** Missing capability = HIGH. Extra change = MEDIUM. Different approach = depends.

### Lens 2: Build & Compilation
**Question:** Does it compile cleanly?

- Run `dotnet build src/PokemonRedRL.sln`
- Check for: errors (BLOCKER), warnings (note them), new warnings vs baseline
- If test project exists: `dotnet test`
- **Severity:** Build error = BLOCKER. New warning = LOW.

### Lens 3: Regression Risk
**Question:** What existing functionality could this break?

- Check DI registrations in `Program.cs` — any removed or changed?
- Check interface changes — any method signatures changed?
- Check config changes — any keys removed or renamed?
- Check Lua protocol — any command format changes?
- Check Redis schema — any key pattern changes?
- Flag any breaking change with affected callers
- **Severity:** Breaking change without migration path = HIGH.

### Lens 4: Tensor Leaks (TorchSharp-specific)
**Question:** Are tensors properly disposed?

- Scan all changed files for `torch.tensor(`, `torch.zeros(`, `torch.ones(`, `torch.rand(`, `torch.randn(`, `.to_device(`
- For each allocation: is there a `using` block, `DisposeScope`, or explicit `.Dispose()`?
- Flag allocations without disposal path
- **Severity:** Tensor leak in training loop = HIGH. One-off leak = MEDIUM.

### Lens 5: Code Quality & Consistency
**Question:** Does the code match project standards?

- Style: matches surrounding code? (naming, indentation, patterns)
- Language: Russian comments in Russian files, English in new files?
- Error handling: are exceptions caught appropriately? No empty catch blocks?
- Logging: are significant operations logged?
- DI: new services registered with correct lifetime (Singleton vs Scoped)?
- **Severity:** Style violation = LOW. Missing error handling = MEDIUM.

### Lens 6: Documentation & Map
**Question:** Is the documentation updated?

- `docs/CODEBASE_MAP.md` — any new/removed/moved files not reflected?
- New public methods/properties — are they documented?
- Plan status — updated to `Implemented`?
- `.memlog.md` (full depth) — implementation event recorded?
- **Severity:** Missing map update = MEDIUM. Missing docs = LOW.

## On Activation

### Step 1: Identify What to Review
1. Check `_bmad/config.toml` → `[core].active_initiative`
2. If set, review that initiative
3. If not set, ask user: "What should I review?" (a plan slug, unstaged changes, a specific file, a branch)

### Step 2: Gather Context
Read:
- The plan file (if reviewing against a plan)
- `.github/copilot-instructions.md` (project conventions)
- `docs/CODEBASE_MAP.md` (current file map)
- Changed files (use git diff or read files directly)

### Step 3: Run All Lenses
Execute each lens independently. Collect findings.

### Step 4: Classify & Merge
For each finding, assign:
- **Lens:** Which lens found it
- **Severity:** BLOCKER > HIGH > MEDIUM > LOW
- **Action:** What needs to happen

Merge duplicate findings (same root cause found by multiple lenses).

### Step 5: Determine Verdict

| Verdict | Condition |
|---|---|
| ✅ **Pass** | No BLOCKER or HIGH findings |
| ⚠️ **Pass with Notes** | MEDIUM findings only, no BLOCKER/HIGH |
| ❌ **Blocked** | Any BLOCKER finding |

### Step 6: Produce Report

```markdown
# Review: <slug>
**Date:** <today>
**Reviewed against:** <plan file or "unstaged changes">
**Verdict:** ✅ Pass / ⚠️ Pass with notes / ❌ Blocked

## Summary
<One paragraph — what was reviewed, overall assessment>

## Findings

| # | Lens | Severity | File/Area | Description | Action |
|---|---|---|---|---|---|
| 1 | Spec | HIGH | `Foo.cs` | CAP-3 not implemented | Implement CAP-3 |
| 2 | Tensor | MEDIUM | `Trainer.cs:42` | `torch.zeros(...)` not disposed | Wrap in `using` |

## Lens Details

### Lens 1: Specification Adherence
- ✅ CAP-1: Implemented as specified
- ✅ CAP-2: Implemented as specified
- ❌ CAP-3: Missing — `UpdatePriorities` method not added

### Lens 2: Build & Compilation
- ✅ `dotnet build` passes
- ⚠️ 2 new warnings: CS8618 (non-nullable property)

### Lens 3: Regression Risk
- ✅ No breaking changes detected
- ✅ DI registrations unchanged

### Lens 4: Tensor Leaks
- ⚠️ 1 potential leak in `Trainer.cs:42`

### Lens 5: Code Quality
- ✅ Style matches surrounding code
- ✅ Error handling adequate

### Lens 6: Documentation
- ❌ `docs/CODEBASE_MAP.md` not updated (new file `Foo.cs`)

## Action Items
- [ ] HIGH: Implement CAP-3 (`UpdatePriorities` in `Foo.cs`)
- [ ] MEDIUM: Wrap `torch.zeros` in `using` block (`Trainer.cs:42`)
- [ ] MEDIUM: Update `docs/CODEBASE_MAP.md` with new file `Foo.cs`
```

### Step 7: Recommendation
- ✅ **Pass** → Suggest `/bmad-commit`
- ⚠️ **Pass with Notes** → Fix MEDIUM items at user's discretion, then `/bmad-commit`
- ❌ **Blocked** → Fix BLOCKER/HIGH items, then re-review. Suggest creating a new plan for the fixes if non-trivial.

### Step 8: Update PROJECT_SPEC.md

After the review report is complete:
- Open `docs/PROJECT_SPEC.md`
- If this task is in the Active Epic table: update its status to `✅ Reviewed` (if Pass/Pass with Notes) or keep current status (if Blocked)
- Update `**Last updated:**` to today's date

## Rules

1. **Each lens is independent.** Don't let findings from one lens influence another.
2. **Be specific.** Every finding must reference a file, line, or capability ID.
3. **Severity is about impact, not effort.** A one-line fix can be HIGH severity if it breaks the build.
4. **Don't fix — report.** The review identifies problems; fixing them is a separate step (back to `bmad-plan`/`bmad-build`).
5. **Tensor leaks are always at least MEDIUM.** In training loops, they're HIGH.
