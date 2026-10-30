# S9 - Vision Project and Frame Pipeline

**Status:** Done
**ID:** S9
**Depth:** Standard
**Flow:** Full
**Initiative:** I1-computer-vision-rl-overhaul
**Parent Epic:** E1-vision-perception
**Parent Branch:** `epic/e1-vision-perception`
**Branch:** `story/s9-vision-project-frame-pipeline`
**Created:** 2026-10-30
**Approved:** 2026-10-30
**Reviewed:** 2026-10-30 - Pass with Notes
**Archived:** 2026-10-30
**Committed:** 2026-10-30 - feat(S9): add host-local vision frame pipeline

## Why

The representation is settled by ADR-0001, but the Agent has no visual input pipeline. ControlPanel already records each slot's emulator PID and window handle; its preview service is WPF-specific and is not a training frame provider. S9 delivers a bounded, independently verifiable pipeline that S11 can consume without changing the existing learner.

## What

### Capabilities

| ID | Intent | Success Signal |
|---|---|---|
| CAP-1 | Associate visual input with the selected run and agent slot. | Two distinct fixture slots resolve only their own window; missing, ambiguous, stale, or mismatched PID/window/port associations fail explicitly rather than choosing a window by title. A live frame is visually checked against its selected emulator. |
| CAP-2 | Supply game images independent of dashboard visibility. | Captured images exclude window chrome, menus, and letterboxing; resize and DPI cases are checked. Off-page slots remain capturable. Invalid or minimized windows produce an explicit unavailable result, never a silently accepted replacement image. |
| CAP-3 | Produce the accepted temporal vision representation. | Deterministic fixtures yield chronological float32 tensors of shape [2,84,84], finite values in [-1,1], with black/white endpoints verified. Episode reset and unavailable captures cannot mix previous-episode or other-agent frames into the stack. |
| CAP-4 | Keep vision optional and isolated from memory-only execution. | With Vision.Enabled absent or false, no capture/preprocessing services run, the 14-value state output is bit-identical for fixed fixtures, and learner, action selection, reward, replay payloads, and Lua traffic are unchanged. Linux memory-only execution remains supported. |
| CAP-5 | Bound resource ownership and expose measured capacity. | Per-agent scoped state is independent; disposal and cancellation checks pass. Live 1/3/10-window measurements report requested/completed/failed captures, missed deadlines, latency percentiles, CPU, private memory, and GDI handles. A sustained soak records whether resources stabilize. |

### Constraints

- ADR-0001 fixes PrintWindow capture, TorchSharp/torchvision preprocessing, F=2, 84x84 grayscale, and normalization using mean/std 0.5. Keep all 14 memory values unchanged.
- Vision.Enabled defaults to false and gates services at composition time. Unsupported enabled configurations must fail clearly; they must not silently fall back to memory-only training.
- The Agent is net10.0 and also runs in Linux Docker; Windows capture must not force a Windows-only target or WPF dependency onto that memory-only runtime.
- Use manifest-backed slot identity, not window titles or automatic port scanning, for enabled capture.
- Capture state and frame stacks are scoped per agent. Define caller/service ownership of every returned tensor and native buffer before implementation.
- No new production NuGet dependency without a justified, approved architecture adjustment. Focused test infrastructure is allowed, but must be reconciled explicitly with ADR-0001's no-new-packages rule before installation.
- No Lua protocol or Redis schema changes. Do not write benchmark images or emulator runtime state into committed ROM/save/config locations.
- Verification runtime limit, explicitly requested by the user on 2026-10-30 after the historical single-window soak: no new test may run longer than five minutes. Current concurrency conditions use 60 seconds each. Do not repeat or extend the earlier 30-minute soak without revised authorization.

### Non-goals

- CNN, fusion, trainer integration, frame replay serialization, or learning-quality A/B trials: S11.
- Visual reward signals: S12. General test infrastructure and CI: S14.
- Headless emulator/container capture transport: S16. Screenshot-grid redesign: S18.
- Automated mGBA startup, fast-forward control, or unrelated ControlPanel cleanup.

### Success Signal

A Windows-host diagnostic consumer obtains correctly associated [2,84,84] tensors through the production interfaces, with isolated stacks and explicit capture failures. Focused checks and the solution build pass; live capacity and soak evidence identify supported concurrency rather than extrapolating S1's three-window spike. Memory-only behavior remains unchanged. Missing live evidence is a review blocker unless explicitly dispositioned by the user, not an assumed pass.

## How

### Architecture Notes

Introduce PokemonRedRL.Vision with IFrameProvider and IVisionPreprocessorService, separating native capture from platform-independent tensor processing. No dependency on ControlPanel or its BitmapSource output is permitted. ControlPanel supplies manifest/slot identity to the Agent. The approved [ADR-0003](../../adr/ADR-0003-vision-frame-pipeline.md) defines the capture boundary, identity handoff, viewport fallback, temporal lifecycle, and deployment behavior.

S9 provides a diagnostic consumer and DI wiring, not a partially enabled vision learner. S11 connects the output to the network. The architecture must make this distinction explicit so Vision.Enabled=true cannot misleadingly report vision training while the current network ignores frames.

### Steps

- [x] 1. Settle the integration contract in the architecture phase.
  - **Files:** Agent/Program.cs; ControlPanel/Services/AgentProcessManager.cs; ControlPanel/Services/RunManifestService.cs; ControlPanel/Models/AgentSlot.cs; new Vision interfaces.
  - **What:** Specify manifest handoff and persistence ordering, PID/HWND validation, viewport extraction, platform guard, configuration loading from per-slot working directories, diagnostic-only entry point, capture timing, stack warm-up/reset/failure semantics, and resource ownership. Resolve the questions below before coding.
  - **Verify:** Approved architecture traces CAP-1 through CAP-5 without introducing S11 work or a WPF dependency in the Agent.
- [x] 2. Add the Vision project and focused verification harness.
  - **Files:** src/PokemonRedRL.Vision/; src/PokemonRedRL.sln; focused test project or agreed existing-toolchain harness.
  - **What:** Implement platform-independent contracts, fixture frame sources, per-agent stack lifecycle, and disposal checks. Agree test dependencies before adding packages.
  - **Verify:** Fixture checks cover ordering, warm-up, reset, invalid frames, independent scopes, tensor dtype/shape/range, and ownership; build the solution.
