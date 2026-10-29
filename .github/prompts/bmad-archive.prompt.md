---
description: 'Archive a completed Story, Epic, Initiative, or a dispositioned brainstorm artifact. Verifies lifecycle prerequisites; never stages, commits, merges, or pushes.'
---

# BMAD Archive

Archive one completed work record before it is committed and merged into its parent, or archive one dispositioned brainstorm artifact. This prompt owns record movement and archive metadata only. It never stages or commits, creates or merges branches, pushes, or deletes branches.

## Preconditions

For a `BR-<4-digit-sequence>` target, use the dedicated **Archive a Brainstorm Artifact** route and skip work-item branch/review/child-completion checks; explicit approval and ID/citation validation still apply.

1. Read `_bmad/config.toml`, `[paths].work_items_file`, and the ID registry at `[ids].registry_file`. For a BR target, resolve only the artifact and citation paths; for a work-item target, resolve its record, parent, children, status, and dependencies from WORK_ITEMS and verify the ID/filename against the parent list. Archive preserves existing IDs and filenames; it never allocates replacements.
2. For work-item archives only, verify the current branch matches the work record. For a Story archive, a passing review and its expected implementation diff may be present; unrelated changes are not allowed. For Epic/Initiative closure, require a clean worktree before archive edits. BR artifacts do not require a matching branch or work-item review.
3. If any precondition fails, explain the blocker and stop without changing files.
4. Propose the exact archive/status/path changes and wait for approval before moving or editing records.

## Archive a Brainstorm Artifact

Use this route only when the user explicitly names a brainstorm ID/path or asks to archive brainstorms.

1. Read the artifact and verify its `BR-<4-digit-sequence>` ID and filename against `[ids].registry_file`.
2. Archive only when its status is `Incorporated` or its disposition is explicitly `Not pursued`, and the outcome/rationale is recorded. An `Active` or unresolved brainstorm must first be dispositioned through `/bmad-brainstorm`.
3. Propose moving it from `[paths].brainstorm_dir` to `[paths].brainstorm_archive_dir`, preserving the ID and filename, and list every known citation to update. Wait for explicit approval.
4. Move only that artifact and update its Project Spec artifact-register row, canonical path, and known citations. Set the artifact status to `Archived — Incorporated` or `Archived — Not pursued`, preserving its disposition. Do not change delivery status in WORK_ITEMS or copy brainstorm options into authoritative project decisions; the consuming Initiative/Epic/Story/ADR owns accepted decisions.
5. Report the archived BR ID/path. Do not commit, create branches, or update delivery statuses. Stop here; do not continue into **Archive by Work Level** or **Complete the Archive**.

Do not physically archive ADRs. An obsolete decision remains in `[paths].adr_dir` with `Status: Superseded` or `Deprecated` and a successor/reason link; use `/bmad-architecture` to record that transition.

## Archive by Work Level

### User Story or Bug

- Require a passing `/bmad-review`. The reviewed implementation and story plan may still be uncommitted; verify their diffs match the approved plan and do not stage or commit them here.
- Confirm the current `<S-ID>-<slug>.md` plan is still under `[paths].stories_dir` and its ID and parent match the branch's flow metadata.
- Leave completion statuses unchanged until `/bmad-commit` succeeds. Preserve the plan's current review status while moving it into the archive; update links affected by the move.
- Move the plan without staging it (for example, with PowerShell `Move-Item <paths.stories_dir>/<S-ID>-<slug>.md <paths.story_archive_dir>/`); update relative links affected by the move. `/bmad-commit` stages the reviewed move later.
- Do not move Full Flow specs or other artifacts under `[paths].full_specs_dir`; they remain durable project records.

### Epic

- Require every non-historical child Story to be committed, archived, and merged into this Epic branch. Verify each recorded Story branch is an ancestor of `HEAD` with `git merge-base --is-ancestor <story-branch> HEAD`.
- Verify the Epic's own acceptance signals on the integrated branch using its documented cross-story checks; child Story reviews do not prove the combined Epic works. If the verification strategy is missing, stop and use `/bmad-test-design`; if the checks are defined but not executed or failing, return to `/bmad-build`. Retry archive only after the accepted checks pass.
- Recommend `/bmad-retrospective` after child integration to inspect cross-story findings and follow-up actions. It is an optional Epic-level learning step, not a per-Story gate.
- Record the Epic's canonical archive path in WORK_ITEMS; leave its completion status unchanged until `/bmad-commit` succeeds. Update the parent Initiative's link only if the Epic's canonical path changed; do not write status fields into either record.
- Resolve `[paths].epics_dir` and `[paths].epic_archive_dir`; create the archive directory if needed, then move the existing `E<n>-<slug>.md` record without staging (for example, with PowerShell `Move-Item`), preserving its ID-prefixed filename; update relative links affected by the move. `/bmad-commit` stages the reviewed move later.

### Initiative

- Require every child Epic to be archived and merged into this Initiative branch. Verify each recorded Epic branch is an ancestor of `HEAD`.
- Verify any Initiative-level success signals and cross-Epic integration evidence documented by the project; do not infer them from child Epic completion alone. If criteria or scope are missing or invalidated, stop and return to `/bmad-decompose` or `/bmad-correct-course` before closure.
- Record the Initiative's canonical archive path in WORK_ITEMS; leave its completion status unchanged until `/bmad-commit` succeeds. Update its own record to point to the archived location without adding a status field.
- Resolve `[paths].initiatives_dir` and `[paths].initiative_archive_dir`; create the archive directory if needed, then move the existing `I<n>-<slug>.md` record without staging (for example, with PowerShell `Move-Item`), preserving its ID-prefixed filename; update relative links affected by the move. `/bmad-commit` stages the reviewed move later.

Children marked historical/baseline in WORK_ITEMS are complete baseline work and do not require new branch merges. Use the status vocabulary defined in WORK_ITEMS; never archive a parent while any active child remains incomplete or unmerged.

## Complete the Archive

1. Perform only the approved record moves and archive metadata/status/link updates. Do not edit implementation code.
2. Inspect the exact changed paths and `git diff`; run `git diff --check`.
3. Report the archived paths and state that all archive changes remain uncommitted for `/bmad-commit`.

Stop here. `/bmad-commit` is the next operation and commits the reviewed implementation plus the archive changes. `/bmad-merge` is allowed only after that commit succeeds.