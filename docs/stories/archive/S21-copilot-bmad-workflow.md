# S21 — GitHub Copilot BMAD Workflow Integration
**ID:** S21
**Status:** Committed
**Parent:** Standalone
**Branch:** `story/s21-copilot-bmad-workflow`
**Base branch:** `development`
**Depth:** Standard
**Created:** 2026-10-29

## Why

Consolidate the project's existing BMAD prompts and personas into one discoverable Copilot entrypoint and a concise delivery guide, informed by the ForgeAI reference while excluding platform-specific tooling.

## Capabilities

| ID | Intent | Success Signal |
|---|---|---|
| CAP-1 | Start or resume one cohesive work outcome from one Copilot prompt using English or Russian natural-language requests, creating a separate branch for each initiative, epic, story, or bug. | `/start-flow` distinguishes independently shippable outcomes from cross-layer implementation, creates Initiative branches from `development`, Epic branches from their Initiative, Story/Bug branches from their Epic (standalone work from `development`), and routes to the next phase; plan artifact depth is selected separately. |
| CAP-2 | Give contributors one accurate guide from idea through reviewed commit. | `docs/BMAD-WORKFLOW.md` describes routes, approval gates, records, and commit safety. |
| CAP-3 | Keep workflow commands and status handling consistent across the repo. | No obsolete prompt entrypoints remain; planning, review, archive, commit, and merge prompts agree on lifecycle and staging. |
| CAP-4 | Model work as Initiative → Epic → User Story/Bug, with child work branches based on their parent branch. | The active vision/RL roadmap is an Initiative linked to Epics and their child stories; `development` is the configured root, and branch prefixes and parent ancestry are documented and configurable. |
| CAP-5 | Select Quick or Full delivery for story/bug flows; use decomposition flows for Initiatives and Epics. | Start Flow explains and asks for Quick/Full on stories/bugs, then routes to the corresponding existing BMAD prompts. |
| CAP-6 | Keep archive, commit, publish, and merge as separate approval-gated operations. | `/bmad-archive` changes records only and requires parent-level acceptance evidence; `/bmad-commit` commits implementation plus archive changes and asks separately before pushing the child branch; `/bmad-merge` requires the published ref to match the reviewed commit, integrates exactly one parent level, records `Merged into <parent>`, handles already-integrated branches safely, and recommends the next phase without starting it. Initiative roots and standalone work return to `development`; promotion from `development` to `main` remains a manual user action. |
| CAP-7 | Store User Story implementation plans and their archive under the `stories` hierarchy. | No active configuration, prompt, source comment, or Markdown link points at the retired plans directory. |
| CAP-8 | Keep the Copilot command catalog complete and make its personas and workflow prompts reusable across repositories without weakening their quality gates. | Every prompt has a documented purpose; all seven personas and workflow prompts avoid project-specific paths/stack facts; repository details come from instructions/context and configured `[paths]`; all referenced path keys exist; acceptance, verification, approval, and handoff criteria remain explicit and are cross-checked against the relevant ExamAI workflows. |
| CAP-9 | Assign every Initiative, Epic, Story, and Bug a stable project-wide ID and ID-prefixed canonical filename/branch. | `_bmad/config.toml [ids]` defines I/E/S prefixes; WORK_ITEMS lists all allocated IDs and next values; prompts allocate IDs only at Start Flow or parent decomposition, never reuse them, and check registry/parent/branch collisions; all canonical work records use matching ID-prefixed filenames and metadata while the retired T numbers remain documented aliases. |
| CAP-10 | Give ADRs and brainstorm artifacts stable identities and explicit lifecycle rules. | ADRs use sequential ADR IDs and remain in place when superseded with successor links; brainstorms use BR IDs, record where accepted decisions were incorporated, and are archiveable after incorporation or explicit rejection; existing records and citations resolve through the artifact registry. |

## Constraints

- Use GitHub Copilot native repository files only; do not add Forge, MCP, tracker, or external runtime dependencies.
- Preserve existing project-specific personas and BMAD phase prompts where they already fit.
- Preserve unrelated and pre-existing worktree changes.
- Use `development` as the workflow root; never merge workflow branches into `main`, which the user promotes manually.

## Non-goals

- Import ForgeAI's `vsdlc` runtime, telemetry, `flow.json`, YouTrack, Figma, TMS, or other connectors.
- Import ForgeAI's issue-tracker schema, hosted merge requests, or automatic remote pushes.

