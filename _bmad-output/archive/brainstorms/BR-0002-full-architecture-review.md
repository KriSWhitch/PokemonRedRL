# BR-0002 — Brainstorm: Full Project Architecture Review

**ID:** BR-0002
**Status:** Archived
**Disposition:** Incorporated
**Incorporated into:** [I1 Initiative](../../../docs/initiatives/I1-computer-vision-rl-overhaul.md), [E1 Vision](../../../docs/epics/E1-vision-perception.md), [E2 RL](../../../docs/epics/E2-rl-training-and-models.md), [E3 Emulator](../../../docs/epics/E3-emulator-integration-runtime.md), [E4 Reliability](../../../docs/epics/E4-reliability-validation.md), [ADR-0001](../../../docs/adr/ADR-0001-vision-representation.md), [ADR-0002](../../../docs/adr/ADR-0002-rl-algorithm-review.md)
**Archived:** 2026-10-30
**Date:** 2026-10-11
**Stance:** Ideate for Me (Mary 📊 — independent analysis)
**Participants:** AI (Mary — Analyst persona)
**Scope:** Emulator choice, ML framework, ControlPanel, training approach, dependencies, Docker-first deployment, Kafka vs Redis, P/Invoke vs Lua

---

## Topic Statement

Full architecture review: is mGBA still the best emulator? Is TorchSharp the best ML framework for C#? Do we need OpenCV? How should ControlPanel evolve? What training approach is optimal? Can everything run from a single Docker container? Is Kafka better than Redis? Should we replace Lua with P/Invoke? Everything is on the table except the core constraint: mGBA-class emulator + C#.

---

## Executive Summary

**The project's foundation is solid. No revolution needed — targeted evolution.**

- ✅ **mGBA stays.** It's the best Game Boy emulator with a scripting API. No viable alternative exists.
- ✅ **TorchSharp stays.** It's the only production-ready RL framework for C#. ML.NET doesn't do RL.
- ❌ **OpenCV goes.** `torchvision.transforms` covers all preprocessing needs. One less native dependency.
- 🟡 **ControlPanel evolves.** Headless mGBA + screenshot grid in one window instead of 20 separate windows.
- 🟡 **PPO on the horizon.** DQN is fine for now; PPO + LSTM is the natural next step after vision works.
- 🟡 **P/Invoke to mGBA core** is the long-term architectural ideal — eliminates TCP and Lua overhead.

---

## Top Candidates (after pre-mortem)

### 1. Keep mGBA — it's the best choice (🟢 — Risk: LOW)

**What:** mGBA remains the emulator. No migration to PyBoy, BizHawk, SameBoy, or RetroArch.

**Evidence:**
- mGBA has the most powerful Lua scripting API of any Game Boy emulator — full memory access, frame callbacks, button injection
- Our Lua bridge (`mgba_socket.lua`) is already working and validated at ~141-145 Hz frame capture
- PyBoy is slower (Python), has no C# integration path
- BizHawk is C# but its Lua API is weaker for memory reading, and it's heavier (TAS-focused)
- SameBoy has no scripting API
- RetroArch/libretro would require C FFI and loses the simplicity of Lua

**Why it could fail:** mGBA 0.10.5 has no `--script` flag (confirmed in T2b), making automated launch harder. But this is a convenience issue, not a blocker.

**Early warning signs:** mGBA project goes unmaintained (last release: 0.10.5, 2024 — still active).

**Recommendation:** Stay. If mGBA ever becomes unmaintained, SameBoy + custom C FFI is the fallback.

---

### 2. Keep TorchSharp, remove OpenCV (🟢 — Risk: LOW)

**What:** TorchSharp stays as the sole ML framework. OpenCvSharp4 is removed — `torchvision.transforms` handles all frame preprocessing.

**Evidence:**
- ML.NET does NOT support reinforcement learning or custom neural network architectures
- TensorFlow.NET is less mature for .NET 10 and has a smaller community
- Python subprocess (Stable-Baselines3) adds latency and complexity — two runtimes, serialization overhead
- TorchSharp is a .NET Foundation project, actively maintained, tracks PyTorch releases
- `torchvision.transforms.Resize`, `.Grayscale`, `.Normalize` cover 100% of needed frame preprocessing
- OpenCV would only be needed for advanced CV (contour detection, template matching) — not in current scope

**Why it could fail:** TorchSharp could lag behind PyTorch on critical CUDA versions. Mitigation: pin LibTorch and CUDA versions.

**Early warning signs:** TorchSharp release cycle falls >3 months behind PyTorch.

