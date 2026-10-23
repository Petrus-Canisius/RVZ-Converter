# RVZ Converter 2.0.0-rc.1

RVZ Converter is a desktop application created by **Petrus-Canisius**. It converts Wii and GameCube disc images through DolphinTool and Wiimms ISO Tools (WIT). Both engines must be installed separately. They are not bundled.

## Downloads

| Operating system | Architecture | Download |
| --- | --- | --- |
| Windows | x64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-win-x64.zip) |
| Windows | ARM64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-win-arm64.zip) |
| Linux | x64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-linux-x64.tar.gz) |
| Linux | ARM64 | [tar.gz](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-linux-arm64.tar.gz) |
| macOS | x64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-osx-x64.zip) |
| macOS | ARM64 | [ZIP](https://github.com/Petrus-Canisius/RVZ-Converter/releases/download/v2.0.0-rc.1/RVZConverter-osx-arm64.zip) |

## Implemented conversion matrix

| Input | ISO | RVZ | CISO | WBFS (Wii only) |
| --- | --- | --- | --- | --- |
| ISO | Same format | DolphinTool | WIT | WIT |
| RVZ | DolphinTool | Same format | DolphinTool → ISO → WIT | DolphinTool → ISO → WIT |
| GCZ | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| WIA | DolphinTool or WIT fallback | DolphinTool | WIT | WIT |
| CISO | DolphinTool or WIT fallback | DolphinTool or WIT → ISO → DolphinTool fallback | Not supported | WIT |
| WBFS | DolphinTool or WIT fallback | DolphinTool or WIT → ISO → DolphinTool fallback | WIT | Not supported |

Routes depend on installed engine checks and image readability. If DolphinTool cannot read a source that WIT can read, WIT can create an intermediate ISO. RVZ output still requires DolphinTool. RVZ input to CISO or WBFS requires DolphinTool to create an intermediate ISO before WIT writes the final format. These intermediates and destination staging require extra disk space.

Same-format conversion is not supported. WBFS output is restricted to Wii discs. CISO means the Wii/GameCube format, not PSP CSO. Missing data already removed from a scrubbed, modified or incomplete source cannot be restored. CISO and WBFS output is not promised to be bit-for-bit identical.

## Validation and limitations

The conversion matrix lists implemented routes. CI checks do not establish that every downloadable package or conversion route works in every environment. See [technical validation records](docs/VALIDATION.md) for the exact test scope and results.

macOS packages are unsigned and not notarized. See [README.md](README.md), [platform limitations](docs/PLATFORMS.md), and [third-party notices and licenses](THIRD-PARTY-NOTICES.md).
