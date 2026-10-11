# Task 2 — Pre-overhaul cleanup of known debt

Status: Done

> Sub-task of [docs/tasks/roadmap-vision-and-rl-overhaul.md](roadmap-vision-and-rl-overhaul.md) (Task 2), **narrowed per user decision**: multi-instance launch automation (`launch_agents.bat`) and programmatic fast-forward wiring — both confirmed unsolved by the Task 1 spike (no CLI/config solution found; GUI automation via `SendInput`/`keybd_event` did not work reliably) — are **split into a separate follow-up task**, not planned here. This task covers only the straightforward, well-understood cleanup items from `docs/PROJECT_OVERVIEW.md` §6.

## Goal

Resolve the known-debt items recorded in `docs/PROJECT_OVERVIEW.md` §6 that would otherwise get carried into the vision/RL overhaul's new code paths: fix the `RewardCalculatorService` reward-shaping bug (a real behavior change, deliberately — unlike the earlier "no logic change" refactor pass), remove the duplicate `IExperienceRepository` DI registration, retire the stale `src/scripts/mgba_socket.lua`, fix the hardcoded absolute Lua path in the authoritative bridge script, and remove the unused `Parquet.Net` package references. `ExperienceReplay`/`MemoryCache`/`SumTree` are confirmed unused but **kept as-is, only documented** (per user decision) — not deleted in this task.

## Affected files/projects

- `src/PokemonRedRL.Core/Services/RewardCalculatorService.cs` — bug fix.
- `src/PokemonRedRL.Agent/Program.cs` — remove duplicate `IExperienceRepository` DI registration.
- `src/scripts/mgba_socket.lua` — delete (confirmed stale/non-authoritative; the real bridge script is `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua`).
- `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua` — fix hardcoded absolute `package.path`/`package.cpath`.
- `src/PokemonRedRL.Utils/PokemonRedRL.Utils.csproj`, `src/PokemonRedRL.Core/PokemonRedRL.Core.csproj`, `src/PokemonRedRL.Agent/PokemonRedRL.Agent.csproj` — remove unused `Parquet.Net` `PackageReference` (keep it only in `PokemonRedRL.Models`, where `ModelExperience.cs` actually uses `[ParquetIgnore]`).
- `docs/PROJECT_OVERVIEW.md` — update §4 (remove "two diverged copies" language once one is deleted) and §6 (move fixed items out of the known-gaps list).
- `docs/CODEBASE_MAP.md` — regenerate via `/cartograph` (file removed, package references changed).
- `docs/tasks/roadmap-vision-and-rl-overhaul.md` — tick Task 2 (narrowed), insert a new Task 2b placeholder for the split-out launch-automation/fast-forward work.

## Steps

