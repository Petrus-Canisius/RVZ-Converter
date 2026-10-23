# RVZ Converter 2.0.0-rc.1

This prerelease adds a shared Avalonia desktop application for Windows, Linux and macOS. It supports data-based inspection for ISO, RVZ, GCZ, WIA, CISO and WBFS inputs, with ISO, RVZ, CISO and WBFS outputs where installed engines support the route. The classic interface supports a conversion queue, progress reporting, logs, engine discovery and cancellation.

The application was built for all six target architectures. Windows x64 was launched and visually checked. Other targets were cross-published. Their native launch and engine checks were not run. The regression suite passed 74/74 tests, including synthetic-image engine tests on Windows x64. Real-image conversion remains **NOT TESTED**. macOS packages are unsigned and not notarized.

DolphinTool and WIT must be installed separately. They are not bundled. See [README.md](README.md) and [third-party notices](THIRD-PARTY-NOTICES.md).
