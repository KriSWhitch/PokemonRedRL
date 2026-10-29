---
description: 'Create or refine an Initiative-to-Epic or Epic-to-User-Story breakdown. Use for roadmap decomposition; this prompt creates planning artifacts only, not product code.'
---

# BMAD Decompose — Initiative and Epic Planning

Decompose an Initiative into Epics, or an Epic into independently reviewable User Stories. Resolve project-specific document paths, ID conventions, and status vocabulary from `_bmad/config.toml` and repository instructions; do not rely on an external tracker or integration.

For requirement discovery and story intent, load Mary and John from `[paths].agents_dir` as the analyst and product roles. Do not copy their activation menus or invent tracker-specific IDs.

## Resolve Scope

1. Read `_bmad/config.toml`, `[paths].work_items_file`, and the ID registry; locate the target Initiative/Epic and parent using the configured paths. Use WORK_ITEMS for status, hierarchy, dependencies, and next work; use Project Spec for technical context only.
2. Resolve scope from the current branch and the user's request:
   - Initiative branch: produce or refine an Initiative charter and its child-Epic list under `[paths].initiatives_dir`.
   - Epic branch: produce or refine one Epic and its child-User-Story list under `[paths].epics_dir`.
3. If parent or scope is ambiguous, ask one focused question before proposing work.

## Initiative to Epics

- Group work by cohesive user/research outcome, capability, and delivery boundary, not by technical layer alone.
- `/start-flow` reserves an Initiative ID. For each new Epic on an Initiative branch, allocate the next unused `E<n>` from `[ids]` and reserve it in the registry and child record, including the exact `<epic-prefix>/e<n>-<slug>` branch and initial `Not created` branch state. Existing IDs are immutable.
- Each Epic owns a non-overlapping outcome and has explicit intent, observable success signals, constraints, dependencies, and non-goals.
- For an approved Initiative breakdown, create one ID-prefixed Epic record per accepted child under `[paths].epics_dir` and link it from the Initiative before `/start-flow` creates that Epic's branch. The record is the thin scope envelope; its own decomposition into Stories happens on the Epic branch.
- Keep the hierarchy Initiative → Epic → Story/Bug. A standalone Story/Bug is allowed only without a parent Initiative/Epic and uses the configured default base branch.
- Mark completed historical work as complete without reopening it on new branches.

## Epic to User Stories

- Slice into vertical, independently reviewable and testable increments. Each story should produce a coherent user-visible or researcher-visible result where possible.
- A foundation story is acceptable only when it cannot reasonably be included in the first end-to-end capability.
- Every story has a stable ID, intent, concrete done signal, dependencies, and status. Assign each story to exactly one Epic.
- Allocate new Stories/Bugs globally as `S<n>` in build order, checking the ID registry, active/archived records, and visible branch refs. Update the registry and owning Epic's child list in the same approved edit; record the exact `<story-prefix>/s<n>-<slug>` or `<bug-prefix>/s<n>-<slug>` branch with initial `Not created` branch state. Never reuse canceled or retired IDs. Use `<ID>-<slug>.md` for plan filenames.
- Keep implementation detail in the task plan created when that story starts; this decomposition is the scope map, not a substitute for its approved implementation plan.

## Approval and Output

1. Draft a proposed hierarchy in the conversation first, including each new item's reserved ID, canonical ID-prefixed filename, title/slug, parent, order, and dependencies. Do not create or edit decomposition files before the user approves the proposed structure.
2. Check that the hierarchy covers all in-scope requirements, exposes cross-Epic dependencies, and has no duplicate ownership.
3. After approval, update the Initiative or Epic record and WORK_ITEMS registry. Preserve stable IDs; never renumber or reuse retired IDs. Use filenames `I<n>-<slug>.md` or `E<n>-<slug>.md`. Do not duplicate hierarchy/status tables in Project Spec.
4. Update links and the configured codebase map when files are added or moved.
5. Do not create branches for child work during decomposition. Each child gets its own branch when `/start-flow` begins that child flow.