- [x] 3. Implement native capture and manifest-backed identity.
  - **Progress:** Native backend, explicit viewport fallback, manifest parser, saved attachment handoff, and fixture checks implemented. Live game-only image, minimize/restore, resize rejection, closed-window rejection, native failure cleanup, and off-page capture verified. Actual DPI transition was not run and is explicitly accepted by the user as an unverified residual risk; see the verification disposition below.
  - **Files:** Vision capture/identity implementations; relevant ControlPanel launch/manifest services; Agent configuration and startup.
  - **What:** Validate slot identity, obtain game-only pixels through PrintWindow, release all native resources on success and failure, and pass an explicit manifest association to the consumer.
  - **Verify:** Wrong/missing/stale identity is rejected; distinct windows, resize/DPI, off-page slots, minimized/closed windows, and shutdown are checked; build the solution.
- [x] 4. Implement preprocessing and optional composition.
  - **Files:** Vision preprocessing/stack services; Agent/Program.cs; Agent/appsettings.json; Agent project references.
  - **What:** Apply the accepted tensor transform and register scoped services only for the supported enabled consumer. Leave memory-only learning and persistence untouched.
  - **Verify:** Run fixture and disabled-path regression checks, unsupported-platform checks, and the solution build.
- [x] 5. Measure live capacity and resource stability.
  - **Progress:** One/three/ten-window 4/40 Hz measurements exist; three and ten windows were measured with previews off/on, and the historical single-window 30-minute soak passed. Ten windows sustain the measured 4 Hz cadence after startup but miss both the throughput and p95 targets at requested 40 Hz. The user explicitly accepts the measured capacity limits and waives the remaining longer higher-concurrency soak; no unexecuted check is marked Passed. New tests remain capped at five minutes.
  - **Files:** Agreed diagnostic harness; this plan's verification evidence.
  - **What:** Exercise 1, 3, then 10 manually attached windows with dashboard previews both active and inactive. Measure capture plus preprocessing at requested 4 Hz and 40 Hz per slot for 60 seconds each, followed by a 30-minute soak at the highest supported tested concurrency/rate. Record failures and limits, including any environment blocker.
  - **Verify:** Report counts, deadline misses, latency percentiles, CPU/private memory/GDI trends, and actual tested concurrency. A 40 Hz target requires an end-to-end p95 at or below 25 ms without sustained backlog; report a miss as a limitation, not a fabricated performance guarantee. Resource growth requires investigation before acceptance.
- [x] 6. Update documentation and run acceptance preparation.
  - **Files:** docs/CODEBASE_MAP.md; docs/PROJECT_OVERVIEW.md; this plan; project context only if a design risk changes.
  - **What:** Document platform support, manifest invocation, diagnostic versus training behavior, measured limits, and ownership. Regenerate the map after structural changes.
  - **Verify:** Focused checks and dotnet build src/PokemonRedRL.sln pass; each capability links to observable evidence for bmad-review.

### Dependencies

- Production packages: TorchSharp 0.107.0 plus TorchVision 0.107.0 in the Vision project; this dependency correction and continued implementation were explicitly approved on 2026-10-30. Other additions require approval.
- Test packages: a focused xUnit project with test-only dependencies is approved by ADR-0003; choose compatible versions during implementation. No packages installed during planning or test design.
- Persistence/protocol: no Redis or Lua changes; manifest handoff may require an additive compatibility-preserving contract.
- Deployment: Windows desktop session required for native capture. Existing Linux Docker memory-only mode remains supported; no remote-frame transport is added.

## Risks and Edge Cases

| Risk | Likelihood | Mitigation |
|---|---|---|
| A captured client area includes menu/status bars or black borders. | High | Specify and verify game viewport extraction across window sizes and DPI; do not resize the entire application window into training input. |
| A stale manifest or reused HWND identifies another process. | Medium | Validate process/window ownership and slot identity at capture; reject mismatch. |
| ControlPanel starts an Agent before the updated association is saved. | Medium | Define persistence ordering and handoff contract before startup integration. |
| Per-slot working directory prevents expected configuration loading. | High | Verify resolved configuration explicitly; test missing/false flag and enabled invocation. |
| Native capture blocks or returns blank/stale pixels. | Medium | Define supported window states and bounded scheduling; measure capture behavior and surface failures without inventing image freshness guarantees. |
| Frames or native handles accumulate. | High | Explicit ownership, failure-path cleanup, scoped stacks, repeated-disposal tests, and live resource soak. |
| Ten windows plus UI previews miss the training cadence. | Medium | Measure progressive concurrency with previews on/off and bounded buffering; record the supported envelope. |

## Open Questions for Architecture Approval

- D1 (approved 2026-10-30): S9 delivers Windows-host diagnostic capture; enabled learning remains in S11 and Linux Docker remains memory-only.
- D3 (approved 2026-10-30): ADR-0003 defines validated render-area capture or an explicit bounded viewport, two-successful-frame warm-up, chronological ordering, and history reset on episode reset, capture failure, or identity/geometry change.
- D2 (approved 2026-10-30): Add a focused xUnit project with test-only packages. The ADR-0001 production dependency restriction remains; record the test-only clarification in the architecture decision before installation.

## Verification

**Test design approved:** 2026-10-30. This section defines required evidence; executed results are recorded below. The focused xUnit project now exists at src/PokemonRedRL.Vision.Tests.

### Setup and Evidence

- Automated fixtures use fixed pixel buffers, manifests, controllable identity/capture adapters, and independent DI scopes. They require neither mGBA nor Redis; TorchSharp checks require the matching native runtime. Reset state between cases and dispose every fixture-owned tensor and scope.
- Live checks require an interactive Windows desktop, available mGBA windows, explicit viewport geometry where discovery is ambiguous, and a saved run manifest. The user supplies manual emulator/window attachment; the coding agent performs available measurements and records evidence. Missing prerequisites are blockers, not passes.
- Store diagnostic images and measurement output in ignored run directories or OS temporary storage. Stop only diagnostic-owned processes, release handles and tensors, and leave committed ROM/save/config files unchanged.
- For each case, record the test name or manual procedure, environment, expected versus observed result, and artifact location here during implementation/review. High-risk cases need evidence or an explicit user-approved disposition.

