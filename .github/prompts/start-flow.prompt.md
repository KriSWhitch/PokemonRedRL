---
description: 'Start or resume a repository BMAD flow, choose Quick or Full delivery, and create its parent-based branch. Trigger for /start-flow, start flow, begin work, запусти процесс, начни работу, запустить BMAD, or similar Russian or English requests.'
---

# BMAD Start Flow — Repository Workflow

Use this prompt as the single entry point for starting or resuming an Initiative, Epic, User Story, or Bug flow. Resolve hierarchy paths, ID prefixes, and branch prefixes from `_bmad/config.toml` and project constraints from repository instructions. It creates or resumes a local branch and routes to repository prompts without external tracker, MCP, or Forge requirements.

## Start or Resume

1. Respond in the language the user is using. Keep that language for the rest of the flow unless they ask to switch.
2. Read repository root/IDE instructions and `_bmad/config.toml`; read `[paths].work_items_file` first for active hierarchy, status, dependencies, and next item, then read only the relevant Initiative/Epic/Story record and Project Spec context.
3. Inspect `git status --short --branch`. Do not discard, overwrite, stash, commit, push, or silently absorb existing changes.
4. Classify the work as Initiative, Epic, User Story, or Bug. Resolve its parent from the hierarchy records; ask if parent or scope is ambiguous. Check that a Story represents one cohesive outcome: separate independently shippable user outcomes, but do not split cross-file or cross-layer implementation of one outcome. If the request contains multiple independent outcomes, recommend an Epic/decomposition or ask the user which outcome to start; do not silently drop or defer any. A User Story belongs to one Epic; an Epic belongs to one Initiative. A standalone Story may use the configured default base branch.
	- Resolve the stable ID from `[ids]` and the registry at `[ids].registry_file`. Keep the ID of an existing record. For a new Initiative or standalone Story, reserve the next unused ID (`I<n>` or `S<n>`) in the registry as part of creating its record, with its exact branch name and initial `Not created` branch state. A new Epic or child Story must receive its ID through `/bmad-decompose` on the parent branch before its own flow starts. Never create an ID-less record or branch.
	- If the request is exploratory, has no clear outcome, or leaves materially different approaches open, offer `/bmad-brainstorm` before opening a delivery branch. If accepted, run the brainstorm and stop; start the Initiative/Epic/Story flow after the outcome is selected. Skip this offer when the user has already supplied a clear deliverable.
5. If the matching flow branch already exists and is checked out, resume it. If a local or fetched remote branch exists elsewhere, ask whether to resume it; for an approved remote resume, create a local tracking branch with `git switch --track <git.remote_name>/<branch>`. Never create a second branch with a different name for the same flow.
6. For a new User Story, recommend and ask the user to choose Quick or Full Flow before creating its branch. Recommend Quick when requirements and behavior are clear enough to specify as implementation begins; recommend Full when requirements or architecture need their own reviewed artifacts first. Ask: `Which delivery route should this User Story use?` Offer `Quick Flow` (one approved implementation spec, then build) and `Full Flow` (requirements and plan reviewed before build); mark the recommended choice. Bugs always use Quick Flow. Initiative and Epic flows use decomposition, not Quick/Full delivery.
	Quick/Full selects the delivery route, not the planning artifact depth. For Full Flow, `/bmad-plan` uses `[workflow].default_depth` as its initial suggestion and adjusts it to fit scope/risk; do not ask the user to choose the same dimension twice. Quick Flow always uses its concise implementation spec.
7. Resolve the branch base and create the new branch as described in **Branch Rules**. If the worktree is dirty, do not proceed without explicit user direction; never stash changes automatically.
8. Read and follow the selected phase prompt from `[paths].prompts_dir` as the authoritative phase procedure. Continue in this conversation; do not claim to have invoked another prompt unless it was actually invoked.

Ask only for information that changes the route or a decision the user must make. When the request is sufficiently clear, choose the route and proceed to its next human approval gate.

## Route

