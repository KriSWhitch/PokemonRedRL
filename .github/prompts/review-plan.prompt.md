---
mode: agent
description: Critically review an existing implementation plan for gaps and risks before coding starts.
---

# Review Plan

Your job is to **critique**, not to implement or re-plan from scratch.

## Steps

1. **Load the current plan.** Read it from `docs/tasks/<slug>.md` (the file created by `/plan-task`) and cross-check against the todo list (`manage_todo_list`). If no plan file exists, tell the user to run `/plan-task` first and stop.
2. **Re-verify assumptions against the actual codebase.** Don't trust the plan's claims blindly — spot-check the files it references with `read_file`/`grep_search` to confirm they exist and behave as described.
3. **Look specifically for:**
   - **Missing steps**: DI registration in `Program.cs`, both sides of the C#↔Lua protocol, tensor disposal in TorchSharp code, updates to `docs/CODEBASE_MAP.md` if structure changes.
   - **Edge cases**: connection retries/timeouts, empty Redis/experience-repository results, cancellation (`CancellationTokenSource`) handling, concurrent agents contending on shared state (`ParameterServer`, Redis keys).
   - **Security issues**: secrets/connection strings hardcoded vs. config, unsanitized input crossing the TCP boundary.
   - **Irreversible or risky actions**: git history rewrites, deleting files, schema/protocol changes that break compatibility with the Lua side, force-pushes.
   - **Scope creep**: steps doing more than the original request asked for.
   - **Ordering problems**: steps that depend on later steps, or that can't be verified incrementally.
4. **Produce a review verdict**, structured as:
   - ✅ What's solid.
   - ⚠️ Gaps/risks found, each with a concrete suggested fix to the plan.
   - ❓ Open questions that need a decision before implementing.
5. **Update the plan file** `docs/tasks/<slug>.md` directly (and the mirrored todo list) to incorporate the fixes, rather than just listing them — the plan artifact should be implementation-ready after this step. Set its `Status:` field to `Reviewed` once gaps are resolved (or leave `Draft` if it needs another pass).
6. **Tell the user** whether the plan is ready for `/implement-task`, or needs another round of `/plan-task`/`/review-plan` if the gaps are significant.

## Output discipline

- The only file you edit in this step is `docs/tasks/<slug>.md` (no source code edits).
- Be genuinely critical — the point of this step is to catch what `/plan-task` missed, not to rubber-stamp it.
