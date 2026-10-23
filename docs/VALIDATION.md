# Validation record: 2026-10-23

The build machine runs Windows x64 with .NET SDK 10.0.402.

- The shared Core and Avalonia app built in Release with zero warnings and errors.
- The regression suite passed 74/74 tests. It covers conversion routes, RVZ arguments, inspection, intermediates, failures, Unicode paths, collisions, cleanup, split publication, cancellation, disk space, FAT32, discovery, executable compatibility and platform paths.
- DolphinTool and WIT ran from their local Windows x64 distributions. Their help, dependencies and RVZ preset were checked. Synthetic ISO to RVZ to ISO, ISO/RVZ to CISO, and GCZ/WIA to RVZ conversions passed. The synthetic ISO round trip matched by SHA-256. This does not prove real disc correctness.
- Wii WBFS integration and large real split outputs were NOT TESTED. Split safety and orchestration use controlled fixtures.
- The native Windows UI opened successfully. The main window and About page were inspected. Tab navigation and Escape dismissal worked. Native render checks covered 100, 125, 150 and 200 percent scales and a compact 660 by 580 layout. They showed determinate and indeterminate progress fixtures. They were not four OS display-scaling sessions. Screenshots are in `artifacts/visual-avalonia`.
- No suitable real game image was found. Real-image conversion is NOT TESTED on every platform. No games were downloaded.
- Native CI has NOT RUN. No ARM64 runtime validation is claimed.

The six packages were rebuilt after removing Release debug metadata. Their architecture, launch files, native runtime files, notices and archive checksums were verified. Native runtime validation remains separate.

| Target | Application build | Native launch | DolphinTool | WIT | Real conversion | Package | Blocker |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Windows x64 | Passed | Launched and visually checked | Verified locally | Verified locally | NOT TESTED | `RVZConverter-win-x64.zip` | No real sample. Wii WBFS not tested. |
| Windows ARM64 | Cross-published | NOT RUN | Advertised. Not verified. | No verified native build. | NOT TESTED | `RVZConverter-win-arm64.zip` | Native runner and WIT ARM64. |
| Linux x64 | Cross-published | NOT RUN | CLI/source required. Not verified. | Advertised. Not verified. | NOT TESTED | `RVZConverter-linux-x64.tar.gz` | Native display, dependencies and engines. |
| Linux ARM64 | Cross-published | NOT RUN | CLI/source required. Not verified. | Source build required. NOT RUN. | NOT TESTED | `RVZConverter-linux-arm64.tar.gz` | Native runner and WIT source validation. |
| macOS x64 | Cross-published | NOT RUN | Universal build advertised. Not verified. | Universal build advertised. Not verified. | NOT TESTED | `RVZConverter-osx-x64.zip` | Native runner and engine checks. Unsigned bundle. |
| macOS ARM64 | Cross-published | NOT RUN | Universal build advertised. Not verified. | ARM64 slice advertised. Not verified. | NOT TESTED | `RVZConverter-osx-arm64.zip` | Native runner and engine checks. Unsigned bundle. |