## Success Signal

The `/start-flow` prompt creates correctly nested local branches, separates cohesive outcomes from independent deliverables, selects a delivery route separately from planning-artifact depth, and routes Initiative/Epic decomposition; the active initiative has explicit Epic/story children. Build plans expose intent gaps, irreversible actions, and footprint before approval. Acceptance review verifies behavior against observable outcomes; parent closure checks integrated acceptance evidence. Archive changes are committed before a merge, and the archive/commit/merge prompts each have one responsibility. All workflow docs agree; editor diagnostics and `git diff --check` pass.

Every canonical work record, branch, hierarchy row, and reference uses one stable project-wide ID; the existing Initiative/Epics/Stories are migrated without duplicate IDs or broken links, with legacy T-number aliases preserved.
Architecture decisions and brainstorm artifacts use separate ID sequences. Superseded ADRs remain at their canonical path, while incorporated/dispositioned brainstorms can be archived without losing their consumer links.

## Steps

- [x] Add `/start-flow` as the single route into existing BMAD phases, including Russian natural-language triggers.
- [x] Replace the Forge-specific AME note with a project-local BMAD delivery guide.
- [x] Remove obsolete prompt commands and reconcile plan, build, review, and commit status/safety rules.
- [x] Update project instructions, PROJECT_SPEC, migration notes, README, codebase map, and persona state references.
- [x] Validate changed files and confirm obsolete entrypoints are absent.
- [x] Move the active roadmap to Initiative scope and create Epic/story decomposition records while preserving historical links.
- [x] Rename the plans directory to `docs/stories/` and update plan, archive, configuration, source-comment, and documentation paths.
- [x] Add branch configuration, automatic branch creation/resume, parent lineage, and Quick/Full route selection.
- [x] Add separate archive, commit, and merge workflows for story → epic → initiative → development, with explicit gates; keep development → main promotion manual.
- [x] Inventory ExamAI BMAD skills and agents; adapt only relevant Copilot-native roles and optional commands without Forge runtime dependencies.
- [x] Separate reusable agent role instructions from project-specific paths and facts using the repository-level context-source map.
- [x] Generalize reusable workflow prompts to load project facts from repository instructions and resolve document/artifact locations through `_bmad/config.toml [paths]`.
- [x] Cross-check adapted commands and personas against relevant ExamAI sources; retain portable evidence, risk, approval, handoff, and test-first criteria while excluding runtime-specific machinery.
- [x] Recheck the complete start-to-finish lifecycle against ExamAI and current BMAD guidance; align route/depth selection, acceptance evidence, parent closure, merge no-op, and next-phase handoff.
- [x] Add project-wide I/E/S allocation and registry policy, migrate existing canonical record filenames/content/branches/references, and preserve legacy T-number aliases.
- [x] Add ADR/BR ID sequences, register existing decision artifacts, and define non-destructive ADR supersession plus dispositioned brainstorm archiving.
- [x] Clarify mandatory acceptance review versus optional code review; route brainstorm, UX, test design, help, and course correction only when relevant.
- [x] Add project-specific frontmatter to all personas and connect new optional prompts to Mary/John/Winston/Sally/Quinn as appropriate.
- [x] Update affected prompts, personas, project state, and workflow documentation; validate all references.

## Verification

- [x] Copilot editor diagnostics report no errors in changed files.
- [x] Search confirms the old prompt files are absent from `.github/prompts/`.
- [x] `git diff --check` passes for tracked changes.
- [x] Relative links in workflow and hierarchy docs resolve.
- [x] `_bmad/config.toml` parses as TOML and all `[paths]` keys referenced by prompts are configured.
- [x] Compare build, review, code review, UX, test design, retrospective, brainstorm, and persona guidance against the corresponding source workflows.
- [x] Verify every Start Flow handoff resolves to an installed prompt and every cataloged command exists.
- [x] Verify canonical filename IDs match record metadata, IDs are unique, all markdown links resolve, and TOML contains the configured ID prefixes/registry.
- [x] Verify ADR/BR IDs, statuses, incorporation links, archive paths, and successor/archive policies against current artifacts.
- [x] Verify Initiative → Epic → Story/Bug ancestry uses `development` as root and standalone Story/Bug merges return to `development`, never `main`.

## Risks

Copilot can discover the Russian phrases through the prompt description, but a single prompt file cannot define multiple slash-command aliases. `/start-flow` remains the canonical slash command.