---
mode: agent
description: Execute a reviewed implementation plan step by step.
---

# Implement Task

Your job is to **execute** the plan that already passed `/plan-task` and `/review-plan` — not to re-plan or second-guess the reviewed scope without good reason.

## Steps

1. **Load the reviewed plan** from `docs/tasks/<slug>.md` (`Status: Reviewed`) and mirror it into `manage_todo_list`. If the plan file doesn't exist or is still `Draft`, tell the user to run `/plan-task` (and `/review-plan`) first and stop.
2. **Work through the plan one step at a time:**
   - Mark exactly one todo `in-progress` before starting it.
   - Read any file before editing it.
   - Make the smallest correct change for that step — follow the conventions in [.github/copilot-instructions.md](../copilot-instructions.md) (DI registration patterns, `Interfaces/`+`Services/` folder split, TorchSharp tensor disposal, matching existing comment language in a file you're editing).
   - Mark the todo `completed` immediately, and tick the matching `- [ ]` checkbox to `- [x]` in `docs/tasks/<slug>.md` — don't batch completions.
3. **Verify incrementally.** After meaningful chunks of work, build the affected project(s) (`dotnet build src/PokemonRedRL.sln` or a targeted `.csproj`) and use `get_errors` to catch compile issues early rather than at the end.
4. **If you hit a blocker or the plan turns out to be wrong/incomplete** (missing step, wrong assumption, new edge case discovered): stop, explain the discrepancy, update the plan, and — if the change in approach is significant — recommend a quick pass back through `/review-plan` rather than silently improvising a large deviation.
5. **If the change affects repo structure** (new/removed/moved files or projects), run `/cartograph` to refresh `docs/CODEBASE_MAP.md` as part of this step, not as an afterthought.
6. **When all steps are complete**, do a final `dotnet build` of the whole solution, set `Status: Implemented` in `docs/tasks/<slug>.md`, and summarize what changed, file by file.
7. **Stop after implementation.** Tell the user the change is ready for `/review-changes`. Do not commit or push in this step (the updated `docs/tasks/<slug>.md` stays as an uncommitted/staged change, to be committed together with the code in `/commit-changes`).

## Output discipline

- Don't add functionality, refactors, or "improvements" beyond what the plan calls for.
- Don't add tests unless the plan calls for them — but do flag untested non-trivial logic for the user to decide on.
- Never run destructive git commands (`reset --hard`, force-push, history rewrites) in this step.
