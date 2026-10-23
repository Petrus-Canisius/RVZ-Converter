# Third-party notices

**RVZ Converter** application: created by **Petrus-Canisius**. Product, Authors and Company metadata retain this identity. This attribution applies to the application and its original icon, not the conversion engines or dependencies below. No Dolphin or Nintendo logo is used. No application license is inferred from third-party licenses.

## Separately installed conversion engines (not bundled)

- **DolphinTool**, part of **Dolphin Emulator**, by the Dolphin Emulator project contributors. [Official project](https://dolphin-emu.org/), [source and license](https://github.com/dolphin-emu/dolphin). Source files identify **GPL-2.0-or-later**. The complete distribution may contain additional third-party dependencies/notices. Keep them intact.
- **Wiimms ISO Tools (WIT)** is by **Wiimm (Dirk Clemens) and contributors**. See the [official project](https://wit.wiimm.de/) and [source mirror](https://github.com/Wiimm/wiimms-iso-tools). Its license is **GPL-2.0-or-later**. See `project/gpl-2.0.txt`. Windows packages include Cygwin and other libraries. Keep their licenses and runtime dependencies with them.

Neither engine is authored by Petrus-Canisius. They are launched as independent native processes. The published application contains no engine binaries, engine DLLs or automatic downloader. Before redistributing either engine, separately review and fulfill the applicable license, corresponding-source and dependency requirements. These packages make no claim that an engine redistribution review has been completed.

## Bundled application dependencies

- **Avalonia 11.3.22** and platform backends, AvaloniaUI contributors / AvaloniaUI OÜ, **MIT**. [Pinned release](https://github.com/AvaloniaUI/Avalonia/releases/tag/11.3.22), [license](https://github.com/AvaloniaUI/Avalonia/blob/11.3.22/licence.md). `licenses/Avalonia-LICENSE.txt` contains the notice.
- **MicroCom.Runtime 0.11.0**, Nikita Tsukanov, **MIT**. [Source](https://github.com/kekekeks/MicroCom). Notice in `licenses/MicroCom-LICENSE.txt`.
- **Tmds.DBus.Protocol 0.21.3**, Tom Deseyn and contributors, **MIT**. [Source](https://github.com/tmds/Tmds.DBus). Notice in `licenses/Tmds-DBus-LICENSE.txt`.
- **SkiaSharp 2.88.9**, Microsoft and contributors, **MIT**. Bundled Skia and native dependencies have additional licenses. Package license and notice files are copied into `licenses/SkiaSharp-*` and native-asset directories.
- **HarfBuzzSharp 8.3.1.1**, Microsoft and contributors, **MIT**, with HarfBuzz under its upstream permissive license. Native-asset package notices are preserved under `licenses/HarfBuzzSharp*`.
- **ANGLE Windows native assets**, Google / ANGLE contributors, **BSD-3-Clause** and dependency notices in the resolved package's LICENSE.
- **Microsoft .NET 10 runtime**, .NET Foundation / Microsoft and contributors, **MIT** with additional third-party components. The target runtime's LICENSE.TXT and THIRD-PARTY-NOTICES.TXT are copied into `licenses/Microsoft.NETCore.App` for each self-contained package.

The publish script copies license, notice and NuGet metadata from the resolved package versions. Managed platform assemblies may appear in several packages. Foreign native conversion engines are never included. See each package's notices for full texts. Fonts come from the operating system and are not redistributed. Avalonia.Headless is a test dependency.
