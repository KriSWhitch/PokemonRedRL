---
description: 'Commit reviewed implementation together with prepared archive/status changes using explicit path selection and Conventional Commits. Never archives, merges, or pushes.'
---

# BMAD Commit — Record Approved Changes

Commit the reviewed implementation and archive changes prepared by `/bmad-archive`. This command owns staging and committing only; it does not move/archive records, merge branches, or push.

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
4. If any step fails, stop and report the failure; do not silently repair unrelated changes.

Do not move files, archive records, merge, or push here. After the commit succeeds, suggest `/bmad-merge`. Push requires separate explicit approval after merge.