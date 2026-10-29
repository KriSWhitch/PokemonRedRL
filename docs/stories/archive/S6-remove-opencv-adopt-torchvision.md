# S6 — Remove OpenCV, adopt torchvision.transforms
**ID:** S6 (legacy ID: T4a)
**Status:** ✅ Done
**Depth:** Light
**Created:** 2026-10-11

## What

Remove `OpenCvSharp4` + `OpenCvSharp4.runtime.win` from `PokemonRedRL.Utils.csproj` — they were added in T3 as a placeholder for the future vision pipeline, but the brainstorm confirmed that `torchvision.transforms` (Resize → Grayscale → Normalize) covers all preprocessing needs. There is not a single `using OpenCvSharp` in the codebase — the packages are just dead weight in the dependency tree. After removal, document the torchvision.transforms pipeline.

## Steps

- [x] 1. Remove `OpenCvSharp4` and `OpenCvSharp4.runtime.win` from `PokemonRedRL.Utils.csproj`
  - **Files:** `src/PokemonRedRL.Utils/PokemonRedRL.Utils.csproj`
  - **What:** Delete the two `<PackageReference Include="OpenCvSharp4"...` lines
  - **Verify:** `dotnet build src/PokemonRedRL.sln` passes

- [x] 2. Add documentation comment about the torchvision.transforms pipeline
  - **Files:** `src/PokemonRedRL.Utils/PokemonRedRL.Utils.csproj` (XML comment) or `docs/PROJECT_OVERVIEW.md` §7
  - **What:** Document that frame preprocessing will use `torchvision.transforms.Compose([Resize(84,84), Grayscale(), Normalize(...)])` — no OpenCV needed
  - **Verify:** Comment is readable and accurate

## Verification

- [x] `dotnet build src/PokemonRedRL.sln` passes
- [x] `grep -r "OpenCvSharp" src/` returns nothing

## Risks

None. Packages are unused in code — pure removal.