| Situation | Route |
|---|---|
| The request is an unclear idea or several materially different approaches | Offer `/bmad-brainstorm` before a delivery branch. If selected, use its Quick Discovery mode and stop; after choosing an outcome, restart `/start-flow`. |
| New Initiative | Reserve the next `I<n>` ID in the registry and create its branch, then read and follow `bmad-decompose.prompt.md` to establish the Initiative and propose child Epics. |
| New Epic under an Initiative, not yet in its child list | On the Initiative branch, use `/bmad-decompose` to propose and reserve the next `E<n>` ID; after approval, restart `/start-flow` to create the Epic branch. |
| New Story/Bug not yet in its Epic child list | On the Epic branch, use `/bmad-decompose` to add it with the next `S<n>` ID; after approval, restart `/start-flow` to create the child branch. |
| Existing Epic or Story/Bug with a registered ID | Use that ID to create/resume its branch and follow its delivery route. Never issue another ID for the same record. |
| User Story, Quick Flow | Create its branch from the parent Epic, then `/bmad-build` writes and gets approval for a spec. If UI behavior is materially unclear, pause for `/bmad-ux`; for high-risk boundaries, pause for `/bmad-test-design`. Then build → required `/bmad-review` → `/bmad-archive` → `/bmad-commit` → `/bmad-merge`. |
| User Story, Full Flow | Create its branch from the parent Epic, then `/bmad-plan` → optional `/bmad-ux` for unclear UI → `/bmad-architecture` → optional `/bmad-test-design` for high risk → `/bmad-build` → required `/bmad-review` → `/bmad-archive` → `/bmad-commit` → `/bmad-merge`. If architecture is unchanged, record that decision explicitly. |
| Bug | Create its branch from the parent Epic (or default base if standalone), then Quick Flow: `/bmad-build` → `/bmad-review` → `/bmad-archive` → `/bmad-commit` → `/bmad-merge`. Add `/bmad-test-design` or `/bmad-code-review` only when risk justifies it or the user requests it. |
| The request is an obvious mechanical change (for example, a typo or formatting-only edit) | Make the requested change directly; do not create planning ceremony. |
| An approved plan exists and implementation is requested or is the next agreed phase | Read and follow `bmad-build.prompt.md`. |
| Implementation is ready for required validation against its plan | Read and follow `bmad-review.prompt.md`. |
| The user asks for a code-focused review or accepts an extra review for high-risk code | Read and follow `bmad-code-review.prompt.md` as an optional second pass; it does not replace `bmad-review` and is not run automatically for every Story. |
| A material UI change has unresolved interaction/state decisions | Read and follow `bmad-ux.prompt.md`; it captures an interaction contract before implementation and does not require an external design tool. |
| A high-risk change needs an explicit verification strategy before implementation | Read and follow `bmad-test-design.prompt.md`; it proposes tests/checks but does not add a framework or write test code. |
| The user asks what happens next or where a flow stands | Read and follow `bmad-help.prompt.md`; do not change files or Git state. |
| New evidence materially invalidates the approved requirements, scope, or architecture | Read and follow `bmad-correct-course.prompt.md`; pause implementation until the revised plan is approved. |
| Review passed and the user asks to record the work | Read and follow `bmad-archive.prompt.md`, then `bmad-commit.prompt.md`, then `bmad-merge.prompt.md`. Each operation has one responsibility; push remains separately gated. |
| A completed cycle is being examined for process lessons | Read and follow `bmad-retrospective.prompt.md`; retrospective is optional. |

For UI work with unclear behavior, involve Sally through `/bmad-ux`. Clear, minor UI changes need no separate UX artifact. Use only design tools and integrations available and approved in the project context.

## Branch Rules

Read prefixes and the default base branch from `_bmad/config.toml` `[git]`:

| Work item | Record ID | Branch | Created from |
|---|---|---|---|
| Initiative | `I<n>` | `<git.initiative_branch_prefix>/i<n>-<slug>` | `[git].default_base_branch` |
| Epic | `E<n>` | `<git.epic_branch_prefix>/e<n>-<slug>` | its Initiative branch |
| User Story | `S<n>` | `<git.story_branch_prefix>/s<n>-<slug>` | its Epic branch, or default base when standalone |
| Bug | `S<n>` | `<git.bug_branch_prefix>/s<n>-<slug>` | its Epic branch, or default base when standalone |

IDs are required, project-global, sequential by type, and never reused or renumbered. Allocate the next value from the configured registry after checking active/archive records and visible branch refs; reserve it in the parent/registry before creating child work. If a concurrent branch already reserved that ID, stop before merge and allocate a new ID to the later unmerged item. Record filenames use the same uppercase prefix (`I<n>-<slug>.md`, `E<n>-<slug>.md`, `S<n>-<slug>.md`); branch IDs are lowercase. For a new flow, report the allocated ID, canonical record filename, branch, and parent in the branch-creation summary. For a new branch, derive a lowercase kebab-case slug from its approved title. Fetch refs from `[git].remote_name` when available and use the exact parent branch as the base; never guess a similarly named parent. Prefer `<git.remote_name>/<git.default_base_branch>` when it exists; otherwise use the matching local branch. If the parent branch is missing, stop and start/restore the parent flow first. If the target branch already exists, resume it only after confirming its ID and parent match this flow.

Before creating a branch, require a clean worktree unless the user explicitly confirms that the listed changes belong in this flow. In that case, the current branch must exactly match the resolved parent and the new branch must be created from that current parent HEAD; do not base it on a different remote ref. If it does not match, stop and ask the user to commit/stash separately. Do not stash or move changes automatically. For a clean worktree, use the latest fetched exact parent ref with `git switch --create <new-branch> <parent-ref>`. After creation, update the matching WORK_ITEMS row with the exact branch name and `Active` branch state without changing the work-item status. When resuming, verify the recorded branch; do not downgrade a later state such as `Committed; awaiting merge`. Report the resulting branch and parent. Do not push or merge during startup.

The active hierarchy is Initiative → Epic → User Story/Bug. Historical completed work is not recreated as new branches. After review, `/bmad-archive` moves the record and prepares status changes; `/bmad-commit` commits those changes with the reviewed implementation; only then does `/bmad-merge` merge into the immediate parent. The same order applies when closing an Epic or Initiative.

## Human Gates

- Present a plan and wait for approval before implementation unless the user has already approved that exact plan.
- During implementation, follow the plan and project instructions; report meaningful checkpoints and validation results.
- Review findings are reported with evidence. Do not silently widen scope to fix unrelated findings.
- A passed review does not authorize archive, commit, merge, or push. Ask for approval before each operation.

## Copilot Command Behavior

`/start-flow` is the canonical slash command. The English and Russian phrases in the description are natural-language triggers for this same prompt; Copilot prompt files do not create multiple slash-command aliases from one file. Do not create duplicate alias prompts unless the user explicitly asks for separate slash commands.