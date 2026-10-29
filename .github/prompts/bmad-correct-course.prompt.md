---
description: 'Assess a material change to an approved Initiative, Epic, or User Story and propose the smallest coherent plan revision. Use when implementation reveals changed requirements, invalid assumptions, or a scope/architecture shift.'
---

# BMAD Correct Course

Reconcile new evidence with the approved work contract. This command proposes and updates planning artifacts only; it does not implement code, review, archive, commit, merge, or push.

Load John from `[paths].agents_dir` for scope and acceptance impacts, and Winston from the same directory when architecture or integration boundaries are affected.

## Gather Evidence

1. Read `_bmad/config.toml`, `[paths].work_items_file`, the active Initiative/Epic, and the current Story plan resolved from `[paths]`. Read Project Spec only for relevant architecture and project risks.
2. Read the relevant changed files/diff and identify the observation that makes the current plan unreliable.
3. Distinguish a local implementation detail from a material change to intent, acceptance, constraints, dependencies, risk, or architecture. For a local detail that stays within approved outcomes, do not invoke course correction; record it in the Story plan and continue with `/bmad-build`.

## Analyze Impact

Summarize:

- Evidence and the plan assumption/requirement it conflicts with.
- Impact on acceptance signals, scope, dependencies, affected Epic/Initiative, architecture, verification, and already completed work.
- Smallest viable options: revise this Story, split/reassign stories using `/bmad-decompose`, defer the new request, or stop/re-scope.
- Recommended option and what must change in the plan records.

Show exact proposed edits (old → new) and wait for explicit approval. Do not edit records before approval.

## Apply the Approved Revision

After the user approves an option:

1. Update the existing Story plan and affected Epic/Initiative records only where required. Preserve stable IDs; never silently renumber or duplicate a Story.
2. Update success signals, non-goals, dependencies, risks, and verification to reflect the approved change.
3. Update WORK_ITEMS only if the approved hierarchy, scope, dependency, next work item, or status changed. Update Project Spec only when technical decisions or risks change.
4. Set the revised Story plan back to `Status: Planned` and wait for explicit approval of that revised plan before resuming `/bmad-build`.
5. Report changed files and the next required approval. Do not implement the revised work in this command.

If the change exceeds the current Epic or Initiative, pause and use `/bmad-decompose` with the user rather than expanding the Story silently.