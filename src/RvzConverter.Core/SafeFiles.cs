using System.Text.RegularExpressions;

namespace RvzConverter.Core;

public interface IFileMover { void Move(string source, string destination); }
public sealed class FileMover : IFileMover
{
    public void Move(string source, string destination) => File.Move(source, destination, overwrite: false);
}

public static partial class SafeFiles
{
    public static string OutputName(string source, OutputFormat format)
        => Path.GetFileNameWithoutExtension(source) + "." + format.ToString().ToLowerInvariant();

    public static bool IsWbfsPart(string name, string stem)
        => name.Equals(stem + ".wbfs", StringComparison.OrdinalIgnoreCase)
           || Regex.IsMatch(name, "^" + Regex.Escape(stem) + @"\.wbf[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static void CheckCollision(string destination, string fileName, OutputFormat format)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (var entry in Directory.EnumerateFileSystemEntries(destination))
        {
            string name = Path.GetFileName(entry);
            if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                (format == OutputFormat.WBFS && IsWbfsPart(name, stem)))
                throw new ConversionException($"A file or directory already exists: {entry}. Choose another destination or source name. Overwriting is not allowed.");
        }
    }

    public static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ConversionException($"The directory '{directory.FullName}' is a link or junction. Select its physical path for safe cleanup and volume checks.");
    }

    public static string CreateOwnedDirectory(string parent, string prefix, string owner)
    {
        RejectReparseAncestors(parent);
        var path = Path.Combine(Path.GetFullPath(parent), prefix + owner);
        if (Directory.Exists(path) || File.Exists(path)) throw new IOException("The task directory already exists.");
        Directory.CreateDirectory(path);
        using var stream = new FileStream(Path.Combine(path, ".rvz-owner"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(owner);
        return path;
    }

    public static void CleanupOwned(string? directory, string owner, Action<string>? log)
    {
        if (directory is null || !Directory.Exists(directory)) return;
        try
        {
            RejectReparseAncestors(directory);
            var marker = Path.Combine(directory, ".rvz-owner");
            if (File.ReadAllText(marker) != owner) throw new IOException("Task ownership marker mismatch.");
            // Inspect without following links before any recursive deletion.
            CheckTree(directory);
            Directory.Delete(directory, recursive: true);
            log?.Invoke($"Task directory cleaned: {directory}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConversionException)
        { log?.Invoke($"Cleanup skipped: {directory}. {ex.Message}"); }
    }

    private static void CheckTree(string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Link detected inside the task directory.");
            if ((attributes & FileAttributes.Directory) != 0) CheckTree(entry);
        }
    }
}
