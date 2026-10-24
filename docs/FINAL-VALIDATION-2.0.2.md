# RVZ Converter 2.0.2 final validation

Audit: 24 October 2026. Release commit: `cb5809e84f4df1965db0d9c0bad9f935a66343b1`.

**Five distributions meet the complete-engine requirement. Windows ARM64 does not: native WIT is missing and that artifact is withheld. The requested six-complete-distribution objective is not achieved.** Four new application/source pairs were published on v2.0.2 and match its exact commit. Windows x64 retains its original published bytes. No existing tag or previous release is changed.

## Native evidence

[Run 40076853202](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40076853202) passed six native jobs. Each checks process CPU architecture, runs 80 regressions, launches native UI, extracts the package into a relocated Unicode path, runs from a different working directory with only system directories on PATH and executes bundled-engine integration. PATH isolation does not constitute installation on a clean OS.

| Target | Native runner | Synthetic conversion calls | Engines | Release decision |
| --- | --- | --- | --- | --- |
| Windows x64 | windows-latest, X64 | 14 across 12 routes | DolphinTool and WIT | Keep original package |
| Windows ARM64 | windows-11-arm, Arm64 | 8 across 6 routes | DolphinTool only | Withhold incomplete package |
| Linux x64 | ubuntu-24.04, X64 | 14 across 12 routes | DolphinTool and WIT | Publish |
| Linux ARM64 | ubuntu-24.04-arm, Arm64 | 14 across 12 routes | DolphinTool and WIT | Publish |
| macOS Intel | macos-15-intel, X64 | 14 across 12 routes | DolphinTool and WIT | Publish |
| macOS Apple Silicon | macos-15, Arm64 | 14 across 12 routes | DolphinTool and WIT | Publish |

Each fourteen-call integration run includes ISO to RVZ/CISO; RVZ to ISO/CISO; GCZ/WIA to ISO/RVZ/CISO; CISO to ISO/RVZ; and a repeated ISO/RVZ round trip. WIT's SHA-1 implementation passed native known-answer tests on the five WIT targets: empty input, `abc`, incremental `abc`, one million `a` bytes. Native dependency records were reviewed. Linux engines need glibc symbols up to 2.38; the declared distribution baseline remains Ubuntu 24.04/glibc 2.39. macOS engine load commands declare 14.0 and resolve system libraries/frameworks.

The unsupported direct Dolphin CISO-to-ISO assertion is deliberately reproduced, captured and rejected. Source hashes remain unchanged and diagnostic output is discarded. It is an expected failure regression. Regressions cover malformed/truncated inputs, PSP CISO rejection, corrupt RVZ headers, missing/incompatible engines, queue continuation, collisions, cancellation, cleanup ownership, disk budgets and WBFS restrictions/split handling. These cases are not all real-disc or real-engine tests.

Local Windows x64 extraction checks passed for the CI package and original published package. Both ran native synthetic engine checks with system-only PATH. The relocated CI package also launched native UI. Full records are retained locally in ignored `artifacts/final-validation-40076853202/`.

## Real images on Windows x64

One Wii image (`R8PP01`, 4,699,979,776 bytes) and one GameCube image (`GZ2P01`, 1,459,978,240 bytes) were available locally. No game content is uploaded. The harness invokes `ConversionCoordinator` from the tested package's own core assembly and uses its bundled engines. An SDK runs the harness; separate package-launch checks verify self-contained execution. Working paths contain spaces, Unicode, ampersand and percent characters.

Both Windows x64 archives passed eleven initial real conversion calls: Wii ISO to RVZ/CISO/WBFS, RVZ to ISO, CISO to ISO/RVZ, WBFS to ISO/RVZ; GameCube ISO to RVZ/CISO and RVZ to ISO. Both sources were rehashed afterward and unchanged. ISO to RVZ to ISO preserved the full source hash:

| Source | Original and round-trip ISO SHA-256 |
| --- | --- |
| Wii | `45826a280c04edc6fd34a5ce6199dd283bf564e915738f0d3df9bf21a130491f` |
| GameCube | `ab8a811eb484ab5db065d69dde4a343bbebd897c5d74ad447632aff4722cc652` |

WIT `VERIFY` reported `+OK` for UPDATE and DATA partitions of the source Wii ISO and WBFS output. Dolphin verification of the initial Wii RVZ reported `Problems Found: No`. Exit status alone is insufficient: RVZ output from CISO/WBFS reported `Problems Found: Yes`, severity **Low**, errors in **121609 unused blocks** in the DATA partition, despite exit code 0. WIT verified reconstructed ISO partitions. CISO/WBFS reconstructed ISO hashes differ from the original. These routes are not represented as lossless archival copies or clean full-disc verification. Normal application validation checks container readability rather than this additional full-disc diagnostic.

The original published Windows x64 package also passed fourteen additional calls: Wii RVZ to CISO/WBFS, GCZ and WIA each to ISO/RVZ/CISO/WBFS, CISO to WBFS, WBFS to CISO, and GameCube CISO to ISO/RVZ. This covers all twenty implemented Wii routes on one specific Wii image, plus five GameCube routes. The generated Wii GCZ and WIA sources returned the original ISO hash when converted to ISO. GameCube CISO materialization produces a padded 4,699,979,776-byte ISO; it is not the original 1,459,978,240-byte ISO. No bit-identical CISO round trip is claimed.

