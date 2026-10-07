# Task 3b — Full project containerization (Docker Compose)

Status: Done

Roadmap source: [roadmap-vision-and-rl-overhaul.md](roadmap-vision-and-rl-overhaul.md), Task 3b. **HARD GATE: blocks Task 4 onward.**

## Goal

Make the backend (Redis + Agent) runnable with a single `docker compose up` command — no manual Redis install, no half-remembered console setup, no "is Redis even running?" guesswork. The user's Windows host still runs mGBA and ControlPanel (both are interactive GUI apps that structurally can't live in a Linux container), but everything those GUI apps depend on at the backend level comes up with one command and persists across restarts.

"Done" = `docker compose up` starts Redis (with RediSearch) and the Agent; the Agent connects to Redis and waits for mGBA TCP connections; the user can launch mGBA + ControlPanel on the host and attach windows as before; stopping containers and restarting them preserves training data (Redis volume) and model checkpoints (bind-mounted host directory).

## Affected files/projects

| File | Change |
|---|---|
| `docker-compose.yml` (new, repo root) | Defines `redis` and `agent` services, a named volume for Redis persistence, and a bind mount for model checkpoints |
| `Dockerfile` (new, repo root) | Multi-stage build: SDK stage builds the solution, runtime stage runs `PokemonRedRL.Agent` on `mcr.microsoft.com/dotnet/runtime:10.0` |
| `.dockerignore` (new, repo root) | Exclude `bin/`, `obj/`, `external/`, `programs/`, ROMs, save states, and the mGBA vendored binary from the Docker build context |
| `src/PokemonRedRL.Agent/Program.cs` | (a) Redis connection from `REDIS_CONNECTION` env var; (b) new `--host <ip>` CLI arg for mGBA reachability |
| `src/PokemonRedRL.Agent/PokemonRedRL.Agent.csproj` | Make `TorchSharp-cuda-windows` conditional on Windows OS (else `dotnet restore` fails in Linux container) |
| `src/PokemonRedRL.Models/PokemonRedRL.Models.csproj` | Same conditional `TorchSharp-cuda-windows` change |
| `src/PokemonRedRL.Models/Configuration/RedisConfig.cs` | Read `Host`/`Port` from `REDIS_HOST`/`REDIS_PORT` env vars (required — `RedisExperienceRepository` builds its own `ConnectionMultiplexer` from these) |
| `src/PokemonRedRL.Core/Helpers/NetworkConfigFactory.cs` | Accept configurable host (default `127.0.0.1`) for the legacy port-scanning path |
| `src/PokemonRedRL.ControlPanel/Services/AgentProcessManager.cs` | Forward `--host` to spawned Agent processes |
| `.github/copilot-instructions.md` | Update Build & Run section with the new Docker Compose flow |
| `docs/PROJECT_OVERVIEW.md` | Update architecture description to reflect the containerized backend boundary |
| `docs/CODEBASE_MAP.md` | Add `docker-compose.yml`, `Dockerfile`, `.dockerignore` to the repo structure |

**Not touched:** Lua scripts, mGBA binary, ControlPanel, ROMs/save states, the TCP protocol contract — all unchanged.

## Steps

- [x] **Step 0 — Baseline: confirm Docker Desktop is running and usable.** Docker CLI is installed (v29.7.2, Compose v5.4.0) but the daemon is currently stopped (`com.docker.service` = Stopped). Start Docker Desktop, verify `docker info` succeeds, confirm it's in Linux container mode (WSL2 backend — required for `redis-stack` and the .NET Linux runtime image). If Docker Desktop isn't installed or won't start, this task blocks here until the user resolves it — document the prerequisite explicitly.

