using System.Buffers.Binary;
using System.Text.Json;

namespace RvzConverter.Core;

public sealed record EngineCheck(bool DolphinAvailable, bool WitAvailable, string Message, bool RvzPresetAvailable = true,
    string DolphinPath = "", string WitPath = "");

public static class EngineLocator
{
    public static IEnumerable<string> Candidates(EngineKind engine, string saved, string appDirectory, string? searchPath = null,
        DesktopPlatform? platformOverride = null)
    {
        var platform = platformOverride ?? PlatformPaths.Current;
        string[] names = platform == DesktopPlatform.Windows
            ? engine == EngineKind.Dolphin ? ["DolphinTool.exe"] : ["wit.exe"]
            : engine == EngineKind.Dolphin ? ["dolphin-tool", "DolphinTool"] : ["wit"];
        string folder = engine == EngineKind.Dolphin ? "dolphin" : "wit";
        var candidates = new List<string>();
        var roots = new List<string> { Path.Combine(appDirectory, "tools") };
        string fullAppDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDirectory));
        if (Path.GetFileName(fullAppDirectory).Equals("app", StringComparison.OrdinalIgnoreCase)
            && Directory.GetParent(fullAppDirectory) is { } packageRoot)
            roots.Add(Path.Combine(packageRoot.FullName, "tools"));
        if (platform == DesktopPlatform.MacOS) roots.Add(Path.GetFullPath(Path.Combine(appDirectory, "..", "Resources", "tools")));
        // Development only: look for the solution marker, never arbitrary drive-wide searches.
        var ancestor = new DirectoryInfo(appDirectory);
        for (int i = 0; i < 8 && ancestor is not null; i++, ancestor = ancestor.Parent)
            if (File.Exists(Path.Combine(ancestor.FullName, "RvzConverter.slnx"))) { roots.Add(Path.Combine(ancestor.FullName, "tools")); break; }
        foreach (string root in roots.Distinct(PlatformPaths.PathComparer))
        {
            foreach (string name in names)
            {
                candidates.Add(Path.Combine(root, name));
                string namedRoot = Path.Combine(root, folder);
                if (!Directory.Exists(namedRoot)) continue;
                try { candidates.AddRange(Directory.EnumerateFiles(namedRoot, name, new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 6, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Take(16).Order(PlatformPaths.PathComparer)); }
                catch (IOException) { }
            }
        }
        if (!string.IsNullOrWhiteSpace(saved)) candidates.Add(saved);
        foreach (string entry in (searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "").Split(platform == DesktopPlatform.Windows ? ';' : ':', StringSplitOptions.RemoveEmptyEntries))
        {
            string directory = entry.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(directory)) continue;
            foreach (string name in names) candidates.Add(Path.Combine(directory, name));
        }
        foreach (string standard in StandardDirectories(platform, engine))
            foreach (string name in names) candidates.Add(Path.Combine(standard, name));
        return candidates.SelectMany(Normalize).Distinct(PlatformPaths.PathComparer);
        static IEnumerable<string> Normalize(string candidate)
        {
            string? path = null;
            try { path = Path.GetFullPath(candidate); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
            return path is null ? [] : new[] { path };
        }
    }
    public static IEnumerable<string> StandardDirectories(DesktopPlatform platform, EngineKind engine) => platform switch
    {
        DesktopPlatform.Windows => new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), engine == EngineKind.Dolphin ? "Dolphin" : "Wiimms ISO Tools", engine == EngineKind.Dolphin ? "" : "bin"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Dolphin Emulator"),
            Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "wit", "bin")
        },
        DesktopPlatform.MacOS => engine == EngineKind.Dolphin
            ? ["/Applications/Dolphin.app/Contents/MacOS", Path.Combine(PlatformPaths.Home, "Applications", "Dolphin.app", "Contents", "MacOS"), "/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin"]
            : ["/opt/homebrew/bin", "/usr/local/bin", "/opt/local/bin", "/opt/wit/bin"],
        _ => ["/usr/local/bin", "/usr/bin", engine == EngineKind.Dolphin ? "/opt/dolphin/bin" : "/opt/wit/bin"]
    };
    public static string Find(string engine, string executable, params string[] roots)
        => roots.SelectMany(root => Candidates(engine == "dolphin" ? EngineKind.Dolphin : EngineKind.Wit, "", root)).FirstOrDefault(File.Exists) ?? "";
}

