# PokemonRedRL — Project Overview

> **Purpose of this document:** explain *what this project is trying to do, why it's built the way it is, and where the current implementation is known to be incomplete or inconsistent* — enough context for a human or an AI agent to reason about changes without having to reverse-engineer intent from code alone. For a file-by-file inventory, see [docs/CODEBASE_MAP.md](CODEBASE_MAP.md) (this document does not repeat that content, only links to it).

## 1. Problem statement / motivation

The goal is to train a reinforcement-learning agent that plays **Pokémon Red** (and, per `src/data/roms/pokemon_firered.gba`, potentially Pokémon FireRed) end-to-end from raw game state, without hand-coded scripted behavior. Unlike training against a purpose-built RL environment (e.g. an Atari/Gym wrapper), the agent drives a *real, unmodified* Game Boy ROM running inside the **mGBA** emulator, reading and writing game memory through a small Lua script. This constrains the whole architecture:

- The "environment" is a live emulator process, not an in-process simulator — every state read/action write is a network round trip, which is why the C# side is built around explicit connect/retry logic (`ConnectionManager`) rather than a simple function call.
- Reward and state must be derived from raw memory addresses (HP, map ID, badges, money, party stats, battle flags) reverse-engineered for Pokémon Red, because the game has no RL-friendly API of its own.
- Because a single emulator instance can only run one game at a time, scaling up training means running **multiple mGBA instances in parallel**, each paired with its own agent process/thread, sharing learned knowledge through a central store (Redis) rather than in-process shared memory.
- The intended operator experience is a dedicated **desktop control panel**: one application that can launch/attach many emulator+agent pairs, display them in an adaptive paged emulator wall, supervise training, and provide the stable process/window/port manifest needed by future computer-vision capture.

## 2. High-level RL approach

- **Algorithm:** Deep Q-Network (DQN) with a target network (`PokeDQN` + a periodically-synced copy), trained on transitions `(state, action, reward, next_state, done)` — see `DQNTrainer.TrainStep`.
- **Policy network:** a small 3-layer MLP (`Linear(14→128) → ReLU → Linear(128→64) → ReLU → Linear(64→6)`), reflecting a deliberately low-dimensional hand-crafted state vector rather than raw pixels — see `StatePreprocessorService.StateToTensor` and `PokeDQN`.
- **Exploration:** epsilon-greedy, epsilon decays exponentially each step (`ExplorationAgent.GetEpsilon`, from `1.0` down to `0.01`) based on the trainer's global step count, not per-episode.
- **Experience storage:** experience is **not** kept only in local memory — every transition is pushed straight to **Redis** via `IExperienceRepository` (Redis Streams for raw storage + a Sorted Set for priorities), so multiple concurrent agent processes/threads all read and write the same shared pool. This is why `ExperienceReplay`/`MemoryCache`/`SumTree` (local, in-process alternatives to the same idea) exist in `PokemonRedRL.Models/Services/` but are **not currently wired up anywhere** — they read like an earlier, single-process design that was superseded by the Redis-backed repository without being deleted. Treat them as historical/unused unless you're intentionally reviving a local-replay-buffer path.
- **Prioritized replay:** priorities are derived from TD-error magnitude (`Math.Abs(tdError)`), sampled with ~70% prioritized / ~30% uniform-random mix (`ExplorationAgent.TrainWithAutoBatch`, `LoadInitialExperienceAsync`) to balance exploiting high-error transitions against avoiding overfitting to a narrow experience slice.
- **Multi-agent weight sharing:** `ParameterServer` periodically pushes/pulls the full model `state_dict` to/from a single Redis key (`DQNTrainer.SyncWithGlobalModel`), so independently-running agents converge toward one shared policy instead of training N unrelated models. `AdaptiveLRScheduler` similarly persists a shared, reward-driven learning-rate schedule in Redis so all agents adapt learning rate together based on a smoothed global reward signal (`ExplorationAgent`/`DQNTrainer.TrackEpisodeReward`).

## 3. Why the project is split into 5 assemblies

