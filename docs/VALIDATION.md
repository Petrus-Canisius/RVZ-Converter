# Validation records

## Final distribution audit, 24 October 2026

The [final 2.0.2 report](FINAL-VALIDATION-2.0.2.md) supersedes the pending-publication statements below. Run [40076853202](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40076853202) passed all six native jobs at the unchanged release commit `cb5809e84f4df1965db0d9c0bad9f935a66343b1`. Five targets have both engines. Windows ARM64 has no native WIT and remains withheld. Real Wii and GameCube image tests were subsequently executed locally on Windows x64. The historical records below describe the earlier evidence available when written.

The release-gate correction passed all six native jobs in [40082658249](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40082658249). The documentation update passed all six again in [40083225768, attempt 2](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40083225768/attempts/2). Its first attempt was cancelled to avoid redundant engine builds and is not counted as a validation. Four application/source pairs were added to v2.0.2; the original Windows x64 pair was preserved. All ten public archive downloads match the regenerated SHA-256 index, audited bytes and GitHub asset digests. Windows x64 testing includes all twenty Wii routes on one image and five GameCube routes; two RVZ outputs after CISO/WBFS report low-severity unused-block warnings, which remain in the final report and evidence JSON.

## Application version 2.0.2

The local Windows x64 build used .NET SDK 10.0.402 and MSVC 19.51.36260.0. The application build completed with no warnings or errors. The regression suite passed 81 tests. The engine build emitted upstream compiler and CMake warnings. These were not hidden.

The corrected Windows x64 archive was extracted into a path containing spaces and Unicode. Its root launcher ran from another working directory with only Windows system directories on PATH. Bundled engine discovery, engine help commands, the requested RVZ preset and actual synthetic conversions passed. Both executable product names are RVZ Converter. Company metadata is Petrus-Canisius. Both file versions are 2.0.2.0.

The package contains native DolphinTool 2506 and WIT 3.05a+rvz1. WIT uses Mbed TLS 2.28.0 SHA-1. A native known-answer test passed for empty input, `abc`, incremental `abc` and one million `a` bytes. The legacy SSLeay/OpenSSL SHA-1 and assembly objects are not linked. Dependency inspection found only the reviewed app-local Cygwin runtime and Windows system DLL requirements for WIT. DolphinTool uses the static Microsoft C++ runtime.

Actual synthetic engine coverage includes ISO to RVZ and CISO, RVZ to ISO and CISO, GCZ and WIA to ISO, RVZ and CISO, and CISO to ISO and RVZ. There were fourteen conversion invocations across twelve distinct routes. ISO to RVZ to ISO preserved the generated ISO hash. CISO materialization used WIT's padded virtual length and the resulting ISO and RVZ metadata were checked. Each output passed container reopening checks.

The raw unsupported Dolphin CISO to ISO call reproduced the exact line-44 assertion with the separately installed 2609a engine. The locally built 2506 engine reproduced the corresponding line-52 assertion. The process runner read the error dialog and stopped the engine without choosing Ignore. Both calls captured empty stdout and stderr and a forced termination exit code of -1. The synthetic CISO SHA256 before and after was `6c48406d330788dcbd359372100e75444ddc1e028b0384cb7dd4170c6309ccef`. No diagnostic output was accepted. See [the investigation](ASSERTION-INVESTIGATION.md).

Collision refusal, coordinator cancellation, owned cleanup, source protection and queue continuation passed. A native process-tree test separately verified cancellation of a child and grandchild before returning. The assertion test exercised actual Dolphin termination. The package cancellation fixture cancels at the conversion transition. It is not a real-game cancellation test.

The packaged Windows application opened native windows. Main, compact progress, error queue and About render captures were reviewed. Render fixtures cover 100, 125, 150 and 200 percent. These are render-scale checks, not four separate hardware display configurations. The indeterminate progress capture is explicitly a synthetic UI fixture.

| Target | Current revised package | Native validation scope |
| --- | --- | --- |
| Windows x64 | Prepared and locally verified | Extracted package, both revised engines, synthetic routes and native UI passed |
| Windows ARM64 | Not published for 2.0.2 | Revised package NOT RUN. Native WIT is unavailable |
| Linux x64 | Not published for 2.0.2 | Revised Mbed TLS WIT package NOT RUN |
| Linux ARM64 | Not published for 2.0.2 | Revised Mbed TLS WIT package NOT RUN |
| macOS Intel | Not published for 2.0.2 | Revised Mbed TLS WIT package NOT RUN |
| macOS Apple Silicon | Not published for 2.0.2 | Revised Mbed TLS WIT package NOT RUN |

Historical native run [40069837264](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40069837264) passed application, engine, extracted-package and UI checks on Linux x64, Linux ARM64 and both macOS architectures. Those builds predate the revised WIT hash provider. They are not the 2.0.2 packages being distributed. Its Windows package checks failed because PowerShell did not wait for the GUI launcher and `$LASTEXITCODE` was unset. The package test now uses ProcessStartInfo, waits explicitly and checks the actual exit code. This correction passed locally on Windows x64.

Run [40071642623](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40071642623) could not start any of its six jobs because hosted runner execution was blocked at account level. No build, engine or runtime result can be inferred from that run. The revised engines must be validated on the other native targets before their packages are published. No machine security or repository visibility setting was changed.

No suitable real game image was available. Real-image conversion is NOT TESTED. WBFS integration with a Wii partition fixture and large split outputs is NOT TESTED. Regression tests cover Wii-only restrictions, split arguments, part collisions, missing parts, partial publication and disk-space checks. Container readability is not complete disc-integrity verification.

The release archive audit checked the clean root, native architecture, engine manifest, executable metadata, required notices, source distribution and absence of game images, private logs, preferences and optional debug files. The source archives retain original third-party identifiers, copyright and license notices. No application license was added.

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
