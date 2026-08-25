---
mode: agent
description: Stage and commit reviewed changes; push only with explicit user confirmation.
---

# Commit Changes

Only run this after `/review-changes` has passed (✅ or ⚠️ with accepted notes). If no review has happened yet, tell the user to run `/review-changes` first and stop.

## Steps

1. **Confirm review status.** If you (the agent) didn't just perform `/review-changes` in this conversation, re-run its checks (build + diff inspection) before committing — don't commit unreviewed work.
2. **Inspect the diff one more time** (`git status`, `git diff`) to write an accurate commit message and to make sure nothing unintended is staged (build artifacts, local secrets/config). Note: `src/data/roms/` and `src/data/models/` backups are intentionally tracked in this repo — do not exclude them unless the user asks.
3. **Stage deliberately**, including the task's plan file `docs/tasks/<slug>.md` (set its `Status:` to `Done` first) — the plan is committed together with the code it describes. Prefer `git add <specific paths>` over `git add -A`/`git add .` unless you've verified the full `git status` output is exactly what you expect to commit.
4. **Write a clear commit message**: short imperative summary line, blank line, then a short body explaining *why* if it's not obvious from the diff. Reference `docs/tasks/<slug>.md` briefly.
5. **Commit locally.** Run `git commit`. Do not use `--no-verify` or bypass hooks.
6. **Do NOT push automatically.** Pushing is a shared/hard-to-reverse action. Ask the user explicitly whether to push now, and to which branch/remote. Only run `git push` after they confirm.
7. **Never** force-push, amend/rewrite already-pushed commits, or reset branches without explicit, specific user confirmation for that exact action.

## Output discipline

- One logical change per commit where practical; don't bundle unrelated fixes into the same commit as the requested task.
- If the working tree has unrelated pre-existing changes not part of this task, leave them out of the commit and mention them to the user instead of silently including or discarding them.