| Project | Why it's separate |
|---|---|
| `PokemonRedRL.DAL` | Pure data shapes (`GameState`, `GameStateResponse`, `PokemonData`) with no behavior — kept dependency-free so both the emulator layer and the RL layer can reference the same types without pulling in TorchSharp/Redis/sockets. |
| `PokemonRedRL.Utils` | Genuinely cross-cutting code with no domain logic of its own: enums shared by both the C# and Lua sides (`Buttons`, `ActionType`), JSON (de)serialization, TorchSharp tensor byte (de)serialization. Deliberately has no dependency on `Core` or `Models` so it can be referenced from anywhere without cycles. |
| `PokemonRedRL.Core` | Everything about *talking to the emulator* and turning raw state into RL-ready signals: the TCP/socket protocol, the emulator client, reward shaping, state normalization. This is the layer that would need to change if the bridge protocol changed or a different emulator/game were targeted. |
| `PokemonRedRL.Models` | Everything about *learning*: the network, the trainer, prioritized replay, Redis-backed experience/parameter storage. Deliberately emulator-agnostic — nothing in here references sockets, mGBA, or Lua. |
| `PokemonRedRL.Agent` | The composition root: wires up DI, decides how many parallel agents to run, and owns the episode loop (`ExplorationAgent`) that ties `Core` (perception/action) and `Models` (learning) together. |

This mirrors a fairly standard "ports and adapters" split: `Core` is the adapter to the outside world (emulator), `Models` is the domain/algorithm, `DAL`/`Utils` are shared primitives, `Agent` is the entry point.

## 4. The emulator bridge protocol and its constraints

