---
description: 'Merge a completed child branch into its immediate parent after its record has been archived and the user approves.'
---

# BMAD Merge

Merge the current completed branch into exactly one immediate parent branch. This prompt owns Git merge operations only. It does not archive files, update work-item statuses, push, or delete branches.

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
3. Show the published child branch, immediate parent branch, commit containing its archive changes, and merge command. Ask for explicit approval before switching branches or merging.

## Merge

After approval:

1. Recheck that the worktree is clean and the commit containing both implementation and archive changes is still present.
2. Switch to the parent branch and verify it is clean.
3. Reconfirm the fetched remote child ref still points at the reviewed local child HEAD. Check whether the child branch is already an ancestor of the parent. If it is, report that the merge is already complete and do not create another merge commit. Otherwise merge only the published child branch with `git merge --no-ff <child-branch>`.
4. If conflicts occur, stop and preserve the conflict state; do not resolve by guessing.
5. Run the focused verification commands required by the approved plan and repository instructions. Do not assume a language, solution path, or build tool.
6. Report the resulting merge commit, or the already-integrated no-op. Ask separately before pushing. Never delete the child branch automatically.
7. Update only branch/integration metadata in WORK_ITEMS; set the child's branch state to `Merged into <exact immediate-parent branch>` and do not alter task completion status here. Read the updated parent record and identify the next pending child work item. Recommend `/start-flow` for that item; if no child remains, recommend the parent's `/bmad-archive` closure flow when eligible. After an Initiative or standalone Story/Bug merges to the configured base, report that it is integrated into that base. Do not create the next branch automatically.

Do not make any archive or status changes in this prompt. If the work is not archived under its configured path, stop and direct the user to `/bmad-archive` first.