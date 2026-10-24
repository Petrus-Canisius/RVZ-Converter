using System.Security.Cryptography;
using System.Text.Json;
using System.Buffers.Binary;

namespace RvzConverter.Core;

/// <summary>Opt-in packaged integration check. Uses only generated non-game data.</summary>
public static class PackageVerification
{
    public static async Task<int> RunAsync(string appDirectory, string reportPath)
    {
        var lines = new List<string>();
        string root = Path.Combine(Path.GetTempPath(), "rvz-package-" + Guid.NewGuid().ToString("N") + " é 日本語");
        Directory.CreateDirectory(root);
        try
        {
            string installation = PlatformPaths.Current == DesktopPlatform.MacOS && !Path.GetFileName(Path.TrimEndingDirectorySeparator(appDirectory)).Equals("app", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFullPath(Path.Combine(appDirectory, "..", "Resources")) : Path.GetFullPath(Path.Combine(appDirectory, ".."));
            string tools = Path.Combine(installation, "tools");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(tools, "engines.json")));
            bool expectWit = manifest.RootElement.GetProperty("wit").GetProperty("available").GetBoolean();
            var runner = new NativeProcessRunner();
            var engines = await new EngineService(runner).DiscoverAsync(new Preferences(), appDirectory, lines.Add, default);
            Require(engines.DolphinAvailable && engines.RvzPresetAvailable, "Bundled DolphinTool and RVZ preset unavailable.");
            var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Require(engines.DolphinPath.StartsWith(tools + Path.DirectorySeparatorChar, pathComparison), "DolphinTool was found outside the package.");
            Require(engines.WitAvailable == expectWit, "WIT availability differs from the package manifest.");
            if (expectWit) Require(engines.WitPath.StartsWith(tools + Path.DirectorySeparatorChar, pathComparison), "WIT was found outside the package.");
            lines.Add("PASS native bundled engine discovery without PATH configuration");
            string iso = Path.Combine(root, "Synthetic non-game é 日本語 & %.iso"); ProbeImages.WriteIso(iso);
            // WIT's initial container probe needs more than a few compressed
            // kilobytes. Deterministic non-game filler keeps compressed fixture
            // files above that minimum without relying on copyrighted content.
            byte[] filler = new byte[65536]; new Random(12345).NextBytes(filler);
            using (var image = File.OpenWrite(iso)) { image.Position = 0x10000; image.Write(filler); }
            byte[] hash = Hash(iso);
            string assertionCiso = Path.Combine(root, "Structurally complete synthetic assertion fixture.ciso");
            // WIT's CISO reader materializes at least a single-layer Wii length,
            // even for GameCube. The map must represent that padded length.
            const int cisoBlockSize = 262144;
            byte[] cisoHeader = new byte[32768]; "CISO"u8.CopyTo(cisoHeader); BinaryPrimitives.WriteUInt32LittleEndian(cisoHeader.AsSpan(4), cisoBlockSize);
            byte[] isoBytes = File.ReadAllBytes(iso); int blocks = isoBytes.Length / cisoBlockSize; Array.Fill(cisoHeader, (byte)1, 8, blocks);
            using (var cisoFile = File.Create(assertionCiso)) { cisoFile.Write(cisoHeader); cisoFile.Write(isoBytes); }
            Require(ImageInspector.Read(assertionCiso).Format == ImageFormat.CISO, "Synthetic assertion fixture detection failed.");
            Require(await new EngineService(runner).DolphinCanRead(engines.DolphinPath, assertionCiso, "GTEST1", lines.Add, default), "DolphinTool cannot inspect the synthetic CISO fixture.");
            byte[] assertionHash = Hash(assertionCiso);
            lines.Add("Synthetic CISO SHA256 before diagnostic: " + System.Convert.ToHexString(assertionHash));
            if (expectWit)
            {
                var inspected = await runner.RunAsync(new(engines.WitPath, ["DUMP", Path.GetFileName(assertionCiso)], root, "Independent WIT CISO inspection"), lines.Add, default);
                Require(inspected.ExitCode == 0 && (inspected.StandardOutput + inspected.StandardError).Contains("GTEST1"), "WIT could not inspect the complete synthetic CISO.");
            }
            lines.Add("Expected failure regression: direct CISO to ISO. No output from this diagnostic is accepted.");
            bool assertionReported = false;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                try
                {
                    var rejected = await runner.RunAsync(new(engines.DolphinPath,
                        ConversionPlanner.DolphinArguments(assertionCiso, Path.Combine(root, "unsupported-output.iso"), OutputFormat.ISO, Path.Combine(root, "assertion-user")), root, "Unsupported direct CISO to ISO regression"), lines.Add, timeout.Token);
                    assertionReported = rejected.ExitCode != 0 && (rejected.StandardError + rejected.StandardOutput).Contains("GetDataSizeType");
                }
                catch (ConversionException ex) when (ex.Message.Contains("interactive error dialog") && ex.Message.Contains("GetDataSizeType")) { lines.Add(ex.Message); assertionReported = true; }
            }
            Require(assertionReported, "Dolphin's inaccurate-size rejection was not reproduced and captured.");
            Require(Hash(assertionCiso).SequenceEqual(assertionHash), "Assertion regression changed its CISO source.");
            lines.Add("Synthetic CISO SHA256 after diagnostic: " + System.Convert.ToHexString(Hash(assertionCiso)));
            lines.Add("PASS actual inaccurate-size assertion captured. Engine stopped and awaited. Source unchanged. Diagnostic output discarded.");
            string rvz = await Convert(iso, OutputFormat.RVZ);
            string roundtrip = await Convert(rvz, OutputFormat.ISO);
            Require(Hash(roundtrip).SequenceEqual(hash), "Synthetic ISO/RVZ round trip changed data.");
            File.Delete(roundtrip);
            var inputs = new List<string> { iso, rvz };
            foreach (string format in new[] { "gcz", "wia" })
            {
                string path = Path.Combine(root, "Synthetic fixture." + format);
                string[] options = format == "gcz" ? ["--block_size", "16384"] : ["--block_size", "2097152", "--compression", "lzma2", "--compression_level", "5"];
                var result = await runner.RunAsync(new(engines.DolphinPath, ["convert", "--input", iso, "--output", path, "--format", format, "--user", Path.Combine(root, "dolphin-user"), .. options], root, "Create synthetic container"), lines.Add, default);
                Require(result.ExitCode == 0 && File.Exists(path), "DolphinTool could not create " + format);
                inputs.Add(path);
            }
            if (expectWit) inputs.Add(assertionCiso);
            foreach (string input in inputs)
                foreach (var format in expectWit ? new[] { OutputFormat.ISO, OutputFormat.RVZ, OutputFormat.CISO } : new[] { OutputFormat.ISO, OutputFormat.RVZ })
                {
                    if (ImageInspector.Read(input).Format.ToString() == format.ToString()) continue;
                    string result = await Convert(input, format);
                    File.Delete(result);
                }
            var collisionOptions = Options(OutputFormat.RVZ);
            Directory.CreateDirectory(collisionOptions.Destination);
            string collision = Path.Combine(collisionOptions.Destination, Path.GetFileNameWithoutExtension(iso) + ".rvz");
            File.WriteAllText(collision, "Pre-existing destination");
            var collisionResult = (await new ConversionCoordinator(runner, new WindowsVolumeProbe()).RunAsync([iso], collisionOptions, log: lines.Add))[0];
            Require(collisionResult.State == ConversionState.Failed && File.ReadAllText(collision) == "Pre-existing destination", "Existing output was overwritten.");
            using var cts = new CancellationTokenSource();
            var cancelOptions = Options(OutputFormat.RVZ);
            var progress = new InlineProgress(update => { if (update.State == ConversionState.Converting) cts.Cancel(); });
            var cancelled = await new ConversionCoordinator(runner, new WindowsVolumeProbe()).RunAsync([iso], cancelOptions, progress, lines.Add, cts.Token);
            Require(cancelled.All(r => r.State == ConversionState.Cancelled), "Cancellation was not reported.");
            Require(Hash(iso).SequenceEqual(hash), "A source fixture was modified.");
            Require(!Directory.EnumerateDirectories(root, ".rvz-stage-*", SearchOption.AllDirectories).Any(), "Owned staging directory was not cleaned.");
            lines.Add("PASS collision refusal, cancellation, cleanup and source protection");
            lines.Add("WBFS integration: no suitable Wii partition fixture. Format restrictions and splitting are covered by regression tests.");
            lines.Add("Real game conversion: not tested. All integration inputs were generated non-game fixtures.");
            lines.Add("PASS packaged verification completed");
            return 0;
            ConversionOptions Options(OutputFormat format) => new(Path.Combine(root, "output " + Guid.NewGuid().ToString("N")), Path.Combine(root, "temporary"), format, true, engines.DolphinPath, engines.WitPath);
            async Task<string> Convert(string input, OutputFormat format)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var result = (await new ConversionCoordinator(runner, new WindowsVolumeProbe()).RunAsync([input], Options(format), log: lines.Add, token: timeout.Token))[0];
                Require(result.State == ConversionState.Completed, result.Message);
                lines.Add($"PASS actual engine conversion {ImageInspector.Read(input).Format} to {format}");
                return result.Outputs[0];
            }
        }
        catch (Exception ex) { lines.Add("FAIL " + ex); return 1; }
        finally
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            await File.WriteAllLinesAsync(reportPath, lines);
            if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("rvz-package-")) Directory.Delete(root, true);
        }
    }
    private static byte[] Hash(string path) { using var file = File.OpenRead(path); return SHA256.HashData(file); }
    private static void Require(bool condition, string message) { if (!condition) throw new ConversionException(message); }
    private sealed class InlineProgress(Action<ConversionUpdate> action) : IProgress<ConversionUpdate> { public void Report(ConversionUpdate value) => action(value); }
}