- Transport: a single newline-terminated **plain-text TCP** protocol (`SocketProtocol.SendCommand`) — write a command + `\n`, read until a `\n`-terminated response. No framing beyond the newline, no binary length prefix, no authentication, no TLS (acceptable only because it's `127.0.0.1`-only, single-machine).
- Commands recognized by the current authoritative Lua script (`src/mGBA-0.10.5-win64/scripts/mgba_socket.lua`): `get_state` (returns a JSON-encoded full `GameState`), one command per `Buttons` value (queues a timed button press for a fixed number of frames — see `enqueueButtonPress`/`BUTTON_PRESS_DURATION`), `ping` (health check used by `NetworkConfigFactory` to find a live mGBA instance), `disconnect`.
- Port discovery: rather than a fixed port per agent, `NetworkConfigFactory.Create()` **scans** a port range (`basePort..basePort+maxAttempts`) sending a raw `ping`/`pong` handshake to find a currently-idle, already-running mGBA instance — i.e. the operator is expected to have already launched N mGBA processes listening on sequential ports (see `launch_agents.bat`) before starting the .NET agents.
- Any change to this protocol is a two-sided change: the command vocabulary, the response shape, and the state field list must stay in lockstep between `SocketProtocol`/`MGBAEmulatorClient`/`GameStateSerializer`/`GameStateResponse` (C#) and the Lua script's `frame_callback` (mGBA). There's no schema or contract test enforcing this today — see §6.
- There is a single authoritative bridge script, `src/mGBA-0.10.5-win64/scripts/mgba_socket.lua`. An older, non-authoritative `src/scripts/mgba_socket.lua` variant (plain CSV protocol, no JSON, immediate non-queued button presses) previously existed alongside it and has been removed as part of the pre-overhaul cleanup — it was never loaded by any running mGBA instance in practice.

## 5. Key design tradeoffs (and why they may be worth revisiting)

- **Hardcoded infrastructure config:** Redis address (`localhost:6379`) and the mGBA port-scan range are hardcoded in `Program.cs`/`RedisConfig`/`NetworkConfigFactory` rather than read from `appsettings.json`. Fine for a single-machine dev setup; would need externalizing before running agents/Redis on separate machines.
- **No automated tests anywhere in the solution.** Given the emulator dependency, true integration tests are hard, but the pure-logic pieces (`RewardCalculatorService`, `StatePreprocessorService`, `SumTree`, TD-error math in `DQNTrainer`) are unit-testable in isolation today and currently aren't covered at all.
- **Fixed, hand-picked 14-float state vector** (`StatePreprocessorService`) rather than a learned/embedded representation — simple and fast, but caps how much game state the agent can actually condition on (no visited-map memory beyond the current episode's `visitedLocations` set, no inventory, no move data despite `PokemonData` already capturing per-mon stats).
- **Normalization constants are ad hoc** (`x / 20f`, `y / 20f`) and already flagged in-code (`// todo:` comments in `StatePreprocessorService`) as not reflecting the real per-map coordinate ranges — positions on larger maps will normalize outside `[0,1]`.
- **Duplicate `IExperienceRepository` DI registration** in `Program.cs` (`AddScoped` at one line, `AddSingleton` at another, further down) — the last registration wins, so it behaves as a `Singleton` in practice, but this is not obviously intentional from reading the code and should be cleaned up in a dedicated DI-lifetime review rather than assumed to be correct.

## 6. Known gaps / open questions (for future review or an AI agent to act on)

These were identified while writing this document and are recorded here rather than silently fixed, because each one changes observable behavior:

1. **`ExperienceReplay`, `MemoryCache`, `SumTree` appear unused** by the current Redis-backed data path — confirmed unused by the pre-overhaul cleanup pass; kept as-is rather than deleted, in case they become the seed of a future local-buffer optimization. Not currently wired up anywhere.
2. **`DQNTrainer.SelectAction` allocates a `new Random()` per call** — statistically harmless but wasteful and non-reproducible; consider a single shared/seedable `Random` instance if run-to-run reproducibility ever matters.
3. **No automated verification of the emulator protocol contract** — a change to `GameStateResponse`'s shape or the Lua JSON response would only be caught at runtime. A lightweight contract test (serialize a sample JSON payload matching the Lua script's `state` table, deserialize with `GameStateSerializer`) would catch drift cheaply.

> **Fixed by the pre-overhaul cleanup task** ([docs/tasks/pre-overhaul-cleanup.md](tasks/pre-overhaul-cleanup.md)): the `RewardCalculatorService` badge/level reward-shaping bug (now updates `_previousBadges`/`_previousLevels` correctly), the duplicate `IExperienceRepository` DI registration (now a single `Singleton`), the diverged `mgba_socket.lua` copy (the stale `src/scripts/mgba_socket.lua` was deleted — see §4), and the hardcoded absolute Lua path (now resolved relative to the script's own location).

## 7. Target direction: desktop-supervised, computer-vision-augmented agent (planned, not yet implemented)

> Everything in this section is **aspirational** — it describes where the project is headed, not the current state described in §1–6. It exists so any contributor/agent understands the destination before touching the architecture. The concrete, ordered breakdown of how to get there lives in [docs/tasks/roadmap-vision-and-rl-overhaul.md](tasks/roadmap-vision-and-rl-overhaul.md).

**Goal:** an agent capable of actually completing Pokémon Red over a long training run, not just wandering/leveling. Getting there requires two changes beyond what exists today:

0. **Desktop control plane for training operations.** `PokemonRedRL.ControlPanel` (WPF, `net8.0-windows`) exists and implements the MVP described in [docs/tasks/automated-mgba-launch-and-fast-forward.md](tasks/automated-mgba-launch-and-fast-forward.md): a run manifest (`runs/<timestamp>/manifest.json`) with one isolated runtime directory per slot, one `PokemonRedRL.Agent` process per emulator slot (`--port`/`--agent-index` CLI mode added to `Program.cs`), an adaptive 1–9 paginated emulator wall with `PrintWindow`-based previews and parsed per-tile telemetry, and manual-attach emulator/window linking. **Known, deliberately-recorded limitation:** automated mGBA launch (auto-starting instances + auto-loading the bridge script) is currently blocked — neither the vendored 0.10.5 binary nor a locally-built upstream binary support the needed `--script` automation on this workspace, so the shipped mode is manual-attach only (user starts mGBA + loads the script by hand; the control panel discovers/monitors/tears down from there). This replaces the previous manual Tools → Scripting workflow and broken `launch_agents.bat` assumption, but does not yet remove the manual mGBA-startup step itself.

1. **Computer vision as a first-class input.** Today the agent only sees a hand-picked, low-dimensional memory-derived vector (14 floats). It has no visual awareness at all — it can't see obstacles, NPCs, dialogue boxes, menus, or battle UI state except via specific memory addresses someone already reverse-engineered. Adding a rendered-frame input (via screen capture from mGBA) lets the agent perceive the game more like a human would, and — importantly for training efficiency — lets reward shaping use *visual* novelty/progress signals (e.g. "this frame looks meaningfully different from recent frames") instead of relying solely on map/coordinate bookkeeping.
2. **A reviewed and likely upgraded RL approach.** The current single-network DQN + prioritized replay + Redis-shared weights setup is a reasonable baseline but was never benchmarked against alternatives (Double/Dueling DQN improvements, n-step returns, PPO, etc.). Before scaling training time/compute, the algorithm itself needs a deliberate review, not just a bigger network.

**Recommended default architecture (subject to confirmation during `/review-plan` on the roadmap):** a **hybrid** state representation — keep the existing memory-derived feature vector (it's cheap, precise, and already reverse-engineered) and *add* a CNN branch fed by captured frames, fused before the Q-head. Pure pixels-only (classic Atari-style DQN) is not recommended as a full replacement: Pokémon Red's win conditions (badges, party state, battle outcomes) are exact and already trivially readable from memory — throwing that away and re-deriving it from pixels would be strictly harder to learn for no benefit. Vision's value here is *supplementary* spatial/contextual awareness and reward shaping, not a replacement for the reliable memory-based signals.

**The frame-capture mechanism is now decided (resolved by the Task 1 spike, [docs/tasks/frame-capture-spike.md](tasks/frame-capture-spike.md)):** mGBA 0.10.5's Lua scripting API does **not** expose a live in-memory framebuffer read — the only built-in capture primitive is `emu:screenshot(filename)` (note: the global is `emu`, a `CoreAdapter`, **not** `core` — the official docs list `screenshot` under the `Core` class but there is no top-level `core` object), which writes a PNG to disk. That was benchmarked against direct OS-level window capture (`PrintWindow`) with real, measured results:

| Mechanism | Measured sustained rate | Verdict |
|---|---|---|
| `emu:screenshot()` + C# file read | **~1.0 Hz** (avg ~1000ms/call) | ❌ Fails even the 1x-speed bar (~4 Hz) by 4x |
| OS-level window capture (`PrintWindow` via P/Invoke) | **~141–145 Hz**, no degradation at 3 concurrent windows | ✅ Clears the 10x-fast-forward bar (~40 Hz) with ~3.5x headroom |

**Decision: computer vision will be implemented via direct OS-level window capture (`PrintWindow`), not the Lua/screenshot bridge.** This is a deliberately different mechanism from the existing memory-state TCP bridge — vision capture will run independently, correlating each captured window to its agent via the process handle obtained at launch time (not window title matching, since titles aren't distinguishable across instances today).

**Important side-findings from this spike, relevant beyond vision:**
- **`src/mGBA-0.10.5-win64/launch_agents.bat` does not work as written.** mGBA 0.10.5 has no `--port` or `--script` CLI flag (confirmed via `mGBA.exe --help`); passing them causes mGBA to print `unknown option -- port` and the script never loads. The batch file's `--port`/`--script` invocation was never actually functional.
- **The real, current workflow is manual**: `qt.ini`'s `[recentScripts]` lists numbered per-instance script copies (`mgba_socket_10.lua`, etc.) in the gitignored `additional_scripts/` folder, indicating scripts are loaded by hand via Tools → Scripting in each window, not automated.
- **There is no scripted/headless way to load a Lua script in mGBA 0.10.5** — no CLI flag, no config key. External keyboard-simulation automation (`SendInput`/`keybd_event` targeting the Tools menu) was attempted and did not reliably work from an external process, a known category of Windows synthetic-input limitation, not a simple bug.
- This means **reliable, unattended multi-instance training launch is currently a real, unsolved production gap**, independent of vision — see the roadmap's Task 2.

## 8. Where to go next

- File-by-file structure: [docs/CODEBASE_MAP.md](CODEBASE_MAP.md).
- Repo-wide conventions and the plan → review → implement → review → commit workflow: [.github/copilot-instructions.md](../.github/copilot-instructions.md).
- Historical record of *this* refactor/documentation pass, including what was and wasn't changed and why: [docs/tasks/repo-refactor-and-project-overview-doc.md](tasks/repo-refactor-and-project-overview-doc.md).
- Ordered breakdown of the computer-vision + RL overhaul described in §7: [docs/tasks/roadmap-vision-and-rl-overhaul.md](tasks/roadmap-vision-and-rl-overhaul.md).
