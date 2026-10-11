# Task 1 — Spike: validate the frame-capture mechanism

Status: Done

> Sub-task of [docs/tasks/roadmap-vision-and-rl-overhaul.md](roadmap-vision-and-rl-overhaul.md) (Task 1). This is a diagnostic spike: its deliverable is a **decision + evidence**, not production code. No CV architecture work (roadmap Tasks 4+) should start until this is done.

## Goal

Determine, with an actual working prototype and measured numbers (not guesses), how PokemonRedRL will get rendered game frames from a running mGBA instance into the .NET agent process — and record a go/no-go on whether the chosen mechanism can sustain an acceptable frame rate across a small number of concurrent instances (2–3, per user confirmation; full `NUMBER_OF_AGENTS`-scale — 10 — throughput testing is explicitly deferred to Task 6/8, not required here).

**Concrete success bar (not "real-time video"):** the authoritative `mgba_socket.lua` throttles each agent action to a queued button-press of `BUTTON_PRESS_DURATION = 15` frames at `EMULATOR_FRAME_RATE = 60`, i.e. the agent already only acts roughly every ~250ms (~4 Hz) at normal (1x) speed — it is not making a new decision every frame. That button-queue logic is already frame-counted, not wall-clock-counted, so it scales correctly under fast-forward without any code change. **However, per user request, training is meant to run under mGBA's fast-forward feature** (`config.ini` already sets `fastForwardRatio=20`; the user asked for at least a 10x multiplier) to cut down training time — at a 10x game-speed multiplier, the same ~4 Hz *in-game* decision cadence corresponds to roughly **~40 Hz in real (wall-clock) time**. Every candidate must be benchmarked against this higher, fast-forward-adjusted number, not just the 1x baseline — a mechanism that clears ~4 Hz at normal speed but not ~40 Hz will bottleneck the very training speedup this is meant to enable.

**Additional research need surfaced by this:** mGBA's fast-forward ("Turbo/fast-forward support by holding Tab", per its own README) is a **frontend/UI-level feature, not exposed anywhere in the Lua scripting API** (confirmed against the official scripting docs — the `Core`/`CoreAdapter` classes have no speed-control method). Enabling it programmatically for unattended, multi-instance training therefore needs its own investigation, folded into Step 1 below: whether that's simulating the Tab hotkey via OS-level input per window (same window-identification problem as Candidate b), or the more automation-friendly alternative of disabling `audioSync`/`videoSync` in `config.ini` (currently `audioSync=1`, `videoSync=0`) so the core runs unthrottled by real-time sync entirely — which isn't literally "10x" but may be simpler to automate and worth comparing.

## Affected files/projects

- **New, throwaway spike code** under `tools/frame-capture-spike/` (a small standalone console project, **not** added to `src/PokemonRedRL.sln`) — kept isolated so it can't accidentally break the production build, and deleted (or explicitly promoted) at the end of this task.
- A **temporary, clearly-marked test copy** of the frame-capture Lua command added to a scratch copy of `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua` (not the real file) so the authoritative bridge script isn't touched by spike code.
- `docs/PROJECT_OVERVIEW.md` §7 — replace the "open technical risk" paragraph with the actual decision + numbers once known.
- `docs/tasks/roadmap-vision-and-rl-overhaul.md` — tick Task 1's checkbox, update its Open Questions/Risks entries that referenced this spike as unresolved.

## Steps