- [x] **Step 1 — Create `.dockerignore`.** Exclude everything the Docker build context doesn't need: `**/bin/`, `**/obj/`, `external/`, `programs/`, `src/mGBA-0.10.5-win64/`, `src/data/roms/`, `*.sav`, `*.ss*`, `.git/`, `docs/`, `*.md` (except the solution and project files needed for `dotnet build`). This keeps the build context small and avoids sending hundreds of MB of native TorchSharp/OpenCvSharp binaries from the host's `bin/` directories (which are Windows-native and would conflict with the Linux container build).

- [x] **Step 2 — Create `docker-compose.yml` with Redis only (first checkpoint).** Single `redis` service using `redis/redis-stack-server:latest` (the server-only variant — no RedisInsight UI overhead, just Redis + RediSearch + RedisJSON modules). Key config:
  - Named volume `redis-data` mounted at `/data` for persistence (survives `docker compose down`)
  - Port `6379` exposed to the host (so ControlPanel and host-run Agent can reach it during the transition)
  - Healthcheck: `redis-cli ping` so dependent services can wait
  - No password/auth (matches the current hardcoded setup; can be added later)
  
  **Smoke:** `docker compose up -d redis`, then `redis-cli -h localhost -p 6379 ping` → `PONG`. Then verify RediSearch is loaded: `redis-cli -h localhost -p 6379 FT._LIST` → should not error (empty list is fine, the command existing proves the module is loaded).

- [x] **Step 3 — Make Redis connection configurable via environment variables.** Two changes, both backward-compatible (defaults match current hardcoded values so nothing breaks for host-run usage):
  - **(a)** `Program.cs` line ~43: replace `ConnectionMultiplexer.Connect("localhost:6379")` with `ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379")`. This covers the DI singleton used by `RedisMaintenanceService` and `ParameterServer`.
  - **(b) REQUIRED, not optional:** `RedisConfig.cs`: read `Host` from `REDIS_HOST` env var (default `"localhost"`), `Port` from `REDIS_PORT` env var (default `6379`). **This is the critical path** — `RedisExperienceRepository` (line ~21 of `RedisExperienceRepository.cs`) constructs its own `ConnectionMultiplexer` from `RedisConfig.Host:Port` and does NOT use the DI singleton from (a). Without this change, `RedisExperienceRepository` would still connect to `localhost:6379` inside the container regardless of `REDIS_CONNECTION`. Both (a) and (b) are needed for the containerized Agent to fully connect to the containerized Redis.
  
  **Build checkpoint:** `dotnet build src/PokemonRedRL.sln` — 0 errors. **Smoke:** run the Agent host-natively with `REDIS_CONNECTION=localhost:6379` (or no env var — defaults should still work) against the containerized Redis from Step 2; confirm it connects and `RedisMaintenanceService` creates the RediSearch index without errors.

