# BMAD Workflow for PokemonRedRL

This is the short operating guide for taking work from an idea to a reviewed commit with GitHub Copilot. The canonical entry point is `/start-flow`; the phase prompts in `.github/prompts/` contain the detailed procedures.

## Scope

This repository uses the project-local BMAD adaptation in `.github/prompts/`, `.github/agents/`, and `_bmad/config.toml`. It does not install or depend on the AI Forge platform, `vsdlc`, external issue trackers, Figma, TMS, MCP servers, telemetry hooks, or Forge flow records. Work items, decisions, and plans are managed in this repository and in the conversation.

The workflow adopts ForgeAI's useful delivery ideas: one clear route per work item, Quick/Full selection based on decisions needed before coding, explicit human approval gates, parent-based branches, and upward archive/merge. It does not import Forge-specific branch automation, artifact tracking, or integrations.

## Start or Resume

Use `/start-flow`, or ask in natural language: "start flow", "begin work", "начни работу", "запусти процесс", "запусти BMAD", or a similar phrase. These phrases invoke the same prompt; Copilot does not provide multiple slash aliases for one prompt file.

The entry prompt reads `docs/PROJECT_SPEC.md` first, classifies the work level, checks the current branch and worktree, asks Quick/Full for User Stories, resolves the parent, and creates or resumes a local branch. It never stashes, commits, pushes, or merges automatically.

## Work Hierarchy

| Level | ID and canonical record | Branch | Parent |
|---|---|---|---|
| Initiative | `I<n>` — `docs/initiatives/I<n>-<slug>.md` | `initiative/i<n>-<slug>` | configured default branch |
| Epic | `E<n>` — `docs/epics/E<n>-<slug>.md` | `epic/e<n>-<slug>` | its Initiative branch |
| User Story | `S<n>` — `docs/stories/S<n>-<slug>.md` | `story/s<n>-<slug>` | its Epic branch |
| Bug | `S<n>` — `docs/stories/S<n>-<slug>.md` | `bug/s<n>-<slug>` | its Epic branch, or default branch if standalone |

ID prefixes and the registry path are configured in `_bmad/config.toml [ids]`; [WORK_ITEMS](WORK_ITEMS.md) records allocated IDs and next values. IDs are project-global, never reused or renumbered; slugs may change while IDs remain stable. Branch prefixes and the default base are configured in `_bmad/config.toml [git]`. WORK_ITEMS is authoritative for parent assignment and child membership; Initiative/Epic records carry outcome and story-contract detail. A story belongs to one Epic unless explicitly standalone. The current [Initiative](initiatives/I1-computer-vision-rl-overhaul.md) is divided into four Epics; its legacy roadmap path is only a compatibility link.

## Choose the Route

| Work | Route |
|---|---|
| New Initiative or Epic | Reserve the Initiative ID through `/start-flow`; for child Epics, use `/bmad-decompose` on the parent Initiative branch to reserve an `E<n>` before creating the Epic branch |
| Clear User Story | Quick Flow: `/bmad-build` writes and gets approval for a concise implementation spec, then builds; pause for optional `/bmad-test-design` before code if high-risk |
| User Story with requirements/architecture to settle first | Full Flow: `/bmad-plan` → `/bmad-architecture` → optional `/bmad-test-design` for high risk → `/bmad-build`; `/bmad-plan` selects Light/Standard/Full artifact depth separately from the Quick/Full route and uses the configured default as its suggestion |
| User Story with material, unclear UI behavior | Add `/bmad-ux` before implementation to approve a WPF interaction contract; no Figma is required |
| Bug | Quick Flow, always |
| Unclear idea or alternatives need exploration | Optional bounded `/bmad-brainstorm` before opening a delivery branch, then restart `/start-flow` with the selected outcome |
| Obvious mechanical edit (typo, formatting-only) | Make the requested edit directly; skip planning ceremony |
| Implemented work ready to compare with its plan | `/bmad-review` |
| User asks where the flow stands or what comes next | `/bmad-help` |
| Approved assumptions/scope materially change | `/bmad-correct-course`, then reapprove the revised plan |
| Code review without a plan, or an explicitly requested extra source audit | `/bmad-code-review`; optional and separate from the required `/bmad-review` |
| Reviewed work that the user wants committed and merged | `/bmad-archive` → `/bmad-commit` → `/bmad-merge` |
| Completed cycle worth learning from | Optional `/bmad-retrospective` |

The deciding question for Quick vs Full is whether requirements or architecture need their own reviewed artifacts before implementation, not how many files change. Each Story should target one cohesive outcome; split independently shippable outcomes through Epic decomposition, but keep cross-file/cross-layer implementation of one outcome together. Bugs always use Quick Flow. Initiative and Epic flows decompose into children rather than using either delivery route.

## Delivery Loop

