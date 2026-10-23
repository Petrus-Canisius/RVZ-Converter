namespace RvzConverter.Core;

public sealed class PublicationException(string message, IReadOnlyList<string> committed, string recoveryDirectory, Exception inner)
    : Exception(message, inner)
{
    public IReadOnlyList<string> Committed { get; } = committed;
    public string RecoveryDirectory { get; } = recoveryDirectory;
}

/// <summary>Individual no-overwrite moves. Multi-file publication is explicitly NOT atomic.</summary>
public sealed class OutputPublisher(IFileMover mover)
{
    public IReadOnlyList<string> Publish(IReadOnlyList<string> staged, string destination, string finalName,
        OutputFormat format, CancellationToken token)
    {
        var committed = new List<string>();
        string recovery = Path.GetDirectoryName(staged[0])!;
        string stem = Path.GetFileNameWithoutExtension(finalName);
        try
        {
            SafeFiles.RejectReparseAncestors(destination);
            SafeFiles.CheckCollision(destination, finalName, format);
            foreach (var file in staged)
            {
                token.ThrowIfCancellationRequested();
                string target = Path.Combine(destination, stem + Path.GetExtension(file));
                mover.Move(file, target);
                committed.Add(target);
            }
            return committed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or ConversionException)
        {
            throw new PublicationException(
                $"Placement interrupted: {committed.Count} file(s) moved. Moved files are preserved; remaining files are in '{recovery}'. Moving multiple parts is not atomic. No pre-existing file was changed. {ex.Message}",
                committed, recovery, ex);
        }
    }
}
