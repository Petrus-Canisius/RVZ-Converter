# RVZ Converter 2.0.1

Created by **Petrus-Canisius**. DolphinTool and Wiimms ISO Tools (WIT) are independent projects. Install them separately. They are not bundled.

## Downloads

| Operating system | Architecture | Download |
| --- | --- | --- |
| Windows | x64 | [ZIP, version 2.0.1](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.1/RVZConverter-win-x64.zip) |
| Windows | ARM64 | [ZIP, version 2.0.1](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.1/RVZConverter-win-arm64.zip) |
| Linux | x64 | [tar.gz, version 2.0.0](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-linux-x64.tar.gz) |
| Linux | ARM64 | [tar.gz, version 2.0.0](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-linux-arm64.tar.gz) |
| macOS | x64 | [ZIP, version 2.0.0](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-osx-x64.zip) |
| macOS | ARM64 | [ZIP, version 2.0.0](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-osx-arm64.zip) |

The new Windows package root contains `RVZConverter.exe`, `README.txt`, `app/`, `docs/`, and `licenses/`. Extract the complete archive. Keep `app/` beside the root launcher. Windows packages include the .NET runtime. They do not require administrator privileges.

The Windows launcher uses the original application icon and Petrus-Canisius metadata. It starts the application from `app/` and forwards arguments. Optional engine distributions can be placed in sibling `tools/dolphin/` and `tools/wit/` folders. You can also locate installed executables in the application. Keep engine dependencies with their executables.

## Implemented conversion matrix

| Input | ISO | RVZ | CISO | WBFS (Wii only) |
| --- | --- | --- | --- | --- |
| ISO | Same format | DolphinTool | WIT | WIT |
| RVZ | DolphinTool | Same format | DolphinTool → ISO → WIT | DolphinTool → ISO → WIT |
| GCZ | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| WIA | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| CISO | DolphinTool or WIT fallback | DolphinTool or WIT → ISO → DolphinTool fallback | Not supported | WIT |
| WBFS | DolphinTool or WIT fallback | DolphinTool or WIT → ISO → DolphinTool fallback | WIT | Not supported |

Routes depend on engine availability and image readability. Same-format conversion is not supported. WBFS output is restricted to Wii discs. CISO means the Wii/GameCube format, not PSP CSO. Intermediate ISO files need extra disk space. Conversion cannot restore data already missing from a source image.

## Validation and limitations

The Windows x64 launcher and portable layout passed a local runtime smoke test from a different working directory with paths containing spaces and Unicode. The ARM64 package was cross-published. Check [docs/VALIDATION.md](docs/VALIDATION.md) for its native runtime status and the detailed test record.

This packaging update does not validate every conversion route with real disc images. Real-image conversion remains unverified. Linux and macOS downloads remain version 2.0.0 packages. The macOS packages are unsigned and not notarized.

See [README.md](README.md), [engine setup](docs/ENGINES.md), [platform limitations](docs/PLATFORMS.md), and [third-party notices and licenses](THIRD-PARTY-NOTICES.md). DolphinTool is part of Dolphin Emulator. WIT is by Wiimm and contributors. Neither engine is created by Petrus-Canisius.
