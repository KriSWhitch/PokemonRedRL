---
description: 'Merge a published, completed child branch into its immediate parent, then ask permission to push the parent branch.'
---

# BMAD Merge

Merge the current completed branch into exactly one immediate parent branch. After the local merge is committed, ask for explicit permission to push the updated parent branch and perform that push if approved. This prompt does not archive files, change task completion status, force-push, or delete branches.

## Preconditions

1. Read `_bmad/config.toml`, `[paths].work_items_file`, the ID registry, and current/parent work records and archive paths resolved from `[paths]`.
2. Verify the current branch matches the work record and the worktree is clean.
3. Require a successful commit on the child branch that includes the archive changes for this work level:
   - Story/Bug: its plan is under `[paths].story_archive_dir`.
   - Epic: its record is under `[paths].epic_archive_dir`.
   - Initiative: its record is under `[paths].initiative_archive_dir`.
4. Require the exact child branch and commit to be published to `[git].remote_name`; the fetched remote child ref must point at the local child HEAD. If it is absent or behind, stop and route to `/bmad-commit` to publish it. Never force-push.
5. If any precondition fails, explain the blocker and stop without switching branches or merging.

## Resolve the Immediate Parent

- Story/Bug → its recorded Epic branch.
- Epic → its recorded Initiative branch.
- Initiative → `[git].default_base_branch` from `_bmad/config.toml`.
- A standalone Story/Bug with `Parent: Standalone` or no recorded parent → configured default base.
- Never skip a level. In particular, never merge an Epic directly to the configured default base or a Story directly to an Initiative when it belongs to an Epic.

## Validate and Approve

1. Fetch remote refs. Resolve the parent from the work record, not from a guessed slug. Verify the child's stable ID matches its archive filename and branch; for child work also verify the parent's child list, while standalone work must match its standalone WORK_ITEMS row. Verify the fetched remote child ref equals the local child HEAD. Stop on mismatch.
2. Confirm the parent branch exists locally; if it exists only on `[git].remote_name`, create a local tracking branch without changing its history. If the local parent is behind or diverged from its remote, stop and ask the user to synchronize it.
3. Show the published child branch, immediate parent branch, commit containing its archive changes, merge command, and the parent branch that would be pushed after a successful merge. Ask for explicit approval before switching branches or merging. The push requires a second, separate approval after the merge commit exists.

## Merge

After approval:

1. Recheck that the worktree is clean and the commit containing both implementation and archive changes is still present.
2. Switch to the parent branch and verify it is clean.
3. Reconfirm the fetched remote child ref still points at the reviewed local child HEAD. Check whether the child branch is already an ancestor of the parent. If it is, report the merge as a no-op and continue to the push-approval step only if the parent is ahead of its remote. Otherwise start the merge without committing it yet: `git merge --no-ff --no-commit <child-branch>`.
4. If conflicts occur, stop and preserve the conflict state; do not resolve by guessing or push. After conflicts are resolved, resume this flow before committing.
5. Once the merge result is clean, update only branch/integration metadata in WORK_ITEMS: set the child's branch state to `Merged into <exact immediate-parent branch>`. Do not alter task completion status. Stage this registry change with the merge result, inspect the complete staged diff, and confirm it contains only the approved child merge plus this integration-state update.
6. Run the focused verification commands required by the approved plan and repository instructions. Do not assume a language, solution path, or build tool.
7. Create the merge commit from the staged merge using the prepared merge message. The merge commit must contain both the child changes and the WORK_ITEMS integration state; do not leave a post-merge file edit behind.
8. After the local merge succeeds, show the exact parent branch, remote ref, and commits that would be pushed. Ask the user separately for approval to push. If declined, stop with the merge committed locally and clearly report that the parent is ahead of its remote. For an already-integrated no-op, offer the push only if the local parent contains commits absent from its remote.
9. If approved, fetch the remote again and verify the fetched remote parent ref is an ancestor of the local parent HEAD. If not, stop and report the remote advancement/divergence; never force-push. Otherwise push only the immediate parent branch: use `git push --set-upstream <remote> <parent-branch>` if it has no upstream, otherwise `git push <remote> <parent-branch>`. Never push a different branch, and never push `main` as part of the development workflow.
10. Verify the pushed remote parent ref equals the local parent HEAD and that the worktree is clean. Report the merge commit and push result. Read the updated parent record and identify the next pending child work item. Recommend `/start-flow` for that item; if no child remains, recommend the parent's `/bmad-archive` closure flow when eligible. Do not create the next branch automatically.

Do not make archive or task-completion status changes in this prompt. Branch integration metadata must be included in the merge commit. If the work is not archived under its configured path, stop and direct the user to `/bmad-archive` first.