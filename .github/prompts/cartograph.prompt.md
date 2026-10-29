---
mode: agent
description: Regenerate the full repository map at the path configured in _bmad/config.toml.
---

# Cartograph — repository map generator

Your job is to produce (or refresh) a complete, accurate, file-by-file map at the path configured by `[paths].codebase_map_file` in `_bmad/config.toml`.

## Steps

1. **Load context and output path.** Read `_bmad/config.toml` and the repository instructions; resolve `[paths].codebase_map_file` and project-specific documentation expectations.
2. **Survey the repo structure.** Use `list_dir` recursively (or `file_search` with broad globs) to enumerate every top-level folder and project. Do not rely on memory or the previous version of the map — re-verify against the current filesystem.
3. **Identify projects/modules.** Read authoritative workspace/solution manifests to establish project boundaries, language/runtime targets, and dependencies.
4. **Read source files as needed** to describe what each file/folder actually does — not a guess, a description grounded in the code.
5. **Describe the runtime architecture.** Include a Mermaid `flowchart` diagram showing how the major components/processes interact, and one paragraph describing the primary data flow end to end.
6. **Write the map** to `[paths].codebase_map_file` with this structure:
   - High-level architecture (diagram + data-flow paragraph)
   - Solution/project layout (tree + one-line responsibilities)
   - Project-by-project detail (tables: file/folder → purpose)
   - AI-agent workflow assets (link the configured prompt and instruction directories)
   - Known gaps / follow-ups (missing tests, missing CI, hardcoded config, etc.) — keep this honest, don't invent problems that don't exist and don't hide real ones
7. **Update the "Last generated" date** at the top of the file to today's date.
8. **Cross-check** every relative link you write actually resolves to a file that exists in the workspace.

## When to run this

- After any change that adds, removes, renames, or moves a file, folder, or project.
- Before running `/bmad-review` on a structural change, so the reviewer is comparing against an up-to-date map.

## Output discipline

- Only edit `[paths].codebase_map_file` (create its parent directory if needed). Do not modify source files.
- Prefer updating the existing file over creating a new one if it already exists.
- Keep descriptions factual and concise — this is a map, not marketing copy.
