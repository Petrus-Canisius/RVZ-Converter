using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using RvzConverter.App;
using RvzConverter.Core;

namespace RvzConverter.Tests;
internal static class Program
{
    private static readonly List<(string Name, Func<Task> Run)> Tests = [];
    private static void Test(string name, Action run) => Tests.Add((name, () => { run(); return Task.CompletedTask; }));
    private static void Async(string name, Func<Task> run) => Tests.Add((name, run));
    public static async Task<int> Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US"); CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        if (args.Contains("--child")) { Console.WriteLine("stdout child"); Console.Error.WriteLine("stderr child"); if (args.Contains("--wait")) await Task.Delay(60000); return 0; }
        if (args.Contains("--child-tree"))
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(Program).Assembly.Location);
            start.ArgumentList.Add("--child"); start.ArgumentList.Add("--wait");
            using var child = Process.Start(start)!; Console.WriteLine("grandchild=" + child.Id); await Task.Delay(60000); return 0;
        }
        if (args.Contains("--doctor")) { var check = await new EngineService(new NativeProcessRunner()).DiscoverAsync(new Preferences(), AppContext.BaseDirectory, Console.WriteLine, default); Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(check, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); return 0; }
        Register(); if (args.Contains("--engines")) Async("Native engines: synthetic ISO/RVZ/GCZ/WIA/CISO routes", NativeEngines);
        int failures = 0; var time = Stopwatch.StartNew();
        foreach (var (name, run) in Tests) { try { await run(); Console.WriteLine("PASS " + name); } catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + "\n" + ex); } }
        Console.WriteLine($"{Tests.Count - failures}/{Tests.Count} passed; {failures} failed; {time.Elapsed.TotalSeconds:0.0}s. Real game conversion: NOT TESTED.");
        return failures == 0 ? 0 : 1;
    }
    private static void Register()
    {
        foreach (var source in Enum.GetValues<ImageFormat>()) foreach (var output in Enum.GetValues<OutputFormat>())
            Test($"Matrix {source} -> {output}", () =>
            {
                if (source.ToString() == output.ToString()) { Assert.Throws<ConversionException>(() => ConversionPlanner.Plan(source, DiscPlatform.Wii, output, true, true, true)); return; }
                var plan = ConversionPlanner.Plan(source, DiscPlatform.Wii, output, true, true, true);
                Assert.Equal(source == ImageFormat.RVZ && output is OutputFormat.CISO or OutputFormat.WBFS, plan.IntermediateIso);
                Assert.Equal(output is OutputFormat.CISO or OutputFormat.WBFS ? EngineKind.Wit : EngineKind.Dolphin, plan.Steps[^1].Engine);
            });
        foreach (var format in Enum.GetValues<ImageFormat>()) foreach (var platform in Enum.GetValues<DiscPlatform>())
            Test($"Data detection {format}/{platform} ignores extension", () => { using var f = new Fixture(); string path = f.Source(format, platform, "misleading.bin"); var image = ImageInspector.Read(path); Assert.Equal(format, image.Format); Assert.Equal(platform, image.Disc.Platform); });
        Test("RVZ exact preset and no scrubbing", () => { var args = ConversionPlanner.DolphinArguments("a é &.iso", "b 日本.rvz", OutputFormat.RVZ, "u"); Assert.Equal("zstd", FakeRunner.After(args, "--compression")); Assert.Equal("5", FakeRunner.After(args, "--compression_level")); Assert.Equal("131072", FakeRunner.After(args, "--block_size")); Assert.True(!args.Contains("--scrub")); Assert.Equal("a é &.iso", FakeRunner.After(args, "--input")); });
        Test("Fallback CISO/WBFS -> ISO -> RVZ when Dolphin cannot read source", () => { foreach (var source in new[] { ImageFormat.CISO, ImageFormat.WBFS }) { var p = ConversionPlanner.Plan(source, DiscPlatform.Wii, OutputFormat.RVZ, true, true, true, false); Assert.True(p.IntermediateIso); Assert.Equal(EngineKind.Wit, p.Steps[0].Engine); Assert.Equal(EngineKind.Dolphin, p.Steps[1].Engine); } });
        Test("WIT-only ISO fallback", () => Assert.Equal(EngineKind.Wit, ConversionPlanner.Plan(ImageFormat.CISO, DiscPlatform.Wii, OutputFormat.ISO, false, true, false).Steps[0].Engine));
        Test("Missing engines and unsupported preset rejected selectively", () => { Assert.Throws<ConversionException>(() => ConversionPlanner.Plan(ImageFormat.RVZ, DiscPlatform.Wii, OutputFormat.CISO, false, true, false)); Assert.Throws<ConversionException>(() => ConversionPlanner.Plan(ImageFormat.ISO, DiscPlatform.Wii, OutputFormat.RVZ, true, true, false)); Assert.Throws<ConversionException>(() => ConversionPlanner.Plan(ImageFormat.ISO, DiscPlatform.Wii, OutputFormat.CISO, true, false, true)); Assert.True(!ConversionPlanner.Plan(ImageFormat.RVZ, DiscPlatform.Wii, OutputFormat.ISO, true, false, false).IntermediateIso); });
        Test("WBFS output rejects GameCube", () => Assert.Throws<ConversionException>(() => ConversionPlanner.Plan(ImageFormat.ISO, DiscPlatform.GameCube, OutputFormat.WBFS, true, true, true)));
        Test("Malformed/truncated and PSP CISO rejected", () => { using var f = new Fixture(); foreach (var bytes in new[] { new byte[256], "RVZ\x01"u8.ToArray(), "CISO\x18\0\0\0"u8.ToArray() }) { string p = Path.Combine(f.Root, Guid.NewGuid().ToString()); File.WriteAllBytes(p, bytes); Assert.Throws<ConversionException>(() => ImageInspector.Read(p)); } });
        Test("RVZ header corruption rejected", () => { using var f = new Fixture(); string path = f.Source(); var data = File.ReadAllBytes(path); data[0x58] ^= 1; File.WriteAllBytes(path, data); Assert.Throws<ConversionException>(() => ImageInspector.Read(path)); });
        Test("CISO/WBFS lengths are labelled upper bounds", () => { using var f = new Fixture(); Assert.True(!ImageInspector.Read(f.Source(ImageFormat.CISO)).Disc.ExactSize); Assert.True(!ImageInspector.Read(f.Source(ImageFormat.WBFS)).Disc.ExactSize); });
        foreach (var target in Enum.GetValues<OutputFormat>())
            Async($"Orchestration {target}: sources, Unicode, stages, validation and cleanup", async () =>
            {
                using var f = new Fixture(); string path = f.Source(target == OutputFormat.RVZ ? ImageFormat.ISO : ImageFormat.RVZ); var before = SHA256.HashData(File.ReadAllBytes(path)); var runner = new FakeRunner { Split = true }; List<ConversionUpdate> updates = [];
                var result = (await new ConversionCoordinator(runner, new FakeVolume()).RunAsync([path], f.Options(target), new SyncProgress(updates.Add)))[0];
                Assert.Equal(ConversionState.Completed, result.State); Assert.True(result.Outputs.All(File.Exists)); Assert.True(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))));
                Assert.True(updates.Any(u => u.State == ConversionState.Validating)); Assert.True(updates.Any(u => u.State == ConversionState.Publishing)); Assert.Equal(100, updates.Last().Percentage);
                Assert.True(updates.Where(u => u.State != ConversionState.Completed).All(u => u.Percentage != 100)); f.Clean();
                if (target == OutputFormat.WBFS) Assert.Equal(2, result.Outputs.Count);
            });
        foreach (bool shared in new[] { true, false }) Async($"WIT direct route snapshot on {(shared ? "shared" : "separate")} volumes", async () =>
        { using var f = new Fixture(); var runner = new FakeRunner(); string source = f.Source(ImageFormat.ISO); var result = (await new ConversionCoordinator(runner, new FakeVolume(shared)).RunAsync([source], f.Options(OutputFormat.CISO)))[0]; Assert.Equal(ConversionState.Completed, result.State); var call = runner.Calls.Single(c => c.Arguments[0] == "COPY"); Assert.True(!Path.IsPathRooted(call.Arguments[1])); Assert.True(call.Arguments.Contains("--no-expand")); f.Clean(); });
        foreach (string failing in new[] { "convert", "COPY", "DUMP" }) Async($"Engine failure at {failing} preserves source and continues", async () =>
        {
            using var f = new Fixture(); string source = f.Source(); var runner = new FakeRunner(); bool failed = false;
            runner.Intercept = (r, _) => { if (!failed && r.Arguments[0] == failing && !r.Arguments.Contains("--help") && !r.Arguments.Contains("probe.iso") && !r.Label.Contains("preset")) { failed = true; return Task.FromResult<ProcessResult?>(new(17, "", "Injected failure")); } return Task.FromResult<ProcessResult?>(null); };
            var results = await new ConversionCoordinator(runner, new FakeVolume()).RunAsync([source, f.Source()], f.Options(OutputFormat.CISO)); Assert.Equal(ConversionState.Failed, results[0].State); Assert.Equal(ConversionState.Completed, results[1].State); Assert.True(File.Exists(source)); f.Clean();
        });
        Async("Invalid RVZ preset disables only RVZ", async () => { using var f = new Fixture(); var result = await new EngineService(new FakeRunner { Preset = false }).CheckAsync(f.Engine, f.Engine, null, default); Assert.True(result.DolphinAvailable && result.WitAvailable && !result.RvzPresetAvailable); });
        Async("Discovery skips incompatible local binary and verifies saved native executable", async () =>
        {
            using var f = new Fixture(); string folder = Path.Combine(f.Root, "tools", "dolphin"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, OperatingSystem.IsWindows() ? "DolphinTool.exe" : "dolphin-tool"), "wrong executable");
            var prefs = new Preferences { DolphinPath = f.Engine, WitPath = f.Engine }; var result = await new EngineService(new FakeRunner()).DiscoverAsync(prefs, f.Root, null, default);
            Assert.True(result.DolphinAvailable && result.WitAvailable); Assert.Equal(f.Engine, result.DolphinPath);
        });
        Async("Discovery falls through older local engines with incomplete command support", async () =>
        {
            using var f = new Fixture(); string d = Path.Combine(f.Root, "tools", "dolphin"), w = Path.Combine(f.Root, "tools", "wit"); Directory.CreateDirectory(d); Directory.CreateDirectory(w);
            string dolphin = Path.Combine(d, OperatingSystem.IsWindows() ? "DolphinTool.exe" : "dolphin-tool"), wit = Path.Combine(w, OperatingSystem.IsWindows() ? "wit.exe" : "wit"); File.Copy(f.Engine, dolphin); File.Copy(f.Engine, wit);
            var runner = new FakeRunner { Intercept = (r, _) => Task.FromResult<ProcessResult?>(r.Executable == dolphin && r.Arguments[0] == "header" || r.Executable == wit && r.Arguments[0] == "HELP" ? new(0, "Old engine without required options", "") : null) };
            var result = await new EngineService(runner).DiscoverAsync(new() { DolphinPath = f.Engine, WitPath = f.Engine }, f.Root, null, default); Assert.Equal(f.Engine, result.DolphinPath); Assert.Equal(f.Engine, result.WitPath); Assert.True(result.DolphinAvailable && result.WitAvailable);
        });
        Test("Malformed remembered engine path cannot hide PATH candidates", () => { using var f = new Fixture(); string directory = Path.Combine(f.Root, "valid"); var paths = EngineLocator.Candidates(EngineKind.Dolphin, "\0bad", f.Root, directory).ToArray(); Assert.True(paths.Any(p => Path.GetDirectoryName(p) == directory)); });
        Async("Output validation failure cannot publish or display success", async () =>
        {
            using var f = new Fixture(); var runner = new FakeRunner(); runner.Intercept = (r, _) => Task.FromResult<ProcessResult?>(r.Arguments[0] == "header" && FakeRunner.After(r.Arguments, "--input").Contains(".rvz-stage-") ? new(9, "", "bad output") : null);
            var updates = new List<ConversionUpdate>(); var result = (await new ConversionCoordinator(runner, new FakeVolume()).RunAsync([f.Source()], f.Options(), new SyncProgress(updates.Add)))[0];
            Assert.Equal(ConversionState.Failed, result.State); Assert.True(!updates.Any(u => u.State is ConversionState.Publishing or ConversionState.Completed || u.Percentage == 100)); Assert.Equal(0, Directory.GetFiles(f.Destination).Length); f.Clean();
        });
        Async("Cancellation during engine conversion cleans owned staging", async () =>
        {
            using var f = new Fixture(); using var cts = new CancellationTokenSource(); var runner = new FakeRunner(); runner.Intercept = async (r, t) => { if (r.Arguments[0] == "convert" && r.Label == "DolphinTool") { cts.Cancel(); await Task.Delay(1, t); } return null; };
            var results = await new ConversionCoordinator(runner, new FakeVolume()).RunAsync([f.Source(), f.Source()], f.Options(), token: cts.Token); Assert.True(results.All(r => r.State == ConversionState.Cancelled)); f.Clean();
        });
        Async("Pre-existing output is never overwritten", async () => { using var f = new Fixture(); string source = f.Source(); string output = Path.Combine(f.Destination, SafeFiles.OutputName(source, OutputFormat.ISO)); File.WriteAllText(output, "keep"); var result = (await new ConversionCoordinator(new FakeRunner(), new FakeVolume()).RunAsync([source], f.Options()))[0]; Assert.Equal(ConversionState.Failed, result.State); Assert.Equal("keep", File.ReadAllText(output)); f.Clean(); });
        Test("WBFS collisions include every numbered split part and directories", () => { using var f = new Fixture(); File.WriteAllText(Path.Combine(f.Destination, "disc.WBF17"), "keep"); Assert.Throws<ConversionException>(() => SafeFiles.CheckCollision(f.Destination, "disc.wbfs", OutputFormat.WBFS)); });
        Test("WBFS split arguments are strictly below 4 GiB", () => { var args = ConversionPlanner.WitArguments("input.iso", "image.wbfs", OutputFormat.WBFS, true); Assert.Equal("4000M", FakeRunner.After(args, "--split-size")); Assert.True(DiskSpace.WbfsPartSize < 1L << 32); Assert.Equal("ALL", FakeRunner.After(args, "--psel")); });
        Test("WBFS missing split part rejected", () => { using var f = new Fixture(); string source = f.Source(ImageFormat.WBFS); File.WriteAllBytes(Path.ChangeExtension(source, ".wbf2"), [0]); Assert.Throws<ConversionException>(() => ImageInspector.Read(source)); });
        Test("Cleanup ownership boundaries", () => { using var f = new Fixture(); string dir = SafeFiles.CreateOwnedDirectory(f.Temporary, "task-", "owner"); string keep = Path.Combine(dir, "keep"); File.WriteAllText(keep, "keep"); SafeFiles.CleanupOwned(dir, "wrong", null); Assert.True(File.Exists(keep)); SafeFiles.CleanupOwned(dir, "owner", null); Assert.True(!Directory.Exists(dir)); Assert.True(Directory.Exists(f.Destination)); });
        Test("Partial WBFS publication preserves committed and remaining files", () => { using var f = new Fixture(); string stage = SafeFiles.CreateOwnedDirectory(f.Temporary, "task-", "owner"); string one = Path.Combine(stage, "image.wbfs"), two = Path.Combine(stage, "image.wbf1"); File.WriteAllText(one, "one"); File.WriteAllText(two, "two"); var pub = new OutputPublisher(new InjectedMover((n, s, d) => { if (n == 2) { File.WriteAllText(d, "pre-existing race"); throw new IOException("race"); } File.Move(s, d, false); })); var error = Assert.Throws<PublicationException>(() => pub.Publish([one, two], f.Destination, "disc.wbfs", OutputFormat.WBFS, default)); Assert.Equal(1, error.Committed.Count); Assert.True(File.Exists(two)); Assert.Equal("pre-existing race", File.ReadAllText(Path.Combine(f.Destination, "disc.wbf1"))); });
        Test("Disk budgets include snapshot, ISO and cross-volume staging", () => { using var f = new Fixture(); var image = ImageInspector.Read(f.Source(ImageFormat.RVZ)); var route = ConversionPlanner.Plan(ImageFormat.RVZ, DiscPlatform.Wii, OutputFormat.CISO, true, true, true); var same = DiskSpace.Calculate(image, route, true); var different = DiskSpace.Calculate(image, route, false); Assert.Equal(image.Disc.IsoSize + DiskSpace.Reserve, same.TemporaryBytes); Assert.Equal(same.TemporaryBytes + same.OutputBytes, same.SharedVolumeBytes); Assert.Equal(same.TemporaryBytes + same.OutputBytes, different.TemporaryBytes); var direct = ConversionPlanner.Plan(ImageFormat.ISO, DiscPlatform.Wii, OutputFormat.CISO, true, true, true); Assert.Equal(image.Disc.SourceSize + DiskSpace.Reserve, DiskSpace.Calculate(image, direct, true).TemporaryBytes); });
        Test("FAT32 ISO/intermediate and shared free space are enforced", () => { using var f = new Fixture(); var image = new ImageInfo(ImageFormat.RVZ, new(DiscPlatform.Wii, 5L << 30, 100, "", "STEST1"), []); var route = ConversionPlanner.Plan(ImageFormat.RVZ, DiscPlatform.Wii, OutputFormat.ISO, true, false, false); Assert.Throws<ConversionException>(() => DiskSpace.Ensure(image, route, f.Options(), new("a", "NTFS", long.MaxValue), new("b", "FAT32", long.MaxValue))); Assert.Throws<ConversionException>(() => DiskSpace.Ensure(image, route, f.Options(), new("a", "NTFS", 100), new("a", "NTFS", 100))); });
        Test("Platform-specific preference/cache/log locations", () => { string home = Path.Combine(Path.GetTempPath(), "user"); var linux = PlatformPaths.For(DesktopPlatform.Linux, home, _ => null); Assert.True(linux.Preferences.Contains(".config")); Assert.True(linux.Logs.Contains(".local")); Assert.True(PlatformPaths.For(DesktopPlatform.MacOS, home, _ => null).Preferences.Contains(Path.Combine("Library", "Application Support"))); Assert.True(PlatformPaths.For(DesktopPlatform.Windows, home, _ => null).Preferences.EndsWith("RVZConverter")); string custom = Path.Combine(home, "custom"); Assert.True(PlatformPaths.For(DesktopPlatform.Linux, home, key => key == "XDG_CACHE_HOME" ? custom : null).Cache.StartsWith(custom)); Assert.True(PlatformPaths.For(DesktopPlatform.Linux, home, _ => "relative").Cache.StartsWith(home)); });
        Test("Discovery tools precede saved path, then PATH; bounded traversal", () => { using var f = new Fixture(); string toolDir = Path.Combine(f.Root, "tools", "dolphin", "distribution"); Directory.CreateDirectory(toolDir); string name = OperatingSystem.IsWindows() ? "DolphinTool.exe" : "dolphin-tool"; string tool = Path.Combine(toolDir, name); File.WriteAllText(tool, "fake"); string saved = Path.Combine(f.Root, "saved", name), path = Path.Combine(f.Root, "PATH"); var list = EngineLocator.Candidates(EngineKind.Dolphin, saved, f.Root, path).ToList(); Assert.True(list.IndexOf(tool) < list.IndexOf(saved)); Assert.True(list.IndexOf(saved) < list.IndexOf(Path.Combine(path, name))); Assert.True(!list.Any(p => p.Contains("unrelated"))); });
        Test("Binary compatibility rejects other OS and architecture", () => { using var f = new Fixture(); Assert.True(ExecutableCompatibility.Problem(f.Engine) is null); var other = PlatformPaths.Current == DesktopPlatform.Windows ? DesktopPlatform.Linux : DesktopPlatform.Windows; Assert.True(ExecutableCompatibility.Problem(f.Engine, other) is not null); var arch = RuntimeInformation.OSArchitecture == Architecture.X64 ? Architecture.Arm64 : Architecture.X64; Assert.True(ExecutableCompatibility.Problem(f.Engine, architecture: arch) is not null); });
        Test("Progress ignores summary ratios; never early 100", () => { var parser = new WitProgressReader(); Assert.Equal<int?>(null, parser.Read("[progress:summary]")); Assert.Equal<int?>(null, parser.Read("percent=80")); parser.Read("[progress]"); parser.Read("verb=copied"); Assert.Equal<int?>(42, parser.Read("percent=42")); var vm = new ConversionProgressViewModel(); vm.Begin(5); vm.Update(new("x", ConversionState.Converting, "Intermediate ISO", TimeSpan.Zero, 100), 2, 5); Assert.True(vm.IsIndeterminate && vm.Percentage is null); Assert.Equal("File 2 of 5", vm.QueuePosition); vm.Update(new("x", ConversionState.Converting, "Final", TimeSpan.Zero, 42), 2, 5); Assert.Equal<int?>(42, vm.Percentage); vm.Update(new("x", ConversionState.Publishing, "Placement", TimeSpan.Zero), 2, 5); Assert.True(vm.Percentage is null); vm.Finish([new("x", ConversionState.Failed, "bad", TimeSpan.Zero, [])], false); Assert.True(vm.Percentage is null); vm.Finish([new("x", ConversionState.Completed, "ok", TimeSpan.Zero, [])], false); Assert.Equal<int?>(100, vm.Percentage); });
        Async("Process ArgumentList, concurrent streams, cancellation", async () =>
        {
            using var f = new Fixture(); var runner = new NativeProcessRunner(); string exe = Environment.ProcessPath!; string[] prefix = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? [typeof(Program).Assembly.Location] : [];
            var request = new ProcessRequest(exe, [.. prefix, "--child", "space é 日本語 & %"], f.Root, "test"); var result = await runner.RunAsync(request, null, default); Assert.Equal(0, result.ExitCode); Assert.True(result.StandardOutput.Contains("stdout") && result.StandardError.Contains("stderr")); using var cts = new CancellationTokenSource(300); try { await runner.RunAsync(request with { Arguments = [.. prefix, "--child", "--wait"] }, null, cts.Token); throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { }
        });
        Async("Native cancellation terminates descendants before returning", async () =>
        {
            using var f = new Fixture(); using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); int child = 0; string exe = Environment.ProcessPath!;
            string[] prefix = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? [typeof(Program).Assembly.Location] : [];
            var request = new ProcessRequest(exe, [.. prefix, "--child-tree"], f.Root, "Process tree test", line => { if (line.StartsWith("grandchild=")) { child = int.Parse(line[11..]); cts.Cancel(); } });
            try { await new NativeProcessRunner().RunAsync(request, null, cts.Token); throw new Exception("Cancellation expected"); } catch (OperationCanceledException) { }
            Assert.True(child > 0, "Child process did not start");
            try { using var process = Process.GetProcessById(child); Assert.True(process.HasExited, "Descendant still running"); } catch (ArgumentException) { }
        });
    }
    private static async Task NativeEngines()
    {
        using var f = new Fixture(); var runner = new NativeProcessRunner(); var engines = await new EngineService(runner).DiscoverAsync(new Preferences(), AppContext.BaseDirectory, null, default);
        Assert.True(engines.DolphinAvailable, "DolphinTool unavailable"); Assert.True(engines.WitAvailable, "WIT unavailable"); Assert.True(engines.RvzPresetAvailable, "RVZ preset unsupported");
        Console.WriteLine($"Verified native engines: {engines.DolphinPath}; {engines.WitPath}");
        string iso = Path.Combine(f.Root, "synthetic é & 日本語.iso"); ProbeImages.WriteIso(iso);
        string rvz = await Convert(iso, OutputFormat.RVZ); string roundTrip = await Convert(rvz, OutputFormat.ISO); Assert.True(SHA256.HashData(File.ReadAllBytes(iso)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(roundTrip))), "Synthetic ISO round trip differs");
        await Convert(rvz, OutputFormat.CISO); string ciso = await Convert(iso, OutputFormat.CISO); Assert.Equal(ImageFormat.CISO, ImageInspector.Read(ciso).Format);
        foreach (string format in new[] { "gcz", "wia" })
        {
            string source = Path.Combine(f.Root, "input." + format); var args = new List<string> { "convert", "--input", iso, "--output", source, "--format", format, "--user", Path.Combine(f.Root, "user"), "--block_size", "131072" };
            if (format == "wia") { args[args.IndexOf("--block_size") + 1] = "2097152"; args.AddRange(["--compression", "lzma2", "--compression_level", "5"]); }
            if (format == "gcz") args[args.IndexOf("--block_size") + 1] = "16384";
            var r = await runner.RunAsync(new(engines.DolphinPath, args, f.Root, "Synthetic fixture"), null, default); Assert.True(r.ExitCode == 0, r.StandardOutput + r.StandardError); Assert.Equal(Enum.Parse<ImageFormat>(format, true), ImageInspector.Read(source).Format); await Convert(source, OutputFormat.RVZ);
        }
        async Task<string> Convert(string source, OutputFormat target)
        {
            string dest = Path.Combine(f.Destination, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dest);
            var options = new ConversionOptions(dest, f.Temporary, target, true, engines.DolphinPath, engines.WitPath); var logs = new List<string>();
            var result = (await new ConversionCoordinator(runner, new FakeVolume()).RunAsync([source], options, log: logs.Add))[0]; Assert.True(result.State == ConversionState.Completed, result.Message + "\n" + string.Join("\n", logs)); return result.Outputs[0];
        }
    }
}