1. **Orient.** Read `docs/WORK_ITEMS.md` first for current hierarchy, status, dependencies, branch state, and next work item. Then read Project Spec for stack, architecture decisions, and risks; read the relevant Initiative/Epic for the work contract. Check `git status` before touching files.
2. **Start.** Run `/start-flow`; choose the work level and parent. For Story work, choose Quick or Full. The prompt reserves or reads the stable ID and creates the branch from its configured parent. If an ancestor branch is missing, start/resume the Initiative, then its Epic, then the Story; Start Flow stops instead of creating a descendant directly from `development` or auto-creating ancestors.
3. **Decompose (parent flows).** On an Initiative branch, `/bmad-decompose` defines child Epics. On an Epic branch, it defines child User Stories. Approve the proposed structure before the prompt writes it. Each child starts later as its own flow and branch.
4. **Plan (Full Flow).** Use `/bmad-plan`; Story plans live in `docs/stories/S<n>-<slug>.md` and Full-depth specs may have companions in `_bmad-output/spec-S<n>-<slug>/`. Planning-artifact depth (Light/Standard/Full) is distinct from the delivery route (Quick/Full). Approve the plan before code.
5. **Design when needed.** Use `/bmad-ux` when a material UI change still has unresolved interactions or states. Full Flow runs `/bmad-architecture` after the approved plan. Record an explicit no-op when architecture is unchanged. For high-risk work, `/bmad-test-design` can add a risk-based verification plan; it never installs a test framework or writes test code.
6. **Build.** Quick Flow's `/bmad-build` investigates repository context, checks for a single coherent outcome, surfaces unresolved intent gaps, irreversible actions, and implementation footprint, then pauses for spec approval before code. It also pauses for `/bmad-ux` or `/bmad-test-design` when those optional gates apply. Full Flow builds from approved planning, UX/architecture, and verification artifacts. Run focused tests/builds for each meaningful slice.
7. **Review.** `/bmad-review` is the required acceptance gate against the plan, verification evidence, integration contracts, and project records. It traces each behavior-changing acceptance signal to an observable result; a passing build alone is insufficient. `/bmad-code-review` is an optional additional correctness/performance/security/testability audit; it does not replace the required review.
8. **Archive.** After review, `/bmad-archive` moves the Story plan or eligible parent record and prepares status/link updates. It does not stage or commit. Before closing an Epic/Initiative, verify parent-level acceptance and integration evidence; child merges alone do not prove the combined outcome. An Epic retrospective is recommended, but remains optional.
9. **Commit.** `/bmad-commit` commits the reviewed implementation together with the prepared archive/status changes. The archive is therefore committed before any merge.
10. **Merge upward.** `/bmad-merge` verifies the archive changes are committed, asks for approval, and merges exactly one level: Story → Epic → Initiative → base branch. It detects an already-integrated child as a no-op and recommends the next child flow or parent closure without starting it automatically.
11. **Learn (optional).** Use `/bmad-retrospective` after a meaningful cycle and apply accepted lessons to the project docs or prompts.

If requirements or architecture change materially after approval, pause implementation, update the plan with the user, and then continue against the revised plan. Do not silently change the contract mid-build.

## Human Approval Gates

- The plan is approved before planned implementation starts.
- Architecture decisions that materially affect the design are presented before they are recorded as accepted.
- Review findings are resolved, explicitly deferred, or accepted by the user; a clean build alone is not a review.
- The exact staged changes and commit message are shown before committing.
- Branch creation is authorized by starting a flow after the parent and route are clear; dirty worktrees require an explicit decision and are never stashed automatically.
- Archive edits, commits, and local merges each require their own explicit approval. A merge shows the child/parent pair; pushes require separate explicit approval.

## Records and Statuses

- `docs/WORK_ITEMS.md` is the sole source of truth for work-item IDs, hierarchy, status, scope, dependencies, branch/integration state, and next actions. Do not copy its tracking tables into Project Spec, Initiative, or Epic records.
- `docs/PROJECT_SPEC.md` is the source of project/technical context, accepted decisions, and open risks. Initiative/Epic records carry intent, boundaries, success signals, constraints, and child breakdown, not duplicate status or dependency tables.
- A story plan's lifecycle is `Planned` → `Approved` → `Implemented` → `Reviewed` → archived in the worktree → `Committed`; its `Status` remains `Reviewed` until `/bmad-commit` commits the archive and implementation together. Merge is a separate parent-level operation. Close an Epic only after every child branch is merged.
- `/bmad-archive` moves a completed Story plan to `docs/stories/archive/`, or an eligible Epic/Initiative record to its level's `archive/` folder, and records the canonical path in WORK_ITEMS. `/bmad-commit` marks the item `Done` after the archive and implementation commit succeeds. `/bmad-merge` merges one committed child into its immediate parent and records integration state without changing completion status.
- Full-depth specs and retrospectives stay in `_bmad-output/` as project artifacts. Brainstorms use BR IDs: active artifacts stay in `_bmad-output/`, and dispositioned artifacts may be archived under `_bmad-output/archive/brainstorms/`. ADRs use ADR IDs and remain in `docs/adr/` when superseded; update status and successor links in place.
- Brainstorms use stable `BR-nnnn` IDs. Keep active artifacts in `_bmad-output/`; after decisions are incorporated or explicitly abandoned, record the disposition and use `/bmad-archive` to move them to `_bmad-output/archive/brainstorms/` and update citations. ADRs use `ADR-nnnn`; keep them in `docs/adr/` when superseded, mark the old record `Superseded`, and link its successor so history and citations remain stable.
- Update `docs/CODEBASE_MAP.md` when files or projects are added, removed, or moved. Keep `docs/PROJECT_OVERVIEW.md` and `docs/PROJECT_SPEC.md` synchronized when design decisions or project state change.

