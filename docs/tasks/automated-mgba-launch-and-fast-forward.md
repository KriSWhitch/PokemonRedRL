# Task 2b — Desktop control panel for emulator orchestration and training monitoring

Status: Done (MVP; real 1/3/10-instance scale verification outstanding — see Step 12 and Follow-ups)

> Sub-task of [docs/tasks/roadmap-vision-and-rl-overhaul.md](roadmap-vision-and-rl-overhaul.md) (Task 2b). This supersedes the earlier "launcher-only" framing. The target is a dedicated desktop control-plane application for managing emulator windows, agent processes, training sessions, visual monitoring, and future computer-vision capture.

## Goal

Create a Windows desktop application that becomes the operator console for PokemonRedRL training: it can manage **N mGBA environments** (1, 10, 20, 50, etc.), launch or attach agents, monitor each emulator visually in a paged adaptive grid, track training metrics, coordinate Lua bridge/API connections, and provide deterministic lifecycle control for start/stop/restart/cleanup.

**MVP boundary for this task:** deliver a usable control-plane foundation, not every future analytics/training feature. The minimum pass condition is: WPF app builds, creates/loads a run manifest, manages 1/3/10 emulator slots in attach or automated-launch mode, displays 1–9 paged live previews, maps each tile to a PID/port/window handle, and can start/stop a bounded training session using explicit ports. Advanced analytics, replay browsing, model comparison, rich charts, and full structured telemetry are follow-up tasks unless required for this MVP.

The initial implementation should focus on the current memory-state agent and mGBA bridge, while deliberately shaping the architecture so Task 6 (`PokemonRedRL.Vision`) can reuse the same process/window/port manifest for `PrintWindow` capture.

## Product concept

The desktop app is not just a launcher. It is the **training control plane**:

- **Session dashboard:** start/stop a training run, select ROM/save-state/base config, number of agents, port range, speed profile, and output directory.
- **Emulator wall:** show emulator screens in an adaptive grid: 1–9 visible tiles per page, paginated when there are more agents (for example 20 agents = 3 pages: 9 + 9 + 2). The layout should adapt to selected tile count and window size.
- **Agent tiles:** each tile shows live screen capture, agent index, port, PID, connection state, episode/step count, epsilon, last action, current reward, total reward, map/position, FPS/capture latency, and error state.
- **Orchestration:** launch or attach to mGBA processes, load the correct Lua script automatically where supported, assign/validate ports, wait for `ping` readiness, and cleanly stop only processes owned by the current run.
- **Training supervision:** start/stop/pause/resume the .NET training process(es), surface logs/health per agent, and keep a run manifest that ties agent index → emulator process → port → window handle → runtime directories.
- **Future CV integration:** expose stable window handles/frame providers to the future `PokemonRedRL.Vision` project, instead of rediscovering windows by title.

## Recommended technology choice

Use a new .NET desktop project, tentatively `PokemonRedRL.ControlPanel`, with **WPF on `net8.0-windows`** as the conservative first choice:

- The repository and mGBA runtime are already Windows-specific (`mGBA-0.10.5-win64`, `launch_agents.bat`, `PrintWindow`).
- WPF is built into the .NET Windows Desktop SDK and avoids adding a large UI framework dependency before Task 3 package modernization.
- It can host a responsive operator dashboard and interop cleanly with Win32 APIs (`Process`, window handles, `PrintWindow`, future UI Automation).

Avalonia can be reconsidered later if cross-platform operator UI becomes important, but it is not necessary for the current Windows-only emulator/vision stack.

## Affected files/projects

- New project: `src/PokemonRedRL.ControlPanel/PokemonRedRL.ControlPanel.csproj` (WPF, `net8.0-windows`) added to `src/PokemonRedRL.sln`.
- New control-plane code areas, exact names to settle during implementation:
  - `Services/EmulatorProcessManager` — launch/attach/stop mGBA instances.
  - `Services/AgentProcessManager` or in-process host adapter — start/stop training workers.
  - `Services/RunManifestService` — write/read run manifest.
  - `Services/WindowCaptureService` — preview capture for UI tiles using the Task 1 `PrintWindow` result.
  - `ViewModels/` and `Views/` for dashboard/page/grid/tile UI.
