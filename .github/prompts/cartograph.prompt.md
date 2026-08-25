---
mode: agent
description: Regenerate the full repository map at docs/CODEBASE_MAP.md.
---

# Cartograph — repository map generator

Your job is to produce (or refresh) a **complete, accurate, file-by-file map** of this repository at [docs/CODEBASE_MAP.md](../../docs/CODEBASE_MAP.md).

## Steps

1. **Survey the repo structure.** Use `list_dir` recursively (or `file_search` with broad globs) to enumerate every top-level folder and project. Do not rely on memory or the previous version of the map — re-verify against the current filesystem.
2. **Identify all projects/modules.** For a .NET solution, read every `.sln`/`.csproj` to get the authoritative list of projects, target framework, and package references. For other stacks, identify the equivalent (package.json, pyproject.toml, etc.).
3. **Read source files as needed** (`grep_search` for `public class|public interface|public enum` style declarations first to get an inventory cheaply, then `read_file` on files whose purpose isn't obvious from the name) to describe what each file/folder actually does — not a guess, a description grounded in the code.
4. **Describe the runtime architecture.** Include a Mermaid `flowchart` diagram showing how the major components/processes talk to each other (e.g. emulator ↔ bridge script ↔ application ↔ data store), and one paragraph describing the primary data flow end to end.
5. **Write the map** to `docs/CODEBASE_MAP.md` with this structure:
   - High-level architecture (diagram + data-flow paragraph)
   - Solution/project layout (tree + one-line responsibilities)
   - Project-by-project detail (tables: file/folder → purpose)
   - AI-agent workflow assets (link the prompt files in `.github/prompts` and `.github/copilot-instructions.md`)
   - Known gaps / follow-ups (missing tests, missing CI, hardcoded config, etc.) — keep this honest, don't invent problems that don't exist and don't hide real ones
6. **Update the "Last generated" date** at the top of the file to today's date.
7. **Cross-check** every relative link you write actually resolves to a file that exists in the workspace.

## When to run this

- After any change that adds, removes, renames, or moves a file, folder, or project.
- Before running `/review-changes` on a structural change, so the reviewer is comparing against an up-to-date map.

## Output discipline

- Only edit `docs/CODEBASE_MAP.md` (create `docs/` if it does not exist). Do not modify source files.
- Prefer updating the existing file over creating a new one if it already exists.
- Keep descriptions factual and concise — this is a map, not marketing copy.