**Recommendation:** Remove `OpenCvSharp4` package reference. Use `torchvision.transforms` pipeline for frame preprocessing. One less native dependency = fewer version conflicts.

---

### 3. Headless mGBA + P/Invoke (long-term) (🟡 — Risk: MEDIUM)

**What:** Instead of running mGBA as a GUI process with Lua TCP bridge, compile mGBA as a headless library and call its C API directly from C# via P/Invoke. No TCP, no Lua, no GUI windows.

**Evidence:**
- mGBA is a C library at its core; the Qt/SDL GUI is a separate layer
- mGBA's `core/` directory exposes functions like `mCoreInitConfig`, `mCoreLoadROM`, `mCoreRunFrame`, memory read/write
- P/Invoke would eliminate: TCP latency, Lua GC pauses, JSON serialization overhead, 20 GUI windows
- This is how PyBoy works — it's a Python wrapper around a C emulator core

**Why it could fail:** 
- mGBA's C API is not documented as a public API — it's internal
- Building mGBA as a DLL for Windows requires CMake configuration changes
- API stability not guaranteed between mGBA versions
- Significant upfront investment (2-4 weeks)

**Early warning signs:** mGBA core API changes break the P/Invoke layer on every update.

**Recommendation:** Spike task (1-2 days): try to build mGBA as a DLL, call `mCoreInitConfig` + `mCoreLoadROM` from C#. If it works, plan full migration. If not, TCP+Lua is fine.

---

### 4. ControlPanel: screenshot grid instead of 20 windows (🟡 — Risk: LOW)

**What:** ControlPanel gets screenshots from each agent via the existing TCP protocol (add a `screenshot` command to Lua), renders them in a grid layout. mGBA windows can be minimized or run headless. One window to rule them all.

**Evidence:**
- User explicitly wants this: "previously launched 10-20 separate windows instead of one, I like the idea of a shared interface"
- WPF `ItemsControl` with `UniformGrid` can display N screenshots efficiently
- Screenshots for monitoring only need 5-10 fps, not 60 — reduces bandwidth
- This is already partially designed in the epic roadmap (T2b mentions "adaptive paged emulator wall")

**Why it could fail:** WPF rendering 20 × 160×144 bitmaps at 10 fps = ~4.6 MB/s of pixel data. Manageable but needs `WriteableBitmap` pooling.

**Early warning signs:** UI thread blocks during screenshot updates.

**Recommendation:** Add `screenshot` command to Lua protocol. Build grid view in ControlPanel. Target 5 fps per agent for monitoring.

---

### 5. PPO + LSTM as next algorithm after vision DQN (🟡 — Risk: MEDIUM)

**What:** After the vision pipeline works with DQN, plan a migration to PPO (Proximal Policy Optimization) with an LSTM layer for memory. PPO is more sample-efficient and stable for partially observable environments.

**Evidence:**
- Pokémon is partially observable (hidden state: party, inventory, unseen map areas)
- PPO + LSTM is the standard approach for partially observable RL (e.g., OpenAI Five, Hide and Seek)
- DQN can work but requires careful reward shaping; PPO is more forgiving
- CleanRL provides reference single-file PPO implementations that can be ported to TorchSharp

**Why it could fail:** PPO is more complex to implement correctly (GAE, value clipping, advantage normalization). TorchSharp PPO has fewer reference implementations than PyTorch.

**Early warning signs:** DQN with vision shows unstable training; reward shaping becomes a never-ending tuning exercise.

**Recommendation:** Keep DQN for now — it's working. After T8 (dual-modality network) ships, run a PPO spike: port CleanRL's PPO to TorchSharp, compare sample efficiency on a simple navigation task.

---

## Full Idea Catalog

