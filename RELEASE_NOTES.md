# RVZ Converter 2.0.2

Created by **Petrus-Canisius**.

Five complete native distributions are available. Each includes the .NET runtime, DolphinTool and WIT. **Windows ARM64 is withheld**: its CI artifact has no native WIT. Passing CI does not make that artifact complete.

This patch fixes Dolphin's inaccurate-size assertion during CISO and WBFS conversion. Those inputs use WIT for ISO output and a WIT intermediate ISO for RVZ output. Missing WIT is reported before conversion. Windows engine dialogs are captured and the engine is stopped without choosing Ignore. Failed output is discarded. Sources remain protected and existing destination files are not overwritten.

## Downloads

| System | Architecture | Application | Corresponding engine source |
| --- | --- | --- | --- |
| Windows | x64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-win-x64.zip) | [Source](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/EngineSources-win-x64.tar.gz) |
| Windows | ARM64 | Withheld: native WIT missing | Not published |
| Linux | x64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-linux-x64.tar.gz) | [Source](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/EngineSources-linux-x64.tar.gz) |
| Linux | ARM64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-linux-arm64.tar.gz) | [Source](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/EngineSources-linux-arm64.tar.gz) |
| macOS | Intel | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-osx-x64.zip) | [Source](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/EngineSources-osx-x64.tar.gz) |
| macOS | Apple Silicon | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-osx-arm64.zip) | [Source](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/EngineSources-osx-arm64.tar.gz) |

[SHA256SUMS.txt](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/SHA256SUMS.txt) covers all ten application/source archives. Existing Windows x64 binary and source assets retain their original bytes. The four new application/source pairs come directly from [run 40076853202](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40076853202), whose commit exactly matches the unchanged v2.0.2 tag: `cb5809e84f4df1965db0d9c0bad9f935a66343b1`. Previous releases and tags are preserved.

## Installation

Verify SHA-256 and extract the entire archive. Windows opens the root `RVZConverter.exe`; Linux runs `./RVZConverter`. Keep `app/`, `tools/`, `docs/` and `licenses/` together. macOS opens `RVZ Converter.app`; its dependencies, engines and notices are inside the bundle. No SDK, compiler, PowerShell or separate .NET installation is required to run the application.

Windows 11 is the Windows baseline. Linux targets Ubuntu 24.04/glibc 2.39 with desktop libraries, X11/XWayland and fonts, including `libcap2`. macOS declares a 14.0 minimum; native CI ran on macOS 15. macOS packages have ad-hoc signatures, no Developer ID signature and no notarization. Gatekeeper may block launch. Rosetta is not required. See the current [installation guide](https://github.com/Petrus-Canisius/RVZ-Converter/blob/main/docs/PLATFORMS.md).

Archive documentation preserves the release-commit snapshot. This page and the current [final validation report](https://github.com/Petrus-Canisius/RVZ-Converter/blob/main/docs/FINAL-VALIDATION-2.0.2.md) supersede its earlier pending-validation statements.

## Validation and limitations

All six native jobs passed 80 regressions, native UI smoke, package extraction/Unicode relocation, restricted PATH and bundled-engine discovery. Five targets executed fourteen synthetic conversion calls across twelve routes; Windows ARM64 executed eight calls across six Dolphin-only routes. Synthetic fixtures do not establish real-game compatibility.

Local Windows x64 checks used both the CI archive and the originally published archive. Real Wii and GameCube ISO/RVZ round trips preserved the complete original ISO SHA-256. Real CISO/WBFS routes and separate Wii partition verification were exercised. The [report](https://github.com/Petrus-Canisius/RVZ-Converter/blob/main/docs/FINAL-VALIDATION-2.0.2.md) and [machine-readable evidence](https://github.com/Petrus-Canisius/RVZ-Converter/blob/main/docs/validation-2.0.2-evidence.json) list routes, hashes and diagnostics.

**CISO/WBFS transformations are not lossless archival copies.** On the tested Wii image, RVZ output from CISO/WBFS produced Dolphin's `Problems Found: Yes`, `Severity: Low`: errors in 121609 unused DATA-partition blocks, despite exit code 0. WIT verified UPDATE and DATA partitions. These outputs are not claimed to pass a clean full-disc check. Prefer ISO to RVZ for bit-preserving storage.

Real games on Linux/macOS/Windows ARM64, large split WBFS outputs, gameplay, macOS 14 execution and clean-OS installation remain untested. PATH isolation does not replace clean-OS testing. Normal application container reopening does not perform full disc-integrity verification. No claim of compatibility with all Wii games is made.

Supported inputs are Wii/GameCube ISO, RVZ, GCZ, WIA, CISO and Wii WBFS. Outputs are ISO, RVZ, CISO and Wii-only WBFS. Same-format conversions are refused. CISO means Wii/GameCube CISO, not PSP CSO. WIT may pad an unknown original image length; conversion cannot recreate missing data. Intermediate ISO files and staging need extra space. RVZ uses Zstandard level 5 and 128 KiB blocks.

## Credits and redistribution

**DolphinTool 2506** is by the Dolphin Emulator project contributors, revision `aed420c95d7a152363917e4b827d6cc85eacb502`. **WIT 3.05a+rvz1** is by **Wiimm (Dirk Clemens) and contributors**, based on revision `fc1c0b840cb3ac41ca6e4f1d5e16da12b47eab58`. Neither engine is authored by Petrus-Canisius.

Modified WIT includes the GCZ correction and Mbed TLS 2.28.0 SHA-1 under Apache-2.0. Combined engine builds use the GPL version 3 option. Legacy SSLeay/OpenSSL SHA-1 objects and assembly are not linked; original source notices remain intact. Full GPL texts, Mike Scott's Rijndael notice, dependency and runtime notices accompany the packages. Matching source archives contain pinned sources/submodules, modifications and build recipes. Windows x64 includes app-local Cygwin and corresponding Cygwin/ncurses/zlib sources. No application license was added or inferred. See [third-party notices](https://github.com/Petrus-Canisius/RVZ-Converter/blob/v2.0.2/THIRD-PARTY-NOTICES.md).