Exact output hashes and warning summaries are listed in the [sanitized evidence JSON](validation-2.0.2-evidence.json). Raw local logs retain complete engine output. No emulator boot or gameplay check was performed. Image-specific results do not establish compatibility with all Wii/GameCube games.

## Archive and redistribution audit

All six Actions artifact ZIPs were downloaded and matched GitHub's artifact SHA-256 digests. ZIP CRCs and embedded application SHA-256 sidecars passed. Every application ZIP member and application/source tar.gz stream was read. Names/listings were checked for unsafe paths, game fixtures, private Git metadata, debug outputs and unexpected root layout. Engine PE/ELF/Mach-O architecture matches each RID. Complete packages include manifests, runtimes, both engines, documentation and licenses. Source revisions and patch hashes match engine manifests. Cygwin/ncurses/zlib source packages match their recorded SHA-512 values.

Dolphin project credits, Wiimm/Dirk Clemens and contributor credits, Mike Scott's Rijndael notice, original copyright, GPL texts, runtime and dependency notices remain intact. WIT's Apache-2.0 Mbed TLS SHA-1 combination uses the GPLv3 option. Legacy crypto source notices are preserved. Corresponding preferred sources, pinned dependencies, modifications and build recipes accompany each binary. Windows x64 also provides matching runtime dependency source. No new application license is assigned.

References: [Apache GPL compatibility guidance](https://apache.org/licenses/GPL-compatibility.html), [Cygwin licensing](https://www.cygwin.com/licensing.html), [GNU compatibility guidance](https://www.gnu.org/licenses/license-list.html). ARM64's missing WIT is verified directly from its manifest and package.

## Release gate and publication

`eng/verify-packages.ps1` now rejects missing native WIT by default. `-AllowIncompleteEngines` permits inspection of the experimental ARM64 artifact; it does not authorize publication as complete. Negative validation fails on Windows ARM64 as intended. Selecting five complete targets passes. The correction is commit `90693a7b9a7501195267eaf215c432bee84a97f8`; [run 40082658249](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40082658249) finished **6/6 successful**. The documentation update at `2a7b598a293441a7f96881721177d27de2bd0e20` also finished **6/6 successful** in [run 40083225768, attempt 2](https://github.com/Petrus-Canisius/RVZ-Converter/actions/runs/40083225768/attempts/2), reusing the verified engine caches. Attempt 1 of the latter run was deliberately cancelled to avoid duplicate engine rebuilding; no validation result is inferred from that cancelled attempt. Final audit-record updates only record these completed results and do not change application, engine, packaging or test code.

Application hashes selected for distribution:

| Archive | SHA-256 |
| --- | --- |
| RVZConverter-win-x64.zip | `c18b32dc450539f7b7f9ecacd32f7d593f2c751a3201fbfb34654c0d758c0226` |
| RVZConverter-linux-x64.tar.gz | `5dde101f5e5863f4bed2863e20e6de28345dcc5ae1e34cde8716b2cceef71e39` |
| RVZConverter-linux-arm64.tar.gz | `fd80ddd9086e28e067d1b84416e6f6b5a914fce9eb5f8860e0ccfd088360074f` |
| RVZConverter-osx-x64.zip | `058439bc8ddd23063939bb423a7cff4d2dd07ba99dce369638d519925cf8b89e` |
| RVZConverter-osx-arm64.zip | `c406f6e0b82b6b9daca4cd917c59b9ff40fefb6ad58e377cb36b988489497224` |

The separately tested Windows x64 CI rebuild has archive hash `389d99cd2e40f03e7f74311e9a006ce8472c1be1dac4f6d49998eb4e0dd89208`. It does not replace the original published asset. All ten application/source archives were downloaded again from the official release. Each matched the regenerated `SHA256SUMS.txt`, the audited local bytes and GitHub's release asset digest. The downloaded five-package set passed the release layout, manifest, required-notice and corresponding-source checks again. Source hashes and post-download results are in the evidence record. New release guards and online documentation are later maintenance commits, not the provenance of the immutable 2.0.2 packages. Packages were not repacked with later documentation.

## Not tested and remaining blocker

- Native WIT on Windows ARM64: **unavailable, package withheld**. Completion requires a native WIT port and native tests. Emulated x64 engines would not meet this requirement.
- Real games on Linux/macOS/Windows ARM64; every Wii/GameCube title.
- Large split WBFS output on actual FAT32 media. Split argument/collision/missing-part tests do not replace this.
- Clean-OS installation without any development environment installed. PATH isolation, relocation and read-only package checks are the available evidence. The local Docker API was unavailable, so no clean-container result is inferred.
- macOS 14 runtime, downloaded-app Gatekeeper acceptance, notarization and trusted publisher signing.
- Gameplay/emulator boot, long-running real-game cancellation and hardware display scaling on every target.

Original archive documentation is unchanged. Current online instructions and this addendum supersede stale claims that native CI was unavailable and packages were pending. Earlier releases and tags remain intact. Only the current-release checksum index and release notes are regenerated.
