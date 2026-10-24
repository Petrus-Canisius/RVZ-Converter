# RVZ Converter 2.0.2

Created by **Petrus-Canisius**.

This release updates Windows x64. Other new packages are held pending native validation of the revised engines. Hosted native CI is temporarily unavailable. The Windows ARM64 link below points to the earlier app-only release.

This patch fixes Dolphin's inaccurate-size assertion during CISO and WBFS conversion. Those inputs use WIT for ISO output. RVZ output uses a WIT intermediate ISO, then DolphinTool. Missing WIT is reported before conversion. Windows engine dialogs are captured and the engine is stopped without choosing Ignore. Failed output is discarded.

The classic interface, progress bar and source protection remain in place. Sources are never modified or deleted. Existing output files are not overwritten.

## Downloads

| Operating system | Architecture | Download |
| --- | --- | --- |
| Windows | x64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-win-x64.zip) |
| Windows | ARM64 | [Earlier 2.0.1 ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.1/RVZConverter-win-arm64.zip) |
| Linux | x64 | Pending native package |
| Linux | ARM64 | Pending native package |
| macOS | Intel | Pending native package |
| macOS | Apple Silicon | Pending native package |

[SHA256SUMS.txt](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/SHA256SUMS.txt) covers application and engine source archives.

Extract the complete archive. Windows uses the root `RVZConverter.exe`. Linux uses the root `RVZConverter`. Their dependencies are in `app/` and engines are in `tools/`. Keep the directories together. macOS uses `RVZ Converter.app`. Its application, engines, documentation and licenses are inside the bundle. The .NET runtime is included.

## Included engines

DolphinTool 2506 uses source revision `aed420c95d7a152363917e4b827d6cc85eacb502`. WIT 3.05a+rvz1 uses source revision `fc1c0b840cb3ac41ca6e4f1d5e16da12b47eab58`. WIT includes a GameCube GCZ correction and the Apache-licensed Mbed TLS 2.28.0 SHA-1 provider. The legacy SSLeay/OpenSSL SHA-1 objects and assembly are not linked.

Each engine has the package's native architecture. Windows x64 includes the required app-local Cygwin runtime. No engine is downloaded at startup. External engine recovery remains available in the main window.

The earlier Windows ARM64 package has no bundled engines and does not include this assertion fix. New Windows ARM64 source builds can include DolphinTool only. There is no native ARM64 Cygwin runtime for this WIT build. No emulated engine is bundled. CISO and WBFS output require WIT. CISO and WBFS input also require WIT for ISO or RVZ output.

The [EngineSources-win-x64.tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/EngineSources-win-x64.tar.gz) asset contains corresponding engine sources, pinned dependencies and build materials. License texts and notices are inside the application package. DolphinTool is by the Dolphin Emulator project contributors. WIT is by Wiimm and contributors. Neither engine is authored by Petrus-Canisius. No application license has been added.

## Supported conversions

| Input | ISO | RVZ | CISO | WBFS (Wii only) |
| --- | --- | --- | --- | --- |
| ISO | Not supported | DolphinTool | WIT | WIT |
| RVZ | DolphinTool | Not supported | DolphinTool to ISO, then WIT | DolphinTool to ISO, then WIT |
| GCZ | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| WIA | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| CISO | WIT | WIT to ISO, then DolphinTool | Not supported | WIT |
| WBFS | WIT | WIT to ISO, then DolphinTool | WIT | Not supported |

Routes depend on compatible engines and source readability. Same-format conversions are not supported. WBFS output is restricted to Wii discs. CISO means Wii/GameCube CISO, not PSP CSO.

Intermediate ISO files and staging require extra disk space. WIT may add zero padding when materializing an unknown original length. CISO maps smaller than WIT's minimum padded ISO length are rejected before conversion. Conversion cannot restore missing data. CISO and WBFS output are not promised to be bit-identical. RVZ uses Zstandard level 5 with 128 KiB blocks.

## Requirements and limitations

Windows packages target Windows 11 or later. Linux builds target Ubuntu 24.04 with glibc 2.39 or later. Desktop libraries, X11 or XWayland, Fontconfig and fonts remain system requirements. macOS requires version 14 or later. Packages have no Developer ID signature or notarization. Ad-hoc signatures do not establish publisher trust. Gatekeeper or local security policy may block launch. No Rosetta dependency is assumed.

The assertion was reproduced using generated non-game CISO data. Synthetic engine tests do not establish real-game conversion results. Real-image conversion remains unverified. WBFS integration lacks a suitable Wii partition fixture. Container reopening does not perform complete disc-integrity verification.

The Windows x64 package passed local native extraction, relocation, engine discovery, actual synthetic conversions and UI launch checks. Its engine SHA-1 provider passed known-answer tests. The local application regression suite passed 81 tests. Earlier native Linux and macOS checks predate the revised WIT provider. They do not validate an unpublished revised package.

See the [assertion investigation](https://github.com/Petrus-Canisius/RVZ-Converter/blob/v2.0.2/docs/ASSERTION-INVESTIGATION.md), [technical validation record](https://github.com/Petrus-Canisius/RVZ-Converter/blob/v2.0.2/docs/VALIDATION.md), [installation requirements](https://github.com/Petrus-Canisius/RVZ-Converter/blob/v2.0.2/docs/PLATFORMS.md), and [third-party notices](https://github.com/Petrus-Canisius/RVZ-Converter/blob/v2.0.2/THIRD-PARTY-NOTICES.md).
