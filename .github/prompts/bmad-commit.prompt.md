---
description: 'Commit reviewed implementation together with prepared archive/status changes; offer a separately approved push before merge. Never archives or merges.'
---

# BMAD Commit — Record Approved Changes

Commit the reviewed implementation and archive changes prepared by `/bmad-archive`. After the commit, offer a separately gated push of that branch before merge. This command does not move/archive records or merge branches.

## Preconditions

1. Read the repository instructions and resolve the current plan/work record, ID registry, and `[paths].work_items_file` from `_bmad/config.toml`. Confirm the ID matches across filename, frontmatter, parent registry, and current branch; stop on mismatch rather than changing the ID during commit.
2. For implementation changes, require a passing `/bmad-review` verdict. Parent-only archive commits may rely on `/bmad-archive`'s completed-child checks.
3. Require `/bmad-archive` to have prepared the archive/status changes for this work level.
4. Run the project's documented verification commands when applicable. Do not invent a build or test command.
5. Inspect `git status`; preserve unrelated changes. Stop on merge conflicts, detached HEAD, or ambiguous worktree ownership.

## Review the Proposed Commit

1. Summarize changed paths and both unstaged and staged diffs. Call out generated, licensed, large, or otherwise restricted files according to repository instructions.
2. Select a Conventional Commit type and scope from the actual diff and project conventions. Include the stable work-item ID in the subject/body when the repo's commit convention supports it; do not use a fixed component-scope list from this prompt.
3. Reference the work record using its current path after archive, resolved from `_bmad/config.toml [paths]`.
4. Show the exact commit message and exact paths to stage. Ask for explicit approval before staging or committing.

## Commit

1. Update the approved plan and WORK_ITEMS row to the committed/done statuses defined by this project's workflow. On successful commit, set work-item status to `Done` and branch state to `Committed; awaiting merge`. Preserve any parent state that `/bmad-archive` prepared; do not copy status tables into PROJECT_SPEC.
2. Stage only the reviewed implementation, plan, archive, and required status/link changes. Never use `git add -A` or stage unrelated user changes by default.
3. Inspect the staged diff and confirm it contains exactly the approved paths. Commit with the approved message.
4. Report the resulting commit hash and exact remote branch target. Ask separately for approval to push this committed branch before merge; do not push without that approval. If approved, use `git push --set-upstream <remote> <branch>` for a branch without an upstream, or `git push` for its existing upstream. Never force-push. If the remote has diverged, stop and report it.
5. If any step fails, stop and report the failure; do not silently repair unrelated changes.

Do not move files, archive records, or merge here. If push is declined, leave the branch committed locally and do not recommend `/bmad-merge` until it is published. After a successful push, suggest `/bmad-merge`.

## Publish an Existing Commit

If the work item is already committed and the worktree is clean, this prompt may be run again solely to publish the existing branch. For this route, do not require new archive/status edits or create another commit; verify the passing review, archived work record, current branch, and existing commit. Show the exact remote target and request explicit approval. Do not create an empty or metadata-only commit just to push. Use the same non-force push rules above; after success, suggest `/bmad-merge`.
