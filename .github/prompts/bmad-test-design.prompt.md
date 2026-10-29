---
description: 'Create a risk-based verification plan for an Epic or high-risk Story using the current project's actual architecture and constraints. Does not add a test framework or write test code.'
---

# BMAD Test Design — Risk-Based Verification

Define what evidence will demonstrate that the planned change works. This command designs verification only; implementation of approved tests belongs to the build workflow.

## Read Context

1. Read `_bmad/config.toml` and the repository instruction file configured by `[paths].instructions_file`.
2. Read `[paths].work_items_file` for the Story/Epic hierarchy, status, and dependencies; load only the active plan and relevant architecture. Use Project Spec only for technical context.
3. Load the QA persona from `[paths].agents_dir` and inspect existing tests. Do not assume a framework, runtime, service, or platform.

## Mode

- **Design:** create a verification strategy when the active plan has none. This is the default.
- **Update:** revise an existing strategy when scope, implementation, or evidence changes. Preserve valid cases and decisions; do not duplicate checks.
- **Validate:** assess an existing strategy against every acceptance signal, changed contract, and high-priority risk. Report coverage, weak or missing oracles, impractical setup, and residual risks without editing unless the user asks for proposed changes.
- Infer the mode from the request and current plan. Ask only when the intended operation is ambiguous.

## Design

For every acceptance signal and project invariant, define:

| Field | Requirement |
|---|---|
| Requirement | Acceptance-signal ID or invariant being verified |
| Risk | Failure mode, impact, likelihood, and priority (High/Medium/Low) |
| Scenario | Positive, boundary, invalid-input, failure/recovery, regression, or non-functional case as applicable |
| Evidence | Automated test, build/static check, runtime smoke test, or documented manual procedure |
| Boundary | Test level and affected component/integration/resource/user boundary |
| Oracle | Observable pass condition and expected state/data change; avoid vague "works" |
| Setup | Required environment, test data/fixtures, reset, permissions, and cleanup |

Derive scenarios from the acceptance signal, changed contracts, and failure modes; do not generate cases from a generic checklist without a relevant risk. Prefer deterministic checks for pure logic and explicit smoke procedures for environment-dependent behavior. Test invariants for stochastic or distributed systems rather than exact outputs. Consider repeatability, flakiness, runtime cost, and whether a check belongs in the normal test path. Every High risk must have planned evidence or an explicit residual-risk disposition with owner/decision needed.

Do not install packages, create a test project, or modify application code in this command. If automation requires missing infrastructure, record it as a dependency or scope decision for the owning work item; coordinate material scope changes through `/bmad-correct-course` or `/bmad-decompose`.

## Output and Approval

- For Design/Update on a Story, propose the `## Verification` section. For an Epic, propose `## Verification Strategy` in its record.
- For Validate, report the existing artifact's coverage and findings first; include proposed edits only when requested.
- Include a coverage summary: acceptance signals covered, High risks without evidence, environment blockers, and any manual steps that need a named operator or prerequisite.
- For any edit, show the proposed matrix and exact file changes first. Wait for approval before editing.
- After approval, update the planning record and WORK_ITEMS only if a dependency or work-item status changed. Do not copy status data into Project Spec.
- Stop after recording the verification plan. The build workflow writes and runs tests only when they are part of the approved scope; the required acceptance review reports evidence against the plan.