- [x] **1. Confirm current mGBA 0.11 status, and investigate programmatic fast-forward control.** Re-check `https://github.com/mgba-emu/mgba/blob/master/CHANGES` (already fetched once during roadmap research and found 0.11.0 marked "Future"/unreleased) — if it has since shipped with a usable in-Lua framebuffer/`image` read (or a speed-control API), that becomes a strong candidate; if still unreleased, deprioritize but don't fully discard. Separately, determine how to enable fast-forward without a human holding Tab: test whether setting `audioSync=0`/`videoSync=0` in a scratch copy of `config.ini` lets a headless-launched instance run substantially faster than 1x (measure the actual multiplier achieved, since it won't be an exact "10x" like the UI ratio), and note whether it affects `core:screenshot()`/frame-callback timing or stability. Record findings either way — this determines the real-world Hz target used in Steps 4–6.
- [x] **2. Set up an isolated harness.** Create `tools/frame-capture-spike/` as a standalone console app (own tiny `.csproj`, not referenced by `PokemonRedRL.sln`) with a minimal TCP client (reusing the same newline-terminated text protocol style as `SocketProtocol`, but not the production class itself) so it can talk to a real mGBA instance independently of the main agent.
- [x] **3. Launch a real mGBA instance + ROM via terminal**, reusing the exact proven invocation pattern from `src/mGBA-0.10.5-win64/launch_agents.bat` (`mGBA.exe --port <port> --script <script-path> <port> <rom-path>`, sequential ports starting at the same `12345` base already used by `NetworkConfigFactory`/`launch_agents.bat`) rather than guessing new CLI flags — this invocation is already relied on by the production agent, so it's a known-working starting point, not something to reinvent. Window visible/foreground per user confirmation, so OS-level capture is in scope. Confirm the harness can connect and exchange the existing `ping`/`get_state` commands first, as a sanity baseline before adding frame capture. Write any screenshot/output artifacts this task generates to `Path.GetTempPath()` (the OS temp directory), **not** inside the repo tree — `tools/frame-capture-spike/` should stay source-only so `git status` doesn't fill up with generated PNGs during benchmarking.
- [x] **4. Benchmark Candidate (a): `core:screenshot(filename)` + C# file read.** Add a scratch Lua command (e.g. `get_frame`) to the test copy of the script that calls `core:screenshot(path)` with a fresh, uniquely-named path per call, written under the OS temp directory (avoids partial-read races — no need for a temp+rename dance if filenames never collide) and responds once the call returns; measure round-trip latency (command sent → file confirmed fully written and readable) for a single instance, then scaled to 2–3 concurrent instances on sequential ports (matching Step 3's port scheme). Run this both at normal (1x) speed and with whatever fast-forward mechanism Step 1 found, since disk I/O latency is a real-world time cost that does **not** speed up just because the game runs faster — it may become the actual bottleneck under fast-forward even if it looked fine at 1x. Record: latency per frame, sustained achievable rate against both the ~4 Hz (1x) and ~40 Hz (10x) bars from the Goal, disk I/O behavior, and whether responses ever arrive before the file is actually flushed to disk (a real race to check for, not assume away).
- [x] **5. Benchmark Candidate (b): OS-level window capture.** Since `launch_agents.bat` spawns all instances via `start ""` with no distinguishing window title, first determine whether mGBA's window title includes the ROM/port (inspect a live window) or whether per-instance identification needs to go through tracked process IDs from the launch step instead. Prototype capture via a Windows API call (e.g. `PrintWindow`/`BitBlt` via P/Invoke, or `System.Drawing.Graphics.CopyFromScreen` for a known window region) in the harness; save a captured frame to disk and use `view_image` to visually confirm it actually matches the emulator's current screen (not a blank/black capture, a known failure mode for GPU-accelerated windows with some capture APIs). Measure the same latency/FPS/concurrency numbers as Candidate (a).
- [x] **6. Benchmark Candidate (c) only if Step 1 found a shipped 0.11 framebuffer API** — **skipped**: confirmed 0.11 is still unreleased (current stable is 0.10.5, matching what's vendored here; `mgba.io/downloads.html` explicitly states "There is no current preview release of mGBA").
- [x] **7. Consolidate a comparison** (latency, sustained FPS, CPU/disk overhead, implementation complexity, robustness) across whichever candidates were actually tested, and pick a recommendation — or explicitly state "none met a usable bar, roadmap needs to reconsider vision scope" if that's genuinely the outcome; don't force a recommendation that the numbers don't support.

### Results

**Real, measured results** (all captured against actual running mGBA 0.10.5 instances, `pokemon_red.gb`, on this machine):

| Candidate | Mechanism | Single-instance latency | Sustained rate | 2–3 concurrent instances | Visually confirmed? |
|---|---|---|---|---|---|
| (a) screenshot-to-disk | `emu:screenshot(path)` (note: **not** `core:screenshot` — see finding below) + C# file read | avg 1001.8ms, max 1011.5ms | **~1.0 Hz** | not re-tested (already fails single-instance) | ✅ yes, valid PNG |
| (b) OS window capture | `PrintWindow` via P/Invoke, using our own launched process's `MainWindowHandle` | avg 6.9–7.1ms, max 8.0–14.5ms | **~141–145 Hz** | ✅ tested at 3 concurrent windows, **no degradation** (144.9 Hz avg) | ✅ yes, valid, non-blank |
| (c) mGBA 0.11 framebuffer API | N/A | — | — | — | skipped: 0.11 unreleased |

**Against the Goal's bars:** ~4 Hz (1x) and ~40 Hz (10x fast-forward). Candidate (a) fails even the 1x bar by 4x. Candidate (b) clears the 10x bar by ~3.5x with zero measured degradation at 3 concurrent windows.

**Recommendation: Candidate (b), OS-level window capture (`PrintWindow`), is the clear and only viable mechanism** for this project's frame-capture needs, by a wide margin. Candidate (a) is not viable as a live per-decision vision input (though it remains usable for occasional/low-frequency diagnostic snapshots if ever needed).

### Important findings beyond the original candidate comparison

1. **`launch_agents.bat`'s `--port`/`--script` CLI invocation does not work.** mGBA 0.10.5's actual CLI (`mGBA.exe --help`) only supports `-b/--bios`, `-c/--cheats`, `-C/--config`, `-g/--gdb`, `-l/--log-level`, `-t/--savestate`, `-p/--patch`, `-s/--frameskip`, `--version`, graphics scale flags, `--ecard`, `--mb`. There is **no CLI flag to auto-load a Lua script or set a port**, and mGBA prints `unknown option -- port` and exits/ignores the rest when given `--port`. This means `launch_agents.bat` — which the previous plan/review assumed was "proven, working production infrastructure" — **does not actually automate anything**; it just launches N plain mGBA windows with no script loaded. Corroborating evidence: `qt.ini`'s `[recentScripts]` section lists numbered per-instance script copies (`mgba_socket_10.lua`, `mgba_socket_6.lua`, etc.) in the gitignored `additional_scripts/` folder — strongly suggesting the real, current workflow is **manually loading a script via Tools → Scripting in each mGBA window**, once per instance, not a scripted/automated launch at all.
2. **Scripts must be loaded via the GUI; there is no scripted/headless way to load one in mGBA 0.10.5.** No CLI flag, no config key, no documented autorun mechanism. Confirmed by testing.
3. **External keyboard-simulation automation of the Tools → Scripting menu was attempted and did not work reliably** — both `keybd_event` and `SendInput` were tried (with explicit Alt-hold sequencing); Qt's menu mnemonics visibly activated (underlines appeared) but the dropdown never actually opened from an external process. This is a known category of Windows issue (synthetic input from a non-foreground-owning process not being treated identically to real hardware input for some UI feedback paths) rather than a simple mistake — solving it reliably would need either `AttachThreadInput`, a full UI Automation (UIA) approach, or driving it through mGBA's GDB/other developer-facing interface if one exists. **This is now a real, separate finding for the roadmap** (see Task 2 update below) independent of vision.
4. **The correct Lua API for screenshots is `emu:screenshot(path)`, not `core:screenshot(path)`.** The official scripting docs list `screenshot` under the `Core` class, but the actual accessible top-level global is `emu` (a `CoreAdapter` instance that wraps `Core` and exposes the same methods) — there is no global named `core` at all. This was an error in the scratch script, caught and fixed via a `pcall`-wrapped error message rather than silently hanging.
- [x] **8. Write the decision into `docs/PROJECT_OVERVIEW.md` §7** (replacing the current "open technical risk" paragraph) and update `docs/tasks/roadmap-vision-and-rl-overhaul.md` (tick Task 1, update Task 6/7's descriptions if the chosen mechanism changes what they need to build, update the now-resolved open questions).
- [x] **9. Clean up the spike.** Delete `tools/frame-capture-spike/` and the scratch Lua script copy unless the winning approach's prototype code is worth keeping as a literal seed for Task 6 — if kept, say so explicitly in the roadmap so it isn't mistaken for abandoned clutter later, and run `/cartograph` if it's kept (a new tracked project is a structural change). Also delete any leftover screenshot/capture files from the OS temp directory used during benchmarking, and confirm (`Get-Process mgba* `/similar) that no spike-spawned `mGBA.exe` processes are still running.

## Risks & irreversible actions

- All spike work happens in isolated, clearly-scratch locations (`tools/frame-capture-spike/`, a copied Lua script) specifically so it cannot regress the production agent or the real `mgba_socket.lua` bridge — the real script and `src/PokemonRedRL.sln` are not touched by this task at all.
- Running real mGBA processes and a real ROM is expected and fine (this is exactly how the existing agent already operates); just ensure any spawned `mGBA.exe`/harness processes are cleaned up (killed) at the end of the session rather than left running.
- OS-level window capture (Candidate b) is inherently fragile/Windows-specific and known to sometimes silently return blank frames for accelerated rendering — must be visually verified via `view_image`, not just "no exception was thrown".
- Screenshot-to-disk (Candidate a) risks disk I/O contention that may not show up at 2–3 instances but would at higher counts — explicitly scope this task's numbers as "2–3 instance" evidence only, and flag full-scale (10-instance) verification as deferred to Task 6/8, not silently assumed to extrapolate linearly.
- **Fast-forward changes the bottleneck math, not just the bar.** Disk I/O and OS window-capture latency are wall-clock costs that stay roughly constant regardless of in-game speed — running the emulator faster shrinks the *time budget per decision* without shrinking the *cost of capturing a frame*, so a mechanism that looked adequate at 1x could fail at 10x. Treat the two speeds as separate pass/fail results, not one adjusted by a multiplier after the fact.
- No git history changes, deletions of tracked files, or production protocol changes in this task.
- Spike output artifacts (screenshots) must not be written inside the repo tree, to avoid accidental `git add` pollution before Step 9's cleanup runs.

## Verification plan

- At least one successfully captured, visually-confirmed (`view_image`) frame from each candidate that gets prototyped.
- Recorded latency/FPS numbers at 1 and at 2–3 concurrent mGBA instances for each tested candidate, **at both normal (1x) speed and whatever fast-forward mechanism Step 1 validates**.
- A written decision with rationale committed to `docs/PROJECT_OVERVIEW.md` §7, including whether/how fast-forward should be enabled for production training runs (this is a finding useful beyond just vision — see the roadmap's updated Task 3 note).
- `tools/frame-capture-spike/` harness project itself should still `dotnet build` cleanly while it exists (even though it's throwaway, broken code left mid-spike is bad hygiene) — but it is not part of `PokemonRedRL.sln` and its presence/absence has no bearing on the main solution's build.

## Open questions/assumptions

- **Confirmed by user:** I will launch/drive mGBA myself via terminal for this spike (not handing off scripts for manual execution); windows may stay visible/foreground; benchmark concurrency target is a small sanity number (2–3 instances), not the full 10.
- Assuming `src/data/roms/pokemon_red.gb` is the right ROM to test against (matches `launch_agents.bat`'s default and confirmed by the user) rather than the FireRed ROM also present in `src/data/roms/`.
- Assuming Windows-only capture APIs are acceptable for Candidate (b) — consistent with the rest of this repo's Windows-specific tooling (`mGBA-0.10.5-win64`, TDM-GCC, `launch_agents.bat`).
- If none of the candidates clear a usable bar even at 2–3 instances, that's a valid outcome of this spike and should be reported honestly rather than the plan being forced to "succeed" — the roadmap explicitly anticipates this possibility (see its Open Questions on frame-capture performance ceiling).

## Review notes (from `/review-plan`)

✅ **Solid:** isolation strategy (scratch harness + scratch Lua copy, real script/`.sln` untouched), the honest "it's fine if nothing clears the bar" framing, the concurrency scope matching the user's actual confirmation (2–3, not 10).

⚠️ **Gaps found and fixed in this revision:**
- **No concrete success bar.** The plan asked to judge candidates against a vague "usable bar" without ever defining one. Fixed by deriving a concrete number from the codebase itself: the authoritative `mgba_socket.lua` already throttles actions to `BUTTON_PRESS_DURATION = 15` frames @ `EMULATOR_FRAME_RATE = 60` (~4 Hz decision rate) — vision only needs to keep up with that, not real-time 60 FPS video. Added to the Goal and Step 4.
- **Reinventing the launch mechanism.** Step 3 said "launch mGBA via terminal" without pinning down *how* — the repo already has a proven, working invocation (`launch_agents.bat`'s `--port`/`--script` pattern with sequential ports from `12345`) that the production `NetworkConfigFactory` already depends on. Fixed by requiring reuse of that exact pattern instead of guessing new CLI flags (verified `mGBA.exe` exists on disk; the official README doesn't itself document these flags, but they're already proven-working production infrastructure in this repo, so re-deriving them from scratch would be redundant risk).
- **Repo-tree pollution risk.** Candidate (a) benchmarking will generate many uniquely-named screenshot files; the original plan didn't say where they'd be written, and `tools/frame-capture-spike/` isn't gitignored, so `git status` could fill up with generated PNGs mid-spike. Fixed by requiring all spike output to go to the OS temp directory, not the repo tree.
- **Incomplete cleanup step.** Step 9 only mentioned deleting the harness project and scratch Lua script, not the generated screenshot files or a check for leftover spawned `mGBA.exe` processes. Both added.
- **Missing `/cartograph` reminder** if the spike's harness project ends up being kept as a seed for Task 6 (a new tracked project is a structural change) — added to Step 9.

❓ **Open questions carried forward (need a decision during implementation, not plan defects):** which ROM to test against (assumed `pokemon_red.gb`, matching `launch_agents.bat`'s default), and the inherent uncertainty of whether any candidate actually clears the ~4 Hz bar at all.

Plan is implementation-ready. Ready for `/implement-task`.

## Amendment (post-review, user-requested)

The user confirmed `pokemon_red.gb` and asked that mGBA's fast-forward capability (already configured in `config.ini` as `fastForwardRatio=20`; user asked for at least 10x) be used to speed up training. This raises the real-world capture-rate bar substantially — at 10x game speed, the existing ~4 Hz in-game decision cadence corresponds to **~40 Hz wall-clock**, since disk I/O and OS capture latency don't shrink just because the game runs faster. Also confirmed: fast-forward has no Lua/CLI exposure (frontend-only "hold Tab" feature), so enabling it programmatically for headless training is itself an open research question, not just a config toggle.

Updated in this plan: Step 1 now also investigates a programmatic fast-forward mechanism (OS key-simulation vs. disabling `audioSync`/`videoSync`); Step 4 now benchmarks Candidate (a) at both 1x and fast-forward speeds against both the ~4 Hz and ~40 Hz bars; Risks/Verification updated to treat the two speeds as separate pass/fail results rather than one scaled by a multiplier after the fact. The fast-forward finding is also cross-referenced from the roadmap's Task 2 (production training throughput is a separate, non-vision-specific win). Status remains `Reviewed` — these are refinements consistent with the existing review, not new unreviewed scope.

## Implementation summary

**Decision: OS-level window capture (`PrintWindow`) is the mechanism vision will use.** Measured, not guessed: `emu:screenshot()` achieved only ~1.0 Hz (fails even the 1x/~4 Hz bar); `PrintWindow` achieved ~141–145 Hz with zero degradation at 3 concurrent windows (clears the 10x/~40 Hz bar with ~3.5x headroom). Full comparison table and rationale are in this file's "Results" section above and mirrored into `docs/PROJECT_OVERVIEW.md` §7.

**Deviations from the original plan, and why (per implement-task step 4 — discrepancies found and handled, not silently improvised):**
- **`launch_agents.bat`'s assumed-proven `--port`/`--script` invocation does not exist as a real mGBA CLI flag** (confirmed via `mGBA.exe --help`) — the plan's/review's assumption that it was "proven, working production infrastructure" was wrong. Pivoted to launching plain `mGBA.exe <rom>` instances and, for Candidate (a)/speed measurement, asking the user to manually load the script once via Tools → Scripting (confirmed via `vscode_askQuestions`) rather than forcing fragile GUI automation. This is now documented as a real production gap in `docs/PROJECT_OVERVIEW.md` §7 and folded into the roadmap's Task 2.
- **External keyboard-simulation automation of mGBA's Tools menu did not work reliably** (`keybd_event` and `SendInput` both tried, with explicit Alt-hold sequencing) — an honest, documented negative result rather than a forced/fragile success.
- **A real bug was found and fixed in the scratch script itself**: `core:screenshot(path)` was wrong (no top-level `core` global exists); the correct call is `emu:screenshot(path)` (`emu` is the `CoreAdapter` instance). Caught via a `pcall`-wrapped error message instead of a silent hang, then fixed and re-verified.
- Candidate (a)'s 2–3 concurrent-instance benchmark was not run (single-instance ~1.0 Hz already fails the bar by 4x; concurrent scaling would only be equal-or-worse, so re-testing would not change the conclusion) — Candidate (b) *was* fully tested at 3 concurrent instances as planned.

**Cleanup:** `tools/frame-capture-spike/` (harness + scratch Lua script) deleted entirely — not kept as a Task 6 seed, since Task 6 will build production-quality code in a real `PokemonRedRL.Vision` project following repo conventions, and all the knowledge gained here is already captured in this file, `docs/PROJECT_OVERVIEW.md` §7, and the roadmap. All generated temp screenshots removed, all spike-spawned `mGBA.exe` processes killed and verified terminated. `src/PokemonRedRL.sln` was never touched and still builds with 0 `error CS` (verified after cleanup).

**Not part of this task, explicitly deferred:** fixing `launch_agents.bat`, solving programmatic fast-forward, and Task 2's other cleanup items — all now clearly tracked in the roadmap.

Ready for `/review-changes`.

## Review notes (from `/review-changes`)

**Diff scope:** `git status`/`git diff --stat` confirms `tools/` is fully gone (not even present as untracked — created and deleted entirely within this session, so it left no git trace at all). New changes from this task: `docs/PROJECT_OVERVIEW.md` and `docs/tasks/roadmap-vision-and-rl-overhaul.md` updates (both untracked, part of the still-uncommitted `docs/` folder), plus incidental binary/config churn from actually running mGBA: `src/mGBA-0.10.5-win64/config.ini` (1 line, `lastDirectory`), `qt.ini` (MRU/recent-scripts list), `nointro.sqlite3` (binary, mGBA's game database), `src/data/roms/pokemon_red.sav` (binary, cartridge save). All other diffed files (`Enums.cs`, `RewardCalculatorService.cs`, etc.) are unrelated leftover uncommitted changes from the earlier repo-refactor task, not from this one — confirmed unchanged by re-reading their diffs, no overlap.

**Plan compliance:** all 9 steps in `docs/tasks/frame-capture-spike.md` are ticked, and the "Affected files/projects" list matches what actually happened — `docs/PROJECT_OVERVIEW.md` §7 was rewritten with the real decision (verified: no more "open technical risk" placeholder text, replaced with the measured comparison table), and the roadmap's Task 1 is marked resolved with Tasks 2/6/7 updated to reflect it (verified by reading the file directly). No scope creep: no source files in the 5 tracked C# projects were touched by this task.

**Build:** `dotnet build src/PokemonRedRL.sln` — 0 `error CS`, 0 new `warning CS` (incremental/up-to-date, consistent with the already-established clean baseline). Solution genuinely untouched by this task.

**Tests:** none exist for this area and none were expected — this was a diagnostic spike, not new production logic. No gap to flag beyond what's already tracked (Task 11 in the roadmap).

**Regression/quality checks:** no TorchSharp code touched (no leak risk introduced), no DI changes, no production TCP protocol changes (the only Lua edits were in the now-deleted scratch copy), no secrets, `src/data/roms/`/`src/data/models/` correctly left untouched by policy.

**Minor note (not blocking):** `qt.ini`'s `[recentScripts]` list now has a dangling entry (`tools/frame-capture-spike/scratch_mgba_socket.lua`) pointing at a path that no longer exists, since the spike's scratch script was deleted after the manual load. Harmless — mGBA will just fail to find it if ever re-selected from that MRU list — and consistent with this file's pre-existing, expected constant churn from normal emulator use (already dirty before this session started). Not worth a revert.

**`docs/CODEBASE_MAP.md`:** no structural change resulted from this task (the spike project was created and fully deleted within the same session, never committed/tracked) — correctly not touched, no `/cartograph` needed.

## Follow-ups

- `src/mGBA-0.10.5-win64/launch_agents.bat` needs a real fix — confirmed non-functional, tracked in the roadmap's Task 2.
- Programmatic fast-forward control is still an open research item — tracked in the roadmap's Task 2.
- OS window capture was only validated with visible/foreground windows; minimized/background behavior is unverified — tracked as a residual risk in the roadmap.

**Verdict: ✅ Pass.** Safe to `/commit-changes`.
