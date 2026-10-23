# Platform requirements and limitations

One Avalonia 11.3.22 application targets .NET 10 on six runtime identifiers. Avalonia provides the shared interface without a Windows Desktop dependency. See [Avalonia platform support](https://docs.avaloniaui.net/docs/supported-platforms), [.NET supported operating systems](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md) and [Linux runtime dependencies](https://learn.microsoft.com/en-us/dotnet/core/install/linux).

Self-contained packages include .NET. They do not include operating system libraries, a display server or conversion engines.

- **Windows:** use supported Windows 10/11 x64 or Windows 11 ARM64. Administrator privileges are not required. Extract the complete portable ZIP and start the root `RVZConverter.exe`. Keep the `app` directory beside it. Optional engine distributions can go in sibling `tools/dolphin` and `tools/wit` directories. DolphinTool needs its matching Visual C++ runtime. WIT needs its Cygwin DLLs. Building the Windows launcher requires Visual Studio C++ build tools or a matching MinGW cross compiler.
- **Linux:** glibc is required. Alpine/musl is not targeted. Use X11 or XWayland. The app needs X11 libraries (`libX11`, `libICE`, `libSM`), Fontconfig, readable fonts, Skia/HarfBuzz and the .NET native dependencies. The .NET dependencies include libc, libgcc, libstdc++, ICU, OpenSSL and zlib.

On Ubuntu 24.04, a typical base set is `libx11-6 libice6 libsm6 libfontconfig1 libicu74 libssl3t64 zlib1g libgcc-s1 libstdc++6`. Package names vary by release. File pickers and folder launching need the desktop portal or file manager. Check the apphost and native `.so` files with `ldd`. Linux ARM64 needs native ARM64 libraries and engines. Native Linux execution was NOT RUN on this Windows host.

- **macOS:** packages target macOS 14 or newer. Each bundle contains an apphost and native libraries for its named architecture. Check native libraries with `otool -L`. Apple Silicon engines need an ARM64 slice. The app bundles use the same application source.

The macOS packages have no Developer ID signature and are not notarized. Any SDK ad-hoc metadata is not Developer ID signing. Gatekeeper or library policies may prevent launch. Validate on a native Mac. No security-bypass command is provided. Native signing and notarization have NOT RUN.

PE, ELF and Mach-O architecture checks prove that publish files were produced. They do not prove native launch, display, permissions, library resolution, engine availability or conversion. No emulation dependency is supported. See the [validation matrix](VALIDATION.md).

## Native CI

`.github/workflows/native.yml` defines Windows, Linux and macOS jobs. It includes native ARM64 labels. Tests check runner architecture before launch. Linux visual smoke uses Xvfb. CI installs app dependencies, not conversion engines or game images.

CI does not install DolphinTool, WIT or game images. The workflow checks runner architecture before native application and launcher smoke tests. Check its run status for the exact commit. An ARM64 cross-publish is not a substitute for the Windows ARM64 runtime test. The application does not enable emulation.
