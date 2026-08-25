---
mode: agent
description: Turn a task/request into a concrete, ordered implementation plan.
---

# Plan Task

Your job is **planning only** — do not write or edit any source code in this step.

## Steps

1. **Clarify scope.** Restate the task in your own words. If the request is ambiguous or could be interpreted multiple ways, ask targeted clarifying questions (use `vscode_askQuestions`) before planning — but don't over-ask; infer sensible defaults for anything low-stakes.
2. **Gather context.** Use `semantic_search`, `grep_search`, and `read_file` to understand the relevant parts of the codebase before proposing changes. Consult [docs/CODEBASE_MAP.md](../../docs/CODEBASE_MAP.md) and [.github/copilot-instructions.md](../copilot-instructions.md) for architecture/conventions. If the map looks stale relative to what you find, note that as a follow-up (don't fix it here — that's `/cartograph`'s job).
3. **Check memory.** Look at `/memories/repo/` for previously recorded conventions or gotchas about this codebase, and `/memories/` for general user preferences, before planning.
4. **Produce a written plan** covering:
   - **Goal** — one or two sentences on what "done" looks like.
   - **Affected files/projects** — concrete paths, called out by project (`PokemonRedRL.Agent/Core/Models/DAL/Utils`, Lua scripts, docs).
   - **Ordered steps** — small, verifiable increments. Each step should be independently buildable/testable where possible.
   - **Risks & irreversible actions** — anything touching shared infra, deleting data, rewriting history, or affecting the TCP protocol contract between C# and Lua (changes to one side require the other to change too).
   - **Verification plan** — how you'll confirm it works (`dotnet build`, manual emulator run, unit tests if a test project exists/is being added).
   - **Explicit open questions/assumptions.**
5. **Store the plan as a committed file** at `docs/tasks/<slug>.md` (create `docs/tasks/` if absent; `<slug>` is a short kebab-case name for the task, e.g. `docs/tasks/adaptive-lr-persistence.md`). This file is the single source of truth for the plan across the whole pipeline and gets committed to the repo alongside the implementation — it is not a throwaway note. Use this structure:

   ```markdown
   # <Task title>

   Status: Draft

   ## Goal
   ## Affected files/projects
   ## Steps
   - [ ] Step 1 ...
   - [ ] Step 2 ...
   ## Risks & irreversible actions
   ## Verification plan
   ## Open questions/assumptions
   ```

   Also mirror the steps into `manage_todo_list` so live progress is tracked in-conversation; the `docs/tasks/<slug>.md` file remains the durable, committed record.
6. **Stop after planning.** Tell the user the plan file path and that it's ready for `/review-plan`. Do not proceed to implementation yourself unless the user explicitly says to skip review.

## Output discipline

- No source code edits in this step — only the new `docs/tasks/<slug>.md` plan file.
- Keep the plan concrete and specific to this repo's actual structure (from `docs/CODEBASE_MAP.md`), not generic boilerplate.
