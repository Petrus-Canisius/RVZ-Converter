namespace RvzConverter.Core;

public sealed record ConversionStep(EngineKind Engine, OutputFormat Format, bool Intermediate);
public sealed record ConversionPlan(IReadOnlyList<ConversionStep> Steps, bool CopySource)
{
    public bool IntermediateIso => Steps.Any(s => s.Intermediate);
    public bool TemporaryOutput => Steps[^1].Engine == EngineKind.Wit;
}

public static class ConversionPlanner
{
    public static bool WitReads(ImageFormat format) => format != ImageFormat.RVZ;
    public static ConversionPlan Plan(ImageFormat source, DiscPlatform platform, OutputFormat target,
        bool dolphin, bool wit, bool rvzPreset, bool dolphinReadsSource = true)
    {
        if (source.ToString() == target.ToString()) throw new ConversionException("Source and output formats are identical. Choose a different output format.");
        if (target == OutputFormat.WBFS && platform != DiscPlatform.Wii)
            throw new ConversionException("WBFS output is restricted to Wii discs. Choose ISO, RVZ, or CISO for GameCube.");
        if (target == OutputFormat.RVZ && (!dolphin || !rvzPreset))
            throw new ConversionException("RVZ output requires DolphinTool with the verified Zstandard level 5 / 128 KiB preset.");
        // CISO and WBFS do not carry the original disc length. Materialize a
        // bounded ISO with WIT before passing these containers to RVZ encoding.
        if (target == OutputFormat.RVZ && source is ImageFormat.CISO or ImageFormat.WBFS)
        {
            if (!wit) throw new ConversionException($"{source} to RVZ requires WIT to create an intermediate ISO with a known length.");
            return new([new(EngineKind.Wit, OutputFormat.ISO, true), new(EngineKind.Dolphin, OutputFormat.RVZ, false)], true);
        }
        if (target is OutputFormat.ISO or OutputFormat.RVZ)
        {
            if (dolphin && dolphinReadsSource) return new([new(EngineKind.Dolphin, target, false)], false);
            if (wit && WitReads(source)) return target == OutputFormat.ISO
                ? new([new(EngineKind.Wit, OutputFormat.ISO, false)], true)
                : new([new(EngineKind.Wit, OutputFormat.ISO, true), new(EngineKind.Dolphin, OutputFormat.RVZ, false)], true);
            throw new ConversionException($"{source} to {target} requires a DolphinTool build that can read this image. No compatible fallback is available.");
        }
        if (!wit) throw new ConversionException($"{target} output requires WIT. Use Locate WIT to select a compatible executable.");
        if (WitReads(source)) return new([new(EngineKind.Wit, target, false)], true);
        if (!dolphin || !dolphinReadsSource) throw new ConversionException("This source requires DolphinTool to create an intermediate ISO before WIT can read it.");
        return new([new(EngineKind.Dolphin, OutputFormat.ISO, true), new(EngineKind.Wit, target, false)], false);
    }
    public static IReadOnlyList<string> DolphinArguments(string source, string output, OutputFormat format, string user)
    {
        if (format is not (OutputFormat.ISO or OutputFormat.RVZ)) throw new ConversionException("DolphinTool output must be ISO or RVZ.");
        var args = new List<string> { "convert", "--input", source, "--output", output, "--format", format.ToString().ToLowerInvariant(), "--user", user };
        if (format == OutputFormat.RVZ) args.AddRange(["--compression", "zstd", "--compression_level", "5", "--block_size", "131072"]);
        return args;
    }
    public static IReadOnlyList<string> WitArguments(string source, string output, OutputFormat format, bool split)
    {
        if (format == OutputFormat.RVZ) throw new ConversionException("WIT is not used to write RVZ.");
        var args = new List<string> { "COPY", source, "--no-expand", "--esc", "|", "--dest", output,
            "--" + format.ToString().ToLowerInvariant(), "--enc", "AUTO", "--progress", "--sections" };
        if (format == OutputFormat.ISO) args.Add("--raw");
        else args.AddRange(["--psel", "ALL"]);
        if (format == OutputFormat.WBFS && split) args.AddRange(["--split", "--split-size", "4000M"]);
        return args;
    }
}