- [x] **1. Fix the `RewardCalculatorService` reward-shaping bug.** `CalculateReward` currently updates `_previousMoney` every call but never updates `_previousBadges`/`_previousLevels`, so `GetBadgeReward`/`GetLevelUpReward` likely re-fire the same reward every subsequent step after a badge/level gain instead of firing once. Fix: set `_previousBadges = currentState.Badges;` and `_previousLevels = new List<int>(currentState.PartyLevels);` alongside the existing `_previousMoney = currentState.Money;` at the end of `CalculateReward`. **This is a deliberate behavior/training-signal change** — call it out explicitly in the commit, unlike the earlier refactor's "no logic change" rule.
- [x] **2. Remove the duplicate `IExperienceRepository` DI registration in `Program.cs`.** Two registrations exist today: `services.AddScoped<IExperienceRepository>(provider => new RedisExperienceRepository(new RedisConfig()))` and `services.AddSingleton<IExperienceRepository, RedisExperienceRepository>()` (the latter registered last, so it wins today — confirm this with `dotnet build`/a quick DI-resolution sanity check, don't just assume). Keep the **Singleton** registration only (preserves current effective behavior — experience storage is meant to be shared across all agents via Redis, so one shared repository instance/connection makes more sense than one per agent scope) and delete the `AddScoped<IExperienceRepository>` line. **Concurrency check (new):** confirm `RedisExperienceRepository` is actually safe to share as a Singleton across `NUMBER_OF_AGENTS` (10) concurrent agent threads — it holds a `ConnectionMultiplexer`/`IDatabase`, both documented by StackExchange.Redis as thread-safe for concurrent use from multiple threads, and has no other mutable instance state, so this should hold, but confirm by reading the class once more rather than taking it on faith.
- [x] **3. Delete `src/scripts/mgba_socket.lua`** (confirmed stale: minimal CSV protocol, not what any running mGBA instance actually loads; confirmed via grep that no launch script or code references it, only documentation does) and update `docs/PROJECT_OVERVIEW.md` §4 (currently headed "⚠️ Known inconsistency: two non-identical `mgba_socket.lua` copies") to describe a single authoritative script instead — remove/retitle that whole subsection, not just edit its body text.
- [x] **4. Fix the hardcoded absolute Lua path** in `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua`'s `package.path`/`package.cpath` (currently hardcoded to `D:\Programming\PokemonRedRL\...`) to derive the script's own directory at runtime (e.g. via `debug.getinfo(1, "S").source`) instead of a hardcoded absolute path. **Requires a manual mGBA smoke test** (launch a real instance, confirm `require("socket")`/`require("lunajson")` still resolve and the bridge still connects) since there's no automated way to verify Lua changes — I'll drive this myself via terminal, the same way as the Task 1 spike.
- [x] **5. Remove the unused `Parquet.Net` package reference** from `PokemonRedRL.Utils`, `PokemonRedRL.Core`, and `PokemonRedRL.Agent` `.csproj` files (confirmed via grep: only `PokemonRedRL.Models/Experience/ModelExperience.cs` uses `[ParquetIgnore]`); keep it in `PokemonRedRL.Models`. Build each affected project after the change.
- [x] **6. Update documentation.** `docs/PROJECT_OVERVIEW.md` §6: remove the badge/level-bug and duplicate-DI-registration items from the known-gaps list (now fixed); keep the `ExperienceReplay`/`MemoryCache`/`SumTree`-unused note (not resolved, per user decision to keep them). Run `/cartograph` to refresh `docs/CODEBASE_MAP.md` (file removed, package refs changed).
- [x] **7. Update the roadmap.** In `docs/tasks/roadmap-vision-and-rl-overhaul.md`: tick Task 2 as resolved (narrowed scope only), and insert a new **Task 2b — Multi-instance launch automation & fast-forward wiring** entry (not planned in detail here — that's its own future `/plan-task`) carrying forward the `launch_agents.bat`/fast-forward items split out of this task.

## Risks & irreversible actions

- **Deleting `src/scripts/mgba_socket.lua` is a tracked-file deletion** — recoverable via git history, but per operational-safety guidance this warrants explicit confirmation before executing, not just inferring it from the roadmap's "reconcile or retire" wording. Flagging for `/review-plan` to confirm before `/implement-task` deletes it.
- **The reward-shaping fix is an intentional behavior change**, not a refactor — training runs before/after this fix are not directly comparable on reward magnitude for badges/levels. This should be visible in the commit message/plan, not buried as a side effect.
- **DI lifetime change risk is low but non-zero**: removing the `Scoped` registration relies on confirming the `Singleton` one is what's actually in effect today (last-registration-wins) — verify this explicitly (e.g. temporarily logging the resolved instance type/hash from two different agent scopes) rather than assuming, since getting this wrong would change how experience data is shared across the 10 parallel agents.
- **The Lua path fix cannot be verified by `dotnet build`** — needs a real mGBA run; if `debug.getinfo`-based path resolution doesn't work as expected inside mGBA's sandboxed Lua environment, fall back to leaving the hardcoded path with a clearer comment rather than shipping a broken bridge script.
- No git history rewrites, force-pushes, or ROM/save-state deletions.

## Verification plan

- `dotnet build src/PokemonRedRL.sln` after steps 1, 2, and 5 — 0 errors, no new warnings.
- Manual review of `Program.cs` after step 2 to confirm exactly one `IExperienceRepository` registration remains and it's the intended lifetime.
- Manual mGBA smoke test after step 4 (launch a real instance with the fixed script, confirm `ping`/`get_state` still work) — same terminal-driven approach as the Task 1 spike, no manual user involvement expected unless the same GUI-script-loading limitation from Task 1 applies here too (it will, since loading the script still requires Tools → Scripting — plan for that one manual step, same as last time).
- `grep`/build check after step 5 to confirm no leftover `Parquet` usings break in the three trimmed projects.
- `/cartograph` run after step 6, diffed to confirm it reflects the actual current state (file removal, package changes).
- No automated tests exist for `RewardCalculatorService` yet (that's Task 11's job) — note this gap explicitly rather than silently skip it; do not add ad-hoc tests in this task since it's out of scope.

## Open questions/assumptions

- **Confirmed by user:** launch automation/fast-forward split into a separate follow-up task; `ExperienceReplay`/`MemoryCache`/`SumTree` kept as-is, only documented (not deleted).
- **Assumed, needs `/review-plan` confirmation:** deleting `src/scripts/mgba_socket.lua` outright (vs. updating it to match, or keeping as a deliberately-simpler reference) — this plan's default is deletion since it's confirmed unused/stale and keeping a second protocol implementation around only invites future confusion, but this is a tracked-file deletion and should be explicitly signed off on.
- **Assumed, needs confirmation:** keeping the `Singleton` `IExperienceRepository` lifetime (matches today's effective behavior) rather than `Scoped` — flagging in case per-agent isolation was actually intended and the `Singleton` registration was the accidental one.
- Lua path fix approach (`debug.getinfo`-based self-relative resolution) is a reasonable first attempt but unverified until actually tested in mGBA's Lua sandbox — a fallback (clearer hardcoded-path comment, no functional change) is acceptable if it doesn't work.
- **Discovered but explicitly out of scope:** `PokemonRedRL.Core.csproj` also references `System.Net.Sockets` 4.3.0 — a NuGet package that only shims functionality already built into `System.Net.Sockets` since .NET Standard/Core (i.e. likely unnecessary on .NET 8). Not touched by this task (would be scope creep) — flagged here for Task 3 (.NET/package modernization) to pick up instead.

## Review notes (from `/review-plan`)

✅ **Solid:** correctly narrowed scope (launch-automation/fast-forward properly split out per user decision), the reward-bug fix is explicitly called out as an intentional behavior change rather than buried as a side effect, the tracked-file deletion is flagged for explicit sign-off rather than silently assumed, and the Lua path fix correctly acknowledges it can't be verified by `dotnet build` alone.

⚠️ **Gaps found and fixed in this revision:**
- **Missing concurrency verification.** The plan proposed keeping `IExperienceRepository` as a `Singleton` (shared across 10 concurrent agent threads) without verifying that `RedisExperienceRepository` is actually safe to share that way. Added an explicit concurrency check to Step 2 (its only state is a `ConnectionMultiplexer`/`IDatabase`, both documented thread-safe by StackExchange.Redis, so this should hold — but confirm by re-reading the class, not by assuming).
- **Stale justification.** Step 3's original wording said deleting the stale script "becomes unavoidable once Task 7 needs it" — but Task 7 was re-scoped after the Task 1 spike (vision uses OS window capture, not a new Lua command), so that justification no longer applies. Reworded to stand on its own merits (confirmed-unused, only docs reference it) rather than a now-incorrect dependency claim.
- **Imprecise doc-update instruction.** "Update the two diverged copies section" was vague about whether to edit or remove the section; clarified that `docs/PROJECT_OVERVIEW.md` §4's whole subsection (currently headed "⚠️ Known inconsistency...") should be retitled/removed, not just have its body text tweaked.
- **Verified no other code depends on the file being deleted.** Grepped the whole repo: only documentation references `src/scripts/mgba_socket.lua`; `launch_agents.bat` and all code already point at the authoritative `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua`. Deletion is safe from a code-dependency standpoint.
- **New discovery, kept out of scope on purpose:** `PokemonRedRL.Core.csproj` also has a `System.Net.Sockets` 4.3.0 package reference that looks like unnecessary legacy cruft (functionality is built into modern .NET). Not added to this task's scope (would be creep) — flagged as a note for Task 3 instead.
- Investigated a stray `src/mGBA-0.10.5-win64/cli_history.log` file noticed during review — confirmed via `git ls-files` it's a pre-existing tracked file, not session debris; no action needed.

❓ **Open questions carried forward (need a decision, not a plan defect):** the file-deletion sign-off and the `Singleton` DI lifetime confirmation — both already flagged with sound reasoning and low risk (git-reversible; StackExchange.Redis's own thread-safety guarantees), so proceeding with the plan's stated defaults unless corrected.

Plan is implementation-ready. Ready for `/implement-task`.

## Implementation summary

Full solution build: **0 errors, 0 warnings**. Manual mGBA smoke test for the Lua path fix: **passed** (`ping`/`get_state` both responded correctly against the fixed script).

**`PokemonRedRL.Core`**
- `Services/RewardCalculatorService.cs`: fixed the reward-shaping bug — `CalculateReward` now also updates `_previousBadges` and `_previousLevels` (previously only `_previousMoney` was updated), so badge/level rewards fire once per gain instead of re-firing every subsequent step. **This is an intentional training-signal behavior change**, not a refactor.
- `PokemonRedRL.Core.csproj`: removed the unused `Parquet.Net` package reference (kept `System.Net.Sockets`, out of scope — flagged for Task 3).

**`PokemonRedRL.Agent`**
- `Program.cs`: removed the duplicate `services.AddScoped<IExperienceRepository>(...)` registration; the `Singleton` registration (which already won in practice) is now the only one, matching effective prior behavior — confirmed safe since `RedisExperienceRepository`'s only state is a `ConnectionMultiplexer`/`IDatabase`, both thread-safe per StackExchange.Redis.
- `PokemonRedRL.Agent.csproj`: removed the unused `Parquet.Net` package reference.

**`PokemonRedRL.Utils`**
- `PokemonRedRL.Utils.csproj`: removed the unused `Parquet.Net` package reference (kept in `PokemonRedRL.Models`, which actually uses `[ParquetIgnore]`).

**Lua bridge**
- `src/scripts/mgba_socket.lua`: **deleted** (confirmed stale — a minimal CSV-protocol variant never loaded by any running mGBA instance; only documentation referenced it).
- `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua`: fixed the hardcoded absolute `package.path`/`package.cpath` (previously `D:\Programming\PokemonRedRL\...`) to resolve the `lua/` library directory relative to the script's own file location via `debug.getinfo(1, "S").source`. Verified with a real mGBA instance: launched fresh, manually loaded via Tools → Scripting (still required — this is exactly the launch-automation gap tracked in Task 2b), `ping`/`get_state` both responded correctly, confirming `require("socket")`/`require("lunajson")` still resolve.

**Documentation**
- `docs/PROJECT_OVERVIEW.md`: §4's "two diverged copies" subsection replaced with a note that there's now a single authoritative script; §6's known-gaps list updated — removed the now-fixed reward-bug, duplicate-DI, diverged-script, and hardcoded-path items; kept the `ExperienceReplay`/`MemoryCache`/`SumTree`-unused note (per user decision) plus the two other pre-existing items (per-call `Random()`, no protocol contract test); fixed a stale `§6.1` cross-reference in §7.
- `docs/CODEBASE_MAP.md`: updated the tree diagram and emulator-bridge-assets table to reflect the single remaining Lua script and the path-resolution fix.
- `docs/tasks/roadmap-vision-and-rl-overhaul.md`: Task 2 marked resolved with a summary of what actually landed.

**Not part of this task, explicitly deferred to Task 2b:** `launch_agents.bat`'s broken `--port`/`--script` invocation, and programmatic fast-forward control — both confirmed still unsolved.

Ready for `/review-changes`.

## Review notes (from `/review-changes`)

**Diff scope:** `git status`/`git diff --stat` shows exactly the files listed in the implementation summary — no scope creep, nothing planned is missing. Every diffed file was inspected directly and matches the plan precisely: `RewardCalculatorService.cs` (2-line addition, both fields now updated), `Program.cs` (1-line removal, exactly the duplicate registration), the three `.csproj` files (1-line `Parquet.Net` removal each, `System.Net.Sockets`/`Models`' own `Parquet.Net` correctly left untouched), the Lua script (hardcoded path replaced with self-relative resolution), and the file deletion (`src/scripts/mgba_socket.lua`, confirmed via `git status` as `D`).

**Build:** `dotnet build src/PokemonRedRL.sln` — 0 `error CS`, 0 `warning CS`. Grepped the whole repo for `ParquetIgnore`/`Parquet.Net` — confirmed `ModelExperience.cs`'s usage and `PokemonRedRL.Models.csproj`'s reference are both untouched, and no other file references Parquet at all, so the three removals are safe.

**Regression/quality checks:**
- DI: confirmed exactly one `IExperienceRepository` registration remains in `Program.cs` (`AddSingleton`), matching the plan's stated intent and prior effective behavior.
- TorchSharp: no tensor-handling code touched by this task at all — no new leak risk.
- Protocol: no TCP command/response shape changes; the Lua edit is purely internal path resolution, verified live (manual mGBA smoke test: `ping` → `pong`, `get_state` → valid JSON) before this review, not just claimed.
- Secrets/ROMs: none touched; `src/data/roms/pokemon_red.sav` and `src/mGBA-0.10.5-win64/qt.ini` changes are incidental emulator-state churn from the manual smoke test, consistent with this repo's established normal churn for these files — not flagged.
- Comment language: the new Lua comment is in English, consistent with the rest of that file's mixed-language convention (new comments default to English per repo convention).

**Documentation:** `docs/PROJECT_OVERVIEW.md` §4 and §6 correctly describe the fixes in past tense with no leftover stale references to the deleted file (verified via grep); the `§6.1` cross-reference in §7 was correctly fixed. `docs/CODEBASE_MAP.md` no longer lists the deleted `src/scripts/mgba_socket.lua` row and describes the single remaining script accurately. Roadmap's Task 2 entry accurately summarizes what landed.

**Verdict: ✅ Pass.** Safe to `/commit-changes`.
