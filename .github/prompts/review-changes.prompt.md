---
mode: agent
description: Verify implemented changes against the plan before they can be committed.
---

# Review Changes

Your job is to **verify** the diff produced by `/implement-task`, acting as a skeptical reviewer, not the author.

## Steps

1. **Get the actual diff.** Use `git status`/`git diff` (via the terminal) to see everything changed, added, deleted, or renamed since the last commit — don't rely on the todo list alone, it can miss unintended changes.
2. **Compare against the plan** (`docs/tasks/<slug>.md`). Confirm every planned step was done (all checkboxes ticked), and flag anything in the diff that *wasn't* in the plan (scope creep) or was in the plan but is missing.
3. **Build.** Run `dotnet build src/PokemonRedRL.sln` (or the relevant project) and use `get_errors`. A change is not done if the solution doesn't build.
4. **Run tests** if a test project exists for the affected area; if not and non-trivial logic was added, note that as a gap (don't silently skip mentioning it).
5. **Check for regressions/quality issues specific to this codebase:**
   - TorchSharp `Tensor`s created and not disposed (leaks) in changed code.
   - New DI services registered in `Program.cs` with the correct lifetime (`Scoped` for per-agent/connection state, `Singleton` for shared infra).
   - Changes to the TCP protocol (`SocketProtocol`) reflected on both the C# and Lua sides.
   - Hardcoded secrets/connection strings, unsanitized input at the emulator TCP boundary. Note: `src/data/roms/` and `src/data/models/` backups are intentionally tracked in this repo — don't flag or remove them.
   - Comment language consistency with the surrounding file.
6. **Check `docs/CODEBASE_MAP.md`** — if the change touched repo structure and the map wasn't regenerated, run `/cartograph` now.
7. **Produce a verdict:**
   - ✅ Pass — set `Status: Reviewed & Passed` in `docs/tasks/<slug>.md` and tell the user it's safe to `/commit-changes`.
   - ⚠️ Pass with notes — same as above, plus record the follow-ups under a `## Follow-ups` section in the plan file.
   - ❌ Fail — list concrete issues that must go back through `/implement-task` (or `/plan-task` if the plan itself was wrong); leave `Status: Implemented` unchanged.
8. Do not stage, commit, or push anything in this step (the plan file update is the only file you touch).

## Output discipline

- Be specific: point to file + line, not vague concerns.
- Don't rewrite code yourself here — send it back to `/implement-task` with clear instructions if something needs to change.