public sealed class EngineService(IProcessRunner runner)
{
    public async Task<EngineCheck> DiscoverAsync(Preferences prefs, string appDirectory, Action<string>? log, CancellationToken token)
    {
        string dolphin = await Find(EngineKind.Dolphin, prefs.DolphinPath);
        string wit = await Find(EngineKind.Wit, prefs.WitPath);
        return await CheckAsync(dolphin, wit, log, token);
        async Task<string> Find(EngineKind kind, string saved)
        {
            foreach (string candidate in EngineLocator.Candidates(kind, saved, appDirectory))
            {
                token.ThrowIfCancellationRequested();
                if (!File.Exists(candidate)) continue;
                if (ExecutableCompatibility.Problem(candidate) is { } problem) { log?.Invoke($"Skipped {candidate}: {problem}"); continue; }
                bool compatible = kind == EngineKind.Dolphin
                    ? await Help(candidate, ["convert", "--help"], ["--input", "--output", "--format", "--user"], log, token)
                        && await Help(candidate, ["header", "--help"], ["--input", "--json"], log, token)
                    : await Help(candidate, ["HELP", "COPY"], ["--iso", "--ciso", "--wbfs", "--raw", "--psel", "--split-size", "--progress", "--sections"], log, token)
                        && await Help(candidate, ["TEST", "--enc", "AUTO", "--split-size", "4000M", "--progress", "--sections"], [], log, token);
                if (compatible) return candidate;
                log?.Invoke($"Skipped incompatible engine arguments or dependencies: {candidate}");
            }
            return "";
        }
    }
    public async Task<EngineCheck> CheckAsync(string dolphin, string wit, Action<string>? log, CancellationToken token)
    {
        bool d = ExecutableCompatibility.Problem(dolphin) is null && await Help(dolphin, ["convert", "--help"], ["--input", "--output", "--format", "--user"], log, token)
            && await Help(dolphin, ["header", "--help"], ["--input", "--json"], log, token);
        bool w = ExecutableCompatibility.Problem(wit) is null && await Help(wit, ["HELP", "COPY"], ["--iso", "--ciso", "--wbfs", "--raw", "--psel", "--split-size", "--progress", "--sections"], log, token)
            && await Help(wit, ["TEST", "--enc", "AUTO", "--split-size", "4000M", "--progress", "--sections"], [], log, token);
        bool preset = d && await Help(dolphin, ["convert", "--help"], ["--compression", "--compression_level", "--block_size"], log, token)
            && await VerifyPreset(dolphin, log, token);
        string message = (d ? "DolphinTool verified." : "DolphinTool is required for RVZ and Dolphin conversions. Use Locate DolphinTool.") + "\n" +
            (w ? "WIT verified." : "WIT is required for CISO/WBFS output and WIT fallback routes. Use Locate WIT.") +
            (d && !preset ? "\nRVZ preset verification failed. ISO routes remain available; RVZ output is disabled." : "");
        return new(d, w, message, preset, d ? dolphin : "", w ? wit : "");
    }
    private async Task<bool> Help(string path, string[] args, string[] required, Action<string>? log, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var result = await runner.RunAsync(new(path, args, Path.GetDirectoryName(Path.GetFullPath(path))!, "Engine check"), log, timeout.Token).ConfigureAwait(false);
            string help = result.StandardOutput + result.StandardError;
            return result.ExitCode == 0 && required.All(help.Contains);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { log?.Invoke($"Engine check timed out: {path}"); return false; }
        catch (Exception ex) when (ex is ConversionException or IOException) { log?.Invoke($"Engine check failed: {ex.Message}"); return false; }
    }
    private async Task<bool> VerifyPreset(string dolphin, Action<string>? log, CancellationToken token)
    {
        string owner = Guid.NewGuid().ToString("N"); string? task = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            string cache = PlatformPaths.Directories.Cache; Directory.CreateDirectory(cache);
            task = SafeFiles.CreateOwnedDirectory(cache, "rvz-probe-", owner);
            string iso = Path.Combine(task, "probe.iso"), rvz = Path.Combine(task, "probe.rvz");
            ProbeImages.WriteIso(iso);
            var result = await runner.RunAsync(new(dolphin, ConversionPlanner.DolphinArguments(iso, rvz, OutputFormat.RVZ, Path.Combine(task, "user")), task, "RVZ preset check"), log, timeout.Token);
            if (result.ExitCode != 0 || !File.Exists(rvz)) return false;
            var info = ImageInspector.Read(rvz);
            byte[] header = new byte[0x58];
            using (var stream = File.OpenRead(rvz)) stream.ReadExactly(header);
            bool valid = info.Format == ImageFormat.RVZ && info.Disc.GameId == "GTEST1" && info.Disc.IsoSize == 0x80000
                && BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x4c)) == 5
                && BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x50)) == 5
                && BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x54)) == 131072;
            log?.Invoke(valid ? "RVZ preset verified using a synthetic non-game image: zstd level 5, 131072-byte blocks." : "RVZ preset produced an unexpected container.");
            return valid;
        }
        catch (UnsafeTerminationException ex) { log?.Invoke($"Preset probe directory preserved: {task}. {ex.Message}"); task = null; return false; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { log?.Invoke("RVZ preset check timed out."); return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConversionException) { log?.Invoke($"RVZ preset verification failed: {ex.Message}"); return false; }
        finally { SafeFiles.CleanupOwned(task, owner, log); }
    }
    public async Task<bool> DolphinCanRead(string executable, string path, string expectedId, Action<string>? log, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var result = await runner.RunAsync(new(executable, ["header", "--input", path, "--json"], Path.GetDirectoryName(path)!, "DolphinTool inspection"), log, timeout.Token);
            if (result.ExitCode != 0) return false;
            using var json = JsonDocument.Parse(result.StandardOutput.Trim());
            return json.RootElement.TryGetProperty("game_id", out var id) && id.GetString() == expectedId;
        }
        catch (JsonException) { return false; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
    }
}

public static class ProbeImages
{
    /// <summary>A tiny non-game GameCube disc header and empty filesystem for capability tests only.</summary>
    public static void WriteIso(string path)
    {
        byte[] data = new byte[0x80000];
        "GTEST1"u8.CopyTo(data); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x1c), 0xC2339F3D);
        "RVZ Converter synthetic capability test"u8.CopyTo(data.AsSpan(0x20));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x424), 0x3000);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x428), 12); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x42c), 12);
        data[0x3000] = 1; data[0x300b] = 1;
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write); stream.Write(data);
    }
}
