# Platform requirements and installation

The application uses one Avalonia interface and includes the .NET 10 runtime. Packages are architecture specific. No administrator or root access is needed to run them.

## Windows

Extract the complete ZIP. Open the root `RVZConverter.exe`. Keep `app`, `tools`, `docs` and `licenses` together. The native launcher resolves `app/RvzConverter.exe` from its own path and forwards arguments. Application DLLs stay under `app`.

Native engine builds use the static Microsoft C++ runtime. WIT on Windows x64 includes its app-local Cygwin runtime. No system component is installed at app startup. Windows 11 is the desktop target. Windows ARM64 includes DolphinTool only and cannot produce CISO or WBFS without a future compatible native WIT port. It is not a complete engine package.

## Linux

The build baseline is Ubuntu 24.04 with glibc 2.39. Packages target x64 or ARM64. They do not target Alpine/musl or every Linux distribution.

Extract the complete tar.gz archive and run `./RVZConverter`. Preserve executable permissions. The root shell entry point resolves `app/RvzConverter` from the installation location. It supports paths containing spaces and Unicode and forwards arguments.

Use X11 or XWayland with readable system fonts. Ubuntu desktop dependencies include `libx11-6 libice6 libsm6 libfontconfig1 libicu74 libssl3t64 zlib1g libgcc-s1 libstdc++6 libudev1 libncurses6 libtinfo6`. Native file pickers and folder opening also need a desktop portal or file manager. These operating system ABI libraries are not redistributed. `ldd` and `readelf` results are saved by native CI. Package names differ on other distributions.

To add a launcher to the current user's application menu, run `sh docs/install-desktop.sh`. It writes only to the user's XDG application directory. Keep the package at that path afterward. Run it again after relocating the package. The original SVG icon is under `docs/App.svg`. To remove the optional entry, remove `rvz-converter.desktop` from that user directory.

## macOS

macOS 14 or newer is required. Download the Intel or Apple Silicon package for the native CPU. Extract the ZIP and open `RVZ Converter.app`. The only other root file is `README.txt`. Runtime files are under `Contents/MacOS`. Engines, documentation and notices are under `Contents/Resources`.

The app has no Developer ID signature and is not notarized. Build-time ad-hoc signatures support local execution and do not establish a trusted publisher identity. Nested native files are signed before the app bundle. Gatekeeper may block downloaded packages. Follow your organization's software approval process. No command to disable Gatekeeper or remove quarantine is provided. Rosetta is not required or assumed.

## User data and temporary files

Windows stores preferences and logs under `%LOCALAPPDATA%/RVZConverter`. Linux uses XDG config, cache and state directories. macOS uses the user's Application Support, Caches and Logs directories. Temporary conversion data is stored in the selected temporary directory. Application installation folders are not used for conversion output or user settings.

See [technical validation](VALIDATION.md) for the exact native tests. Build or UI smoke success does not verify every conversion route or every real disc.
