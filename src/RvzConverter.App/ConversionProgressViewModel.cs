using RvzConverter.Core;

namespace RvzConverter.App;

/// <summary>Only engine-reported stage progress is displayed. Overall success alone unlocks 100%.</summary>
public sealed class ConversionProgressViewModel : Observable
{
    private int? percentage;
    private bool indeterminate;
    private string position = "No conversion running.", operation = "Ready.", percentageText = "";
    public int? Percentage { get => percentage; private set { percentage = value; Changed(); Changed(nameof(Value)); } }
    public double Value => Percentage ?? 0;
    public bool IsIndeterminate { get => indeterminate; private set => Set(ref indeterminate, value); }
    public string QueuePosition { get => position; private set => Set(ref position, value); }
    public string Operation { get => operation; private set => Set(ref operation, value); }
    public string PercentageText { get => percentageText; private set => Set(ref percentageText, value); }
    public void Begin(int count) { QueuePosition = $"Queue: {count} file(s)"; Wait("Checking conversion engines…"); }
    public void Wait(string message) { Percentage = null; PercentageText = "Progress unavailable"; IsIndeterminate = true; Operation = message; }
    public void Update(ConversionUpdate update, int index, int total)
    {
        QueuePosition = $"File {index} of {total}"; Operation = update.Message;
        Percentage = update.State == ConversionState.Converting && update.Percentage is >= 0 and < 100 ? update.Percentage : null;
        IsIndeterminate = Percentage is null && update.State is ConversionState.Inspecting or ConversionState.Converting or ConversionState.Validating or ConversionState.Publishing;
        PercentageText = Percentage is { } value ? $"{value}% of current step" : IsIndeterminate ? "Progress unavailable" : "";
    }
    public void Finish(IReadOnlyList<ConversionResult> results, bool cancelled)
    {
        cancelled |= results.Any(r => r.State == ConversionState.Cancelled);
        bool success = !cancelled && results.Count > 0 && results.All(r => r.State == ConversionState.Completed);
        Percentage = success ? 100 : null; PercentageText = success ? "100%" : ""; IsIndeterminate = false;
        Operation = cancelled ? "Cancelled." : results.Any(r => r.State == ConversionState.Failed) ? "Finished with errors. See the queue and log." : success ? "Conversion, validation and final placement completed." : "No files processed.";
    }
    public void Stop(string message) { Percentage = null; PercentageText = ""; IsIndeterminate = false; Operation = message; }
}
