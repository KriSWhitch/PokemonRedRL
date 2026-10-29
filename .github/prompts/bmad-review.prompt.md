---
description: 'Mandatory acceptance review for planned changes. Checks plan/acceptance adherence, documented verification, project integration contracts, resource lifecycle, and records; produces a verdict.'
---

# BMAD Review — Acceptance Gate

Verify whether an implemented change satisfies its approved plan and is ready to archive. This is the required acceptance gate in the delivery flow; it is not a broad source-code audit. Use the optional `/bmad-code-review` for an explicitly requested or separately approved correctness/performance/security/testability review.

## Load Context and Scope

1. Read the repository's root/IDE instructions. Resolve the plan and code-map paths from `_bmad/config.toml [paths]`; read `[paths].work_items_file` for parent/status/dependencies, and Project Spec for technical context only.
2. Resolve review scope from the user's request or `/start-flow`: active Story plan and diff. If no approved plan exists, stop and recommend `/bmad-code-review` for an explicitly requested standalone code review or `/bmad-plan` for unplanned work.
3. Read the approved plan, its companions, the changed files, and project-specific acceptance/integration rules. Never assume a language, runtime, test command, resource type, or status vocabulary.

## Review Lenses

Run all applicable lenses and combine findings without duplication:

### 1. Plan and Acceptance

- Map every capability and acceptance signal to implementation and evidence.
- Identify missed requirements, unapproved scope, and plan steps not completed.
- Flag changed assumptions or architecture that should go through `/bmad-correct-course`.
- For each behavior-changing acceptance signal, trace at least one changed path from its entry point through the relevant state/contract to an observable result. Include the relevant boundary or failure scenario when specified by the plan; do not claim coverage from a passing build alone.

### 2. Verification Evidence

- Run only build/test/check commands documented by the project and relevant to the change.
- Check the plan's required tests, including any approved manual procedures.
- Distinguish passed, failed, skipped, unavailable, and not run. Do not mark an unavailable check as passed.

### 3. Regression and Compatibility

- Trace changed public contracts, callers, configuration, persistence, protocols, and migrations identified by project context.
- Check compatibility and failure behavior at affected boundaries.
- Flag a breaking change without an approved migration path.

### 4. Resource Lifecycle

- Apply lifecycle rules defined by project instructions (for example memory, handles, transactions, streams, or framework-managed resources when relevant).
- Trace ownership and cleanup on success, error, and cancellation paths.
- Do not impose library-specific checks that are not applicable to this project/change.

### 5. Project Integration Contracts

- Check dependency wiring/lifetimes, shared schemas, APIs/protocols, configuration, and deployment integration only where affected.
- Verify producer/consumer changes remain coordinated across documented components.
- Leave general style, performance, and security auditing to `/bmad-code-review` unless a specific project contract is violated.

### 6. Records and Documentation

- Check that project maps and documentation listed by context are updated for structural or behavioral changes.
- Verify plan status and required decision logs follow the repository's configured workflow.
- Do not invent status labels or move/archive records in this command.

## Findings and Verdict

Report findings with file/line or acceptance-criterion references. Each finding includes severity, trigger, impact, and a concise suggested action. Report real issues only; a clean lens is valid.

| Severity | Meaning |
|---|---|
| BLOCKER | Prevents safe or meaningful use; likely critical failure or data loss |
| HIGH | Incorrect behavior, failed acceptance, or major compatibility/reliability risk |
| MEDIUM | Meaningful verification, integration, or maintainability gap |
| LOW | Non-blocking documentation or clarity issue |

Verdict:

- **Pass:** no BLOCKER/HIGH findings and all required verification passed.
- **Pass with Notes:** no BLOCKER/HIGH findings; remaining MEDIUM/LOW findings are explicit.
- **Blocked:** any BLOCKER/HIGH finding or required verification failed.

Present the findings first, then the verdict and verification evidence. Do not fix findings in this command.

## Handoff and Records

- **Pass:** recommend `/bmad-archive`.
- **Pass with Notes:** ask the user to resolve or explicitly defer notes, then recommend `/bmad-archive`.
- **Blocked:** direct in-scope fixes to `/bmad-build`; material scope/requirement changes to `/bmad-correct-course`; then rerun `/bmad-review`.
- Update the Story plan's review status and set the matching WORK_ITEMS row to `Reviewed` after a passing verdict. Do not mark the Story complete here; `/bmad-commit` owns the `Done` status.
- Do not archive, commit, merge, or push in this command.