## Git and Commit Safety

Follow the configured Initiative → Epic → Story branch hierarchy. This repository's configured root is `development`: Initiative branches and standalone work start there, Epic branches start from their Initiative, and Story/Bug branches start from their Epic. `/bmad-merge` returns each item to its immediate parent and never promotes to `main`; the user handles `development` → `main` manually. Never discard changes that predate the task, and do not stage the entire repository by default. Review `git status`, the unstaged diff, and the staged diff; stage only the intended, reviewed paths. Do not auto-sync, rebase, merge, or push as part of branch creation.

ROMs, save states, and model backups are intentionally present in this repository, but are not routine task changes. Do not include them in a commit unless the task explicitly requires it and the user confirms.

## Copilot Commands

| Command | Purpose |
|---|---|
| `/start-flow` | Start or resume work and route to the correct phase |
| `/bmad-help` | Explain current flow state and recommend the next command |
| `/bmad-decompose` | Decompose an Initiative into Epics or an Epic into Stories |
| `/bmad-brainstorm` | Explore an unclear problem or alternatives |
| `/bmad-plan` | Produce and validate an implementation plan |
| `/bmad-correct-course` | Propose an approved plan revision after material changes |
| `/bmad-architecture` | Design components, boundaries, and ADRs |
| `/bmad-test-design` | Design, update, or validate risk-based checks for a Story or Epic |
| `/bmad-build` | Implement an approved plan |
| `/bmad-review` | Review implementation against the plan and project risks |
| `/bmad-code-review` | Standalone code review when requested |
| `/bmad-ux` | Design, update, or validate a user-centered interaction contract when needed |
| `/bmad-archive` | Move a completed work record, or archive a dispositioned brainstorm; never move ADR history |
| `/bmad-commit` | Commit reviewed implementation plus prepared archive changes |
| `/bmad-merge` | Merge an archived child branch into its immediate parent |
| `/bmad-retrospective` | Capture evidence-based process lessons |
| `/cartograph` | Regenerate `docs/CODEBASE_MAP.md` after structural file changes |

## Optional and Excluded

- `/bmad-brainstorm` is an opt-in pre-flow step for ambiguous goals or genuinely different approaches; Start Flow uses its bounded Quick Discovery mode, while a clear Story skips it and a user-requested deep session keeps the full techniques.
- `/bmad-code-review`, `/bmad-test-design`, `/bmad-ux`, and `/bmad-retrospective` are conditional supplements, not gates on every Story.
- Separate PRD/PRFAQ/product-brief ceremonies are not installed: `/bmad-plan` plus Initiative/Epic records cover project requirements. This deliberately keeps the planning path smaller than BMAD's full product/planning catalog while preserving intent, constraints, acceptance signals, architecture, and decomposition where the change needs them.
- Separate `bmad-spec`/sprint-planning artifacts are not installed: Epic → Story decomposition is the planning boundary; no sprint cadence or sprint-status file exists.
- Advanced elicitation and `bmad-forge-idea` are folded into optional `/bmad-brainstorm`; no duplicate ideation command is needed. `bmad-project-context`/`bmad-customize` are unnecessary because repo instructions, PROJECT_SPEC, and the ordinary Copilot prompts are the configuration source.
- `bmad-walkthrough` is not a separate gate: the required `/bmad-review` presents findings and verification for the user to inspect. `bmad-teach-me-testing` is educational rather than part of delivery.
- Playwright/Cypress, autonomous build loops, Figma, TMS, MCP, `vsdlc`, and Vention connector skills are excluded. Testing stays appropriate to .NET/WPF/mGBA and the currently available test infrastructure.

See [BMAD-MIGRATION.md](BMAD-MIGRATION.md) for the transition from the old prompt names and [copilot-instructions.md](../.github/copilot-instructions.md) for repository conventions.