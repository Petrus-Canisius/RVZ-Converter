# Third-party notices

**RVZ Converter** application: created by **Petrus-Canisius**. Product, Authors and Company metadata retain this identity. This attribution applies to the application and its original icon, not the conversion engines or dependencies below. No Dolphin or Nintendo logo is used. No application license is inferred from third-party licenses.

## Bundled conversion engines

- **DolphinTool**, part of **Dolphin Emulator**, by the Dolphin Emulator project contributors. [Official project](https://dolphin-emu.org/), [source and license](https://github.com/dolphin-emu/dolphin). Source files identify **GPL-2.0-or-later**. The complete distribution may contain additional third-party dependencies/notices. Keep them intact.
- **Wiimms ISO Tools (WIT)** is by **Wiimm (Dirk Clemens) and contributors**. See the [official project](https://wit.wiimm.de/) and [source mirror](https://github.com/Wiimm/wiimms-iso-tools). Its license is **GPL-2.0-or-later**. See `project/gpl-2.0.txt`. Windows packages include Cygwin and other libraries. Keep their licenses and runtime dependencies with them.

Neither engine is authored by Petrus-Canisius. They run as separate native processes. DolphinTool 2506 is built from revision `aed420c95d7a152363917e4b827d6cc85eacb502`. WIT 3.05a is built from revision `fc1c0b840cb3ac41ca6e4f1d5e16da12b47eab58`. Windows ARM64 includes DolphinTool only. WIT's Windows implementation requires Cygwin, which has no native ARM64 runtime.

Each package records its engine versions in `tools/engines.json`. Engine GPL texts and dependency notices are under `licenses/engines`. The matching `EngineSources-<rid>.tar.gz` release asset contains the preferred source, pinned submodule sources, build scripts and revision records. Windows x64 also includes the exact source packages for the redistributed Cygwin runtime and statically linked ncurses and zlib dependencies. The application license is unchanged. No application license is inferred from these separate programs.

Dolphin's bundled library sources and their notices are preserved. These include compression libraries, fmt, libusb, mbedTLS, curl and the other upstream build dependencies. Linux system ABI libraries and macOS system frameworks are not redistributed. See `docs/PLATFORMS.md` for system prerequisites. No engine is downloaded at application startup.

## Bundled application dependencies

- **Avalonia 11.3.22** and platform backends, AvaloniaUI contributors / AvaloniaUI OÜ, **MIT**. [Pinned release](https://github.com/AvaloniaUI/Avalonia/releases/tag/11.3.22), [license](https://github.com/AvaloniaUI/Avalonia/blob/11.3.22/licence.md). `licenses/Avalonia-LICENSE.txt` contains the notice.
- **MicroCom.Runtime 0.11.0**, Nikita Tsukanov, **MIT**. [Source](https://github.com/kekekeks/MicroCom). Notice in `licenses/MicroCom-LICENSE.txt`.
- **Tmds.DBus.Protocol 0.21.3**, Tom Deseyn and contributors, **MIT**. [Source](https://github.com/tmds/Tmds.DBus). Notice in `licenses/Tmds-DBus-LICENSE.txt`.
- **SkiaSharp 2.88.9**, Microsoft and contributors, **MIT**. Bundled Skia and native dependencies have additional licenses. Package license and notice files are copied into `licenses/SkiaSharp-*` and native-asset directories.
- **HarfBuzzSharp 8.3.1.1**, Microsoft and contributors, **MIT**, with HarfBuzz under its upstream permissive license. Native-asset package notices are preserved under `licenses/HarfBuzzSharp*`.
- **ANGLE Windows native assets**, Google / ANGLE contributors, **BSD-3-Clause** and dependency notices in the resolved package's LICENSE.
- **Microsoft .NET 10 runtime**, .NET Foundation / Microsoft and contributors, **MIT** with additional third-party components. The target runtime's LICENSE.TXT and THIRD-PARTY-NOTICES.TXT are copied into `licenses/Microsoft.NETCore.App` for each self-contained package.

The publish script copies license, notice and NuGet metadata from the resolved package versions. Managed platform assemblies may appear in several packages. Foreign native conversion engines are never included. See each package's notices for full texts. Fonts come from the operating system and are not redistributed. Avalonia.Headless is a test dependency.
