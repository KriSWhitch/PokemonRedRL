# Work Item Registry

**Updated:** 2026-10-30
**Authority:** This file is the single source of truth for work-item hierarchy, status, scope, dependencies, next actions, and branch state. Initiative/Epic/Story records hold intent, acceptance signals, constraints, and implementation detail; they do not override the status or dependency data here.

## ID Allocation

IDs are project-global, sequential by type, stable, and never reused or renumbered. Allocate an ID in the owning flow before creating its canonical record or branch. Prefixes and registry path are configured in `_bmad/config.toml [ids]`.

| Namespace | Allocated | Next available |
|---|---|---|
| Initiative | I1 | I2 |
| Epic | E1-E4 | E5 |
| Story/Bug | S0, S1-S19, S21 | S22 |
| Retired Story ID | S20 (mistaken migration allocation; never reuse) | - |
| ADR | ADR-0001-ADR-0002 | ADR-0003 |
| Brainstorm | BR-0001-BR-0002 | BR-0003 |

Legacy roadmap aliases: T1=S1, T2=S2, T2b=S3, T3=S4, T3b=S5, T4a=S6, T4=S7, T5=S8, T6=S9, T7=S10, T8=S11, T9=S12, T10=S13, T11=S14, T12=S15, T13=S16, T14=S17, T15=S18, T16=S19. S0 is the historical repository refactor; S20 is retired and never reused; S21 is the Copilot BMAD workflow.

## Initiative

| ID | Initiative | Status | Branch | Branch state | Progress | Next |
|---|---|---|---|---|---|---|
| I1 | [Computer-Vision + RL Overhaul](initiatives/I1-computer-vision-rl-overhaul.md) | In Progress | `initiative/i1-computer-vision-rl-overhaul` | Not created | 8/15 core Stories done | S9 |

## Epics

| ID | Epic | Parent | Status | Branch | Branch state | Child Stories |
|---|---|---|---|---|---|---|
| E1 | [Vision Perception and Pipeline](epics/E1-vision-perception.md) | I1 | Planned | `epic/e1-vision-perception` | Not created | S1, S7, S9, S12 |
| E2 | [RL Training and Models](epics/E2-rl-training-and-models.md) | I1 | Planned | `epic/e2-rl-training-and-models` | Not created | S8, S11, S13 |
| E3 | [Emulator Integration and Runtime](epics/E3-emulator-integration-runtime.md) | I1 | Planned | `epic/e3-emulator-integration-runtime` | Not created | S3, S10 |
| E4 | [Reliability and Validation](epics/E4-reliability-validation.md) | I1 | Planned | `epic/e4-reliability-validation` | Not created | S2, S4, S5, S6, S14, S15 |

## Core Stories

