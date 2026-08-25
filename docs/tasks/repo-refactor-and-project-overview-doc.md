# Full repository structural refactor + PROJECT_OVERVIEW documentation

Status: Done

## Goal

1. Perform a **behavior-preserving** structural/style refactor across all 5 C# projects (naming, formatting, dead code, file organization, comment consistency, safe mechanical fixes like tensor-dispose/nullability where behavior stays equivalent) — no intentional change to game logic, RL algorithm, reward shaping, or network protocol.
2. Add a new narrative document `docs/PROJECT_OVERVIEW.md`: the project's purpose, design concepts, current implementation and *why* it's built this way, so an AI agent gets full context to spot inconsistencies or propose improvements. It links to `docs/CODEBASE_MAP.md` for the file-by-file inventory but does not duplicate it.

## Affected files/projects

- All of `src/PokemonRedRL.Agent`, `PokemonRedRL.Core`, `PokemonRedRL.Models`, `PokemonRedRL.DAL`, `PokemonRedRL.Utils` (C#).
- Lua bridge scripts: `src/scripts/mgba_socket.lua` and `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua` — style/whitespace cleanup only, no protocol/behavior changes (see finding below: the two copies have already diverged in actual protocol, not just style).
- Out of scope: `src/mGBA-0.10.5-win64/scripts/socketserver.lua`, `sockettest.lua`, and `pokemon.lua` — confirmed during implementation that `pokemon.lua` is mGBA's own generic bundled Pokemon Gen1/2/3 memory-reading helper library (standard `Game`/`Generation1En`/`Generation2En`/`Generation3En` API, charmap tables shipped with mGBA itself), not code authored for this project; treat all three like the rest of the vendored `mGBA-0.10.5-win64/` distribution.
- New file: `docs/PROJECT_OVERVIEW.md`.
- Possibly `docs/CODEBASE_MAP.md` (refresh via `/cartograph` at the end if refactor moves/renames anything).

## Steps

- [x] **1. Research pass.** Re-read every `.cs` file in the 5 projects (not just the ones already sampled) to build a definitive list of refactor candidates: inconsistent naming (`RewardCalculatorService: IRewardCalculatorService` missing space, mixed `camelCase`/`PascalCase` locals like `visitedLocations`), leftover `// todo:` comments (`StatePreprocessorService`), unused constructor parameters (`MGBAEmulatorClient` takes `NetworkConfig`/`ConnectionManager` but only reads `config.Port`, never uses `connection` directly — confirmed unused: `MGBAEmulatorClient` is only ever constructed via DI in `Program.cs`, no manual `new MGBAEmulatorClient(...)` call sites exist, so its own `ConnectionManager` param can be safely dropped; `SocketProtocol` gets the same scoped `ConnectionManager` instance independently), unused `using` directives, unreachable/dead code, unusued `System.Formats.Asn1.AsnWriter` static import in `Program.cs` (looks like an accidental IDE auto-import), unnecessary field re-assignment (e.g. `_previousMoney` set both inside `GetMoneyReward` and again in `CalculateReward`).
  - **Missing namespace**: `RedisExperienceRepository.cs` declares no `namespace` at all (lives in the C# global namespace) while every sibling file in `PokemonRedRL.Models/Services/` uses `namespace PokemonRedRL.Models.Services;` — add the missing namespace declaration. Verified via `vscode_listCodeUsages`-style grep that it's only referenced through DI/`using PokemonRedRL.Models.Services;`, so this is a safe, no-behavior-change fix (global-namespace types remain visible everywhere either way). **Extended finding:** `IExperienceRepository.cs` (its interface, in `PokemonRedRL.Models/Interfaces/`) is *also* missing a namespace — matching sibling `Interfaces/` folders use `PokemonRedRL.<Project>.Interfaces` (e.g. `PokemonRedRL.Core.Interfaces`), so this should become `PokemonRedRL.Models.Interfaces`. Consumers needing a new `using PokemonRedRL.Models.Interfaces;` line: `RedisExperienceRepository.cs`, `Program.cs`, `ExplorationAgent.cs`, `DQNTrainer.cs` — grep confirmed these are the only 4 files referencing `IExperienceRepository`.
  - **Mismatched namespace vs. project**: `StatePreprocessorService.cs` physically lives in `PokemonRedRL.Core/Services/` but declares `namespace PokemonRedRL.Utils.Services;` (copy/paste leftover, doesn't match its own project). Fixing it to `PokemonRedRL.Core.Services` requires updating the `using PokemonRedRL.Utils.Services;` directive in `Program.cs` and any other consumer — treat as in-scope but do it in its own sub-step with a build check immediately after, since it has a small blast radius across files.
  - **Stale comment vs. value**: `DQNTrainer._syncInterval = 500` is annotated `// Синхронизация каждые 100 шагов` ("sync every 100 steps") — the comment contradicts the actual value. Fix the comment text only; do **not** change `500` (that would be a behavior change).
  - **Lua bridge scripts — critical finding**: `src/scripts/mgba_socket.lua` and `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua` are **not copies of each other** — they've functionally diverged (the `mGBA-0.10.5-win64` copy implements a much richer protocol: JSON responses via `lunajson`, full `GameState` fields (badges, money, party, battle flags, trainer ID, PC items), a button-press event queue with frame-based durations; the `src/scripts` copy only implements a minimal CSV `hp,map,x,y` response). The `mGBA-0.10.5-win64` copy is almost certainly the one actually loaded by a running mGBA instance and kept in sync with the current `GameState`/`SocketProtocol` C# code; `src/scripts/mgba_socket.lua` looks stale/outdated. **Do not merge or reconcile them as part of this refactor** — that's a behavior/protocol decision, not a style one. Only apply cosmetic fixes independently within each file (encoding artifacts in comments, consistent Russian/English mixing) and record the divergence as a prominent open question in `docs/PROJECT_OVERVIEW.md`.
  - **Encoding check — false alarm, corrected during implementation**: comments initially appeared as mojibake in a PowerShell `Compare-Object` terminal dump, but re-reading both files directly confirmed all Cyrillic comments render correctly (UTF-8, no actual encoding corruption). No encoding fix needed; this was a terminal/console codepage artifact, not a file issue.
- [x] **2. Record known *logic* inconsistencies as documentation-only findings** (do NOT fix as part of this "no logic changes" pass): `RewardCalculatorService` never updates `_previousBadges`/`_previousLevels`, only `_previousMoney` — this looks like a bug (badge/level rewards would likely re-fire every step), but fixing it changes reward-shaping behavior, so it must only be written up in `docs/PROJECT_OVERVIEW.md` as a known open issue/question for the user, not silently patched. Also record: `DQNTrainer.SelectAction` allocates a `new Random()` per call (statistically fine but wasteful/non-seedable) — flag as an optional follow-up, do not change during this pass since altering the RNG source changes the exact action sequence produced for a given run.
- [x] **3. Apply refactor project-by-project** (Utils → DAL → Core → Models → Agent, respecting dependency order so each project still compiles as you go):
  - Normalize whitespace/brace style, class declaration spacing (`class Foo : IBar`), consistent field/property ordering.
  - Add the missing `namespace PokemonRedRL.Models.Services;` to `RedisExperienceRepository.cs` and `namespace PokemonRedRL.Models.Interfaces;` to `IExperienceRepository.cs`, adding the corresponding `using PokemonRedRL.Models.Interfaces;` to `RedisExperienceRepository.cs`, `Program.cs`, `ExplorationAgent.cs`, and `DQNTrainer.cs`; fix `StatePreprocessorService`'s namespace to `PokemonRedRL.Core.Services` and update the `using` in `Program.cs` (and `ExplorationAgent.cs` if it references the old namespace) in the same sub-step, then build immediately.
  - Remove genuinely dead/unused code (unused usings, unused ctor params/fields, the stray `System.Formats.Asn1.AsnWriter` import in `Program.cs`, the unused `ConnectionManager` ctor param in `MGBAEmulatorClient`) — only when `vscode_listCodeUsages`/build warnings confirm they're unused, never by guesswork.
  - Keep existing Russian comments; do not translate wholesale (per repo convention), but fix obviously broken/garbled comment fragments and comment/value mismatches (e.g. `DQNTrainer._syncInterval`) if found.
  - Do not rename any public type, public member, DI-registered service lifetime, or file unless it's a pure typo/casing fix that has zero external references outside the solution (verify via `vscode_listCodeUsages` first).
- [x] **3b. Lua bridge script cleanup** (`src/scripts/mgba_socket.lua`, `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua`): removed stray trailing semicolons and normalized blank lines in `src/scripts/mgba_socket.lua` (the `mGBA-0.10.5-win64` copy was already clean, no changes needed there). Made **zero** changes to memory addresses, command strings, response formats, or control flow in either file, and did not attempt to unify the two `mgba_socket.lua` variants or touch the hardcoded absolute `package.path`/`package.cpath` (flagged as a portability issue in the overview doc instead, since verifying a path change requires a real mGBA run). `pokemon.lua` excluded per the corrected scope above.
- [x] **4. Build after each project** (`dotnet build src/PokemonRedRL.sln`) to confirm no regressions before moving to the next project.
- [x] **5. Write `docs/PROJECT_OVERVIEW.md`** covering: problem statement/motivation, high-level RL approach (DQN + prioritized replay + multi-agent shared learning via Redis), why each project boundary exists, the emulator bridge protocol and its constraints, key design tradeoffs (hardcoded Redis/network config, no test project, epsilon-greedy schedule), and a "Known gaps / open questions" section listing things found in step 2 plus the Lua-script divergence from step 3b, framed so a future AI agent/reviewer can use it to find inconsistencies or propose improvements. Link to `docs/CODEBASE_MAP.md` instead of repeating its content.
- [x] **6. Regenerate `docs/CODEBASE_MAP.md`** via `/cartograph` if the refactor touched file locations, and add a reference to the new `docs/PROJECT_OVERVIEW.md` from `.github/copilot-instructions.md`'s docs section.
- [x] **7. Final full solution build** and summarize every file touched, grouped by project.

## Implementation summary

Full solution build: **0 errors**, warnings unchanged from baseline (all pre-existing nullable/NuGet-advisory warnings, none newly introduced).

**PokemonRedRL.Utils**
- `Enums/Enums.cs`: converted block-scoped `namespace { }` to file-scoped `namespace ...;` for consistency with every other file in the repo. No other changes.

**PokemonRedRL.Core**
- `Services/StatePreprocessorService.cs`: fixed namespace from `PokemonRedRL.Utils.Services` to `PokemonRedRL.Core.Services` (file physically lives in Core, was a copy/paste leftover); fixed `class Foo: IBar` → `class Foo : IBar` spacing.
- `Services/RewardCalculatorService.cs`: fixed `class Foo: IBar` → `class Foo : IBar` spacing; removed a redundant `_previousMoney` reassignment inside `GetMoneyReward` (the outer `CalculateReward` already unconditionally sets it to the same value — no behavior change).
- `Emulator/MGBAEmulatorClient.cs`: removed the unused `ConnectionManager connection` constructor parameter (confirmed via `vscode_listCodeUsages` that the class is only ever constructed through DI, no manual call sites).

**PokemonRedRL.Models**
- `Interfaces/IExperienceRepository.cs`: added the missing `namespace PokemonRedRL.Models.Interfaces;` (was in the global namespace).
- `Services/RedisExperienceRepository.cs`: added the missing `namespace PokemonRedRL.Models.Services;` (was in the global namespace) + `using PokemonRedRL.Models.Interfaces;`.
- `ReinforcementLearning/DQNTrainer.cs`: added `using PokemonRedRL.Models.Interfaces;`; fixed the `_syncInterval` comment (said "every 100 steps", value is actually `500`) to match the real value.

**PokemonRedRL.Agent**
- `Program.cs`: added `using PokemonRedRL.Models.Interfaces;`; removed the now-unused `using PokemonRedRL.Utils.Services;` and the stray `using static System.Formats.Asn1.AsnWriter;` (accidental IDE auto-import, confirmed unused); collapsed a double blank line.
- `ExplorationAgent.cs`: added `using PokemonRedRL.Models.Interfaces;`.

**Lua bridge scripts**
- `src/scripts/mgba_socket.lua`: removed two stray trailing semicolons and normalized blank lines. No command/response/control-flow changes. `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua` needed no changes (already clean). `pokemon.lua`/`socketserver.lua`/`sockettest.lua` confirmed to be vendored mGBA-authored scripts, left untouched.

**Documentation**
- Added `docs/PROJECT_OVERVIEW.md` (motivation, RL approach, project-boundary rationale, bridge protocol + its constraints, design tradeoffs, and a "Known gaps/open questions" section).
- Regenerated `docs/CODEBASE_MAP.md` to reflect the namespace fixes, corrected Lua-script descriptions (authoritative vs. legacy vs. vendored), and the new `docs/PROJECT_OVERVIEW.md`/`docs/tasks/` entries.
- Linked `docs/PROJECT_OVERVIEW.md` from `.github/copilot-instructions.md`.

**Not changed (documented, not fixed — see `docs/PROJECT_OVERVIEW.md` §6):** the `RewardCalculatorService` badge/level reward bug candidate, the duplicate `IExperienceRepository` DI registration, the two diverged `mgba_socket.lua` protocols, the apparently-unused `ExperienceReplay`/`MemoryCache`/`SumTree` classes, the hardcoded absolute Lua `package.path`, and `DQNTrainer.SelectAction`'s per-call `new Random()`.

Ready for `/review-changes`.

## Review notes (from `/review-changes`)

**Diff scope:** `git status`/`git diff --stat` confirms exactly the 10 files listed in the implementation summary above were touched (plus new, untracked `docs/PROJECT_OVERVIEW.md`, `docs/CODEBASE_MAP.md` updates, and this plan file) — no scope creep, nothing planned is missing. Every diff was read line-by-line and matches its stated description in the summary; no logic, protocol strings, or numeric constants were altered anywhere.

**Reviewer question raised by the user:** *"is it actually correct that `GetMoneyReward` no longer sets `_previousMoney`?"* — verified explicitly:
- `GetMoneyReward` is `private` and only ever called from `CalculateReward` (confirmed via grep — no other call sites, no reflection use).
- `CalculateReward` still unconditionally executes `_previousMoney = currentState.Money;` **after** calling `GetMoneyReward`, regardless of which branch `GetMoneyReward` took — this was true before and after the edit.
- No other method invoked between the `GetMoneyReward` call and that final assignment reads `_previousMoney`.
- Therefore the removed inner assignment was write-only-then-immediately-overwritten-with-the-same-value in every case; removing it is behavior-identical. Confirmed safe.

**Build:** `dotnet build src/PokemonRedRL.sln --no-incremental`: **0 errors** both before (stashed baseline) and after the change; **22 `warning CS*`** in both cases — byte-for-byte identical count, so no new warnings were introduced.

**Regression/quality checks:**
- No TorchSharp `Tensor` handling was touched by this change at all (only `.cs` namespace/using/comment edits and `.lua` whitespace) — no new leak risk introduced.
- No new DI registrations were added; the only DI-adjacent edit is removing `ConnectionManager` from `MGBAEmulatorClient`'s constructor — confirmed `ConnectionManager` remains registered `Scoped` in `Program.cs` and is still consumed by `SocketProtocol`, so nothing is orphaned.
- No TCP protocol changes on the C# side, so no corresponding Lua-side change was needed (the Lua edits were whitespace-only, independently verified against the protocol description in `docs/PROJECT_OVERVIEW.md` §4).
- No secrets/connection strings touched; `src/data/roms/`, `src/data/models/` untouched.
- Comment language: all edits preserved existing Russian comments as-is; no mixed-language edits introduced.

**`docs/CODEBASE_MAP.md`:** already regenerated during `/implement-task` (namespace fixes, Lua file re-classification, new doc links) — verified current and consistent with the diff above.

## Follow-ups

- Real logic issues intentionally left unfixed (see `docs/PROJECT_OVERVIEW.md` §6) remain open and are not blockers for this task: `RewardCalculatorService` badge/level staleness, duplicate `IExperienceRepository` DI registration, diverged `mgba_socket.lua` protocols, unused local-replay classes, hardcoded Lua path, per-call `Random()` in `DQNTrainer`.
- Recommend a real mGBA smoke test of `src/scripts/mgba_socket.lua` before ever relying on it again, given it's now confirmed to be the non-authoritative/legacy copy.

**Verdict: ✅ Pass.** Safe to `/commit-changes`.

## Risks & irreversible actions

- Removing a ctor parameter/field that looks unused but is actually consumed via reflection/DI convention would be a silent behavior change — verify with `vscode_listCodeUsages` before removing anything, not just visual inspection.
- Touching `Program.cs` DI registrations at all is higher risk (duplicate `IExperienceRepository` registrations already exist as both `Scoped` and `Singleton`, confirmed at lines 56 and 64 — the `Singleton` registration wins in practice since it's registered last; that's a pre-existing oddity, not something to silently "fix" under a no-logic-change refactor; only note it in the overview doc). The namespace fix for `StatePreprocessorService` does touch `Program.cs`'s `using` directives — keep that specific edit minimal (import list only, no registration changes) and build immediately after.
- Renaming/adding a namespace changes the file's fully-qualified type name; double-check no reflection-based lookup (e.g. `Type.GetType("...")` with a hardcoded string, MessagePack formatter resolution) depends on the old namespace before changing `RedisExperienceRepository`'s namespace — grep for string-based type lookups as part of step 1.
- Lua scripts can't be built/type-checked here — any edit carries a higher real-world risk than the equivalent C# change since a mistake would only surface at runtime inside mGBA. Keep Lua edits strictly cosmetic and flag them clearly for manual verification.
- No irreversible actions planned (no deletions of data, no git history changes); all changes go through the normal review/commit pipeline.

## Verification plan

- `dotnet build src/PokemonRedRL.sln` must succeed with no new warnings introduced (compare warning count before/after).
- Manual diff review of every changed file to confirm no behavioral statement was altered (no changed conditionals, arithmetic, ordering of side-effecting calls, protocol strings, or numeric constants like `_syncInterval`).
- After the two namespace fixes specifically, do a full solution build (not just the touched project) since `using` directives live in consumer files in a different project.
- Lua changes have no automated verification available; review them by diffing before/after per file and, ideally, having the user load the changed script in mGBA once before relying on it.
- No automated tests exist to run; this is called out as a pre-existing gap, not something this task adds.

## Open questions/assumptions

- `docs/PROJECT_OVERVIEW.md` is the chosen filename/location (vs. e.g. `docs/ARCHITECTURE.md`) — a "why/what" narrative doc distinct from the structural `CODEBASE_MAP.md`.
- The pre-existing reward-shaping bug (badges/levels never updated), the duplicate `IExperienceRepository` DI registration, and the two diverged `mgba_socket.lua` copies are flagged for documentation only; reconciling/fixing them would be a separate, explicitly-scoped follow-up task.
- **Needs a decision from the user before/while implementing:** which `mgba_socket.lua` is actually the one loaded by mGBA in current practice? This determines which copy is "authoritative" for future work, even though this task won't merge them.

## Review notes (from `/review-plan`)

✅ **Solid:** dependency-ordered refactor sequence, per-project build gate, explicit "document but don't fix" boundary for real logic bugs, verification plan matches the lack of a test suite.

⚠️ **Gaps found and fixed in this revision:** step 1 didn't originally include the missing/mismatched namespace issues (`RedisExperienceRepository`, `StatePreprocessorService`) or the `_syncInterval` comment/value mismatch — added with concrete sub-steps and an explicit "verify unused ctor param via call-site grep" note for `MGBAEmulatorClient`. Added a risk item about reflection/string-based type lookups before renaming namespaces. A follow-up review added Lua bridge scripts to scope (user request) and surfaced that the two `mgba_socket.lua` copies have functionally diverged, not just stylistically — scoped as cosmetic-only with the divergence itself recorded as a documentation finding.

❓ **Still open (unchanged, needs a user/implementer decision, not a plan defect):** the two open questions above (doc filename, which `mgba_socket.lua` is authoritative) — low-stakes for the filename, but the authoritative-script question should ideally be answered by the user before/while writing the overview doc.

Plan is implementation-ready.