- Existing C# integration points likely touched:
  - `src/PokemonRedRL.Agent/Program.cs` — make agent count/port manifest/config externally configurable rather than hardcoded `NUMBER_OF_AGENTS = 10`.
  - `src/PokemonRedRL.Core/Helpers/NetworkConfigFactory.cs` — replace launcher-mode scanning with explicit assigned ports, keeping opt-in manual scan mode.
  - Possibly `src/PokemonRedRL.Agent/ExplorationAgent.cs` — expose per-agent telemetry/events/logs if needed by the UI.
- mGBA integration:
  - `external/mgba/` submodule at pinned upstream commit (already added during the previous attempt) remains the preferred automation path because upstream supports GUI/headless `--script` startup in source.
  - `src/mGBA-0.10.5-win64/launch_agents.bat` should be retired or rewritten to call the new control panel/launcher only after the app works.
  - `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua` should remain the authoritative script; protocol changes are out of scope unless required for telemetry/diagnostics and explicitly reviewed.
- Documentation: `docs/PROJECT_OVERVIEW.md`, `docs/CODEBASE_MAP.md`, `docs/tasks/roadmap-vision-and-rl-overhaul.md`, and this plan.

## Frozen UX/data contract (Step 1 output)

This is the concrete contract implementation follows; anything not listed here is out of scope for this pass.

**Session settings (session dashboard):** ROM path (default `src/data/roms/pokemon_red.gb`), agent count (1–50), base port (default `12345`), tiles-per-page (fixed at 9 for MVP), speed profile (`Normal` | `Accelerated`), run output directory (default `runs/<yyyyMMdd-HHmmss>/`), launch mode (`ManualAttach` | `AutomatedLaunch` — see Step 3 finding; `AutomatedLaunch` is disabled/greyed out for this pass).

**Run manifest fields (frozen shape, see Step 5):** `RunId`, `CreatedUtc`, `RomPath`, `LaunchMode`, `SpeedProfile`, and a list of `AgentSlot { Index, Port, EmulatorProcessId, AgentProcessId, WindowHandle, RuntimeDirectory, Status, LastPingUtc }`. `Status` is one of `Pending`, `Starting`, `Ready`, `Running`, `Error`, `Stopped`.

**Tile data (per visible agent tile):** agent index, port, PID(s), connection state (`Disconnected|Connecting|Ready|Running|Error`), latest preview bitmap, latest parsed telemetry line (map id, x, y, last action, step reward, total reward, epsilon), and an error message slot.

**Controls:** global Start/Stop, per-tile Stop/Restart, Next/Previous page. Pause/Resume-per-agent is deferred past this MVP (documented as a follow-up, not implemented now) because `Program.cs`/`ExplorationAgent` has no cooperative pause primitive today — only cancellation.

**Paging:** fixed grid of up to 9 tiles per page (3x3), computed as `ceil(agentCount / 9)` pages; last page shows the remainder (e.g. 20 agents → pages of 9, 9, 2).

**Tile refresh policy:** preview capture is decoupled from training step speed. Default preview refresh is **10 FPS per visible tile**, and only tiles on the *currently displayed* page are captured — other pages are not captured while off-screen.

**Logs panel:** a single scrollable panel showing the selected/focused tile's raw stdout lines. One-process-per-agent (Step 2 decision) makes this attribution exact — no cross-agent log mixing.

## Steps