### Risk-Based Matrix

| Case / Requirement | Risk / Priority | Scenario and Boundary | Evidence and Oracle | Setup / Cleanup |
|---|---|---|---|---|
| V-01 / CAP-1 | Wrong-window input corrupts slot identity; Medium | Resolve two distinct manifest slots; reject missing/duplicate indices, mismatched host/port, and invalid PID/HWND ownership at the identity boundary. | Fixture tests select only the requested slot and explicitly reject invalid associations. Live visual inspection confirms the selected emulator, without claiming independent proof of its Lua port. | Two fixture slots with distinct identities; live manually attached windows for visual confirmation. Dispose capture resources. |
| V-02 / CAP-2 | Chrome or incorrect crop corrupts every input; High | Check valid/boundary/out-of-bounds rectangles and ambiguous viewport discovery; inspect live resize/DPI cases and off-page, minimized, and closed windows. | Geometry tests reject invalid regions. Live images exclude menus, borders, and letterboxing; unavailable windows return an explicit failure, never another window or a substituted image. | Synthetic geometry plus saved live association; record desktop DPI/window size and verify images visually. Remove temporary captures after review. |
| V-03 / CAP-3 | Wrong channel order, normalization, or temporal order; Medium | Preprocess black, white, color, and sequential fixture frames at the tensor boundary. | Output is finite float32 [2,84,84], chronological, bounded in [-1,1]; black/white endpoints and color conversion match the specified transform within an explicit numeric tolerance. | Fixed raw buffers with known channel order; isolate preprocessing from native capture and dispose outputs. |
| V-04 / CAP-3, CAP-5 | Cross-episode or cross-agent history leaks; High | Warm-up, episode reset, failed capture, geometry/identity change, and two independent scopes at the stack boundary. | No tensor before two successful captures; reset/failure clears history, recovery requires two new captures, and one scope cannot affect another. Disposing a caller-owned output does not invalidate retained history. | Distinct frame sequences and controllable failures per scope; dispose results and both scopes, including repeated cleanup paths. |
| V-05 / CAP-4 | Working-directory configuration silently changes behavior; High | Start with per-slot working directory and flag absent/false/true; verify intended configuration precedence at the composition boundary. | Absent/false resolves the unchanged memory-only path with no capture service execution. Explicit enabled training fails clearly until S11; configuration does not silently disappear because the working directory changed. | Isolated configuration fixtures and temporary slot directory; restore any process-level settings and remove temporary files. |
| V-06 / CAP-4 | Diagnostics start training/Redis or break Linux; Medium | Exercise diagnostic startup without Redis, unsupported-platform diagnostics, and disabled Linux deployment. Compare fixed memory-state fixtures against the pre-change baseline. | Diagnostics instantiate no learner/Redis infrastructure; unsupported capture fails before Win32 allocation. Linux memory-only smoke and unchanged 14-value tensor bit patterns pass. Review learner/reward/replay/Lua changes against the no-change invariant. | Windows diagnostic startup; available Linux runtime/container for smoke, otherwise record blocker. Capture baseline before behavior edits. Clean up only test-owned processes. |
| V-07 / CAP-5 | Native/tensor leaks exhaust long runs; High | Repeated success, partial failure, cancellation between native calls, returned-output disposal, and service disposal at resource boundaries; perform the agreed live soak. | Ownership tests and failure-path checks release acquired resources; retained stack is bounded. Record private memory/GDI trends during the 30-minute soak; unexplained sustained growth requires investigation, not an automatic pass. | Native acquisition/failure fixtures plus live supported Windows workload; sample after warm-up and through cleanup. Do not equate allocator caching with a proven leak or claim safe interruption of PrintWindow. |
| V-08 / CAP-5 | Capture plus previews cannot sustain requested cadence; Medium | Exercise 1/3/10 windows at 4/40 Hz for 60 seconds per condition, previews on/off, then soak at the highest supported measured condition. | Report requested/completed/failed captures, missed deadlines, end-to-end latency percentiles, CPU/private memory/GDI, actual concurrency, and absence of an accumulating queue. The 40 Hz p95 target is <=25 ms; misses are reported as limits, not hidden. | Manually attached windows and bounded diagnostic consumers; record actual speed/window conditions and release diagnostic-owned resources after each run. |

### Coverage and Residual Risks

- CAP-1: V-01; CAP-2: V-02; CAP-3: V-03/V-04; CAP-4: V-05/V-06; CAP-5: V-04/V-07/V-08. Every identified High risk has planned deterministic or live evidence; executed results and remaining gaps are recorded below.
- Linux runtime availability, native TorchSharp loading, interactive desktop access, and sufficient manually attached windows remain environment prerequisites. Partial concurrency evidence cannot establish ten-window support.
- PID/HWND validates window ownership, not the operator's asserted Lua-port association. Successful PrintWindow does not guarantee frame freshness or bounded completion time. Retain these limits in acceptance evidence; do not silently strengthen guarantees.
- Review requires every behavior-changing acceptance signal to link to an observable result. A passing build or passing fixture suite alone cannot replace live viewport, capacity, or soak evidence.

### Execution Evidence (2026-10-30)

