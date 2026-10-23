# RVZ Converter

RVZ Converter is a desktop application for converting Wii and GameCube disc images. It is created by **Petrus-Canisius** and built with C#, .NET 10 and Avalonia.

## Downloads

Release `v2.0.0-rc.1` contains application version `2.0.0`. Packages are available from the [GitHub Releases page](https://github.com/Petrus-Canisius/RVZ-Converter/releases/tag/v2.0.0-rc.1).

| Operating system | Architecture | Download |
| --- | --- | --- |
| Windows | x64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-win-x64.zip) |
| Windows | ARM64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-win-arm64.zip) |
| Linux | x64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-linux-x64.tar.gz) |
| Linux | ARM64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-linux-arm64.tar.gz) |
| macOS | x64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-osx-x64.zip) |
| macOS | ARM64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-osx-arm64.zip) |

See [SHA256SUMS.txt](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/SHA256SUMS.txt) to verify downloads. Extract the whole package and keep its runtime files and notices together. Packages are self-contained and do not require a separate .NET installation.

## Formats and use

The application detects format and platform from image data. Supported inputs are ISO, RVZ, GCZ, WIA, CISO and WBFS. Outputs are ISO, RVZ, CISO and WBFS.

### Implemented conversion matrix

| Input | ISO | RVZ | CISO | WBFS (Wii only) |
| --- | --- | --- | --- | --- |
| ISO | Same format | DolphinTool | WIT | WIT |
| RVZ | DolphinTool | Same format | DolphinTool to ISO, then WIT | DolphinTool to ISO, then WIT |
| GCZ | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| WIA | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| CISO | DolphinTool or WIT fallback | DolphinTool or WIT → ISO → DolphinTool fallback | Not supported | WIT |
| WBFS | DolphinTool or WIT fallback | DolphinTool or WIT → ISO → DolphinTool fallback | WIT | Not supported |

The application enables a route only when an installed engine passes its checks and can inspect the image. Same-format conversions are not supported. WBFS output is restricted to Wii discs. CISO means the Wii/GameCube format, not PSP CSO.

DolphinTool handles ISO and RVZ output. If DolphinTool cannot read a source that WIT can read, WIT can create an intermediate ISO. RVZ output then needs DolphinTool. CISO and WBFS output use WIT. RVZ to CISO or WBFS needs DolphinTool to create an intermediate ISO first. Intermediate files and destination staging require extra disk space.

The matrix describes implemented routes. It does not mean every route has been validated with a real disc image. See [technical validation records](docs/VALIDATION.md) for test scope and results.

1. Install the required conversion engines separately. They are **not included** in the application packages.
2. Add images by file picker or drag and drop.
3. Choose an output format and destination. Choose a temporary folder if needed.
4. Start the queue. Files run in order. The log explains each conversion step.

The RVZ preset uses Zstandard level 5 with 128 KiB blocks. Engine versions without these options are rejected. The application never changes or removes source images. Outputs are not overwritten. CISO and WBFS transformations are not guaranteed to preserve bits. Scrubbed or incomplete source data cannot be restored.

## Conversion engines

Install complete native engine distributions with their dependencies. The application checks its `tools` directory, remembered paths, `PATH` and documented standard locations. It does not scan whole disks or download engines.

- [DolphinTool](https://dolphin-emu.org/) is part of Dolphin Emulator by the Dolphin Emulator project contributors. It provides supported ISO and RVZ conversions.
- [Wiimms ISO Tools (WIT)](https://wit.wiimm.de/) is by Wiimm and contributors. It provides supported CISO and WBFS conversions.

Neither engine is created by Petrus-Canisius. Review [engine setup and license details](docs/ENGINES.md) before installation. This application does not redistribute either engine.

## Platform requirements and installation

Install DolphinTool and WIT separately. The application packages do not include conversion engines. Follow the [engine installation guide](docs/ENGINES.md) to install compatible native builds and their dependencies.

On Windows, extract the ZIP and run `RvzConverter.exe`. On Linux, install the required system libraries, extract the tar.gz archive, and run the application executable. Linux requires glibc, X11 or XWayland, Fontconfig, and native .NET dependencies. On macOS, extract the ZIP and open the app bundle. The macOS packages are unsigned and not notarized. Gatekeeper or local security policy may block launch.

The application uses system fonts with Tahoma and cross-platform fallbacks. Native dialogs and title bars follow the operating system language. See [platform requirements and limitations](docs/PLATFORMS.md) and [technical validation records](docs/VALIDATION.md). CI results do not validate every package or conversion route.

## Build and test

Install the .NET 10 SDK and PowerShell 7. From the repository directory, run:

```powershell
pwsh -File eng/build.ps1
pwsh -File eng/test.ps1
pwsh -File eng/test.ps1 -WithEngines
```

The native engine tests require DolphinTool and WIT installed on the build machine. They use synthetic data and do not download games. See [build and publishing instructions](docs/PLATFORMS.md).

## Limitations

Validation checks process exit codes, container metadata and reopening by a compatible engine. It checks container readability. It is not full disc-integrity verification. Native engine support varies by platform and source format. FAT32 size limits and conservative space estimates can reject some jobs. Conversion logs contain local paths and engine output. Review logs before sharing them.

## Credits and licenses

RVZ Converter is created by **Petrus-Canisius**. DolphinTool and WIT are independent projects with their own authors and licenses. Application dependency notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `licenses/`. No application license is declared in this repository.
