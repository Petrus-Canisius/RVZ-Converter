using System.Diagnostics;

namespace RvzConverter.Core;

public sealed class ConversionCoordinator(IProcessRunner runner, IVolumeProbe volumes, IFileMover? fileMover = null)
{
    public async Task<IReadOnlyList<ConversionResult>> RunAsync(IEnumerable<string> sources, ConversionOptions options,
        IProgress<ConversionUpdate>? progress = null, Action<string>? log = null, CancellationToken token = default)
    {
        var checks = await new EngineService(runner).CheckAsync(options.DolphinPath, options.WitPath, log, token).ConfigureAwait(false);
        var results = new List<ConversionResult>();
        foreach (string source in sources)
        {
            if (token.IsCancellationRequested)
            {
                var cancelled = new ConversionResult(source, ConversionState.Cancelled, "Cancelled before processing.", TimeSpan.Zero, []);
                results.Add(cancelled); progress?.Report(new(source, cancelled.State, cancelled.Message, cancelled.Elapsed)); continue;
            }
            results.Add(await ConvertOne(source, options, checks, progress, log, token).ConfigureAwait(false));
        }
        return results;
    }

    private async Task<ConversionResult> ConvertOne(string source, ConversionOptions options, EngineCheck engines,
        IProgress<ConversionUpdate>? progress, Action<string>? log, CancellationToken token)
    {
        var clock = Stopwatch.StartNew(); string owner = Guid.NewGuid().ToString("N");
        string? task = null, stage = null; bool keepStage = false, keepTask = false;
        var locks = new List<FileStream>(); IReadOnlyList<string> outputs = [];
        try
        {
            Report(ConversionState.Inspecting, "Inspecting container and disc signatures…");
            var image = ImageInspector.Read(source);
            foreach (string input in image.Files) locks.Add(new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read));
            // Re-read after locking to reject source replacement between initial inspection and processing.
            var lockedImage = ImageInspector.Read(source);
            if (image != lockedImage && (image.Format != lockedImage.Format || image.Disc != lockedImage.Disc || !image.Files.SequenceEqual(lockedImage.Files, PlatformPaths.PathComparer)))
                throw new ConversionException("The source changed during inspection. Add it again after its writer has finished.");
            bool dolphinReads = engines.DolphinAvailable && !(image.Format == ImageFormat.WBFS && image.Files.Count > 10)
                && await new EngineService(runner).DolphinCanRead(options.DolphinPath, Path.GetFullPath(source), image.Disc.GameId, log, token);
            var route = ConversionPlanner.Plan(image.Format, image.Disc.Platform, options.Format,
                engines.DolphinAvailable, engines.WitAvailable, engines.RvzPresetAvailable, dolphinReads);
            string destination = Path.GetFullPath(options.Destination), temporary = Path.GetFullPath(options.TemporaryDirectory);
            SafeFiles.RejectReparseAncestors(destination); SafeFiles.RejectReparseAncestors(temporary);
            Directory.CreateDirectory(destination); Directory.CreateDirectory(temporary);
            string finalName = SafeFiles.OutputName(source, options.Format);
            SafeFiles.CheckCollision(destination, finalName, options.Format);
            var tempVolume = volumes.Get(temporary); var outputVolume = volumes.Get(destination);
            DiskSpace.Ensure(image, route, options, tempVolume, outputVolume);
            bool sameVolume = tempVolume.Id.Equals(outputVolume.Id, StringComparison.OrdinalIgnoreCase);
            var budget = DiskSpace.Calculate(image, route, sameVolume);
            log?.Invoke($"Source: {image.Format}, {image.Disc.Platform}, {image.Disc.GameId}. Disc size {(image.Disc.ExactSize ? "exact" : "upper bound")}: {Display.Bytes(image.Disc.IsoSize)}. Temporary budget: {Display.Bytes(budget.TemporaryBytes)}; destination budget: {Display.Bytes(budget.OutputBytes)}.");
            task = SafeFiles.CreateOwnedDirectory(temporary, "rvz-task-", owner);
            stage = SafeFiles.CreateOwnedDirectory(destination, ".rvz-stage-", owner);
            string current = Path.GetFullPath(source);
            if (route.CopySource)
            {
                Report(ConversionState.Inspecting, "Preparing a read-only source snapshot for WIT…");
                current = Path.Combine(task, "input." + image.Format.ToString().ToLowerInvariant());
                for (int i = 0; i < image.Files.Count; i++)
                    await CopyNew(image.Files[i], i == 0 ? current : Path.ChangeExtension(current, $".wbf{i}"), token);
                // Only fixed relative ASCII names reach WIT; this also protects paths with glob characters.
                var inspected = await Run(options.WitPath, ["DUMP", Path.GetFileName(current)], task, "WIT input inspection", token);
                if (!inspected.StandardOutput.Contains(image.Disc.GameId, StringComparison.Ordinal))
                    throw new ConversionException("WIT could not reopen the source as the expected disc. This source/engine combination is unsupported or damaged.");
            }
            for (int index = 0; index < route.Steps.Count; index++)
            {
                var step = route.Steps[index];
                string label = $"Step {index + 1} of {route.Steps.Count}: " + (step.Intermediate ? "intermediate ISO conversion" : $"final {step.Format} conversion") + $" ({(step.Engine == EngineKind.Dolphin ? "DolphinTool" : "WIT")})";
                Report(ConversionState.Converting, label + "…");
                string next = step.Intermediate ? Path.Combine(task, "disc.iso") : Path.Combine(step.Engine == EngineKind.Dolphin ? stage : task, "image." + step.Format.ToString().ToLowerInvariant());
                if (step.Engine == EngineKind.Dolphin)
                    await Run(options.DolphinPath, ConversionPlanner.DolphinArguments(current, next, step.Format, Path.Combine(task, "dolphin-user")), task, "DolphinTool", token);
                else
                {
                    log?.Invoke(step.Format == OutputFormat.ISO
                        ? "WIT COPY to ISO uses raw selection. Missing/scrubbed source data cannot be restored."
                        : "WIT COPY retains all partitions but scrubs unused blocks; automatic encoding may rebuild encryption and hashes. CISO/WBFS are not promised byte-identical to the source.");
                    var reader = new WitProgressReader();
                    await Run(options.WitPath, ConversionPlanner.WitArguments(Path.GetFileName(current), Path.GetFileName(next), step.Format, options.SplitWbfs), task, "WIT", token, line =>
                    {
                        if (reader.Read(line) is { } percent)
                            progress?.Report(new(source, ConversionState.Converting, label + (percent == 100 ? ": finishing engine output…" : ""), clock.Elapsed, percent < 100 ? percent : null));
                    });
                }
                if (step.Intermediate) ValidateIso(next, image.Disc);
                current = next;
            }
            if (route.TemporaryOutput)
            {
                Report(ConversionState.Converting, "Transferring converted files to destination staging…");
                var temporaryOutputs = ValidateOutputs(task, options, image.Disc, tempVolume.FileSystem);
                foreach (string file in temporaryOutputs)
                {
                    token.ThrowIfCancellationRequested(); string target = Path.Combine(stage, Path.GetFileName(file));
                    if (sameVolume) File.Move(file, target, overwrite: false); else await CopyNew(file, target, token);
                }
            }
            Report(ConversionState.Validating, "Validating container size, signatures, and engine readability…");
            var staged = ValidateOutputs(stage, options, image.Disc, outputVolume.FileSystem);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                if (options.Format is OutputFormat.ISO or OutputFormat.RVZ && engines.DolphinAvailable)
                {
                    if (!await new EngineService(runner).DolphinCanRead(options.DolphinPath, staged[0], image.Disc.GameId, log, timeout.Token))
                        throw new ConversionException("DolphinTool could not reopen the output as the expected disc.");
                }
                else
                {
                    var validation = await Run(options.WitPath, ["DUMP", Path.GetFileName(staged[0])], stage, "WIT output validation", timeout.Token);
                    if (!validation.StandardOutput.Contains(image.Disc.GameId, StringComparison.Ordinal))
                        throw new ConversionException("WIT did not recognize the expected disc identifier in the output.");
                }
            }
            log?.Invoke("Container validation passed. This is a readability/structure check, not full disc integrity verification.");
            token.ThrowIfCancellationRequested();
            Report(ConversionState.Publishing, "Placing validated files in the final destination…");
            outputs = new OutputPublisher(fileMover ?? new FileMover()).Publish(staged, destination, finalName, options.Format, token);
            foreach (string file in outputs) log?.Invoke($"Output placed: {file}");
            return Finish(ConversionState.Completed, "Completed: container validated and final files placed.");
        }
        catch (PublicationException ex) { keepStage = true; outputs = ex.Committed; log?.Invoke(ex.Message); return Finish(token.IsCancellationRequested ? ConversionState.Cancelled : ConversionState.Failed, ex.Message); }
        catch (UnsafeTerminationException ex)
        { keepStage = keepTask = true; log?.Invoke(ex.Message); return Finish(ConversionState.Cancelled, $"Process termination could not be fully confirmed. Working directories preserved: {task}; {stage}. {ex.Message}"); }
        catch (OperationCanceledException)
        { return Finish(token.IsCancellationRequested ? ConversionState.Cancelled : ConversionState.Failed, token.IsCancellationRequested ? "Cancelled. Engine stopped; see the log for cleanup results." : "Engine inspection or validation timed out."); }
        catch (Exception ex) when (ex is ConversionException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        { log?.Invoke($"Conversion failed: {ex.Message}"); return Finish(ConversionState.Failed, ex.Message); }
        finally
        {
            foreach (var input in locks) input.Dispose();
            if (!keepTask) SafeFiles.CleanupOwned(task, owner, log);
            if (!keepStage) SafeFiles.CleanupOwned(stage, owner, log);
        }
        void Report(ConversionState state, string message)
        { log?.Invoke($"{Path.GetFileName(source)}: {message}"); progress?.Report(new(source, state, message, clock.Elapsed, state == ConversionState.Completed ? 100 : null)); }
        ConversionResult Finish(ConversionState state, string message) { Report(state, message); return new(source, state, message, clock.Elapsed, outputs); }
        async Task<ProcessResult> Run(string executable, IReadOnlyList<string> args, string cwd, string label, CancellationToken ct, Action<string>? outputLine = null)
        {
            var result = await runner.RunAsync(new(executable, args, cwd, label, outputLine), log, ct).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new ConversionException($"{label} failed (exit code {result.ExitCode}). See the engine diagnostics in the log.");
            return result;
        }
    }
    private static async Task CopyNew(string source, string target, CancellationToken token)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await input.CopyToAsync(output, token).ConfigureAwait(false); await output.FlushAsync(token).ConfigureAwait(false);
    }
    private static void ValidateIso(string path, DiscInfo expected)
    {
        if (!File.Exists(path)) throw new ConversionException("The engine did not create the expected ISO.");
        long length = new FileInfo(path).Length;
        if (expected.ExactSize ? length != expected.IsoSize : length < 0x50000 || length > expected.IsoSize)
            throw new ConversionException("The output ISO size is outside the source container's declared size/bound.");
        var actual = ImageInspector.Read(path);
        if (actual.Format != ImageFormat.ISO || actual.Disc.Platform != expected.Platform || actual.Disc.GameId != expected.GameId)
            throw new ConversionException("The output ISO signature or disc identifier is inconsistent with the source.");
    }
    public static IReadOnlyList<string> ValidateOutputs(string directory, ConversionOptions options, DiscInfo expected, string fileSystem)
    {
        string primary = Path.Combine(directory, "image." + options.Format.ToString().ToLowerInvariant());
        if (!File.Exists(primary)) throw new ConversionException("The engine did not create the expected output file.");
        var files = options.Format == OutputFormat.WBFS ? ImageInspector.WbfsFiles(primary) : new[] { primary };
        if (!options.SplitWbfs && files.Count > 1) throw new ConversionException("Unexpected split output while WBFS splitting is disabled.");
        foreach (string file in files)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new ConversionException("Output links are not accepted.");
            long length = new FileInfo(file).Length;
            if (length == 0) throw new ConversionException("The engine created an empty output file.");
            if ((fileSystem.Equals("FAT32", StringComparison.OrdinalIgnoreCase) && length > DiskSpace.Fat32MaxFile) ||
                (options.Format == OutputFormat.WBFS && options.SplitWbfs && length > DiskSpace.WbfsPartSize))
                throw new ConversionException("An output file exceeds the allowed file/part size.");
        }
        if (options.Format == OutputFormat.ISO) ValidateIso(primary, expected);
        else if (options.Format == OutputFormat.RVZ)
        {
            var rvz = ImageInspector.Read(primary);
            if (rvz.Format != ImageFormat.RVZ || rvz.Disc.Platform != expected.Platform || rvz.Disc.GameId != expected.GameId ||
                (expected.ExactSize ? rvz.Disc.IsoSize != expected.IsoSize : rvz.Disc.IsoSize > expected.IsoSize))
                throw new ConversionException("The RVZ output metadata does not match the source.");
        }
        else
        {
            using var stream = File.OpenRead(primary); Span<byte> magic = stackalloc byte[4]; stream.ReadExactly(magic);
            if (!magic.SequenceEqual(options.Format == OutputFormat.CISO ? "CISO"u8 : "WBFS"u8)) throw new ConversionException("The output container signature is incorrect.");
        }
        return files;
    }
}
