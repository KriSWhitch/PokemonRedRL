---
description: 'Optional source-level code review for explicitly requested or separately approved high-risk changes. Reviews correctness, performance, security, maintainability, and testability; does not replace plan acceptance review.'
---

# BMAD Code Review

Perform an optional source-level review of code in scope. This command is separate from `/bmad-review`: it audits implementation properties, while `/bmad-review` is the required acceptance gate against the approved plan and project verification requirements.

This command runs only when the user explicitly asks for code review or accepts an additional review recommended for high-risk code. It is not automatically run for every Story and does not change files.

## Load Project Context

1. Read the repository's root/IDE instructions and follow its project-context source map for language, framework, security rules, architecture, build/test commands, and known risks.
2. Resolve review scope from the user's request and Git state: working tree, index, commit/range, file, or folder.
3. Read the complete changed code in scope. Use project-specific checks from context only when relevant; do not assume a framework, runtime, database, or external service.

## Review Dimensions

### Correctness

- Trace normal paths and boundary conditions: absent/empty values, invalid input, timeouts, retries, cancellation, and corrupt data where applicable.
- Check state transitions, invariants, error propagation, and asynchronous control flow.
- Verify changed behavior against its documented contract and actual callers.

### Performance and Resource Safety

- Inspect hot paths for avoidable allocations, repeated work, blocking operations, unbounded collections, and excessive I/O.
- Follow resource ownership and lifecycle rules specified by the project context.
- Report only material performance concerns supported by evidence; avoid speculative micro-optimizations.

### Security

- Check for secrets, unsafe deserialization, insufficient validation, path or command injection, permission issues, and trust-boundary mistakes relevant to the code.
- Do not perform live penetration testing or probe external systems.

### Maintainability and Project Conventions

- Apply the language, formatting, architecture, and dependency conventions discovered in the project context.
- Check that errors, logs, public contracts, and dependency boundaries remain understandable and consistent.

### Testability

- Check whether important behavior can be tested at the appropriate boundary and whether current tests cover the changed contract.
- Identify missing tests for concrete regressions or high-risk paths; do not require a new framework unless the project or approved scope calls for one.

## Findings and Verdict

Report findings first, ordered by severity. Each finding must include severity, file/line, trigger condition, impact, and a concise suggested fix. Do not pad the report; a clean review is a valid result.

Severity:

| Severity | Meaning |
|---|---|
| BLOCKER | Likely security breach, data loss, or unusable system behavior |
| HIGH | Incorrect behavior or serious reliability/performance risk |
| MEDIUM | Meaningful maintainability, edge-case, or test-coverage risk |
| LOW | Non-blocking consistency or clarity improvement |

Verdict:

- **Approve:** no BLOCKER/HIGH findings.
- **Comment:** MEDIUM/LOW findings only.
- **Request Changes:** any BLOCKER/HIGH finding.

Do not fix findings in this command. For an in-scope implementation fix, recommend `/bmad-build`; for a material change to approved scope or architecture, recommend `/bmad-correct-course`. This audit does not replace `/bmad-review` in the delivery flow and does not authorize archive, commit, merge, or push.