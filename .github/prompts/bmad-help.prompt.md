---
description: 'Explain the current repository BMAD state and recommend the next command. Use when the user asks what to do next, where a flow stands, or how the workflow works.'
---

# BMAD Help — Orient and Route

Answer workflow questions from the repository's current state; do not infer progress from a file merely existing.

## Read Current Evidence

1. Read `_bmad/config.toml` and the repository instruction file configured by `[paths].instructions_file`. Read `[paths].work_items_file` for status/hierarchy/next work; use the configured Project Spec for architecture/risk context and the workflow guide for command semantics.
2. Inspect `git status --short --branch` and identify the current work branch.
3. If a Story flow is active, locate its plan under the configured Story directory and read the owning Epic record. If the branch is an Epic or Initiative branch, resolve its record using configured hierarchy paths and inspect its children.
4. Treat explicit user statements and current conversation decisions as evidence. Do not mark a stage complete based only on a command being listed or a draft file existing.

## Respond

Give a concise status with:

- Current work level and parent, if resolvable.
- Stable Initiative/Epic/Story ID and record path; flag any mismatch between the registry, filename, parent list, or branch name.
- Current branch and whether the worktree has unrelated/uncommitted changes.
- Last confirmed phase and the next required command.
- Approval or verification gate that must pass before continuing.
- Optional commands only when relevant: `/bmad-brainstorm` for unclear ideas, `/bmad-test-design` for high-risk test planning, `/bmad-code-review` for a separate code-focused review, `/bmad-correct-course` for material scope changes, `/bmad-retrospective` after a completed cycle.

If the parent branch or active plan cannot be identified, say what evidence is missing and ask one focused question. Do not create branches, edit plans, commit, archive, merge, or push while answering help requests. Offer to continue by naming the single next command.