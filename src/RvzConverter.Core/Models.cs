namespace RvzConverter.Core;

public enum OutputFormat { ISO, CISO, WBFS, RVZ }
public enum ImageFormat { ISO, RVZ, GCZ, WIA, CISO, WBFS }
public enum EngineKind { Dolphin, Wit }
public enum DiscPlatform { GameCube, Wii }
public enum ConversionState { Pending, Inspecting, Converting, Validating, Publishing, Completed, Failed, Cancelled }
public sealed record DiscInfo(DiscPlatform Platform, long IsoSize, long SourceSize, string Title, string GameId, bool ExactSize = true, long? WitIsoSize = null);
public sealed record ImageInfo(ImageFormat Format, DiscInfo Disc, IReadOnlyList<string> Files);
public sealed record ConversionOptions(string Destination, string TemporaryDirectory, OutputFormat Format,
    bool SplitWbfs, string DolphinPath, string WitPath);
public sealed record ConversionUpdate(string Source, ConversionState State, string Message, TimeSpan Elapsed,
    int? Percentage = null);
public sealed record ConversionResult(string Source, ConversionState State, string Message,
    TimeSpan Elapsed, IReadOnlyList<string> Outputs);
public sealed record ProcessRequest(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory, string Label,
    Action<string>? StandardOutputLine = null);
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? log, CancellationToken cancellationToken);
}
public sealed class ConversionException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class UnsafeTerminationException(string message) : Exception(message);
public static class Display
{
    public static string Bytes(long size) => size >= 1024L * 1024 * 1024
        ? $"{size / (1024d * 1024 * 1024):0.00} GiB" : $"{size / (1024d * 1024):0.0} MiB";
    public static string Duration(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" : $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
}
