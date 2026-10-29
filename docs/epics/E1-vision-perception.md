# Epic E1: Vision Perception and Pipeline

**ID:** E1
**Parent Initiative:** [Computer-Vision + RL Overhaul](../initiatives/I1-computer-vision-rl-overhaul.md)
**Tracking:** See [Work Items Registry](../WORK_ITEMS.md) for status, branch, dependencies, and next work.

## Outcome

Give the agent reliable visual input and visual progress signals while preserving memory-derived state as the baseline source of exact game facts.

## Success Signals

- The S1 `PrintWindow` approach captures frames for the intended per-agent workload; S9 records latency, dropped-frame rate, and tested concurrency instead of extrapolating the 3-window spike.
- The S7 contract is implemented: F=2 84×84 grayscale frames, all 14 memory values, the accepted fusion shapes, and `Vision.Enabled=false` preserving bit-identical memory-only behavior.
- Vision-aware reward signals are finite and bounded, and repeated frames cannot farm reward; S12 remains algorithm-agnostic and does not depend on S8/S11.

## Child User Stories

| ID | User Story | Intent | Done Signal |
|---|---|---|---|
| S1 | [Validate frame capture](../stories/archive/S1-frame-capture-spike.md) | Compare emulator screenshot and OS capture paths. | `PrintWindow` performance and limitations are measured and documented. |
| S7 | [Design vision-memory representation](../stories/archive/S7-design-vision-representation.md) | Define how visual frames and the 14-value memory vector feed the policy. | ADR records F=2, tensor shapes, normalization, fusion, `Vision.Enabled`, and the A/B protocol. |
| S9 | Vision project and frame pipeline | Create `PokemonRedRL.Vision` with `IFrameProvider` and `IVisionPreprocessorService`; capture and preprocess frames per agent using the run/window association. | `PrintWindow` frames correlate through the ControlPanel run manifest; output tensors meet the S7 contract; latency/drop metrics and target concurrency are recorded; scoped DI lifetime, tensor disposal, and soak checks pass. |
| S12 | Vision-aware reward shaping | Add visual novelty (e.g. perceptual hash or embedding distance) and visual state signals such as menu/battle detection, without coupling to the selected learner. | Agreed novelty/state signals are finite and bounded; repeated frames cannot repeatedly pay reward; menu/battle signals and regression checks are covered. |

## Branch and Delivery

Create each story branch from `epic/e1-vision-perception`. Use Quick Flow when the behavior is clear and no upfront architecture package is needed; use Full Flow when requirements or architecture must be agreed before code. The task plan is created for the story flow under `docs/stories/`.