| # | Technique | Idea | Cluster | Rating |
|---|---|---|---|---|
| 1 | Obvious | mGBA + Lua bridge works, validated at ~141 Hz | A | 🟢 |
| 2 | Obvious | Redis Stack as shared experience store works | E | 🟢 |
| 3 | Obvious | WPF ControlPanel MVP exists | C | 🟢 |
| 4 | Obvious | .NET 10 + TorchSharp 0.107 works, CUDA verified | B | 🟢 |
| 5 | Obvious | 5-assembly architecture is clean, no cycles | — | 🟢 |
| 6 | Analogous | mGBA vs PyBoy: mGBA faster, better Lua API | A | 🟢 |
| 7 | Analogous | BizHawk is C# but weaker Lua, heavier | A | 🔴 |
| 8 | Analogous | SameBoy has no scripting API | A | 🔴 |
| 9 | Analogous | RetroArch/libretro requires C FFI, loses Lua simplicity | A | 🔴 |
| 10 | Analogous | ML.NET doesn't support RL | B | 🔴 |
| 11 | Analogous | TensorFlow.NET less mature than TorchSharp | B | 🔴 |
| 12 | Analogous | Python subprocess adds latency, two runtimes | B | 🔴 |
| 13 | Analogous | OpenCV vs torchvision: torchvision covers all needs | B | 🟢 |
| 14 | Wild | Full PPO + LSTM instead of DQN + MLP | D | 🟡 |
| 15 | Wild | Kafka instead of Redis for experience streaming | E | 🔴 |
| 16 | Wild | Blazor Server ControlPanel instead of WPF | C | 🟡 |
| 17 | Wild | DreamerV3 world model | D | 🔴 |
| 18 | Wild | Multi-modal transformer instead of CNN+MLP | D | 🔴 |
| 19 | Wild | P/Invoke to mGBA core — eliminate TCP and Lua | A | 🟡 |
| 20 | Constrained | TorchSharp is the only C# RL option | B | 🟢 |
| 21 | Constrained | ONNX Runtime as inference-only fallback | B | 🟡 |
| 22 | Constrained | Headless mGBA + screenshot grid in ControlPanel | C | 🟡 |
| 23 | Constrained | Shared CNN encoder for VRAM efficiency | D | 🟡 |
| 24 | Constrained | 84×84 frame resolution (Atari DQN standard) | D | 🟢 |
| 25 | Inverted | Don't switch to Python subprocess | B | 🟢 |
| 26 | Inverted | Don't remove ControlPanel | C | 🟢 |
| 27 | Inverted | Don't go pixels-only | D | 🟢 |
| 28 | Inverted | Don't add unnecessary dependencies (OpenCV) | B | 🟢 |
| 29 | Inverted | Don't switch emulators without Lua API | A | 🟢 |
| 30 | Inverted | Don't rewrite from scratch | — | 🟢 |
| 31 | First Principles | Emulator must be fast + memory-accessible → mGBA | A | 🟢 |
| 32 | First Principles | ML must be same language as agent → TorchSharp | B | 🟢 |
| 33 | First Principles | Monitoring must be single window → ControlPanel grid | C | 🟢 |
| 34 | First Principles | Vision + memory > either alone → Hybrid | D | 🟢 |
| 35 | First Principles | Redis is right for shared experience at this scale | E | 🟢 |

---

## Clusters

- **Cluster A: Emulator choice** (7 ideas) — 🟢 mGBA confirmed as best option. Headless + P/Invoke is the long-term upgrade path.
- **Cluster B: ML framework** (10 ideas) — 🟢 TorchSharp confirmed. OpenCV removed. ONNX as optional inference fallback.
- **Cluster C: ControlPanel** (4 ideas) — 🟡 WPF stays. Screenshot grid is the key improvement. Blazor Server is a future option.
- **Cluster D: Algorithm** (7 ideas) — 🟡 DQN stays for now. PPO + LSTM is the next step. 84×84 frames. Shared CNN encoder.
- **Cluster E: Infrastructure** (3 ideas) — 🟢 Redis confirmed. Kafka is overengineering. Docker Compose works.

---

## Pre-mortem Details

### Pre-mortem #1: "mGBA became the bottleneck"

**Failure mode:** At 20 agents, mGBA Lua callbacks drop frames, TCP buffers overflow, training throughput plateaus.

**Root cause:** Per-agent GUI process + TCP + Lua + JSON serialization overhead compounds at scale.

**Mitigation:** Headless mGBA build + P/Invoke eliminates all three overheads. Spike: 1-2 days to test feasibility.

**Risk:** MEDIUM (works at 10 agents, unknown at 20).

---

### Pre-mortem #2: "TorchSharp became a dead end"

**Failure mode:** TorchSharp falls behind PyTorch, CUDA 13 incompatibility, critical bug in optimizer, no fix for months.

**Root cause:** TorchSharp is a port — it trails PyTorch by 3-6 months.

**Mitigation:** (1) Pin LibTorch 2.10.0 + CUDA 12.8, (2) ONNX Runtime as inference fallback, (3) Python subprocess as last resort for training.

**Risk:** LOW (TorchSharp 0.107 is stable, .NET Foundation backed).

---

### Pre-mortem #3: "ControlPanel couldn't handle 20 agents"

**Failure mode:** WPF UI freezes rendering 20 screenshot streams. Memory leaks. User gives up and goes back to 20 windows.

**Root cause:** WPF was designed for business apps, not video walls.

