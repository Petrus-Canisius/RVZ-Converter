# Engine installation and verification

Do not rename or extract a single executable away from its dependencies. User-level installation is sufficient. No installation or source-build command below is run automatically by the application.

## Discovery locations

Application-local `tools/dolphin` and `tools/wit` come first, then a remembered valid path, PATH, then:

- Windows: `%ProgramFiles%/Dolphin`, `%ProgramFiles%/Dolphin Emulator`, `%ProgramFiles%/Wiimms ISO Tools/bin`, and the system drive's `wit/bin`.
- Linux: `/usr/local/bin`, `/usr/bin`, `/opt/dolphin/bin`, `/opt/wit/bin`.
- macOS: `/Applications/Dolphin.app/Contents/MacOS`, `~/Applications/Dolphin.app/Contents/MacOS`, `/opt/homebrew/bin`, `/usr/local/bin`, `/opt/local/bin`, `/opt/wit/bin`.

Unix Dolphin executable names are `dolphin-tool` or `DolphinTool`. Windows uses `DolphinTool.exe`. WIT uses `wit` or `wit.exe`. Use Locate to select a native executable installed elsewhere. Shell and Flatpak wrappers are not supported. Unix executables need execute permission. The engine must match the native OS architecture. Rosetta, WOW and QEMU emulation are not enabled.

## Availability reviewed 2026-10-23

[Dolphin's official downloads](https://dolphin-emu.org/download/?lang=en) advertise Windows x64 and ARM64, universal macOS and Linux x86_64/aarch64 Flatpak builds. A GUI or Flatpak listing does not prove that the standalone CLI works outside its sandbox. Check the installed distribution for DolphinTool. Use the source procedure below if it is absent. Only Windows x64 DolphinTool was executed here.

[WIT's official downloads](https://wit.wiimm.de/download.html) advertise v3.05a for Linux x86_64, Windows Cygwin64 and universal macOS x86_64/arm64. No native Windows ARM64 or Linux ARM64 WIT binary is advertised. The macOS ARM64 slice is advertised but not runtime-verified here. The application rejects x64 Cygwin WIT on Windows ARM64.

## Verify on the target machine

Run these from a terminal in the installed engine's directory (use `./dolphin-tool` and `./wit` on Unix):

```text
DolphinTool.exe convert --help
DolphinTool.exe header --help
wit.exe HELP COPY
wit.exe TEST --enc AUTO --split-size 4000M --progress --sections
```

The application additionally makes a small synthetic ISO → RVZ probe requesting `--format rvz --compression zstd --compression_level 5 --block_size 131072`, verifies the written header fields and cleans only that owned probe directory. This validates preset capability, not a game. Failed startup, help, preset or disc reopening disables/fails the affected route with an explanation. Raw native engine output remains verbatim in the log.

On Linux, inspect `file ./dolphin-tool ./wit` and run `ldd` on both tools. On macOS, use `file`, `lipo -archs` and `otool -L`. Verify an ARM64 slice on Apple Silicon. On Windows, install the full architecture-matched distributions. Include Dolphin's Visual C++ runtime and WIT's Cygwin DLLs. Successful startup and help exit codes verify runtime dependencies. Download listings alone are not validation.

## Official-source build procedures (NOT RUN on this Windows machine)

Use a native machine of the desired architecture. Create a new source/build directory. Review upstream dependencies and pin a release or commit. These procedures are starting points. Establish new architecture support with a successful native build and test.

### DolphinTool on Linux or macOS, including ARM64

Consult [Dolphin's Linux build guide](https://github.com/dolphin-emu/dolphin/wiki/Building-for-Linux) or [macOS build guide](https://github.com/dolphin-emu/dolphin/wiki/Building-for-macOS). Current source needs a C++23 toolchain, CMake and upstream dependencies. On macOS, use Xcode command-line tools and native architecture dependencies. On Linux, install the listed development packages. Package names vary by distribution.

```sh
git clone --recurse-submodules https://github.com/dolphin-emu/dolphin.git dolphin-source
cd dolphin-source
cmake -S . -B build -DENABLE_QT=OFF -DENABLE_TESTS=OFF -DCMAKE_BUILD_TYPE=Release
cmake --build build --target dolphin-tool --parallel
./build/Binaries/dolphin-tool convert --help
./build/Binaries/dolphin-tool header --help
```

`dolphin-tool` is the [upstream CMake target](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DolphinTool/CMakeLists.txt). Keep runtime dependencies and licenses with the executable. Install it under your user directory, then use Locate. Windows x64/ARM64 users should prefer the matching official distribution. A Windows source build needs the upstream Visual Studio prerequisites and native dependencies. No cross-compiled engine is verified here.

### WIT on Linux ARM64 (experimental until native validation passes)

The [official GitHub mirror](https://github.com/Wiimm/wiimms-iso-tools) includes the project Makefile and setup script. On a native Linux ARM64 system install GNU make, GCC, Bash, gawk, zlib, ncurses/tinfo development headers, and the optional crypto/dependencies required by your build. A GNU toolchain is expected. Do not force an x86 `-m32`/`-m64` build.

```sh
git clone https://github.com/Wiimm/wiimms-iso-tools.git wit-source
cd wit-source/project
make
file bin/wit
ldd bin/wit
./bin/wit HELP COPY
./bin/wit TEST --enc AUTO --split-size 4000M --progress --sections
```

Keep the output, required data and licenses together in a user directory, then Locate `bin/wit`. If the upstream setup's architecture detection or compiler assumptions fail, a port is required: this application does not claim WIT/Linux ARM64 support until those failures are resolved and a native synthetic plus real-image conversion has passed. The procedure has not been executed here.

For macOS, prefer the official universal build. Verify its architecture slices and dependencies. Source builds need GNU make, gawk and upstream dependencies for each architecture. **Windows ARM64 remains blocked for WIT.** No native build or runtime distribution was verified. The Cygwin x64 package is incompatible with the app's native-only engine policy. WIT conversions remain unavailable until a compatible native engine is supplied and verified. No unverified port or emulation method is presented as supported.

## Formats and transformations

Routes follow installed engine help and official documentation. See the [Dolphin convert implementation](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DolphinTool/ConvertCommand.cpp), [Dolphin readers](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DiscIO/Blob.cpp), [WIT COPY documentation](https://wit.wiimm.de/wit/copy.html) and [WIA/RVZ specification](https://github.com/dolphin-emu/dolphin/blob/master/docs/WiaAndRvz.md).

WIT 3.05a does not read RVZ. Dolphin creates an ISO first. WIT can read ISO, GCZ, WIA, CISO and WBFS when its installed decoder can reopen the image. WIT's `--raw` ISO fallback cannot restore removed data. CISO and WBFS select all partitions and scrub unused blocks while using automatic encoding. Container readability checks are separate from full disc integrity checks.
