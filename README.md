# RVZ Converter

RVZ Converter converts Wii and GameCube disc images. Created by **Petrus-Canisius**. It uses .NET 10 and Avalonia with a compact Windows 98-inspired interface.

## Downloads

Version 2.0.2 provides five complete native distributions. Extract the complete archive before launching. The application includes its .NET runtime, DolphinTool and WIT; no development environment is required.

| Operating system | Architecture | Download |
| --- | --- | --- |
| Windows | x64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-win-x64.zip) |
| Windows | ARM64 | Withheld: native WIT is missing |
| Linux | x64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-linux-x64.tar.gz) |
| Linux | ARM64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-linux-arm64.tar.gz) |
| macOS | Intel | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-osx-x64.zip) |
| macOS | Apple Silicon | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/RVZConverter-osx-arm64.zip) |

[Release notes](https://github.com/Petrus-Canisius/RVZ-Converter/releases/tag/v2.0.2) and [SHA256 checksums](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.2/SHA256SUMS.txt).

All six native CI jobs passed in [run 40076853202](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40076853202). Windows ARM64 passed only its DolphinTool routes and is not a complete distribution. Its CI artifact is withheld from this release. Older releases remain available in the release history. See the [final validation report](docs/FINAL-VALIDATION-2.0.2.md).

Verify the archive's SHA-256 before extraction; commands and system prerequisites are in the [installation guide](docs/PLATFORMS.md). Archive documentation preserves the release commit's original snapshot. The online report records the later validation and distribution audit.

Windows ARM64 source builds can include native DolphinTool but no native WIT. CISO and WBFS output require WIT. CISO and WBFS input also require WIT for ISO or RVZ output. No emulated engine is bundled.

## Use

1. Windows: open the root `RVZConverter.exe`. Keep `app/` and `tools/` beside it.
2. Linux: run the root `RVZConverter`. Keep the full directory together.
3. macOS: open `RVZ Converter.app`. Keep the app bundle intact.
4. Add source images. Choose the output format, destination and temporary folder.
5. Start the queue. The progress bar and log show each conversion step.

Bundled engines are found without changing PATH. Missing engines have a Locate button in the main window. There is no Settings screen. Preferences and logs stay in the platform's user data directories.

## Supported conversions

Input format and disc platform are detected from image data.

| Input | ISO | RVZ | CISO | WBFS (Wii only) |
| --- | --- | --- | --- | --- |
| ISO | Not supported | DolphinTool | WIT | WIT |
| RVZ | DolphinTool | Not supported | DolphinTool to ISO, then WIT | DolphinTool to ISO, then WIT |
| GCZ | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| WIA | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| CISO | WIT | WIT to ISO, then DolphinTool | Not supported | WIT |
| WBFS | WIT | WIT to ISO, then DolphinTool | WIT | Not supported |

Routes require compatible engines that can inspect the image. Same-format conversions are not supported. WBFS output is restricted to Wii discs. CISO means Wii/GameCube CISO, not PSP CSO.

CISO and WBFS do not record an accurate original image length. DolphinTool cannot convert them directly to ISO or RVZ. WIT creates ISO output or an intermediate ISO first. WIT may add zero padding. This cannot restore missing data. CISO maps smaller than WIT's minimum padded ISO length are rejected before conversion.

Intermediate ISO files, source snapshots and destination staging need extra disk space. RVZ uses Zstandard level 5 and 128 KiB blocks. Source images are never changed or deleted. Existing destination files are not overwritten. FAT32 WBFS parts stay below 4 GiB.

The table describes implemented support. It does not establish real-image verification of every route. Conversion cannot recover data removed from scrubbed, modified or incomplete sources. CISO and WBFS output are not promised to be bit-identical.

## Included engines

- **DolphinTool 2506**, source revision `aed420c95d7a152363917e4b827d6cc85eacb502`. Bundled in all five published packages.
- **WIT 3.05a+rvz1**, based on source revision `fc1c0b840cb3ac41ca6e4f1d5e16da12b47eab58`. Bundled in all five published packages. It includes a GameCube GCZ correction and the Apache-licensed Mbed TLS 2.28.0 SHA-1 provider.

Engines run as separate processes. Nothing is downloaded at application startup. External native engines remain available through Locate. Keep their dependencies with them. See [engine details](docs/ENGINES.md) and [source build instructions](docs/BUILD-ENGINES.md).

Each `EngineSources-<rid>.tar.gz` release asset contains the corresponding engine sources, pinned dependencies and build materials for its matching application archive. License texts and dependency notices are included in the application package. Engine authorship belongs to the Dolphin Emulator project, Wiimm and their contributors.

## Platform requirements

- Windows 11 or later. The packages include the application runtime. Windows x64 includes the required app-local Cygwin runtime for WIT. No system component is installed silently.
- Linux builds target Ubuntu 24.04 with glibc 2.39 or later. They require desktop libraries, X11 or XWayland, Fontconfig and fonts. See the [library list](docs/PLATFORMS.md). These builds do not support every Linux distribution.
- macOS 14 or later. Packages have no Developer ID signature or notarization. Ad-hoc signatures do not establish publisher trust. Gatekeeper or local policy may block launch. No Rosetta dependency is assumed.

Linux includes an optional user-level desktop entry installer in `docs/`. No root access is needed to run the application. See [platform installation instructions](docs/PLATFORMS.md).

## Build and test

Install the .NET 10 SDK and PowerShell 7.

```powershell
pwsh -File eng/build.ps1
pwsh -File eng/test.ps1
pwsh -File eng/test.ps1 -WithEngines
```

Native engine tests use generated non-game fixtures. They do not download games. See [native engine builds](docs/BUILD-ENGINES.md), [publishing](docs/PLATFORMS.md) and [technical validation records](docs/VALIDATION.md).

## Limitations and troubleshooting

The application checks process exit codes, output size, signatures and container reopening. It does not perform complete disc-integrity verification. Separate Windows x64 validation used one Wii image and one GameCube image, checked byte-identical ISO/RVZ round trips and ran Wii partition verification. Other architectures used generated non-game fixtures. These results do not establish compatibility with all Wii games. Large split WBFS outputs and installation on clean operating systems without development tools remain untested. See the [precise test scope](docs/FINAL-VALIDATION-2.0.2.md).

The application reports an engine failure if an engine opens an interactive error dialog on Windows. It never selects Ignore. See the [Dolphin assertion investigation](docs/ASSERTION-INVESTIGATION.md).

Check the detailed log for missing libraries, unsupported routes or insufficient space. Conservative space checks can reject jobs. Logs contain local paths and engine output. Review them before sharing.

## Credits and licenses

RVZ Converter is created by **Petrus-Canisius**. DolphinTool and WIT are independent projects. Neither engine is created by Petrus-Canisius. See [third-party notices](THIRD-PARTY-NOTICES.md) and the package's license directory. No application license is declared in this repository.