**Mitigation:** (1) 5 fps per agent (not 60), (2) `WriteableBitmap` pooling, (3) `UniformGrid` virtualization, (4) Blazor Server as Plan B.

**Risk:** MEDIUM.

---

## Deep Dive: Docker-First Architecture

### Current state
- `docker-compose.yml`: 2 services — `redis` + `agent`
- Agent connects to mGBA on Windows host via `host.docker.internal:12345`
- mGBA must be launched **manually** on Windows before `docker compose up`
- This is NOT "everything from a single container"

### Target: single `docker compose up` does everything

Two approaches to containerize mGBA:

| Approach | How | Pros | Cons |
|---|---|---|---|
| **A: mGBA headless as separate container** | Build mGBA without Qt/SDL for Linux, run in its own container, TCP between containers | Minimal code changes, Lua script works as-is | TCP overhead remains, 3 containers total |
| **B: mGBA as .so inside Agent container** | Build mGBA as shared library, P/Invoke from C# | No TCP, no Lua, minimal latency, 2 containers total | Requires P/Invoke layer, mGBA API risk |

**Recommendation:** Start with Approach A (headless mGBA container, TCP), then migrate to Approach B (P/Invoke) after API stability is proven.

### Target docker-compose.yml (Approach B — ideal end state)

```yaml
services:
  redis:
    image: redis/redis-stack-server:latest
    # ... unchanged ...

  agent:
    build: .
    depends_on: [redis]
    environment:
      - REDIS_CONNECTION=redis:6379
      - MGBA_ROM_PATH=/roms/pokemon_red.gb
    volumes:
      - ./src/data/roms:/roms:ro
      - ./src/data/models:/app/src/data/models
    # No --host, no --port needed — mGBA is in-process!
    restart: unless-stopped
```

**Key insight:** With P/Invoke, the Agent container IS the emulator. No `host.docker.internal`, no port scanning, no manual mGBA launch. `docker compose up` = everything.

---

## Deep Dive: Kafka vs Redis

### What Redis does for us today

| Use case | Redis primitive | Kafka equivalent |
|---|---|---|
| Experience storage (transitions) | Streams (XADD/XREAD) | Topic (native append-only log) ✅ |
| Prioritized replay (TD-error sorting) | Sorted Sets (ZADD/ZRANGEBYSCORE) | No built-in — needs external index ❌ |
| Model weights (state_dict) | String (GET/SET, ~100KB-1MB) | Not designed for large payloads ❌ |
| Adaptive LR scheduler state | String (GET/SET) | Overkill for a single value ❌ |
| Health checks, parameter sharing | String | Overkill ❌ |

### Verdict

| Criterion | Redis | Kafka |
|---|---|---|
| Covers all use cases | 5/5 ✅ | 1/5 ❌ |
| Deployment complexity | 1 container, 0 config | Zookeeper/KRaft, topics, partitions |
| Docker | `redis/redis-stack-server` — ready | `confluentinc/cp-kafka` — more complex |
| Replay semantics | Streams with cursor (sufficient) | Native offset-based (better) |
| Scalability | Up to ~100 agents (current scale) | 1000+ agents |

**Recommendation: Redis stays.** Kafka solves only 1 of our 5 use cases (experience streaming) at the cost of adding a second store. If the project grows to 100+ agents — then it's worth extracting experience streaming to Kafka, keeping Redis for weights/priorities/LR.

---

## Deep Dive: P/Invoke vs Lua

### What we have now (Lua → TCP → C#)

```
mGBA core (C) → Lua callback (frame_callback, 60 fps)
  → Lua reads memory: emu:read8(0xD16B), emu:read16(...)
  → Lua serializes to JSON: json.encode(state)
  → TCP socket: client:send(json_str)
  → C# SocketProtocol: Read until '\n'
  → C# GameStateSerializer: JSON deserialize → GameState object
```

**Latency per step:** ~0.5-2ms (TCP localhost) + JSON encode/decode + Lua GC

### What P/Invoke gives us

```
mGBA core (C) ← P/Invoke ← C# MGBAEmulatorClient
  → C# reads memory: mCoreRead8(core, 0xD16B)
  → C# has GameState directly (no JSON, no TCP)
```

**Latency per step:** ~0.01-0.05ms (direct C function call)

### What we lose by dropping Lua

