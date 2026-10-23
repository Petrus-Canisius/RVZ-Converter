# Validation records

## Application version 2.0.0

The local Windows x64 build used .NET SDK 10.0.402.

- Release build completed without warnings or errors.
- The regression suite passed. It covers conversion routes, intermediate ISO selection, RVZ arguments, engine failures, Unicode paths, collisions, cleanup ownership, cancellation, split publication and disk space.
- DolphinTool and WIT ran from their local Windows x64 distributions. Their help, dependencies and RVZ preset were checked. Synthetic ISO to RVZ to ISO, ISO/RVZ to CISO, and GCZ/WIA to RVZ conversions passed. These fixtures were not game images.
- The Windows x64 application opened and its main and About screens were visually checked. The UI render smoke covered several window sizes and scale fixtures.
- No suitable real game image was available. Synthetic tests do not establish successful conversion of real discs. Wii WBFS integration with large split files was not exercised with a real disc.
- GitHub Actions run [39989385333](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/39989385333) passed its native application build, regression, UI smoke and package steps on the configured six target runners. CI does not install conversion engines or test real disc conversion.

## Windows portable package version 2.0.1

- GitHub Actions run [40008854748](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40008854748) passed build, regression, UI smoke and package steps on all six configured native runners: Windows x64, Windows ARM64, Linux x64, Linux ARM64, macOS x64 and macOS ARM64.
- The x64 and ARM64 packages were built on matching native Windows runners. Their root launchers started the self-contained app from a separate working directory. Each smoke test forwarded a path containing spaces and Unicode. The app wrote the smoke result to that path.
- The archive verifier checked the root entries, app runtime configuration, runtime license, package hash and absence of loose root DLLs. The root contains only `RVZConverter.exe`, `README.txt`, `app/`, `docs/` and `licenses/`.
- Engine discovery unit coverage confirms that an app in `app/` checks sibling `tools/dolphin/` and `tools/wit/` under the package root. Cancellation tests passed.
- CI does not install DolphinTool or WIT. The workflow verifies application packaging and smoke launch behavior. It does not verify engine discovery against installed native engines or real disc conversion.
- Linux and macOS packages were not rebuilt for this Windows package-layout update. They remain the version 2.0.0 packages from the previous release. macOS packages are unsigned and not notarized.

Real-image conversion remains unverified. Container readability checks do not perform complete disc-integrity verification.
