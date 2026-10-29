# Epic E4: Reliability and Validation

**ID:** E4
**Parent Initiative:** [Computer-Vision + RL Overhaul](../initiatives/I1-computer-vision-rl-overhaul.md)
**Tracking:** See [Work Items Registry](../WORK_ITEMS.md) for status, branch, dependencies, and next work.

## Outcome

Keep the platform buildable, testable, and documented as the vision and training architecture evolve.

## Success Signals

- Regression-prone services have automated tests in the solution.
- Public architecture and setup documentation match the implemented system.
- The solution builds and the required manual emulator checks are reproducible.

## Child User Stories

| ID | User Story | Intent | Done Signal |
|---|---|---|---|
| S2 | [Pre-overhaul cleanup](../stories/archive/S2-pre-overhaul-cleanup.md) | Remove confirmed defects and stale project wiring. | Reward, paths, Lua duplication, DI, and unused package issues are resolved or recorded. |
| S4 | [.NET and package modernization](../stories/archive/S4-net-package-modernization.md) | Move projects and dependencies to the supported .NET 10 baseline. | Solution builds with current package versions and CUDA is verified. |
| S5 | [Docker Compose for Redis and Agent](../stories/archive/S5-docker-containerization.md) | Make the backend reproducible with Docker Compose. | Redis and Agent start, connect, stop, and preserve data as documented. |
| S6 | [Remove OpenCV dependency](../stories/archive/S6-remove-opencv-adopt-torchvision.md) | Use the TorchSharp/torchvision preprocessing path. | OpenCvSharp is removed and the replacement path is documented. |
| S14 | Testing infrastructure | Add an xUnit project and tests for high-risk components. | Create the test-project skeleton early, in parallel with S9; add tests incrementally for `RewardCalculatorService` (including the S2 regression), `StatePreprocessorService`, vision preprocessing math, and `SumTree`/replay sampling. S10 owns the protocol contract test; S9/S11 add focused component tests as those components land. |
| S15 | Documentation catch-up | Align project docs and code map with delivered behavior. | Per-story documentation updates happen incrementally; final catch-up removes stale planned-language, runs `/cartograph`, and verifies links against delivered behavior. |

## Branch and Delivery

Create each story branch from `epic/e4-reliability-validation`. Scope changes discovered during a story return to its approved plan before implementation continues.