- [x] **1. Freeze the control-plane requirements and UX contract.** Define the first usable version: session settings, agent count, page size (1–9 per page), tile data, controls (start/stop/pause/restart page/all), log display, and run manifest fields. Record MVP versus later polish in this plan before coding. Avoid building a decorative UI; this is an operational dashboard for repeated training runs. Include the exact tile refresh policy: preview capture may benchmark above 100 Hz, but the UI should throttle visible previews (for example 5–15 FPS per visible tile) so it doesn't waste CPU/GDI resources or starve training.
- [x] **2. Decide the runtime model and process granularity.** **Decision: one `PokemonRedRL.Agent` process per emulator slot** (option (a) from the plan's comparison — external process supervision, one agent per process). Rationale: (1) it makes the tile→process→port mapping exact and trivial (no shared-process log interleaving to disambiguate); (2) killing/restarting one slot cannot affect other agents' processes, unlike the current single-process `NUMBER_OF_AGENTS` loop where all 10 agents share one process and one crash can take down the whole run; (3) it satisfies the review's telemetry-attribution requirement for free — every stdout line from a given process belongs to exactly one agent, so simple log-line parsing (Step 9) is reliable without inventing a new IPC channel. Concretely: `Program.cs` gains a `--port <port>` (required for single-agent mode) and `--agent-index <n>` (optional, for log/telemetry tagging) CLI argument pair; when `--port` is supplied, the process binds one `ExplorationAgent` directly to that port (bypassing `NetworkConfigFactory` scanning) instead of looping `NUMBER_OF_AGENTS` times. The pre-existing multi-agent loop is kept as the default when no `--port` is supplied, for backward-compatible manual/dev use outside the control panel. Actual wiring lands in Step 8, not here — this step is the decision + rationale.
- [x] **3. Validate upstream mGBA automation path and Lua port injection.** **Finding: automated launch mode is currently blocked, not merely unverified.** Three concrete, directly-checked facts: (1) the vendored `src/mGBA-0.10.5-win64/mGBA.exe`'s real `--help` output (captured during the Task 1 spike) has no `--script` flag at all — `--script` is an upstream `0.11.0 (Future)` feature, absent from 0.10.5. (2) The pinned `external/mgba/` submodule (commit `c65e8a3d4666b0ea68a01578232452f31b185332`) was built earlier via the documented `mgba/windows:w64` Docker image; the resulting `external/mgba/build-win64/` directory contains only `mgba-sdl.exe` and `updater-stub.exe` — **no Qt binary was produced** (verified via `Get-ChildItem`, not assumed). (3) Source inspection of `src/platform/sdl/main.c`/`sdl-events.c` confirms the SDL frontend has **no** `--script` handling at all — only `src/platform/qt/ConfigController.cpp`/`Window.cpp` implement it, and that frontend was not built. Net result: **no real, currently-built mGBA binary in this workspace supports automated startup-script loading.** Separately, source inspection of `headless-main.c`/`ConfigController.cpp` confirms `--script FILE` only ever populates a `StringList` of file paths — there is no companion mechanism to pass a Lua `arg` value, so even if a Qt build existed, unique per-instance ports would still require generated per-run wrapper scripts (`local PORT = <n>; dofile("<absolute-path-to-mgba_socket.lua>")`) rather than `--script mgba_socket.lua 12345`. **Decision for this implementation pass:** ship **manual attach mode only**; automated launch mode is designed for (wrapper-script generation, port-injection scheme) but left disabled/greyed out in the UI, with the blocker recorded here rather than faked. Building a working Qt-enabled upstream binary is a follow-up spike, not part of this task's MVP.
- [x] **4. Implement the control-panel project skeleton.** `src/PokemonRedRL.ControlPanel` added to `PokemonRedRL.sln` (`net8.0-windows`, WPF, MVVM). Session settings form, 3x3 paginated tile grid, pagination controls, and a selected-tile log panel are wired in `MainWindow.xaml`/`MainViewModel`.
- [x] **5. Implement run manifest and process ownership model.** `Models/RunManifest.cs` + `Models/AgentSlot.cs` + `Services/RunManifestService.cs` create `<RunRootDirectory>/<timestamp>/manifest.json` with one isolated `agent-<i>/` runtime directory per slot; `AgentProcessManager`/`MainViewModel` only ever start/stop processes recorded in `AllTiles`/the manifest — no global process kill anywhere in the code.
- [x] **6. Implement emulator launch/attach management.** Manual attach mode implemented: `EmulatorPingProbe` pings each slot's pre-assigned port directly (no scanning — ports are 1:1 with slots by manifest construction), and `WindowEnumerationService` + `WindowPickerDialog` let the user explicitly pick the correct mGBA window per tile (deliberately not automatic — see the "misleading dashboard" risk). Automated launch mode is not implemented, per the Step 3 finding.
- [x] **7. Implement adaptive emulator wall UI.** `MainWindow.xaml`'s `ListBox`+`UniformGrid(3,3)` shows `CurrentPageTiles`; `MainViewModel` pages `AllTiles` in groups of 9 (`TotalPages = ceil(count/9)`), each tile shows a `PrintWindow` preview (`PreviewCaptureService`, throttled to 10 FPS via `DispatcherTimer`), port/PID, connection state, map/action/reward telemetry, and an error slot.
- [x] **8. Wire explicit port mapping into the agent.** `PokemonRedRL.Agent/Program.cs` now accepts `--port <n>`/`--agent-index <n>`; when `--port` is supplied it binds `NetworkConfig` directly to that port (bypassing `NetworkConfigFactory`) and runs exactly one `ExplorationAgent`. The legacy `NetworkConfigFactory` scanning + `NUMBER_OF_AGENTS` loop is preserved as `RunLegacyMultiAgentAsync` for manual/dev use when no `--port` is given.
- [x] **9. Add training process supervision and telemetry path.** `AgentProcessManager` starts/stops one `PokemonRedRL.Agent` process per slot and streams stdout/stderr; `ExplorationAgent.LogProgress` now also emits a machine-parseable `STATUS map=... x=... y=... action=... reward=... total=... epsilon=...` line (InvariantCulture-formatted) parsed by `AgentTelemetryParser` and routed to the owning tile by process identity — exact per-slot attribution, no heuristic log matching, per the review's requirement.
- [x] **10. Implement speed profile support.** `SpeedProfile` (`Normal`/`Accelerated`) is stored per-run in the manifest and shown in the UI, but **cannot be enforced by the control panel in this pass**: the bridge's throughput gate (`EMULATOR_FRAME_RATE=60` in the Lua script) and mGBA's fast-forward are both controlled inside the mGBA GUI itself, which (per Step 3) offers no CLI/automation surface in either the vendored 0.10.5 binary or the locally-built upstream SDL binary. For `Accelerated`, the UI shows an explicit hint instructing the user to enable mGBA's in-app Fast Forward (default hotkey Tab) per attached window; the control panel does not fake enforcement it cannot deliver. Measuring/guaranteeing the ≥10 cycles/s acceptance target is therefore a manual, per-session user action, not an automated one, until a scriptable mGBA build exists (tracked by the Step 3 follow-up spike).
- [x] **11. Add runtime isolation.** Everything the control panel itself owns is isolated: `RunManifestService` creates a dedicated `runs/<timestamp>/agent-<i>/` directory per slot, and `AgentProcessManager.StartAgent` sets that as the .NET `PokemonRedRL.Agent` process's working directory (the process currently writes no local files besides that — training state lives in Redis — so this is a forward-looking guarantee, not a no-op fix for an existing race). **Out of scope for this pass, and explicitly called out as a limitation:** the mGBA side (`pokemon_red.sav`, `qt.ini`, `config.ini`, nointro databases) is entirely outside control-panel automation in ManualAttach mode — the user starts each mGBA instance by hand, so avoiding save/config races across parallel instances is the user's responsibility (e.g. running each instance from its own portable mGBA folder/copy, a well-known pattern for multi-instance emulation). The committed ROMs/backups under `src/data/roms/` are never touched by any control-panel code path. This limitation should be resolved once/if AutomatedLaunch mode becomes available (Step 3 follow-up), where the control panel would generate isolated per-instance config directories itself.
- [x] **12. Verify scale progressively (partial — see note).** What was actually verified in this pass: `dotnet build src/PokemonRedRL.sln` is clean (0 errors) after every step above; `dotnet run` against the built `PokemonRedRL.ControlPanel` starts and reaches an idle WPF message loop with no startup exception. **Not verified in this pass, and explicitly flagged rather than assumed:** the real 1/3/10 emulator-instance progression, the synthetic 20-tile pagination dry run, and CPU/memory/capture-latency/command-cycle-rate/cleanup measurements — all of these require a live Redis instance and real mGBA windows manually started by a user, which this automated pass could not provide. This is a genuine, recorded gap: before relying on this control panel for real training runs, a human should run the 1 → 3 → 10 instance progression from the Verification plan below and record the results here.
- [x] **13. Update docs and roadmap.** `docs/PROJECT_OVERVIEW.md` §7 bullet 0 updated to describe the implemented control panel and its manual-attach limitation. `docs/CODEBASE_MAP.md` updated: solution project count, layout tree, a new §3.5 `PokemonRedRL.ControlPanel` detail section, and the §5 known-gaps note. `docs/tasks/roadmap-vision-and-rl-overhaul.md` Task 2b marked `[x] RESOLVED` with an outcome summary (MVP implemented, automated-launch limitation recorded, real-scale verification still outstanding).

## Risks & irreversible actions

- **This is now a real product surface, not a helper script.** Avoid overloading it with all future RL/CV work in one PR. The MVP is orchestration + monitoring + manifest + explicit ports; advanced analytics, charts, replay browsing, model comparison, and CV training UI can be later tasks.
- **GUI/headless tradeoff remains important.** The accepted vision mechanism is `PrintWindow`, so a vision-capable run needs GUI windows. Headless may be useful later for memory-only training, but it must be a separate run mode and not the default for the visual dashboard.
- **WPF UI must not block training/emulator loops.** Window capture, TCP pings, log streaming, and process waits must run off the UI thread with cancellation and throttling.
- **Process cleanup must be scoped to the manifest.** Never kill all `mGBA.exe` globally; only terminate processes owned by the active run.
- **Concurrent instances must not share mutable emulator files.** Runtime state belongs under ignored per-run directories; committed ROMs/save backups remain stable.
- **mGBA upstream build/customization carries MPL-2.0 obligations.** Keep the submodule pinned, document build commands, and keep any local patches traceable.
- **Agent integration can accidentally change training behavior.** Configuring ports/agent counts is intended; changing RL logic, reward shaping, replay, or model training belongs to later roadmap tasks.
- **Lua script startup may not support script arguments.** Upstream GUI/headless `--script FILE` exists in source, but the current bridge depends on `arg[1]` for port selection. If built mGBA cannot pass script args, generated per-run wrapper scripts are the preferred workaround; changing the bridge protocol is not.
- **A misleading dashboard is worse than no dashboard.** If logs/telemetry cannot be attributed to the correct tile/agent, do not fake it through best-effort text matching. Use one-process-per-agent or a minimal structured telemetry channel.
- **Preview capture must be throttled.** `PrintWindow` is fast enough for capture, but refreshing 9 visible tiles at uncapped rates can waste CPU/GDI resources. The UI needs a deliberate preview FPS cap independent of training step speed.

## Verification plan

- `dotnet build src/PokemonRedRL.sln` after adding the control panel and every agent/core integration step.
- UI smoke checks: layout at page sizes 1, 2, 4, 6, 9; pagination for 10 and synthetic 20 agents; no overlapping text or unstable tile resizing.
- Emulator smoke checks: attach or launch 1, 3, then 10 environments; every bridge responds to `ping`/`get_state`; each tile maps to the correct PID/port/window handle.
- Window preview verification: capture at least one nonblank frame per visible tile page using the Task 1 `PrintWindow` method.
- Agent integration smoke: run a short bounded training session from the control panel, confirm agents connect only to manifest-assigned ports and logs/telemetry appear in the right tile. If the selected runtime model is one-process-per-agent, kill/restart one slot and confirm other slots continue. If it is one process for all agents, confirm per-agent telemetry is still reliably distinguishable.
- Startup-script verification: prove the final automated mode can launch at least 3 mGBA instances with different assigned ports without manual Tools → Scripting. If generated wrapper scripts are used, verify their paths are in the run manifest and deleted/isolated with the run directory.
- Acceleration measurement: record completed bridge command cycles/s per instance for normal and accelerated profiles; accept at least 10 cycles/s or explicitly block the accelerated profile.
- Cleanup check: stop a run and verify only manifest-owned mGBA/agent processes are terminated and no runtime files were written into committed ROM/config locations.
- Documentation: `docs/PROJECT_OVERVIEW.md` and `docs/CODEBASE_MAP.md` updated; roadmap Task 2b updated with actual outcome.

## Open questions/assumptions

- **Assumption:** WPF is the correct first desktop UI technology because the emulator/control/capture stack is Windows-only today. Revisit Avalonia only if cross-platform operator UI becomes a real requirement.
- **Assumption:** first implementation supervises agents as external process(es). In-process hosting is possible later but riskier because TorchSharp/Redis/training faults could destabilize the UI. The exact granularity (one process per agent vs. one existing multi-agent process) must be decided before coding agent integration.
- **Open:** whether upstream mGBA GUI `--script FILE` can be built and validated locally soon. If not, the control panel should still land with manual attach mode and a clearly visible automation-not-available state.
- **Open:** whether upstream `--script FILE` can pass Lua script arguments. If not, use per-run wrapper scripts for unique port assignment.
- **Open:** exact telemetry channel. MVP can parse logs/current state; a structured telemetry API is preferable but may become its own follow-up task.
- **Open:** final per-agent runtime-state strategy (clone base save, load savestate, or read-only reset pattern) affects reproducibility and training diversity and should be confirmed before the 10-instance verification.

## Review notes (from `/review-plan`)

✅ **Solid:** the re-scope to a desktop control panel matches the user's operational goal better than a launcher-only task; WPF is a pragmatic Windows-first choice; the plan keeps GUI mode as default because `PrintWindow` vision requires visible rendered windows; and the manifest-centered ownership model correctly avoids global process cleanup and stale-port attachment.

⚠️ **Gaps found and fixed in this revision:**
- **Task was too broad without an MVP boundary.** Added an explicit MVP pass condition so implement-task does not drift into charts, replay browsing, or model-comparison tooling.
- **Agent process model was underspecified.** The current `PokemonRedRL.Agent` process internally starts `NUMBER_OF_AGENTS = 10`, which conflicts with a dashboard mental model of one tile per emulator/agent. Step 2 now forces a choice between one process per agent, one existing multi-agent process, or in-process hosting before integration code is written.
- **Lua script argument problem.** Upstream source confirms `--script FILE`, but not script arguments; the bridge currently reads its port from `arg[1]`. Step 3/6 now require validating this and prefer generated per-run wrapper scripts for per-port startup.
- **Preview refresh needed a cap.** `PrintWindow` is fast, but unbounded capture across 9 visible tiles can waste resources. Step 1/risk section now requires an explicit UI preview FPS policy.
- **Telemetry attribution could be misleading.** Step 9 now requires structured telemetry if log parsing cannot reliably map messages to the right tile/agent.
- **Verification now covers script startup and process granularity.** Added checks for 3 automated instances with distinct ports and for one-slot restart isolation depending on selected runtime model.

❓ **Open questions carried forward:** upstream build feasibility, whether `--script FILE` supports arguments, exact agent process granularity, telemetry channel, and per-agent save/reset strategy. These must be decided during implementation before the corresponding code is written.

Plan is implementation-ready. Ready for `/implement-task`.

## Review fixes (from `/review-changes`, first pass — ❌ Fail)

The first `/implement-task` pass built cleanly but `/review-changes` found one blocking functional bug and one repo-hygiene gap, both fixed in a follow-up `/implement-task` pass:

- **Cross-thread UI mutation (blocking).** `AgentProcessManager`'s `OutputDataReceived`/`ErrorDataReceived`/`Exited` events fire on `Process`'s background reader/thread-pool threads, not the WPF UI thread. `MainViewModel.OnLogLineReceived`/`OnAgentExited` were mutating bound `ObservableCollection`/properties directly from those handlers, which would throw or silently misbehave the moment a real agent process produced output. **Fixed:** both handlers now marshal their body through a captured `Dispatcher.CurrentDispatcher.BeginInvoke(...)` before touching any bound state.
- **`runs/` output directory not gitignored (blocking).** `RunManifestService`'s default `RunRootDirectory` writes into the repo tree and nothing excluded it. **Fixed:** added `runs/` to [.gitignore](../../.gitignore).
- **Minor hardening (from review notes, fixed alongside the above):** `AgentProcessManager.StopAgent` now disposes the `Process` object after removal (previously leaked handles); `Kill()` and `Start()` failures now catch `Win32Exception` in addition to `InvalidOperationException` and surface as a log line instead of an unhandled exception; `App.xaml.cs` now has a `DispatcherUnhandledException` handler that shows an error dialog instead of crashing silently.

Re-verified: `dotnet build src/PokemonRedRL.sln` still 0 errors after the fixes; `PokemonRedRL.ControlPanel` still starts cleanly. Real 1/3/10-instance scale verification remains outstanding (unchanged from Step 12 — still requires a manual, user-driven pass).

## Review fixes (from `/review-changes`, second pass — ❌ Fail)

The first fix round (above) introduced a new race: `AgentProcessManager.StopAgent` started disposing the `Process` object right after `Kill()`, but `Process.Exited` fires asynchronously on its own ThreadPool wait-handle callback and could still be in flight, reading `process.ExitCode` on an already-disposed instance — an unhandled exception on a background thread, which `App.xaml.cs`'s `DispatcherUnhandledException` handler does **not** catch (it only covers UI-thread/dispatcher-pumped work), so this could crash the whole app on an ordinary "Stop run" click. **Fixed with defense in depth:** `StopAgent` now sets `process.EnableRaisingEvents = false` before `Kill()`/`Dispose()` (narrows the race by unregistering the wait), and the `Exited` handler in `StartAgent` now wraps its `process.ExitCode` read in a `try/catch (InvalidOperationException)` as a backstop for the residual window that `EnableRaisingEvents = false` alone can't fully close.

Re-verified: `dotnet build src/PokemonRedRL.sln` still 0 errors after this fix.

## Follow-ups (from `/review-changes`, third pass — ✅ Pass with notes)

All blocking issues from the previous two review rounds are fixed and re-verified (`dotnet build src/PokemonRedRL.sln` — 0 errors; diff scope matches the plan with no unplanned changes). Non-blocking items to track before/while relying on this for real training runs:

- **Real 1/3/10-instance scale verification is still outstanding** (Step 12) — requires a live Redis instance and manually-started mGBA windows, which no automated pass can provide. A human should run this progression and record CPU/memory/capture-latency/command-cycle-rate/cleanup results in this file before depending on the control panel for production training runs.
- **`AgentProcessManager`'s `Exited` handler catches only `InvalidOperationException`** around the post-`Dispose()` `process.ExitCode` read (see the second review-fixes entry above). This is the documented exception type for invalid `Process` state access, but if real multi-instance load testing (the item above) ever surfaces a different exception type from this race, broaden the catch.
- **Automated launch mode remains blocked** (Step 3) and **speed profiles remain unenforceable** (Step 10) — both are recorded, intentional MVP limitations, not defects, but worth revisiting once/if a Qt-enabled upstream mGBA build becomes available.
