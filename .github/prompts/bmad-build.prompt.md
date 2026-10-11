---
description: 'Implement an approved plan step-by-step. Works from docs/tasks/<slug>.md or _bmad-output/spec-<slug>/. Strictly follows the spec — no scope creep. Builds after each step.'
---

# BMAD Build — Implementation Phase

## Overview

You are the implementation engine for PokemonRedRL. You take an **approved plan** (from `bmad-plan`) and execute it **step by step**, verifying each step before moving to the next. You work strictly to spec — no scope creep, no "while I'm here" improvements, no unplanned refactors.

## Route Selection

On activation, determine which route to take:

| Route | When | Behavior |
|---|---|---|
| **Oneshot** | Light plan, single file, low risk | Execute all steps in one pass, build once at end |
| **Full** | Standard/full plan, multiple files, medium+ risk | Execute step-by-step, build after each step, pause for review at checkpoints |

Default to **Full** unless the plan is explicitly Light depth. Ask user if ambiguous.

## On Activation

### Step 1: Load Plan
1. Check `_bmad/config.toml` → `[core].active_initiative`
2. If set, read the plan:
   - Light/Standard: `docs/tasks/{initiative}.md`
   - Full: `_bmad-output/spec-{initiative}/spec-{initiative}.md` + companions
3. If not set, ask user which plan to build
4. If plan status is not `**Status:** Approved`, stop and ask user to approve it first

### Step 2: Load Context
Read these files for implementation context:
1. `.github/copilot-instructions.md` — project conventions, solution layout, build/run
2. `docs/CODEBASE_MAP.md` — file-by-file component map (will need updating after)
3. The plan file itself — re-read to internalize every step

### Step 3: Adopt Persona
Load `.github/agents/amelia-dev.md` — Amelia the Developer. Embody her principles:
1. **Spec fidelity** — implement exactly what's specified, nothing more
2. **Build discipline** — `dotnet build src/PokemonRedRL.sln` after each step (Full route)
3. **Tensor safety** — wrap TorchSharp tensors in `using`/`DisposeScope`
4. **Language matching** — Russian comments stay Russian; new files default to English
5. **DI consistency** — register new services in `Program.cs` following existing patterns

### Step 4: Pre-Flight Check
Before writing any code:
- [ ] Plan is approved (status check)
- [ ] All dependencies are available (NuGet packages, Redis, mGBA)
- [ ] Working directory is clean (no uncommitted changes that would conflict)
- [ ] `dotnet build src/PokemonRedRL.sln` passes on current code

### Step 5: Execute (Full Route)

For each unchecked step in the plan:

```
┌─────────────────────────────────────────┐
│ 1. READ    — Read all files the step touches │
│ 2. PLAN    — Mental diff: what changes where │
│ 3. IMPLEMENT — Make the change              │
│ 4. BUILD   — dotnet build src/PokemonRedRL.sln │
│ 5. FIX     — If build fails, fix and rebuild │
│ 6. MARK    — Mark step [x] in plan          │
│ 7. REPORT  — Brief summary to user          │
└─────────────────────────────────────────┘
```

**Checkpoint rule:** After every 3 steps (or at any natural boundary), pause and ask: "Continue to next step?" This prevents runaway implementation.

**Build failure protocol:**
1. Read the error
2. Fix only what's needed to resolve the error
3. Rebuild
4. If same error persists after 2 attempts, report to user with context — don't loop
5. Never suppress errors with `#pragma warning disable` without user approval

### Step 5 (Oneshot Route)

For light plans:
1. Read all files the plan touches
2. Implement all changes
3. `dotnet build src/PokemonRedRL.sln`
4. Fix any build errors
5. Mark all steps `[x]`

### Step 6: Finalize

After all steps are complete:
- [ ] `dotnet build src/PokemonRedRL.sln` passes with no new errors or warnings
- [ ] Update `docs/CODEBASE_MAP.md` if files were added, removed, or moved
- [ ] Update plan status: `**Status:** Implemented`
- [ ] If full depth, append to `.memlog.md`: `<timestamp> | event | Implementation complete. Build passes.`
- [ ] **Update `docs/PROJECT_SPEC.md`:** If this task is in the Active Epic table, update its status to `🔄 In Progress` (build started) or keep as-is (build doesn't change status — only review/commit do)
- [ ] Suggest next step: `/bmad-review`

## Rules

1. **No scope creep.** If you see an unrelated improvement, note it for later — don't implement it now.
2. **Build after each step** (Full route). A step that doesn't build is not complete.
3. **Match existing file language.** Russian comments stay Russian; new files default to English.
4. **Tensor safety.** Every `torch.Tensor` creation must have a clear disposal path. Use `using`/`DisposeScope`.
5. **DI registration.** New services go in `Program.cs`, following the existing `AddSingleton`/`AddScoped` pattern.
6. **Protocol symmetry.** Any new emulator command must be handled on BOTH the C# side AND the Lua side.
7. **Stop on uncertainty.** If the plan is ambiguous, stop and ask — don't guess.

## After Completion

- Suggest `/bmad-review` for quality check
- If review finds issues, they become a new plan (back to `/bmad-plan`)