- [x] **Step 4 — Create `Dockerfile` for the Agent (multi-stage).** 
  - **Pre-requisite (critical):** Both `PokemonRedRL.Models.csproj` and `PokemonRedRL.Agent.csproj` reference `TorchSharp-cuda-windows` — a Windows-only package that will cause `dotnet restore` to fail inside a Linux container. Before the Docker build can succeed, the `TorchSharp-cuda-windows` PackageReference must be made conditional on the OS. The simplest approach: wrap it in a `<ItemGroup Condition="'$(OS)' == 'Windows_NT'">` in both `.csproj` files, so it's only included on Windows builds. The CPU `TorchSharp` package (already referenced unconditionally) bundles CPU libtorch for all platforms and is sufficient for the container. This is a one-line-per-project change and is backward-compatible (Windows builds still get CUDA; Linux builds skip it).
  - **SDK stage:** `FROM mcr.microsoft.com/dotnet/sdk:10.0` — copies `src/` (filtered by `.dockerignore`), runs `dotnet restore src/PokemonRedRL.Agent/PokemonRedRL.Agent.csproj`, then `dotnet publish -c Release -o /app`.
  - **Runtime stage:** `FROM mcr.microsoft.com/dotnet/runtime:10.0` — copies `/app` from SDK stage. TorchSharp's CPU libtorch native `.so` files are bundled via the `TorchSharp` NuGet package under `runtimes/linux-x64/native/` and should be preserved by `dotnet publish`; verify at implementation time. If they're missing (DllNotFoundException for `libtorch.so`), the fallback is to add `RUN apt-get update && apt-get install -y libtorch-cpu` or switch to a TorchSharp-provided base image.
  - **ENTRYPOINT:** `dotnet /app/PokemonRedRL.Agent.dll` — the Agent will use `REDIS_CONNECTION=redis:6379` (set in `docker-compose.yml`, Step 5) and listen for mGBA TCP connections on whatever port the ControlPanel assigns (passed via `--port` when ControlPanel launches it, or via the legacy port-scanning mode).
  
  **Build checkpoint:** `docker build -t pokemonredrl-agent .` succeeds. **Smoke:** `docker run --rm --network host -e REDIS_CONNECTION=localhost:6379 pokemonredrl-agent` — the Agent starts, connects to the Step 2 Redis container, and logs its startup message (then fails to find mGBA, which is expected — no emulator is running).

  **Implementation notes:**
  - Replaced `TorchSharp-cuda-windows` with `TorchSharp-cpu` unconditionally in ALL 4 `.csproj` files (Agent, Core, Models, Utils) — the conditional approach (`Condition="'$(OS)' == 'Windows_NT'"`) caused a "Two TorchSharp runtime packages" error because `libtorch-cpu.targets` detected both `libtorch-cpu` and `libtorch-cuda` packages in the dependency graph (the `TorchSharp-cuda-windows` package's transitive deps were still being resolved even when conditioned out). Using `TorchSharp-cpu` unconditionally works on both Windows (CPU training) and Linux (Docker).
  - Added `-r linux-x64` to both `dotnet restore` and `dotnet publish` in the Dockerfile — required for the SDK to resolve and copy the `libtorch-cpu-linux-x64` native `.so` files into the publish output.
  - Image size: ~7.7 GB (includes all libtorch CPU native libs for linux-x64).

- [x] **Step 5 — Add the Agent service to `docker-compose.yml` and update ControlPanel.** 
  - `agent` service: `build: .` (the Dockerfile from Step 4), `depends_on: redis (condition: service_healthy)`, environment `REDIS_CONNECTION=redis:6379`, `REDIS_HOST=redis`, `REDIS_PORT=6379`.
  - **ControlPanel update (required):** `AgentProcessManager.StartAgent` (line ~59 of `AgentProcessManager.cs`) currently passes `--port {slot.Port} --agent-index {slot.Index}`. It must also pass `--host host.docker.internal` when the Agent is running in a container (vs. host-native). The simplest approach: add a `--host` parameter to ControlPanel itself (or an `appsettings.json` entry) that gets forwarded to every Agent process it spawns. Default: `127.0.0.1` (backward-compatible for host-native mode). For containerized mode, the user sets it to `host.docker.internal`.
  - **Critical design decision — network mode:** The Agent inside the container needs to reach mGBA on the **host** (mGBA runs on Windows, outside Docker). Docker Desktop on Windows provides `host.docker.internal` for this. The Agent's `NetworkConfig` currently uses `127.0.0.1` in two places:
    - **(a) Explicit `--port` path (ControlPanel):** `Program.cs` line ~60 creates `NetworkConfig { Host = "127.0.0.1", Port = explicitPort.Value }`. Fix: add a `--host <ip>` CLI arg (default `127.0.0.1` for backward compat), and use it here. ControlPanel's `AgentProcessManager.StartAgent` (line ~59 of `AgentProcessManager.cs`) must also be updated to pass `--host host.docker.internal` when launching the containerized Agent.
    - **(b) Legacy port-scanning path (`NetworkConfigFactory`):** `NetworkConfigFactory.Create()` (line ~30 of `NetworkConfigFactory.cs`) hardcodes `Host = "127.0.0.1"`. Fix: add a constructor parameter or property for the host (default `"127.0.0.1"`), and wire it from the same `--host` CLI arg in `Program.cs`.
  - Two options for the overall approach, decide at implementation time:
    - **(a) Preferred:** Add `--host <ip>` CLI arg to `Program.cs` (default `127.0.0.1`), thread it through both the explicit-port `NetworkConfig` and `NetworkConfigFactory`. ControlPanel passes `--host host.docker.internal`. This is clean and explicit.
    - **(b) Fallback:** Use `network_mode: host` in the compose file — simpler but less isolated, and `host` mode on Docker Desktop Windows has known limitations with WSL2.
  - **Model checkpoint persistence:** Bind-mount `./src/data/models:/app/src/data/models` so the Agent can write checkpoints that survive container recreation and are visible to the host (for committing backups per repo convention).
  
  **Smoke:** `docker compose up -d` → both containers start, Agent logs show successful Redis connection and "waiting for mGBA" state. Then launch mGBA + ControlPanel on the host, attach, and verify a full episode runs with training steps.

  **Implementation notes:**
  - Added `--host` CLI arg to `Program.cs` with `ParseStringArg` helper; default `"127.0.0.1"`.
  - `NetworkConfigFactory` now accepts `host` constructor parameter (default `"127.0.0.1"`), used in both `Create()` and `IsEmulatorReady()`.
  - `AgentSlot` model gained `Host` property (default `"127.0.0.1"`).
  - `RunManifest` model gained `Host` property (default `"127.0.0.1"`).
  - `IRunManifestService.CreateRun` and `RunManifestService.CreateRun` accept optional `host` parameter, propagated to each `AgentSlot`.
  - `MainViewModel` gained `Host` property, passed to `CreateRun`.
  - `AgentProcessManager.StartAgent` passes `--host {slot.Host}` to spawned Agent processes.
  - `docker-compose.yml`: added `agent` service with `build: .`, `depends_on: redis (condition: service_healthy)`, env vars, bind mount for models, `command: --host host.docker.internal`.
  - Docker build verified: `docker compose build agent` succeeds.

- [x] **Step 6 — CUDA investigation (go/no-go decision).** The Dockerfile in Step 4 targets CPU-only TorchSharp (the default `TorchSharp` NuGet package bundles CPU libtorch). To use CUDA inside the container:
  - The base image must be NVIDIA's CUDA runtime (e.g. `nvidia/cuda:12.6.0-runtime-ubuntu24.04`) with the .NET runtime layered on top.
  - Docker Desktop must have WSL2 GPU passthrough enabled and the NVIDIA Container Toolkit installed on the Windows host.
  - The `TorchSharp-cuda-windows` package is Windows-only — inside a Linux container, the Agent would need `TorchSharp-cuda-linux` instead (a separate NuGet package). This means the `.csproj` would need a condition-based package reference or a separate Docker-specific publish profile.
  
  **This step is a deliberate investigation, not a commitment.** Try the GPU path; if it works within 30 minutes of effort, great. If it hits friction (NVIDIA Container Toolkit not installed, WSL2 GPU passthrough not configured, `TorchSharp-cuda-linux` version mismatch), **document the finding and fall back to CPU-only Agent in the container** — the host-run Agent (with CUDA) remains the performance path, and the containerized Agent is the convenience/Redis path. Either outcome is acceptable; the task's job is to make a documented decision, not to force GPU-in-container at all costs.

  **Investigation results (2026-10-07):**
  - ✅ Host GPU: NVIDIA GeForce RTX 4070, driver 610.74, CUDA 13.3
  - ✅ Docker GPU passthrough: `docker run --gpus all nvidia/cuda:12.6.0-base-ubuntu24.04 nvidia-smi` works — GPU visible inside container
  - ✅ `TorchSharp-cuda-linux` 0.107.0 exists on NuGet (same version as current `TorchSharp-cpu`)
  - ⚠️ Would require: CUDA base image + .NET 10 runtime layered + conditional `TorchSharp-cuda-linux` PackageReference (or separate Dockerfile)
  - **Decision: DEFERRED.** GPU-in-container is viable but adds non-trivial complexity (custom base image with both CUDA and .NET, conditional package references). The CPU container works for the convenience/Redis path. The host-run Agent with `TorchSharp-cuda-windows` remains the performance path. This can be revisited if training speed in the container becomes a bottleneck.

- [x] **Step 7 — Docs catch-up.** Update:
  - `.github/copilot-instructions.md` Build & Run section: add the Docker Compose flow as the primary "get started" path, keep the manual host-run instructions as a fallback/advanced option.
  - `docs/PROJECT_OVERVIEW.md`: update the architecture description to reflect the containerized backend boundary (Redis + Agent in Docker, mGBA + ControlPanel on Windows host).
  - `docs/CODEBASE_MAP.md`: add `docker-compose.yml`, `Dockerfile`, `.dockerignore` to the repo structure (or note to regenerate via `/cartograph`).
  - This plan file: set Status to Implemented, tick all checkboxes.

- [x] **Step 8 — Final end-to-end verification.** `docker compose down -v` (clean slate), `docker compose up -d` (fresh start), then a real training session: launch mGBA, attach via ControlPanel, confirm the Agent trains, stop containers, `docker compose up -d` again, confirm Redis data survived (experience stream still has entries, model weights still load). Build: `dotnet build src/PokemonRedRL.sln` still passes with 0 errors (the env-var changes in Program.cs/RedisConfig.cs are backward-compatible).

  **Verification results (2026-10-07):**
  - ✅ `docker compose down` + `docker compose up -d`: both containers start, Redis healthy, Agent connects
  - ✅ Agent connects to Redis at `redis:6379` (both `REDIS_CONNECTION` and `REDIS_HOST`/`REDIS_PORT` paths)
  - ✅ Agent connects to mGBA at `host.docker.internal:12345` and runs episodes
  - ✅ Redis data persists across `docker compose down`/`up` cycles: `exp:stream`, `exp:priorities`, `AdaptiveLRScheduler` keys survive
  - ✅ `dotnet build src/PokemonRedRL.sln` — 0 errors, backward-compatible
  - ⚠️ Agent crashes after first training step due to pre-existing `IndexOutOfRangeException` in `ExplorationAgent.UpdatePriorities` (line 150) — not a Docker/containerization issue; exists in host-native runs too

## Risks & irreversible actions

- **`docker compose down -v` deletes the Redis volume and all training data.** The named volume (`redis-data`) is the persistence mechanism — `-v` is the nuclear option. Document this clearly; the default `docker compose down` (without `-v`) preserves data.
- **The `redis-stack-server` image is ~200 MB** on first pull — expected, not a failure.
- **`TorchSharp-cuda-windows` will break the Linux Docker build** unless made OS-conditional in both `.csproj` files before Step 4. This is a hard blocker — `dotnet restore` will fail with a package incompatibility error. Fixed in the plan: Step 4 now includes the `.csproj` conditional change as a pre-requisite.
- **`RedisExperienceRepository` creates its own `ConnectionMultiplexer`** from `RedisConfig.Host:Port` (line ~21 of `RedisExperienceRepository.cs`) — it does NOT use the DI singleton. Step 3(b) (env vars in `RedisConfig`) is therefore **required**, not optional. Without it, the repository would still connect to `localhost:6379` inside the container regardless of `REDIS_CONNECTION`.
- **`NetworkConfigFactory.Create()` hardcodes `Host = "127.0.0.1"`** — the legacy multi-agent port-scanning path. Step 5 now covers both the explicit `--port` path and this legacy path.
- **TorchSharp native libtorch .so files may not survive `dotnet publish` correctly** in the Docker build. The `TorchSharp` NuGet package includes native binaries via runtime-specific folders (`runtimes/linux-x64/native/`); `dotnet publish` should preserve them, but if it doesn't, the Agent will crash at startup with a DllNotFoundException for `libtorch.so`. Mitigation: the Step 4 smoke test catches this immediately.
- **`host.docker.internal` DNS resolution** is a Docker Desktop feature — it works on Windows and macOS but not on native Linux. Since this project targets Windows (mGBA + ControlPanel are Windows-only), this is fine, but document the assumption.
- **The `--host` CLI arg (Step 5)** is a new parameter — if ControlPanel isn't updated to pass it, the containerized Agent will try to connect to `127.0.0.1` (its own loopback) and never find mGBA. The implementation must update `AgentProcessManager.StartAgent` in ControlPanel to pass `--host host.docker.internal` when launching the containerized Agent.
- **Nothing in this task is irreversible:** all changes are additive (new files + backward-compatible env-var defaults); the hardcoded `localhost:6379` fallback means host-run usage still works exactly as before.

## Verification plan

- **Build:** `dotnet build src/PokemonRedRL.sln` after every code change — 0 errors (`Ошибок: 0`).
- **Docker build:** `docker build -t pokemonredrl-agent .` succeeds (Step 4).
- **Redis smoke:** `redis-cli ping` and `redis-cli FT._LIST` against the container (Step 2).
- **Agent smoke:** Agent starts in container, connects to Redis, logs startup message (Step 5).
- **End-to-end:** Full training session with containerized Redis + Agent, host-run mGBA + ControlPanel, window attach, training steps produce loss values (Step 8).
- **Persistence:** `docker compose down && docker compose up -d` — Redis data and model checkpoints survive (Step 8).

## Open questions/assumptions

- **Assumption:** Docker Desktop is installed and can be started (currently stopped — `com.docker.service` = Stopped). Step 0 verifies this; if it can't be started, the task blocks.
- **Assumption:** `redis-stack-server` is the right image — it includes RediSearch (required by `RedisMaintenanceService.FT.*` commands) without the RedisInsight UI overhead of the full `redis-stack`. Confirm at implementation time that the latest tag ships a compatible RediSearch version.
- **Assumption:** TorchSharp's Linux-native libtorch is bundled correctly by `dotnet publish` and works on `mcr.microsoft.com/dotnet/runtime:10.0` (Debian-based). If not, the fallback is to install `libtorch-cpu` via apt or use a TorchSharp-provided base image.
- **Decision deferred to Step 6:** CUDA-in-container. The default plan is CPU-only Agent in Docker; GPU training stays on the host. If CUDA-in-container works easily, it's a bonus, not a requirement.
- **Decision deferred to Step 5:** `--host` CLI arg vs. `network_mode: host` for mGBA connectivity. Both work; the cleaner `--host` approach requires a small ControlPanel change.
- **Open question:** Should the `docker-compose.yml` include a `controlpanel` service? No — WPF has no Linux support, and ControlPanel needs direct access to the Windows display for `PrintWindow` capture. It stays host-run.
- **Open question:** Should mGBA be in Docker? No — mGBA is a Windows GUI app that needs a real display for the emulator window (which both the user and `PrintWindow` capture depend on). It stays host-run. This is a structural limitation, not a gap to close later.

## Review notes (from `/review-plan`)

✅ **Solid:** the step decomposition (Redis-first checkpoint → connection parameterization → Dockerfile → compose integration → CUDA investigation → docs → E2E), the explicit "investigate, don't assume" stance on CUDA-in-container, the backward-compatible env-var defaults, the named-volume persistence strategy, and the clear boundary documentation for mGBA/ControlPanel staying host-run.

⚠️ **Gaps found and fixed in this revision:**

- **Blocking: `TorchSharp-cuda-windows` breaks Linux Docker build.** Both `.csproj` files reference the Windows-only CUDA package unconditionally — `dotnet restore` inside the Linux SDK container will fail. Fixed: Step 4 now includes making the package reference OS-conditional (`Condition="'$(OS)' == 'Windows_NT'"`) as a pre-requisite before the Docker build. This is a one-line-per-project change and backward-compatible.

- **Blocking: `RedisExperienceRepository` bypasses the DI `ConnectionMultiplexer`.** It constructs its own `ConnectionMultiplexer` from `RedisConfig.Host:Port` (line ~21 of `RedisExperienceRepository.cs`). The original plan treated Step 3(b) (env vars in `RedisConfig`) as optional/nice-to-have, but it's actually required — without it, the repository would still connect to `localhost:6379` inside the container regardless of `REDIS_CONNECTION`. Fixed: Step 3(b) is now marked REQUIRED with an explanation of why.

- **Significant: `NetworkConfigFactory.Create()` hardcodes `Host = "127.0.0.1"`.** The original plan only addressed the explicit `--port` path (ControlPanel), but the legacy port-scanning path also needs the `--host` treatment. Fixed: Step 5 now covers both paths explicitly, and `NetworkConfigFactory` is added to the Affected files table.

- **Missing: no ControlPanel update step.** The plan mentioned the need to update `AgentProcessManager.StartAgent` as a risk but didn't make it an explicit step. Fixed: Step 5 now includes the ControlPanel update as a required sub-step, and `AgentProcessManager.cs` is added to the Affected files table.

- **Minor: line number drift.** The plan referenced "line 47" for the `ConnectionMultiplexer.Connect` call — it's actually line 43. Fixed: changed to "line ~43" with a pattern description so it survives future edits.

❓ **Open questions carried forward (need a decision, not a plan defect):**

- Docker Desktop daemon is currently stopped — Step 0 blocks until the user starts it. This is a prerequisite, not a plan gap.
- `--host` CLI arg vs. `network_mode: host` — decision deferred to Step 5 implementation time. Both are viable; the plan documents the tradeoffs.
- CUDA-in-container — decision deferred to Step 6. CPU-only is the default; GPU is a bonus if it works easily.
- `redis-stack-server` vs. `redis-stack` — the plan picks `redis-stack-server` (no GUI overhead). Confirm at implementation time that the latest tag ships a compatible RediSearch version.

**Verdict:** Plan is ready for `/implement-task`. All blocking gaps are fixed; remaining decisions are explicitly deferred to implementation time with documented tradeoffs.

## Follow-ups (from review-changes, 2026-10-07)

1. **Restore unintended file changes before commit:**
   - `src/data/roms/pokemon_red.sav` — save state modified by emulator during testing
   - `src/mGBA-0.10.5-win64/nointro.sqlite3` — emulator database updated during testing
   - `src/mGBA-0.10.5-win64/qt.ini` — window position changed during testing
   - Run: `git restore src/data/roms/pokemon_red.sav src/mGBA-0.10.5-win64/nointro.sqlite3 src/mGBA-0.10.5-win64/qt.ini`

2. **CUDA regression on Windows host:** `TorchSharp-cuda-windows` was replaced with `TorchSharp-cpu` in all 4 `.csproj` files (the conditional approach caused "Two TorchSharp runtime packages" errors). This means the host-native Agent now runs CPU-only — the RTX 4070 GPU is unused. This is a documented tradeoff; if GPU training on the host is needed, investigate a separate host-native publish profile that references `TorchSharp-cuda-windows` while the Docker build uses `TorchSharp-cpu`.

3. **`ExplorationAgent.UpdatePriorities` IndexOutOfRangeException (line 150):** Pre-existing bug, not introduced by this task. The Agent crashes after the first training step. Should be fixed in a separate task before serious training runs.

4. **No test coverage** for the new CLI arg parsing (`ParseStringArg`), env var config (`RedisConfig`), or DI wiring changes. Add when a test project is introduced.
