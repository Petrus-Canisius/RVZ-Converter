# Dolphin inaccurate-size assertion

## Identification

The affected local Windows x64 engine identifies itself as Dolphin 2609a. Its embedded Dolphin source revision is `409881c34357d0f105fd473167d15ab0bd9c1628`. This matches the dereferenced official `2609a` tag. The executable SHA256 is `63dd9bad68996f4c75302fde1a2374ebecfa767df6780619d658f41684f97e8a`. The published RVZ Converter 2.0.1 packages did not bundle an engine. This binary was in the separately installed local tools directory.

In that exact revision, `Source/Core/DiscIO/FileBlob.cpp:44` asserts `infile->GetDataSizeType() == DataSizeType::Accurate` in `ConvertToPlain`. The new bundled DolphinTool is based on 2506, revision `aed420c95d7a152363917e4b827d6cc85eacb502`. It has the same precondition at `FileBlob.cpp:52`. Changing engine versions alone does not remove this format constraint.

The Windows x64 2.0.2 package's locally built DolphinTool SHA256 is `e0977dc0bf80cc9ba4a1ca41c5306a02eae0f5d90f0554b81fded01ec637edc8`. Its WIT SHA256 is `26164f5425b131dfcc1ce1286554840c5681b88eee6dc44e98184d3392fc1517`. These hashes identify the packaged builds. They are not upstream signatures.

Source references: [2609a assertion](https://github.com/dolphin-emu/dolphin/blob/409881c34357d0f105fd473167d15ab0bd9c1628/Source/Core/DiscIO/FileBlob.cpp#L44), [2506 assertion](https://github.com/dolphin-emu/dolphin/blob/aed420c95d7a152363917e4b827d6cc85eacb502/Source/Core/DiscIO/FileBlob.cpp#L52), [CISO reader](https://github.com/dolphin-emu/dolphin/blob/aed420c95d7a152363917e4b827d6cc85eacb502/Source/Core/DiscIO/CISOBlob.h), [WBFS reader](https://github.com/dolphin-emu/dolphin/blob/aed420c95d7a152363917e4b827d6cc85eacb502/Source/Core/DiscIO/WbfsBlob.h), and [WIT CISO length calculation](https://github.com/Wiimm/wiimms-iso-tools/blob/fc1c0b840cb3ac41ca6e4f1d5e16da12b47eab58/project/src/lib-ciso.c#L337).

## Cause

Dolphin's `CISOFileReader::GetDataSizeType()` and `WbfsFileReader::GetDataSizeType()` explicitly return `UpperBound`. Those containers do not record an accurate original image length. A readable disc header does not make that length accurate. Plain ISO and RVZ conversion routines require accurate length information. Direct Dolphin conversion from these readers is therefore an unsupported route.

This is not evidence that a source is corrupted. No user game image was provided. The failure was reproduced with a complete generated non-game CISO. RVZ Converter detected it from its data. Dolphin's JSON header inspection returned `GTEST1`. WIT independently reopened the fixture. Source hashes were unchanged.

## Reproduction

The private test artifact records exact paths, arguments, stdout, stderr, dialog text and exit codes. The command shape is:

```text
DolphinTool.exe convert --input "<synthetic fixture>.ciso" --output "<owned test directory>/unsupported-output.iso" --format iso --user "<owned test directory>/user"
```

On Windows x64 with 2609a, the dialog reported the exact condition, file, line 44 and function supplied in the issue. stdout and stderr were both empty. The engine had no natural exit status while waiting for the dialog. The test read the owned dialog and terminated the engine tree without choosing Ignore. The resulting termination exit code was `-1`. The process and captured descendants were awaited before cleanup. No diagnostic output was accepted as a conversion result.

The native Linux x64 package run for the 2506 engine reproduced the same precondition at line 52. The default Unix handler printed the assertion to stderr and aborted. The observed exit code was `132`. Its stdout did not report successful conversion.

## Correction

CISO and WBFS to ISO now use WIT. CISO and WBFS to RVZ use WIT to create an intermediate ISO, then DolphinTool. If WIT is unavailable, planning rejects those routes with a clear English explanation before conversion. This applies to the Windows ARM64 package, where native WIT is unavailable.

WIT's CISO reader uses at least a single-layer Wii virtual length, including for GameCube data. ISO output may therefore include zero padding. This does not restore missing source data. The synthetic regression uses a 256 KiB CISO block map that can represent this length. Output size checks remain enforced.

CISO maps with a capacity below WIT's minimum padded ISO length are rejected before conversion. Otherwise WIT could write beyond the source bound used for disk budgeting. This is an unsupported source/engine combination, not a corruption diagnosis. Regression tests verify rejection, source protection, cleanup and continuation of the queue.

Space budgets for WIT ISO materialization use `max(last mapped block end, single-layer Wii size)`. This matches `SetupCISO` and `SetupReadCISO` in the pinned WIT source. The full CISO map capacity remains the original reader's upper bound. It is not reported as an accurate original length. Intermediate ISO and RVZ metadata must match WIT's materialized length, and must also fit the source bound. This avoids budgeting unused map capacity twice without weakening output size validation.

The inspected DolphinTool `convert --help` and command parser offer no documented batch or noninteractive error option. No guessed flag is used. Assertions and integrity checks remain enabled.

The Windows process runner watches owned native error dialogs. It records their text, stops the engine and descendants, waits for termination and returns a conversion failure. It never clicks a response. Cancellation keeps its own cancelled status. stdout and stderr remain concurrently drained. Every stopped process has its exit code recorded. Cleanup remains limited to the current task's owned directories. Pre-existing destinations and source images are preserved.

## Regression scope

Planner tests require WIT for upper-bound source containers. Packaged integration deliberately reproduces the unsupported raw Dolphin CISO to ISO call, records the failure and rejects its output. It then verifies supported conversion routes through the actual engines. It checks source hashes, collision refusal, cancellation and owned cleanup. Windows, Linux and macOS package jobs run these checks on native target architectures.

The synthetic fixture does not validate real game integrity or every possible WBFS image. Full validation results and workflow references are maintained in [VALIDATION.md](VALIDATION.md).