| ID | Story/Bug | Parent | Scope | Status | Depends on | Blocks | Branch | Branch state | Canonical record |
|---|---|---|---|---|---|---|---|---|---|
| S1 | Frame-capture spike | E1 | Core | Done | - | S7, S9 | - | Historical; branch not tracked | [Archive](stories/archive/S1-frame-capture-spike.md) |
| S2 | Pre-overhaul cleanup | E4 | Core | Done | - | S12 | - | Historical; branch not tracked | [Archive](stories/archive/S2-pre-overhaul-cleanup.md) |
| S3 | ControlPanel MVP | E3 | Core | Done | - | S9 | - | Historical; branch not tracked | [Archive](stories/archive/S3-controlpanel-mvp.md) |
| S4 | .NET and package modernization | E4 | Core | Done | - | - | - | Historical; branch not tracked | [Archive](stories/archive/S4-net-package-modernization.md) |
| S5 | Docker Compose for Redis and Agent | E4 | Core | Done | - | S7 (hard gate) | - | Historical; branch not tracked | [Archive](stories/archive/S5-docker-containerization.md) |
| S6 | Remove OpenCV, adopt torchvision | E4 | Core | Done | - | S7 | - | Historical; branch not tracked | [Archive](stories/archive/S6-remove-opencv-adopt-torchvision.md) |
| S7 | Vision-memory representation | E1 | Core | Done | S1, S5, S6 | S9, S11 | - | Historical; branch not tracked | [Archive](stories/archive/S7-design-vision-representation.md) |
| S8 | RL algorithm review | E2 | Core | Done | - | S11 | - | Historical; branch not tracked | [Archive](stories/archive/S8-rl-algorithm-review.md) |
| S9 | Vision project and frame pipeline | E1 | Core | Next | S1, S3, S7 | S12, S18 | `story/s9-vision-project-frame-pipeline` | Not created | [Epic detail](epics/E1-vision-perception.md#child-user-stories) |
| S10 | Protocol verification and contract test | E3 | Core | Planned | S1 | - | `story/s10-protocol-contract-test` | Not created | [Epic detail](epics/E3-emulator-integration-runtime.md#child-user-stories) |
| S11 | Dual-modality network and trainer | E2 | Core | Planned | S7, S8 | S16, S19 | `story/s11-dual-modality-network-trainer` | Not created | [Epic detail](epics/E2-rl-training-and-models.md#child-user-stories) |
| S12 | Vision-aware reward shaping | E1 | Core | Planned | S2, S9 | - | `story/s12-vision-aware-reward-shaping` | Not created | [Epic detail](epics/E1-vision-perception.md#child-user-stories) |
| S13 | Model persistence redesign | E2 | Core | Planned | - | - | `story/s13-model-persistence-redesign` | Not created | [Epic detail](epics/E2-rl-training-and-models.md#child-user-stories) |
| S14 | Testing infrastructure | E4 | Core | Planned | - | - | `story/s14-testing-infrastructure` | Not created | [Epic detail](epics/E4-reliability-validation.md#child-user-stories) |
| S15 | Documentation catch-up | E4 | Core | Planned | S1-S14 | - | `story/s15-documentation-catch-up` | Not created | [Epic detail](epics/E4-reliability-validation.md#child-user-stories) |

## Follow-on Stories

These IDs are allocated, but the work is outside I1 completion scope unless the Initiative is explicitly amended or a new Initiative is created.

| ID | Story/Bug | Parent | Scope | Status | Depends on | Blocks | Branch | Branch state | Canonical record |
|---|---|---|---|---|---|---|---|---|---|
| S16 | Headless mGBA in Docker | E3 | Follow-on | Planned | S11 | S17 | `story/s16-headless-mgba-docker` | Not created | [Epic detail](epics/E3-emulator-integration-runtime.md#future-candidates) |
| S17 | P/Invoke spike for mGBA core | E3 | Follow-on | Planned | S16 | - | `story/s17-mgba-pinvoke-spike` | Not created | [Epic detail](epics/E3-emulator-integration-runtime.md#future-candidates) |
| S18 | ControlPanel screenshot grid | E3 | Follow-on | Planned | S9 | - | `story/s18-controlpanel-screenshot-grid` | Not created | [Initiative detail](initiatives/I1-computer-vision-rl-overhaul.md#future-candidates) |
| S19 | PPO + LSTM experiment | E2 | Follow-on | Planned | S11 | - | `story/s19-ppo-lstm-spike` | Not created | [Epic detail](epics/E2-rl-training-and-models.md#future-candidate) |

## Standalone and Historical Stories

| ID | Story/Bug | Parent | Branch | Status | Branch state | Canonical record |
|---|---|---|---|---|---|---|
| S0 | Repository refactor and PROJECT_OVERVIEW documentation | Standalone, historical | - | Done | Historical; branch not tracked | [Archive](stories/archive/S0-repo-refactor-project-overview.md) |
| S21 | GitHub Copilot BMAD workflow | Standalone | `story/s21-copilot-bmad-workflow` | Done | Merged into development | [Archive](stories/archive/S21-copilot-bmad-workflow.md) |

No active Bugs are registered. New Bugs use the next global `S<n>` and appear in their owning Epic's child list and the corresponding section above.

## Progress Rules

- This file owns status, parent/child relationships, scope, dependencies, branch state, and the next actionable item. Update it in the same approved operation that creates/revises a work item or changes its lifecycle state.
- `/bmad-plan`/`/bmad-decompose` reserve IDs and add rows before a child branch starts. `/start-flow` records the exact branch and sets its state to `Active` when creating it; `/bmad-build` moves the row to `In Progress`; `/bmad-review` records `Reviewed`; `/bmad-commit` marks the item `Done` and the branch `Committed; awaiting merge`; `/bmad-merge` records `Merged into <parent>` and refreshes the parent's next item.
- Branch states are `Not created`, `Active`, `Committed; awaiting merge`, `Merged into <parent>`, or `Historical; branch not tracked`. Every active Story row records its exact branch name; historical items without a tracked branch use `-`.
- The status vocabulary is `Planned`, `Next`, `In Progress`, `Implemented`, `Reviewed`, `Done`, `Blocked`, or `Cancelled`. Use `Follow-on` in Scope rather than a special status.
- Preserve completed records. Do not delete rows or reuse IDs. If scope is rejected, mark `Cancelled` and retain the ID and rationale.
- This file owns ID allocation for I/E/S/ADR/BR. ADR and brainstorm decision status and citations are registered in the Decision and Brainstorm Records section of `docs/PROJECT_SPEC.md`; do not duplicate artifact lifecycle status here.
