# Engine build and source distribution

Use PowerShell 7, Git, CMake, Ninja and the .NET 10 SDK. Windows builds use Visual Studio C++ tools for the target architecture. Linux builds use GCC on Ubuntu 24.04. macOS builds use Xcode Command Line Tools with deployment target 14.0. The native CI workflow lists build dependencies.

Run `pwsh -File eng/build-engines.ps1 -RuntimeIdentifier <rid>` from any working directory. The script checks the exact upstream revisions in `eng/engines.lock.json`. Dolphin's submodules are checked out at their parent repository's pinned commits. Only submodules used by this CLI build are downloaded. Qt, FFmpeg and the Dolphin GUI are not distributed.

The Dolphin CMake build uses `USE_SYSTEM_LIBS=OFF`. It builds `dolphin-tool` without Qt, analytics, updates, emulator frontends or tests. Its complete vendored dependency sources and notices are included in the source assets. A packaging patch removes optional Windows PDB generation. It does not change conversion behavior.

WIT uses its upstream Makefile. The build enables zlib and uses `-fcommon` for its legacy C common symbols. On macOS, GNU awk is used by the upstream configuration script. `MACOSX_DEPLOYMENT_TARGET=14.0` pins WIT's compiler and linker baseline. Native inspection rejects an engine with a newer minimum system version. The `rvz1` patches accept standard Dolphin GCZ subtypes and select Mbed TLS SHA-1. All container handling remains upstream code apart from the GCZ subtype correction.

`prepare-wit-sha1.ps1` copies Mbed TLS 2.28.0 from the pinned Dolphin source into the WIT build tree. Only SHA-1 and platform zeroization are compiled. The Apache-2.0 license and complete preferred sources are included. The legacy SSLeay/OpenSSL SHA-1 objects and assembly are not linked. No upstream license exception is invented. A native known-answer test checks empty input, `abc`, incremental updates and one million `a` bytes before packaging. Any hash provider error terminates the engine with a diagnostic. No disc integrity check is bypassed.

Windows x64 builds WIT with an isolated Cygwin installation under `artifacts`. Setup runs without administrator privileges or shortcuts. Upstream repository signatures and package checksums are checked by Cygwin setup. The exact installed versions, installer hash, source URLs and SHA512 values are recorded in `INPUTS.json` inside the source asset. Matching Cygwin, ncurses and zlib source packages are included. Compiler runtime terms include the GCC Runtime Library Exception. Build tools are not shipped in user-facing packages.

The reviewed Cygwin installer hash and redistributed dependency versions are pinned in `engines.lock.json`. A changed input stops the build for review. The Windows Dolphin build explicitly enables MSVC C++ unwind semantics with `/EHsc`. Warnings remain errors and assertions remain enabled.

There is no supported native Cygwin ARM64 runtime. The WIT source's Windows branch requires Cygwin POSIX APIs. This is a platform porting blocker. Renaming an x64 binary or assuming emulation would not produce native ARM64 support.

`EngineSources-<rid>.tar.gz` contains the exact preferred engine sources used for that target, selected recursive submodule sources, revision records and engine build materials. Extract it separately from the application. The sources can be built offline with the matching native compiler and the flags recorded in the included scripts. Dolphin's CMake supports building from a source tree without Git metadata. WIT's source package includes its upstream Makefile and bundled compression sources.

Git object IDs identify downloaded source revisions. Release SHA256 values identify the generated archives. Locally recorded hashes do not independently authenticate an unverified download. HTTPS and Cygwin's upstream signature checks are the transport and package verification used here. No extra signature verification is claimed.

Run `pwsh -File eng/publish.ps1 -RuntimeIdentifier <rid>` after the native engines are ready. Then run `pwsh -File eng/test-package.ps1 -RuntimeIdentifier <rid>`. Linux display tests need an X server or Xvfb. Tests extract and relocate the archives, remove engine PATH configuration, execute real engines and test generated non-game containers. Unix packages are also tested with a read-only installation tree.

The application has no newly assigned license. Separate engine build patches and required build materials accompany the GPL engine distribution. Upstream engine authorship and dependency licenses are preserved.