| Loss | Severity | How to compensate |
|---|---|---|
| **Memory-reading logic** (~200 lines of Lua) | 🟡 Medium | Port to C# — mechanical work, 1-2 days |
| **mGBA GUI** (can't watch the game) | 🟡 Medium | ControlPanel with screenshot grid — better than 20 windows |
| **API stability** (mGBA doesn't guarantee backward compatibility) | 🔴 High | Pin mGBA version, don't update without tests |
| **Save states** (emu:saveState/loadState) | 🟡 Medium | mGBA C API has `mCoreSaveState`/`mCoreLoadState` |
| **Hot-reload of script** | 🟢 Low | C# requires recompilation anyway |

### mGBA C API — what we specifically need

| Function | Purpose | Lua equivalent |
|---|---|---|
| `mCoreInitConfig(config, "gb")` | Initialize core for Game Boy | (implicit) |
| `mCoreLoadROM(core, path)` | Load ROM | (implicit at startup) |
| `mCoreRunFrame(core)` | Run one emulation frame | `callbacks:add('frame', ...)` |
| `mCoreRead8(core, addr)` | Read byte from memory | `emu:read8(addr)` |
| `mCoreRead16(core, addr)` | Read 2 bytes (little-endian) | `emu:read16(addr)` |
| `mCoreAddKeys(core, mask)` | Press buttons | `emu:addKeys(mask)` |
| `mCoreClearKeys(core, mask)` | Release buttons | `emu:clearKeys(mask)` |
| `mCoreSaveState(core, path)` | Save state | `emu:saveState(slot)` |
| `mCoreLoadState(core, path)` | Load state | `emu:loadState(slot)` |
| `mCoreGetPixels(core, buffer)` | Screenshot (for ControlPanel) | (none in Lua — we use PrintWindow) |

**All 10 functions exist in the mGBA core API.** Porting the memory-reading logic means moving ~200 lines of Lua from `emu:read8(0xD16B)` → `mCoreRead8(core, 0xD16B)` to C#.

### Migration plan

| Phase | What | Timeline |
|---|---|---|
| **Spike (1-2 days)** | Build mGBA as `.so` under Linux, call `mCoreInitConfig` + `mCoreLoadROM` from C# via P/Invoke | Before T4 |
| **Phase 1** | Port memory reading (all addresses from Lua → C#), replace `MGBAEmulatorClient` | After T7 |
| **Phase 2** | Port button injection (button queue from Lua → C#) | After Phase 1 |
| **Phase 3** | Remove TCP, Lua, port scanning. Update Dockerfile (mGBA .so inside) | After Phase 2 |

---

## Recommendation (updated)

### What stays (no change)
- **mGBA** — best emulator for the task
- **TorchSharp** — only viable C# RL framework
- **Redis Stack** — right choice for shared experience (Kafka is overengineering at this scale)
- **5-assembly architecture** — clean, no cycles
- **DQN** — works, ship vision on DQN first

### What changes (targeted improvements)
| Priority | Change | Effort | Impact |
|---|---|---|---|
| 🔴 P0 | **Remove OpenCV** — use torchvision.transforms | 1 hour | Fewer deps, fewer conflicts |
| 🟡 P1 | **ControlPanel screenshot grid** — add `screenshot` Lua command, grid UI | 2-3 days | User's top request |
| 🟡 P1 | **84×84 frame resolution** — Atari DQN standard, saves VRAM | Design decision in T4 | Less VRAM, proven resolution |
| 🟡 P1 | **Headless mGBA in Docker** — build mGBA without GUI, run as container | 2-3 days | `docker compose up` = everything |
| 🟢 P2 | **P/Invoke spike** — try mGBA as .so, call from C# | 1-2 days | Eliminates TCP + Lua + JSON |
| 🟢 P2 | **PPO + LSTM spike** — after T8 ships, port CleanRL PPO | 3-5 days | Better sample efficiency |

### What NOT to do
- ❌ Don't switch emulators (mGBA is the best)
- ❌ Don't switch ML frameworks (TorchSharp is the only option)
- ❌ Don't rewrite from scratch (foundation is solid)
- ❌ Don't add Kafka (Redis covers all 5 use cases; Kafka covers 1)
- ❌ Don't implement DreamerV3 (too complex, DQN/PPO first)

---

## Next Steps

1. **Apply P0 change now:** Remove OpenCvSharp4, document torchvision.transforms pipeline
2. **Update epic roadmap:** Add headless mGBA Docker task, P/Invoke spike, PPO spike, screenshot grid
3. **Proceed with T4:** Design combined representation — now with confirmed decisions: TorchSharp-only preprocessing, 84×84 frames, hybrid CNN+MLP
4. **Or discuss:** Any of the findings above in more detail
4. **Or discuss:** Any of the findings above in more detail