- Baseline: dotnet build src/PokemonRedRL.sln passed with 0 errors and 13 existing warnings before product changes.
- Initial slice: Vision and Vision.Tests projects added to the solution; FrameTests passed 7/7 cases for RGB buffer validation/copying and viewport bounds, including overflow rejection.
- Preprocessing probe: compilation failed because torchvision is absent from the installed TorchSharp 0.107.0 assembly. The official TorchVision 0.107.0 package supplies the separate bindings (https://www.nuget.org/packages/TorchVision/0.107.0). This contradicts the plan's existing-package assumption. The noncompiling probe was removed; the 7 contract tests pass again. No additional production dependency was installed.
- Dependency correction approved 2026-10-30: add TorchVision 0.107.0 to supply the specified transforms. The revised dependency is approved for continued build; tensor contracts and acceptance scope are unchanged.
- Automated verification: dotnet test src/PokemonRedRL.Vision.Tests/PokemonRedRL.Vision.Tests.csproj passed 30/30 cases. This includes mode guards, configuration fallback and overrides, bit-identical memory-state floats, output ownership, DI isolation, repeated Tensor.TotalCount stabilization, identity/geometry/cancellation fixtures, atomic manifest handoff, and real Agent subprocesses with unavailable Redis.
- Final local gate: dotnet build src/PokemonRedRL.sln passed with 0 errors; existing nullable/System.Drawing warnings and the MessagePack audit warning remain. git diff --check passed. No emulator ROM/save/config changes, commits, pushes, or merges were made during this build phase.
- Linux deployment: docker build -t pokemonredrl-s9-check . succeeded. A network-isolated container rejected enabled diagnostics with the expected Windows-platform message and exit 2. With Vision.Enabled=false it reached the existing Redis initialization and failed as expected against deliberately unavailable 127.0.0.1:1, without a Windows/native-capture exception. This is a startup smoke, not an end-to-end training run.
- Initial environment inspection found no running mGBA. The user subsequently opened one, then three, then ten mGBA windows. The live results below supersede that initial blocker and cover ten concurrent workers with previews off/on plus off-page capture for three windows. The user subsequently accepted the unverified DPI transition and waived the longer higher-concurrency soak; see the explicit disposition and subsequent acceptance review below.
- Dependency warning: restore reports existing MessagePack 3.1.8 advisory GHSA-qhrr-8q5h-9q3h (moderate), now also visible through the test project's Agent reference. No unrelated package remediation is included in S9.

| Case | Executed evidence | Remaining evidence |
|---|---|---|
| V-01 | BindingTests and CaptureTests reject invalid/duplicate/mismatched associations and mid-capture identity changes. Live PID/HWND association and selected game image verified. | Operator port association remains an assertion; the live diagnostic manifest does not prove a Lua bridge at port 12345. |
| V-02 | Geometry fixtures pass. Live SGB/game-only crop, minimize/restore, resize rejection, and original-geometry recovery pass; an owned native fixture verifies closed-window rejection. Three attached windows capture successfully while their tiles are off-page; the tenth worker also captures while its tile is outside the preview page. | Actual DPI change is untested and explicitly accepted by the user as a residual risk. No automatic renderer discovery is claimed. |
| V-03 | PreprocessorTests pass shape/dtype, chronological black/white endpoints, and RGB color conversion. | No additional pure-transform blocker; real source pixels remain V-02. |
| V-04 | PreprocessorTests pass warm-up, reset/failure/identity/geometry reset, output ownership, and independent DI scopes. | Native failure/resource evidence remains V-07. |
| V-05 | ConfigurationTests pass base/slot/CLI precedence and mode guards; StartupTests invoke the real entry point. | Environment override precedence follows ConfigurationBuilder; no live operator-run configuration evidence yet. |
| V-06 | Bit-identical memory fixture and Redis-free subprocess checks pass; Linux image and platform/startup smoke verified. | Full Linux training against live Redis/mGBA was not attempted or claimed. |
| V-07 | Tensor.TotalCount fixture checks pass. A real 30-minute 40 Hz soak has bounded private memory and constant GDI count. Cancellation and 1000 native PrintWindow-failure cleanup attempts pass. | Allocation failures before PrintWindow were not deliberately forced; long multi-window stability remains unverified. These remaining gaps are explicitly accepted by the user. |
| V-08 | One-window 60-second 4/40 Hz runs and historical 30-minute 40 Hz soak measured with previews inactive. Three/ten-window 60-second 4/40 Hz conditions measured with real WPF previews off/on; counts and limits recorded below. | Exact 40 Hz is not supported at measured multi-window concurrency; the user accepts this documented limit. The remaining higher-concurrency soak is explicitly waived. New tests remain limited to five minutes. |

### Single-Window Live Evidence (2026-10-30)

The user supplied one already running mGBA 0.10.5 window with Pokemon Red and a loaded state. Observed identity was PID 74724 / HWND 1444992, physical client 340x290 at 96 DPI, approximately 59 fps. ControlPanel was not running; all measurements below have previews inactive. No game inputs or learner processes were started.

An ignored probe linked the production RunManifestService and models, saved a fresh diagnostic-only manifest with the observed PID/HWND, then used Win32CaptureBackend, WindowFrameProvider, and the diagnostic BMP writer. This validates the production persistence/capture boundary, not the unexercised ControlPanel button interaction. Host/port 127.0.0.1:12345 is diagnostic metadata only; Lua was not contacted, and the unknown ROM path was left empty rather than invented.

Artifacts are local and ignored: [manifest](../../../runs/s9-live/captures/20261030-143420/manifest.json), [full client](../../../runs/s9-live/captures/20261030-143420/full-client.png), [game-only crop](../../../runs/s9-live/captures/20261030-143420/game-4hz.png), and [temporary probe](../../../runs/s9-live/Program.cs). Full-client inspection exposed the menu and SGB decorative border. Explicit manual viewport 74,69,191,173 excludes both; dark areas inside the Game Boy image are actual game content. No automatic border detection was added to the provider.

| Condition | Elapsed | Completed / failed | Ready stacks | Missed deadlines | p50 / p95 | Average CPU, normalized | Final private bytes | GDI handles |
|---|---|---|---|---|---|---|---|---|
| [4 Hz](../../../runs/s9-live/captures/20261030-143420/metrics-4hz.log), one window | 60.008 s | 238 / 0 | 237 | 2 | 9 / 12 ms | 33.81% | 139051008 | 0 |
| [40 Hz](../../../runs/s9-live/captures/20261030-143420/metrics-40hz.log), one window | 60.001 s | 2362 / 0 | 2361 | 38 | 8 / 11 ms | 42.67% | 137129984 | 0 |
| [40 Hz soak](../../../runs/s9-live/captures/20261030-143420/soak-40hz.log), one window | 1800.010 s | 71660 / 0 | 71659 | 340 | 8 / 11 ms | 43.40% | 136871936 | 0 |

The single-window latency target p95 <=25 ms passed. The scheduler discarded missed intervals instead of accumulating queued requests; the soak achieved approximately 39.81 successful frames/s, not a guarantee of every 40 Hz deadline. Startup/JIT and scheduling misses are included. CPU cost is material and must not be extrapolated to ten workers.

Across 1763 soak reports, private memory after the first minute ranged from 135712768 to 138461184 bytes; the first warm sample was 137170944 and the final sample 136871936. The last five minutes ranged from 136830976 to 136871936 bytes. GDI handles stayed 0 throughout. No sustained growth was observed for this condition; this does not prove every allocation-failure path or multi-window envelope.

[Lifecycle evidence](../../../runs/s9-live/captures/20261030-143420/lifecycle.log): actual emulator minimization returned unavailable; restore captured successfully. A temporary +32-pixel resize was rejected, and original geometry was restored and captured successfully. A pre-cancelled request threw OperationCanceledException. One thousand invalid-HWND PrintWindow failures exercised allocation/select/cleanup with GDI count 0 -> 0. Closed-window rejection used a separate probe-owned native window, destroyed by the probe; the user's emulator was not closed. Final emulator state remained PID/HWND unchanged, 340x290, 96 DPI, not minimized.

No ROM/save/config writes were performed by the probes. The running emulator independently modified its tracked nointro.sqlite3 runtime database; that change was left untouched. Live verification remains partial until the remaining matrix conditions are measured or explicitly dispositioned by the user; automated mGBA startup remains outside this story's scope.

### Three-Window Short Runs (2026-10-30)

After the user opened two additional windows, the user explicitly limited tests to five minutes. The four concurrent conditions below each lasted 60 seconds, with all three diagnostic processes running simultaneously. Separate five-second preview/off-page checks were also executed. No new long soak was run.

The saved [three-slot manifest](../../../runs/s9-live/captures/20261030-152108/manifest.json) maps slots 0/1/2 to PID/HWND 74724/1444992, 77320/1052170, and 87264/331270, respectively. All three clients were 340x290 at 96 DPI, approximately 60 emulator fps. [Side-by-side inspection](../../../runs/s9-live/captures/20261030-152108/three-windows.png) confirmed game-only viewport 74,69,191,173 for each. Ports 12345/12346/12347 remain diagnostic associations, not verified Lua ownership.

| Condition, three simultaneous workers | Completed, slots 0 / 1 / 2 | Failed, all slots | Missed deadlines, slots 0 / 1 / 2 | p95 ms, slots 0 / 1 / 2 | Actual successful frames/s per slot |
|---|---|---|---|---|---|
| 4 Hz, previews off | 240 / 240 / 240 | 0 | 0 / 0 / 0 | 25 / 25 / 26 | 4.00 / 4.00 / 4.00 |
| 40 Hz, previews off | 2059 / 2146 / 2105 | 0 | 342 / 254 / 295 | 19 / 18 / 18 | 34.31 / 35.76 / 35.08 |
| 4 Hz, previews on | 240 / 240 / 240 | 0 | 0 / 0 / 0 | 24 / 25 / 24 | 4.00 / 4.00 / 4.00 |
| 40 Hz, previews on | 1923 / 1996 / 1935 | 0 | 477 / 404 / 465 | 22 / 22 / 22 | 32.04 / 33.26 / 32.24 |

All twelve workers exited 0; each produced one fewer ready stack than completed frames because of two-frame warm-up. GDI handles were 0 at each worker's final report. Final private memory across workers/conditions ranged from 136409088 to 140800000 bytes. Diagnostic CPU, normalized across logical processors, was approximately 21.39-26.50% per worker; combined cost is material. Full counts, latency, CPU, memory, and GDI samples are retained in the per-condition/per-slot logs next to the manifest, for example [previews-off, 40 Hz, slot 0](../../../runs/s9-live/captures/20261030-152108/previews-off-40-hz-slot-0.log) and [previews-on, 40 Hz, slot 0](../../../runs/s9-live/captures/20261030-152108/previews-on-40-hz-slot-0.log).

Interpretation: three-window 4 Hz cadence passed with no missed deadlines. The 40 Hz p95 latency target passed, but sustained 40 successful frames/s per slot did not. Skipped deadlines are explicitly counted and no catch-up queue accumulates. These measurements establish approximately 32-36 frames/s per slot under the tested 40 Hz request, not guaranteed ten-worker throughput or sustained exact 40 Hz.

Preview contention used an ignored [WPF harness](../../../runs/s9-preview/Program.cs) referencing the actual ControlPanel MainWindow, MainViewModel, AttachWindow, pagination, and PreviewCaptureService. Only IAgentProcessManager and IEmulatorPingProbe were replaced with no-op fixtures, so no learner, Redis, or Lua traffic was started. Attachments exercised the production persistence method programmatically, not the picker dialog. The real preview timer requested 100 ms updates; [preview results](../../../runs/s9-live/captures/20261030-152108/preview-on.log) show 2592 captures, 0 unavailable, three images present over 120.72 seconds (approximately 7.16 updates/s/tile, not a proven 10 FPS guarantee). WPF CPU averaged 0.58%, final private bytes 94437376, GDI count 37. The [rendered UI](../../../runs/s9-live/captures/20261030-152108/preview-ui-150s.png) shows all three previews. Legacy preview images include emulator chrome; the independent training diagnostic still uses its verified game-only crop. No preview redesign is included.

The [five-second off-page check](../../../runs/s9-live/captures/20261030-152108/off-page.log) added seven unattached UI-only fixture tiles to make a second page, invoked the real NextPageCommand, verified none of the three attached windows was visible in CurrentPageTiles, then successfully captured each through WindowFrameProvider. Those seven empty tiles are not emulator instances or ten-window capacity evidence. The first attempt failed a harness-only oracle requiring ten distinct channel-byte values; all actual Game Boy images had nine. The corrected limited-palette check passed for all three windows; no production change was needed. Non-uniform pixels are a smoke check, not an image-freshness guarantee.

[Concurrent runner](../../../runs/s9-live/RunConcurrent.ps1) enforces an 80-second wall-clock deadline for each 60-second condition and terminates only its owned processes on overrun. [Preview orchestration](../../../runs/s9-live/WithPreviews.ps1) synchronizes readiness and closes only its owned WPF window; the preview harness has a 150-second fallback. Native PrintWindow still cannot be safely interrupted inside a worker, so forced process termination is a harness boundary, not a production cancellation guarantee. All diagnostic/preview processes finished and all three user-owned emulator windows remained open. No ROM/save/config writes or production code changes were made for these short measurements.

### Ten-Window Short Runs (2026-10-30)

The user manually opened seven more mGBA instances. All ten clients were 340x290 at 96 DPI, running approximately 59-60 emulator fps. [Ten-slot manifest](../../../runs/s9-live/captures/20261030-153229/manifest.json) preserves the first three associations and records PID/HWND for every additional slot. [Full-client contact sheet](../../../runs/s9-live/captures/20261030-153229/ten-windows.png) confirms the same explicit viewport 74,69,191,173. The first three windows show loaded gameplay states; the new seven show the ROM intro/title animation. No state was loaded or game input sent by the diagnostic harness.

The existing concurrent runner executed four 60-second conditions; all forty diagnostic workers exited successfully. Each condition ran ten independent capture/preprocessing processes, not ten subscriptions to one captured window. In previews-on conditions the actual ControlPanel first page contained nine attached tiles; slot 9 remained off-page while its diagnostic worker captured normally. This is nine page-visible previews plus ten diagnostic consumers, not ten simultaneously visible previews.

| Condition, ten simultaneous workers | Total completed / failed | Total missed deadlines | Misses after first 5 seconds | Per-slot successful frames/s | Per-slot p95 range | Sum of diagnostic CPU, normalized |
|---|---|---|---|---|---|---|
| 4 Hz, previews off | 2391 / 0 | 10 | 0 | 3.98-4.00 | 18-21 ms | 71.78% |
| 40 Hz, previews off | 8589 / 0 | 15420 | 14016 | 14.08-14.65 | 60-63 ms | 71.53% |
| 4 Hz, previews on | 2382 / 0 | 18 | 0 | 3.97-3.98 | 16-26 ms | 71.10% |
| 40 Hz, previews on | 7716 / 0 | 16291 | 14819 | 12.66-13.08 | 71-73 ms | 68.71% |

The warmed 4 Hz condition had no further skipped intervals in any slot after the first five-second checkpoint. Startup misses remain included in the table rather than being hidden. Requested 40 Hz failed both sustained cadence and the <=25 ms p95 target at ten workers. The scheduler continued skipping deadlines without an accumulating queue; this is a measured capacity limit, not a capture-correctness failure or an established cause for unrelated optimizations.

Every sampled diagnostic GDI count was 0. Final private bytes across the forty workers ranged from 134074368 to 138772480; the largest change from the first report after five seconds to shutdown was +1617920 bytes. These minute-long samples are not a substitute for the originally specified higher-concurrency soak. Complete per-worker reports, including CPU, private memory, GDI, warm-up, and deadline counts, are retained next to the manifest; examples are [previews-off, 40 Hz, slot 0](../../../runs/s9-live/captures/20261030-153229/previews-off-40-hz-slot-0.log) and [previews-on, 40 Hz, slot 9](../../../runs/s9-live/captures/20261030-153229/previews-on-40-hz-slot-9.log).

The [preview harness report](../../../runs/s9-live/captures/20261030-153229/preview-on.log) records 3505 successful previews and 0 unavailable over 121.05 seconds, ten attached tiles and nine images on the current page. That is approximately 3.22 updates/s per page tile rather than a sustained 10 FPS guarantee. WPF CPU averaged 0.55%, final private bytes 105095168, GDI count 37. The [rendered preview UI](../../../runs/s9-live/captures/20261030-153229/preview-ui-150s.png) confirms the original ControlPanel was used; agent startup and ping remained no-op fixtures as in the earlier short runs.

A read-only [monitor enumeration](../../../runs/s9-live/captures/20261030-153229/monitors.log) found a single 1920x1080 monitor at 100% scale. There is no second-DPI monitor available for a non-disruptive move test. Display scaling was not changed; the user subsequently accepted the unexecuted live transition as a residual risk. Fixture DPI rejection tests remain valid but do not replace this live case.

The five-minute limit was respected; no new long soak was run. Existing 80-second per-condition watchdogs and the 150-second preview fallback remained in place. All diagnostic/preview processes finished; user-owned emulator windows were left running. The harness made no production-code, ROM, save, or emulator-config writes. Final worktree inspection also found modifications to pokemon_red.sav and nointro.sqlite3 outside the harness; these user/emulator runtime changes were left untouched, not reverted or included in a commit. The user subsequently dispositioned the remaining verification gates below; a ten-window 40 Hz performance guarantee must not be claimed.

### Diagnostic Invocation

Start a ControlPanel run and attach the intended emulator window to its slot. The attachment is now persisted immediately through an atomic manifest replacement. Normal memory-only Agent startup still does not require a window. Diagnostic capture is a separate manual CLI invocation, not an automatic second training worker or a UI mode.

Use the saved absolute manifest path, matching slot index/host/port, and an explicitly verified game-only viewport measured in physical client pixels. The numbers below are an example, not a safe default crop for every window:

```powershell
dotnet run --no-build --project src/PokemonRedRL.Agent -- --Vision:Enabled true --vision-diagnostics true --manifest "D:\runs\example\manifest.json" --agent-index 0 --host 127.0.0.1 --port 12345 --viewport "0,0,320,288" --duration-seconds 60 --capture-hz 4
```

- Vision.Enabled defaults to false. Environment variable Vision__Enabled or --Vision:Enabled can override it. Base-directory appsettings files are fallback defaults; per-slot working-directory files override them, then environment variables and CLI take precedence.
- Diagnostics require both flags. Enabled ordinary learning is rejected until S11; diagnostics never initialize Redis, maintenance, scheduler, or the learner. Non-Windows diagnostics fail before native capture allocation.
- Supply --frame-output with a new absolute .bmp path under temporary/ignored run storage for visual inspection; existing files are not overwritten. The viewport fallback does not guess Qt renderer classes, black borders, or game content.
- Metrics are JSON lines prefixed VISION_METRICS, emitted roughly once per second and at shutdown: requested/completed/failed captures, ready stacks, skipped deadlines, cumulative p50/p95 in 1 ms ceiling buckets, CPU percent normalized by logical processor count, private bytes, and GDI handles. The histogram has bounded storage; LatencyOver60Seconds identifies clamped outliers, so a saturated bucket is not an exact latency claim. Initial JIT/transform setup and optional first snapshot writing are included in measured latency.
- For V-08 run separate diagnostic processes per manifest slot concurrently at 4 and 40 Hz, with previews both active/inactive. New tests must use --duration-seconds 300 or less under the latest user limit; current measured conditions use 60. The earlier 1800-second single-window soak is historical evidence only, not permission to repeat it. Native capture remains synchronous; Ctrl+C is observed between calls and cannot safely interrupt a blocked PrintWindow.
- Resize/DPI/process identity changes invalidate the provider for that invocation. Reinspect the viewport and restart diagnostics rather than reusing a stale crop. An unavailable capture clears the tensor history; two new successful captures are required before the next stack.

### User-Approved Verification Disposition (2026-10-30)

After the ten-window results and the remaining DPI/soak gates were presented, the user explicitly approved skipping the remaining checks and accepting the implementation. This resolves the remaining build-phase verification blockers by user disposition, not by manufacturing successful test results.

| Remaining item | Recorded result | Acceptance disposition |
|---|---|---|
| Actual DPI transition | Not run; one available monitor at 100% scale. Fixture DPI rejection tests passed. | User accepts the unverified live transition as a residual risk; no display-setting change or additional test is required for S9. |
| Longer higher-concurrency soak | Not run; three/ten-window conditions lasted 60 seconds. Historical one-window 30-minute soak passed. | User waives the remaining longer soak requirement for S9. The five-minute limit on new tests remains in force. |
| Other remaining unexecuted verification scenarios | Not run unless explicit evidence above says otherwise; native allocation failures before PrintWindow were not deliberately forced. | User accepts the remaining verification gaps. No additional unexecuted check is relabeled Passed. |

The measured multi-window 40 Hz limitation remains unchanged and is accepted as a documented capacity limit, not a successful 40 Hz result. Existing MessagePack audit warnings, operator-asserted Lua-port association, and synchronous PrintWindow cancellation limits remain documented; this decision does not claim their remediation.

Build-phase verification completed through executed evidence plus the explicit user-approved exceptions. At that checkpoint S9 was marked Implemented for the separate mandatory bmad-review gate. The disposition itself did not grant a review verdict or authorize archive, commit, merge, or push; the subsequent review is recorded below.

## Verification Checklist

- [x] Architecture and unresolved contract choices approved before implementation; see ADR-0003.
- [x] CAP-1/CAP-2 evidence recorded; unexecuted live DPI transition accepted by explicit user disposition, not marked Passed.
- [x] CAP-3 deterministic tensor/stack tests pass.
- [x] CAP-4 disabled-path regression and Linux memory-only startup checks pass; full training was not claimed.
- [x] CAP-5 live concurrency and soak results recorded; remaining longer-soak and verification gaps explicitly dispositioned by the user.
- [x] dotnet build src/PokemonRedRL.sln passes for the current implemented slice.
- [x] Codebase map and relevant narrative documentation reflect current structural changes.
- [x] Spec Law coherence and preservation passes completed; plan and architecture approved.
- [x] Optional test-design matrix V-01 through V-08 approved and recorded on 2026-10-30. Execution belongs to the build phase.

## Acceptance Review (2026-10-30)

### Findings and Dispositions

No BLOCKER or HIGH findings were identified in the plan-acceptance, compatibility, resource-lifecycle, or affected integration-contract review. This is the mandatory bmad-review, not the optional broad bmad-code-review audit.

| Note | Severity | Trigger and impact | Disposition / action |
|---|---|---|---|
| R-01 | MEDIUM | Actual DPI transitions, longer higher-concurrency stability, and the remaining unexecuted native failure scenarios lack live evidence. Acceptance cannot imply those cases passed. | The user explicitly accepted these residual risks before review. Keep them Not Run / waived, retain executed fixture/live evidence, and preserve the five-minute limit. |
| R-02 | MEDIUM | At ten simultaneous workers a 40 Hz request produces approximately 13-15 successful frames/s per slot and p95 60-73 ms. Claiming ten-worker 40 Hz would overstate supported capacity. | The user accepts the measured limit. Capacity reporting satisfies CAP-5; use the tested 4 Hz envelope after startup and reassess capacity before S11 integration requires a higher rate. |
| R-03 | MEDIUM | Restore/build reports the existing MessagePack 3.1.8 moderate advisory GHSA-qhrr-8q5h-9q3h through Agent/Models/test references. S9 does not remediate that dependency. | The user explicitly confirmed deferring remediation to separate work on 2026-10-30. Retain the warning; no vulnerability-free or deployment-security claim is made. |

### Capability Trace

| Capability | Changed entry / boundary traced | Observable evidence | Review result |
|---|---|---|---|
| CAP-1 | [FrameBinding](../../../src/PokemonRedRL.Vision/FrameBinding.cs) validates the saved slot/host/port/PID/HWND; [WindowFrameProvider](../../../src/PokemonRedRL.Vision/WindowFrameProvider.cs) validates live ownership and pre/post state. ControlPanel AttachWindow persists the association through atomic RunManifestService.Save. | BindingTests select distinct windows and reject invalid associations; CaptureTests reject identity changes; ManifestTests round-trip saved attachments. One/three/ten-window identity and images are recorded above. | Covered; Lua-port ownership remains an explicit operator assertion. |
| CAP-2 | WindowFrameProvider checks viewport/state; [Win32CaptureBackend](../../../src/PokemonRedRL.Vision/Win32CaptureBackend.cs) captures physical client pixels through PrintWindow and restores native resources/context. | Bounds/state fixtures; inspected game-only crops; actual minimize/resize/restore and closed fixture window; independent off-page capture. | Covered with user-approved live DPI exception R-01. |
| CAP-3 | [VisionPreprocessorService.Process](../../../src/PokemonRedRL.Vision/VisionPreprocessorService.cs) applies RGB resize/grayscale/normalization, retains owned history, and returns a separate chronological stack after two successes. | PreprocessorTests verify float32 [2,84,84], endpoints, RGB order, warm-up, reset/failure/source/geometry, output ownership, independent DI scopes, and stable Tensor.TotalCount. | Covered. |
| CAP-4 | [Program](../../../src/PokemonRedRL.Agent/Program.cs) branches into [VisionDiagnostics](../../../src/PokemonRedRL.Agent/VisionDiagnostics.cs) before normal host/Redis initialization; [AgentRuntimeConfiguration](../../../src/PokemonRedRL.Agent/AgentRuntimeConfiguration.cs) resolves mode and configuration. Docker restores the new project without retargeting the Agent. | ConfigurationTests cover absent/false/unsupported modes, base/slot/CLI precedence, and bit-identical fourteen-value memory state. StartupTests invoke the real entry point without Redis; earlier Linux image/platform/memory-only startup smoke is documented above. | Covered for the approved diagnostic boundary; no full Linux training pass is claimed. |
| CAP-5 | Scoped capture/preprocessing services own retained state; tensor DisposeScope and native finally cleanup bound lifetime. Diagnostic scheduling skips missed deadlines rather than building a queue, and emits capacity/resource metrics. | Ownership/disposal/cancellation fixtures, native failure cleanup, historical one-window soak, and short one/three/ten-window conditions with preview/off-page evidence. | Covered with explicit unverified-stability exceptions R-01 and measured capacity limit R-02. |

### Verification and Scope

- Re-executed dotnet build src/PokemonRedRL.sln: passed, 0 errors; existing warnings remain.
- Re-executed dotnet test src/PokemonRedRL.Vision.Tests/PokemonRedRL.Vision.Tests.csproj --no-build: 30 passed, 0 failed, 0 skipped; 2.9-second test duration. No new long live test was run.
- Traced diagnostic DI/startup, configuration precedence, manifest producer/consumer sharing, native success/failure cleanup, tensor ownership/reset, Docker project inputs, and unchanged learner/reward/replay/Lua boundaries against ADR-0003.
- Reviewed current Story branch and worktree. ROM/save/runtime database modifications are unrelated user/emulator state and were not reverted, staged, or included in the review's product-code scope. Ignored probes/images/logs remain local evidence as approved; the tracked CLI/tests reproduce the diagnostic contracts.
- Repository map, overview, technical context, and work registry are synchronized with this review checkpoint. No production fixes, archive, commit, merge, or push were performed by review.

### Verdict and Handoff

**Pass with Notes.** Executed checks passed for the accepted scope; unexecuted checks remain explicitly waived rather than relabeled Passed. R-01/R-02 have prior explicit user acceptance; R-03 is an existing out-of-scope package-maintenance note, not a new S9 regression. S9 is Reviewed, not Done. The next workflow action is bmad-archive; it must not be combined with commit or merge.

## Archive Checkpoint (2026-10-30)

The user confirmed deferring R-03 and explicitly authorized backing up the unrelated runtime files, restoring only those files from HEAD, and archiving this record. mGBA was no longer running when the backup/restore began. Each backup was verified against its source with SHA-256 before restoring the two worktree files; all reviewed S9 changes were preserved.

| Original runtime file | Verified local backup | SHA-256 |
|---|---|---|
| pokemon_red.sav | [Saved runtime copy](../../../runs/s9-live/runtime-backup-20261030-154804-pokemon_red.sav) | 20639FE7450897E969E8EB8A1452E517224E0B46B5104D59EC666547E10CF8BF |
| nointro.sqlite3 | [Saved runtime copy](../../../runs/s9-live/runtime-backup-20261030-154804-nointro.sqlite3) | 2ED6BE48757864637CB4FEB91E426B5933A93791E22841912BDB8F6DEA003BE6 |

The record moved to the configured Story archive with all 33 existing relative links mechanically adjusted and their resolved targets verified unchanged. External canonical links were updated in the work registry, ADR-0003, and codebase map. The review status remains Reviewed; archive does not mark the story Done or change branch state. Backups and diagnostic evidence remain in ignored local run storage.

No files were staged or committed; no branch was created, merged, pushed, or deleted. The next operation is bmad-commit with its separate approval gate, covering the reviewed implementation and prepared archive together.

## Verification Source Retention (2026-10-30)

After archive preparation, the user requested retaining the manual verification programs in the repository. A narrow ignore-rule allowlist exposes six source files: Probe.csproj, Program.cs, RunConcurrent.ps1, and WithPreviews.ps1 under runs/s9-live, plus Preview.csproj and Program.cs under runs/s9-preview. Their responsibilities and build commands are listed in the [codebase map](../../CODEBASE_MAP.md). Earlier references to ignored probes describe their status when the measurements were recorded; captures, manifests, logs, build output, and verified runtime backups remain ignored local artifacts.

Both harness projects build successfully, and both PowerShell scripts parse without errors. No live measurements were rerun or changed, no files were staged or committed, and the story remains Reviewed pending the separate commit workflow.

## Commit Checkpoint (2026-10-30)

The user explicitly approved the message `feat(S9): add host-local vision frame pipeline` and the exact 36-file staging list, including the six verification source files. This commit records the reviewed implementation, prepared archive, source-retention changes, and workflow status together. S9 is Done and its branch is Committed; awaiting merge. The passing review, accepted verification exceptions, and existing deferred dependency advisory remain unchanged; the latest automated run passed all 30 tests with no failures or skips.

Push and merge are not authorized by this approval. Publish origin/story/s9-vision-project-frame-pipeline only after separate explicit approval, then use the separate merge workflow.