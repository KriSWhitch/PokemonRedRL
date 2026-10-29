---
description: 'Design a user-centered interaction contract for a material or underspecified interface change before implementation. Resolve UI platform and output paths from project context.'
---

# BMAD UX — Interaction Design

Use the UX persona to clarify user needs and interaction behavior before implementing an interface change. Create a lightweight contract that fits the current product and UI system; do not assume a platform or external design tool.

## Context

1. Read `_bmad/config.toml` and the repository instruction file configured by `[paths].instructions_file`.
2. Read `[paths].work_items_file` to resolve the active Initiative/Epic/Story and parent; resolve UI source locations, platform, and design conventions from the project context map.
3. Load the UX persona from `[paths].agents_dir` and inspect the relevant existing interface code.
4. For a small, well-specified UI adjustment, do not add a separate UX artifact; let the approved plan drive implementation.

## Mode

- **Design:** create an interaction contract for a material or underspecified change. This is the default when no suitable contract exists.
- **Update:** revise the existing contract when requirements, evidence, or platform constraints change. Preserve accepted decisions that remain valid and call out conflicts.
- **Validate:** compare an existing contract and, when available, the interface against the active plan and user needs. Report uncovered journeys, inconsistent states, accessibility/platform gaps, and assumptions; do not edit unless the user asks for proposed changes.
- Infer the mode from the request and current artifacts. Ask only when the intended operation is ambiguous.

## Working Mode

- **Coaching:** use when user needs or interaction choices are unclear. Ask one focused question at a time; elicit preferences rather than selecting them silently.
- **Fast:** use when the request is sufficiently clear or the user delegates the draft. Propose a contract from verified context, label assumptions, and invite correction.
- Do not repeat questions already answered by the request, plan, research, or existing UI.

## Design

For a material or underspecified interface change, define:

- The target user and task, based on project context rather than assumptions.
- One or more short user journeys: trigger, user goal, key steps/decisions, and successful or failed exit state.
- Entry points, information hierarchy, and proposed component boundaries in the existing UI architecture.
- Loading, empty, error, disabled, and long-running states when applicable.
- Accessibility, keyboard/input, localization, responsive/platform behavior, and performance constraints from context.
- Separate behavior from visual direction. Specify the behavioral contract and any visual changes independently. For a material visual redesign, state the intended hierarchy and the affected color, typography, spacing, shape, elevation, or component conventions; define only the needed delta to the existing design system and platform conventions.
- Observable acceptance signals that can be added to the active Story plan.

Run a surface-coverage check: every stated user need maps to an existing or proposed surface/flow, and every new surface has a user goal. Call out uncovered needs, unresolved decisions, and assumptions instead of filling gaps with invented capabilities. Do not require mockups or external design tools; use them only when available, relevant, and approved. Keep behavior decisions separate from implementation details.

## Approval and Handoff

1. For Validate, report findings against the current contract and plan; include proposed edits only when requested.
2. For Design/Update, present the proposed interaction contract, changed decisions, and trade-offs before writing it.
3. After approval, add it to the active Story plan under `## UX / Interaction`, or create/update a reusable artifact under `[paths.ux_artifacts_dir]` when it spans several Stories.
4. Update acceptance criteria and verification in the owning plan when they change. Update WORK_ITEMS only if an approved dependency or work-item status changes; keep technical decisions in Project Spec/ADRs.
5. Stop before implementation. Continue through the active delivery route after the UX contract is approved.