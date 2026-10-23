using System.Globalization;

namespace RvzConverter.Core;

/// <summary>Reads WIT's documented --sections --progress output, never compression ratios or ETA.</summary>
public sealed class WitProgressReader
{
    private bool inProgress;
    private bool copying;

    public int? Read(string line)
    {
        line = line.Trim();
        if (line.StartsWith('['))
        {
            inProgress = line.Equals("[progress]", StringComparison.Ordinal);
            copying = false;
            return null;
        }
        if (line.Length == 0) { inProgress = false; copying = false; return null; }
        if (!inProgress) return null;
        if (line.StartsWith("verb=", StringComparison.Ordinal))
        {
            copying = line.Equals("verb=copied", StringComparison.Ordinal);
            return null;
        }
        if (copying && line.StartsWith("percent=", StringComparison.Ordinal) &&
            int.TryParse(line.AsSpan(8), NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value is >= 0 and <= 100)
            return value;
        return null;
    }
}
