# Platform requirements and installation

The application uses one Avalonia interface and includes the .NET 10 runtime. Packages are architecture specific. No administrator or root access is needed to run them.

Release 2.0.2 distributes Windows x64, Linux x64/ARM64 and macOS Intel/Apple Silicon. Windows ARM64 is withheld because native WIT is missing. The six native CI jobs passing does not make that artifact a complete package. [Validation and remaining limitations](FINAL-VALIDATION-2.0.2.md).

Download `SHA256SUMS.txt` alongside the selected archive from the same release. Compare the exact filename and digest before extraction. Windows PowerShell: `Get-FileHash .\RVZConverter-win-x64.zip -Algorithm SHA256`. Linux: `sha256sum RVZConverter-linux-x64.tar.gz` (replace `x64` with `arm64` when appropriate). macOS: `shasum -a 256 RVZConverter-osx-arm64.zip` (use `osx-x64` on Intel). The checksum list covers application and matching engine source archives. Source archives are for redistribution/building; they are not required to run the application.

## Windows

Extract the complete ZIP. Open the root `RVZConverter.exe`. Keep `app`, `tools`, `docs` and `licenses` together. The native launcher resolves `app/RvzConverter.exe` from its own path and forwards arguments. Application DLLs stay under `app`.

Native engine builds use the static Microsoft C++ runtime. WIT on Windows x64 includes its app-local Cygwin runtime. No system component is installed at app startup. Windows 11 is the desktop target. The experimental Windows ARM64 artifact includes DolphinTool only and cannot produce CISO or WBFS without a future compatible native WIT port. It is not published as a complete engine package. Do not use an x64 engine as evidence of native ARM64 support.

## Linux

The build baseline is Ubuntu 24.04 with glibc 2.39. Packages target x64 or ARM64. They do not target Alpine/musl or every Linux distribution.

Extract the complete tar.gz archive and run `./RVZConverter`. Preserve executable permissions. The root shell entry point resolves `app/RvzConverter` from the installation location. It supports paths containing spaces and Unicode and forwards arguments.

Use X11 or XWayland with readable system fonts. On Ubuntu 24.04, install missing prerequisites with:

```sh
sudo apt-get update
sudo apt-get install libx11-6 libice6 libsm6 libfontconfig1 libicu74 libssl3t64 zlib1g libgcc-s1 libstdc++6 libudev1 libcap2 libncurses6 libtinfo6 fonts-dejavu-core
mkdir RVZConverter
tar -xzf RVZConverter-linux-x64.tar.gz -C RVZConverter
cd RVZConverter
./RVZConverter
```

Use the `linux-arm64` archive on ARM64. `libcap2` is included in this list because the native CI dependency record resolves `libcap.so.2`. Native file pickers and folder opening also need a desktop portal or file manager. These operating system ABI libraries are not redistributed. `ldd` and `readelf` results are saved by native CI. Package names differ on other distributions.

To add a launcher to the current user's application menu, run `sh docs/install-desktop.sh`. It writes only to the user's XDG application directory. Keep the package at that path afterward. Run it again after relocating the package. The original SVG icon is under `docs/App.svg`. To remove the optional entry, remove `rvz-converter.desktop` from that user directory.

## macOS

macOS 14 or newer is required. Download the Intel or Apple Silicon package for the native CPU. Extract the ZIP and open `RVZ Converter.app`. The only other root file is `README.txt`. The native .NET apphost is under `Contents/MacOS`. Its managed application and runtime files are under `Contents/Resources/app`. Engines, documentation and notices are also under `Contents/Resources`.

The app has no Developer ID signature and is not notarized. Build-time ad-hoc signatures support local execution and do not establish a trusted publisher identity. Nested native files are signed before the app bundle. Gatekeeper may block downloaded packages. Follow your organization's software approval process. No command to disable Gatekeeper or remove quarantine is provided. Rosetta is not required or assumed. Native CI ran on macOS 15; the declared macOS 14 minimum was checked statically in engine load commands, not by running on macOS 14.

## User data and temporary files

Windows stores preferences and logs under `%LOCALAPPDATA%/RVZConverter`. Linux uses XDG config, cache and state directories. macOS uses the user's Application Support, Caches and Logs directories. Temporary conversion data is stored in the selected temporary directory. Application installation folders are not used for conversion output or user settings.

See [technical validation](VALIDATION.md) for the exact native tests. Build or UI smoke success does not verify every conversion route or every real disc.

For a reproducible package check, run the extracted entry point with `--verify-package /absolute/path/to/report.txt` (Windows: `RVZConverter.exe`; macOS: `RVZ Converter.app/Contents/MacOS/RvzConverter`). It creates temporary generated non-game images in the user cache, runs conversions and writes diagnostics. It deliberately reproduces and captures Dolphin's unsupported direct-CISO assertion; that expected diagnostic is not accepted as conversion output. Use a writable report location and allow up to 10 GiB of free temporary space. This optional check requires no SDK or engine download. It does not test real games. Use `--smoke-ui /absolute/path/to/ui-report` separately in a graphical session to record a native launch.
