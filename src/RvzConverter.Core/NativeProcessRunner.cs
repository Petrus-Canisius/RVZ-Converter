using System.Diagnostics;
using System.Text;

namespace RvzConverter.Core;

public sealed class NativeProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(request.Executable),
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        // Cygwin WIT otherwise interprets Unicode command-line paths with the C locale.
        start.Environment["LANG"] = OperatingSystem.IsMacOS() ? "en_US.UTF-8" : "C.UTF-8";
        start.Environment["LC_ALL"] = start.Environment["LANG"];
        foreach (var argument in request.Arguments) start.ArgumentList.Add(argument);
        log?.Invoke($"[{request.Label}] {Path.GetFileName(request.Executable)} {string.Join(" ", request.Arguments.Select(a => $"\"{a}\""))}");
        using var process = new Process { StartInfo = start };
        try { process.Start(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new ConversionException($"Cannot start {request.Label}. Check executable permissions, architecture, and native dependencies ({ex.GetType().Name}, native error {(ex is System.ComponentModel.Win32Exception native ? native.NativeErrorCode : 0)}).", ex);
        }
        var stdout = ReadAsync(process.StandardOutput, "stdout");
        var stderr = ReadAsync(process.StandardError, "stderr");
        var descendants = new List<Process>();
        bool terminationUncertain = false;
        string? interactiveError = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // The registration remains alive through stream draining. Never clean files while the engine is alive.
        using var registration = stop.Token.Register(() =>
        {
            lock (descendants)
            {
                try { descendants.AddRange(ProcessTree.CaptureDescendants(process.Id)); }
                catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception) { terminationUncertain = true; log?.Invoke("Could not enumerate all engine descendants; working files will be preserved."); }
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { terminationUncertain = true; log?.Invoke("Engine termination could not be confirmed; waiting for exit."); }
                foreach (var child in descendants)
                {
                    try { if (!child.HasExited) child.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { terminationUncertain = true; log?.Invoke("A descendant could not be terminated; waiting for exit."); }
                }
            }
        });
        var dialogWatcher = WatchDialogs();
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        await dialogWatcher.ConfigureAwait(false);
        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        registration.Dispose(); // Wait for a cancellation callback still capturing/killing descendants.
        foreach (var child in descendants)
        {
            try { await child.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
            finally { child.Dispose(); }
        }
        if (terminationUncertain) throw new UnsafeTerminationException("Engine descendant termination was not fully confirmed. Cleanup is disabled for this task.");
        log?.Invoke($"[{request.Label}] Exit code: {process.ExitCode}");
        log?.Invoke($"[{request.Label}] Captured stdout: {output.Length} characters; stderr: {error.Length} characters.");
        cancellationToken.ThrowIfCancellationRequested();
        if (interactiveError is not null) throw new ConversionException("The conversion engine opened an interactive error dialog and was stopped without accepting it: " + interactiveError);
        return new(process.ExitCode, output, error);

        async Task WatchDialogs()
        {
            if (!OperatingSystem.IsWindows()) return;
            while (!process.HasExited && !stop.IsCancellationRequested)
            {
                if (EngineDialogWatcher.ReadErrorDialog(process.Id) is { } message)
                {
                    interactiveError = message;
                    log?.Invoke($"[{request.Label}] Interactive engine error: {message}. Stopping the engine and descendants.");
                    stop.Cancel();
                    return;
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        async Task<string> ReadAsync(StreamReader reader, string channel)
        {
            var tail = new StringBuilder();
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (channel == "stdout") request.StandardOutputLine?.Invoke(line);
                log?.Invoke($"[{request.Label} / {channel}] {line}");
                tail.AppendLine(line);
                if (tail.Length > 131072) tail.Remove(0, tail.Length - 65536);
            }
            return tail.ToString();
        }
    }
}
