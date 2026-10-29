---
description: 'Review a completed work cycle against evidence and produce blameless, actionable lessons. Resolve plan, state, and output locations from repository context.'
---

# BMAD Retrospective — Evidence and Learning

Review a completed Story, Epic, Initiative, or other agreed delivery cycle. Every finding must be grounded in evidence; focus on the process and system rather than blame.

## Identify Scope and Context

1. Read `_bmad/config.toml` and the repository instruction file configured by `[paths].instructions_file`.
2. Read `[paths].work_items_file` for hierarchy, status, dependencies, and branch integration; resolve the relevant records and output directory from `[paths]` and the context map. Use the user's explicit scope or the active work item; ask if ambiguous. Use Project Spec for technical context and risks only.
3. Load Mary from `[paths].agents_dir` for evidence-based analysis.

## Completion and Scope Gate

- Resolve the work item's declared acceptance signals and current status from its plan and parent record.
- For an Epic or Initiative, inspect the child records and identify incomplete, unmerged, or explicitly deferred work. Do not silently call the parent complete when child work remains.
- If the cycle is incomplete, report that limitation and ask whether to run a partial retrospective. If proceeding, label its conclusions provisional and list excluded work.

## Gather Evidence

Inspect only evidence relevant to the selected cycle:

- Original and final plan/acceptance criteria.
- Scope and implementation diff, relevant commits/branch history, and parent/child records when applicable.
- Review findings and verification results, including checks that were skipped or blocked.
- Prior retrospective action items and evidence of whether they landed, when available.
- Decision logs or incident/operational records when they exist.
- Follow-up defects or user observations linked to this work.

Treat missing evidence as unknown, not as proof that an event did or did not happen. Separate observations, supported inferences, and open questions.

## Analyze

Identify:

- What worked and should be repeated.
- Where scope, requirements, architecture, implementation, review, archive, or merge caused avoidable rework.
- Surprises and assumptions that proved false.
- Whether each declared success signal was met, partly met, not met, or cannot be verified from available evidence; distinguish shipped scope from deferred scope.
- Recurring patterns supported by multiple work items.
- Concrete action items with an owner/artifact and verifiable completion signal.

Do not invent duration, effort, failure counts, or business impact when Git or project records do not establish them.

## Output and Approval

1. Draft a concise retrospective with an outcome summary, evidence-linked findings, coverage/evidence gaps, and proposed action items.
2. Resolve the report path from `[paths.retrospective_dir]` in `_bmad/config.toml`; use `retro-<slug>.md` unless project context specifies another naming convention.
3. Show the proposed report and any project-instruction or backlog changes. Wait for approval before writing or changing status.
4. After approval, save the report and apply approved lessons to project context. If an action item becomes new work, route it through `/bmad-decompose` or `/start-flow` so its ID and status are registered in WORK_ITEMS; do not edit hierarchy/status directly here.
5. Suggest one next action, typically `/bmad-help` or `/start-flow`.

Retrospective does not implement product code, archive work records, commit, merge, or push.