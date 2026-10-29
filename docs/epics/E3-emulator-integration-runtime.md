# Epic E3: Emulator Integration and Runtime

**ID:** E3
**Parent Initiative:** [Computer-Vision + RL Overhaul](../initiatives/I1-computer-vision-rl-overhaul.md)
**Tracking:** See [Work Items Registry](../WORK_ITEMS.md) for status, branch, dependencies, and next work.

## Outcome

Keep the current host-attached mGBA Lua/C# bridge contract explicit and verified. Headless/containerized mGBA and direct in-process/P/Invoke runtime work are post-Initiative candidates, not hidden requirements of this Epic.

## Success Signals

- Lua-produced JSON is parsed through `GameStateSerializer.Deserialize` into `GameStateResponse`, with all contract fields verified; serialization tests use `System.Text.Json` directly because the current helper exposes no `Serialize` method.
- No protocol change is introduced without coordinated producer/consumer updates; if a bridge change is needed, it has a real-mGBA smoke check.

## Child User Stories

| ID | User Story | Intent | Done Signal |
|---|---|---|---|
| S3 | [Desktop ControlPanel MVP](../stories/archive/S3-controlpanel-mvp.md) | Manage agent processes and manual emulator attachment. | WPF MVP supports session manifest, process lifecycle, attach, preview, and telemetry; unsupported auto-launch is explicit. |
| S10 | Protocol verification and contract test | Verify Lua JSON parsing and the current C# contract; confirm the OS capture path does not require a protocol change. | A representative Lua JSON payload parses through `GameStateSerializer.Deserialize` and all relevant `GameStateResponse` fields are asserted. If serialization round-trip is included, use `System.Text.Json`; do not add a serializer API solely for the test. Run a real-mGBA smoke test if the Lua/C# contract changes. |

## Future Candidates

- S16 — Build headless Linux mGBA without Qt/SDL, connect it to the Agent over TCP, and evaluate the full-stack `docker compose up` target (`story/s16-headless-mgba-docker`).
- S17 — Build mGBA as a shared library and spike `mCoreInitConfig`/`mCoreLoadROM` from C# (`story/s17-mgba-pinvoke-spike`); retain TCP+Lua as fallback.

See WORK_ITEMS for the authoritative dependency edges for these candidates.

These items are outside the current Initiative completion scope and require a separately approved scope update or follow-on Initiative.

## Branch and Delivery

Create each story branch from `epic/e3-emulator-integration-runtime`. Quick/Full selection applies to stories and bugs; this Epic itself is decomposed rather than built as one change.