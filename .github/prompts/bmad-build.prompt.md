---
description: 'Implement an approved plan or run a Quick Flow. Creates and gets approval for a concise Quick spec before code; follows repository verification and safety rules.'
---

# BMAD Build — Implementation

Implement an approved plan in small, verifiable steps. Stay within scope, preserve unrelated work, and use only build/test commands and engineering constraints documented by the active project.

## Delivery Route

Quick/Full is selected by `/start-flow`; it is separate from execution mode.

### Quick Flow

If no approved plan exists:

1. Read the user request, repository instructions, relevant source, and parent work record before asking questions. Size the change by one coherent outcome and implementation session, not by verbs or files; if it contains independent outcomes, stop and route to `/bmad-decompose` or ask the user to select the first outcome.
2. Resolve the plan directory from `_bmad/config.toml [paths].stories_dir`.
3. Use the already-reserved `S<n>` Story/Bug ID. Create a concise plan named `<ID>-<slug>.md` with `Status: Planned`, ID, selected flow, parent IDs/branch metadata required by this project, one intent, acceptance signals, constraints, non-goals, implementation steps, and focused verification.
4. Investigate repository and parent context before asking about gaps. Present unresolved questions with options and consequences; do not approve or implement while a material user-visible gap remains undecided. Show any irreversible action and its safeguard explicitly.
5. Present the plan and wait for approval. Do not edit product code before approval.
6. On approval, set `Status: Approved`. If UI behavior is materially unclear, pause and offer `/bmad-ux`. If project context identifies a high-risk boundary, pause and offer `/bmad-test-design`. Resume implementation only after selected design/verification work is approved.

If an approved plan already exists, reuse it; do not create a duplicate.

### Full Flow

Full Flow requires approved upfront requirements/planning and completion of the architecture phase (`/bmad-architecture`, including an explicit no-op if architecture is unchanged). Use approved parent context and any approved UX/test-design artifacts. Do not create a second Quick spec.

## Execution Mode

| Mode | When | Behavior |
|---|---|---|
| **Single-pass** | Light plan, contained change, low risk | Read all affected files, implement the plan, and run focused verification at the end. |
| **Step-by-step** | Multi-file or higher-risk work | Follow plan order, verify each meaningful step, and pause at the plan's review checkpoints. |

Default to Step-by-step unless the approved plan explicitly selects Light/Single-pass.

## Activation

1. Read repository root/IDE instructions and `_bmad/config.toml`; follow the configured context map for project state, codebase map, build/test commands, and constraints.
2. Resolve the active plan from the path supplied by the user/`/start-flow` or the configured `[paths.stories_dir]` and `[paths.output_dir]`. Use `[core].active_initiative` only when explicitly resuming that Initiative.
3. Confirm the plan is `Approved`. If no plan exists, only create one through the Quick Flow procedure above; Full Flow without approved artifacts stops and routes to `/bmad-plan`.
4. Inspect the worktree and current branch. Preserve unrelated changes. If changes overlap the task, ask before editing; do not stash or stage automatically.
5. Run the project's documented baseline build/check when applicable and available. Report environment blockers rather than inventing replacement commands.
6. Load the implementation persona from `[paths].agents_dir` as configured in `_bmad/config.toml`.
7. Verify the Story/Bug ID matches the registry, parent child list, plan filename, and branch. If it is absent or mismatched, stop and route to `/start-flow` or `/bmad-decompose`; Build must not invent or silently change an ID.
8. When implementation begins, set the work-item row to `In Progress` in `[paths].work_items_file`; the plan remains `Approved` until implementation and its verification are complete.

## Execute and Verify

For each step:

1. Read the affected code and its callers.
2. For behavior changes, add or update a focused failing test before the implementation when the existing test infrastructure supports it. If test-first is impractical, use the approved alternative verification and state why; do not install a framework or expand scope here.
3. Make only the approved change.
4. Run focused verification from the project instructions/plan.
5. If verification fails, diagnose and repair only the in-scope change, then rerun the same check. If blocked or requirements are invalidated, stop and use `/bmad-correct-course`.
6. Mark the plan step complete only when its verification passes; report the result.

Before finalizing:

- Confirm all approved capabilities and acceptance signals are covered.
- Check the project-specific resource lifecycle, dependency wiring, protocol/data contracts, and documentation rules identified in context.
- Update the configured codebase map when files or components moved/changed structurally.
- Set the plan status to `Implemented`; leave review, archive, commit, and merge to their separate commands.

Never commit, archive, merge, or push in this prompt. After implementation, hand off to `/bmad-review`.