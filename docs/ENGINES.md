# Conversion engines

RVZ Converter is created by Petrus-Canisius. DolphinTool is by the Dolphin Emulator project contributors. WIT is by Wiimm and contributors. The engines run as separate processes.

The packages include DolphinTool 2506 from revision `aed420c95d7a152363917e4b827d6cc85eacb502`. They include WIT 3.05a from revision `fc1c0b840cb3ac41ca6e4f1d5e16da12b47eab58`, with the `rvz1` compatibility patches. These recognize Dolphin GameCube GCZ subtype 0 as an ordinary GCZ and select portable SHA-1 for the Windows source build. Upstream WIT treats GCZ subtype 0 as NKit. Its unused Cygwin SHA-1 assembly uses an incompatible calling convention when built without external OpenSSL. The binary VERSION command reports the upstream base version. `tools/engines.json` identifies this patched distribution.

Windows ARM64 includes DolphinTool only. WIT's Windows source depends on POSIX APIs provided by Cygwin. Cygwin supports x64 and x64 emulation on Windows ARM64. It does not provide a native ARM64 runtime. This application does not enable emulation. CISO and WBFS outputs remain disabled in that package. It is not a complete engine package.

Bundled engines are found relative to the application. Windows and Linux use sibling `tools/dolphin` and `tools/wit`. macOS uses `Contents/Resources/tools` inside the app bundle. Discovery does not depend on the current working directory or PATH. It checks native PE, ELF or Mach-O architecture and execute permissions. It then runs the required help commands. DolphinTool must also create the requested RVZ preset with a synthetic capability fixture.

The recovery controls in the main window can locate an external engine. A selected executable must pass the same architecture, dependency and argument checks. Valid locations are remembered in the normal user preference directory. There is no Settings screen and no automatic engine download.

ISO and RVZ outputs from ISO, RVZ, GCZ and WIA use DolphinTool. The RVZ preset is Zstandard level 5 with 128 KiB blocks. WIT writes Wii/GameCube CISO and Wii-only WBFS. RVZ input requires an intermediate ISO before WIT. CISO and WBFS to ISO use WIT. CISO and WBFS to RVZ use a WIT intermediate ISO because those source containers do not store the original disc length. See the [assertion investigation](ASSERTION-INVESTIGATION.md). The application inspects source data and rejects same-format conversion. WIT output transformations are not promised to preserve every source bit. Conversion cannot recreate missing source data.

Engine stdout and stderr are logged together with exit codes. The application validates output size and container readability before publishing files. This is not complete disc integrity verification.

Both engine projects are GPL-2.0-or-later. This distribution uses the GPL version 3 option for combined engine builds. The app's license is unchanged. Full engine and dependency notices are included under `licenses/engines`. Each `EngineSources-<rid>.tar.gz` release asset contains corresponding preferred source, pinned submodules, compatibility patches and build materials. The Windows x64 source asset also contains matching Cygwin, ncurses and zlib source packages. A link to an upstream homepage is not the source distribution mechanism.

See [engine builds](BUILD-ENGINES.md), [system requirements](PLATFORMS.md), [technical validation](VALIDATION.md) and [third-party notices](../THIRD-PARTY-NOTICES.md).
