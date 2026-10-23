using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using RvzConverter.Core;

namespace RvzConverter.App;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; Changed(name); } }
}
public sealed class QueueItem(string path, ImageInfo? image, string? error) : Observable
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public ImageInfo? Image { get; } = image;
    private string status = error is null ? "Pending" : "Invalid", detail = error ?? $"{image!.Format} · {image.Disc.Platform} · {image.Disc.GameId}", elapsed = "—";
    public string Status { get => status; set => Set(ref status, value); }
    public string Detail { get => detail; set => Set(ref detail, value); }
    public string Elapsed { get => elapsed; set => Set(ref elapsed, value); }
}
public sealed class MainViewModel : Observable, IDisposable
{
    private readonly PreferencesStore store;
    private readonly Preferences preferences;
    private readonly SessionLog? log;
    private CancellationTokenSource? cancellation;
    private readonly DispatcherTimer elapsedTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch elapsedClock = new();
    private QueueItem? activeItem;
    private TimeSpan activeElapsed;
    private bool logUnavailable;
    private bool running, checking;
    private string logText = "", engineMessage = "Checking installed conversion engines…", routeMessage = "";
    private EngineCheck engines = new(false, false, "", false);
    public ObservableCollection<QueueItem> Queue { get; } = [];
    public ConversionProgressViewModel Progress { get; } = new();
    public string EmptyMessage => "The queue is empty. Add your own disc images to begin. Source files are never changed.";
    public bool IsEmpty => Queue.Count == 0;
    public bool IsRunning { get => running; private set { Set(ref running, value); Refresh(); } }
    public bool CanEdit => !IsRunning && !checking;
    public bool CanStart => CanEdit && Queue.Any(q => q.Status == "Pending" && Supported(q.Image, preferences.Format));
    public string LogText { get => logText; private set => Set(ref logText, value); }
    public string EngineMessage { get => engineMessage; private set => Set(ref engineMessage, value); }
    public string RouteMessage { get => routeMessage; private set => Set(ref routeMessage, value); }
    public bool DolphinMissing => !engines.DolphinAvailable || !engines.RvzPresetAvailable;
    public bool WitMissing => !engines.WitAvailable;
    public bool IsoAvailable => Available(OutputFormat.ISO);
    public bool RvzAvailable => Available(OutputFormat.RVZ);
    public bool CisoAvailable => Available(OutputFormat.CISO);
    public bool WbfsAvailable => Available(OutputFormat.WBFS);
    public bool IsIso { get => preferences.Format == OutputFormat.ISO; set { if (value) Select(OutputFormat.ISO); } }
    public bool IsRvz { get => preferences.Format == OutputFormat.RVZ; set { if (value) Select(OutputFormat.RVZ); } }
    public bool IsCiso { get => preferences.Format == OutputFormat.CISO; set { if (value) Select(OutputFormat.CISO); } }
    public bool IsWbfs { get => preferences.Format == OutputFormat.WBFS; set { if (value) Select(OutputFormat.WBFS); } }
    public string Destination { get => preferences.Destination; set { preferences.Destination = value; Changed(); } }
    public string TemporaryDirectory { get => preferences.TemporaryDirectory; set { preferences.TemporaryDirectory = value; Changed(); } }
    public bool SplitWbfs { get => preferences.SplitWbfs; set { preferences.SplitWbfs = value; Changed(); } }
    public MainViewModel(PreferencesStore? preferencesStore = null, bool writeLog = true)
    {
        store = preferencesStore ?? new(); preferences = store.Load(out string? warning);
        try { if (writeLog) log = new SessionLog(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { LogText = "Cannot create the session log: " + ex.GetType().Name; }
        if (warning is not null) Write(warning);
        Write("RVZ Converter 2.0.1. Created by Petrus-Canisius. No telemetry or automatic downloads.");
        elapsedTimer.Tick += (_, _) => { if (activeItem is not null) activeItem.Elapsed = Display.Duration(activeElapsed + elapsedClock.Elapsed); };
    }
    public async Task DiscoverAsync()
    {
        if (!CanEdit) return;
        checking = true; Refresh(); EngineMessage = "Checking executable architecture, dependencies and supported arguments…";
        try
        {
            engines = await new EngineService(new NativeProcessRunner()).DiscoverAsync(preferences, AppContext.BaseDirectory, Write, CancellationToken.None);
            if (engines.DolphinAvailable) preferences.DolphinPath = engines.DolphinPath;
            if (engines.WitAvailable) preferences.WitPath = engines.WitPath;
            EngineMessage = engines.Message; Save();
        }
        catch (Exception ex) { EngineMessage = "Engine check failed: " + ex.Message; Write(EngineMessage); }
        finally { checking = false; Refresh(); }
    }
    public async Task LocateAsync(EngineKind kind, string path)
    {
        if (ExecutableCompatibility.Problem(path) is { } problem) { Progress.Stop(problem); Write(problem); return; }
        checking = true; Refresh();
        try
        {
            var result = await new EngineService(new NativeProcessRunner()).CheckAsync(kind == EngineKind.Dolphin ? path : "", kind == EngineKind.Wit ? path : "", Write, CancellationToken.None);
            if (kind == EngineKind.Dolphin ? result.DolphinAvailable : result.WitAvailable)
            { if (kind == EngineKind.Dolphin) preferences.DolphinPath = path; else preferences.WitPath = path; Save(); }
            else { Progress.Stop("The selected engine failed validation. Check its dependencies and the conversion log."); return; }
        }
        finally { checking = false; Refresh(); }
        await DiscoverAsync();
    }
    public void Add(IEnumerable<string> paths)
    {
        if (!CanEdit) return;
        foreach (string raw in paths)
        {
            string path = System.IO.Path.GetFullPath(raw);
            if (Queue.Any(q => PlatformPaths.PathComparer.Equals(q.Path, path))) continue;
            try { Queue.Add(new(path, ImageInspector.Read(path), null)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConversionException) { Queue.Add(new(path, null, "Cannot read this image: " + ex.Message)); }
        }
        Refresh();
    }
    public void Remove(IEnumerable<QueueItem> items) { if (CanEdit) { foreach (var item in items.ToArray()) Queue.Remove(item); Refresh(); } }
    public void Cancel() { cancellation?.Cancel(); Progress.Wait("Cancelling: waiting for conversion processes and their children to stop…"); }
    public async Task StartAsync()
    {
        if (!CanStart) return;
        var batch = Queue.Where(q => q.Status == "Pending").ToArray();
        using var source = new CancellationTokenSource(); cancellation = source; IsRunning = true; Save(); Progress.Begin(batch.Length); elapsedTimer.Start();
        try
        {
            var progress = new Progress<ConversionUpdate>(u =>
            {
                var item = batch.First(q => PlatformPaths.PathComparer.Equals(q.Path, u.Source));
                item.Status = u.State.ToString(); item.Detail = u.Message; item.Elapsed = Display.Duration(u.Elapsed);
                activeItem = u.State is ConversionState.Inspecting or ConversionState.Converting or ConversionState.Validating or ConversionState.Publishing ? item : null;
                activeElapsed = u.Elapsed; elapsedClock.Restart();
                Progress.Update(u, Array.IndexOf(batch, item) + 1, batch.Length);
            });
            var options = new ConversionOptions(Destination, TemporaryDirectory, preferences.Format, SplitWbfs, preferences.DolphinPath, preferences.WitPath);
            var results = await Task.Run(() => new ConversionCoordinator(new NativeProcessRunner(), new WindowsVolumeProbe()).RunAsync(batch.Select(q => q.Path), options, progress, Write, source.Token));
            Progress.Finish(results, source.IsCancellationRequested);
        }
        catch (Exception ex) { Progress.Stop("Conversion could not start: " + ex.Message); Write(Progress.Operation); }
        finally { elapsedTimer.Stop(); activeItem = null; cancellation = null; IsRunning = false; }
    }
    public void Write(string message)
    {
        try { if (!logUnavailable) log?.Write(message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logUnavailable = true; message = "Session log could not be written; view/export the on-screen log. " + message; }
        void Append() { LogText += $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}"; if (LogText.Length > 160000) LogText = LogText[^120000..]; }
        if (Dispatcher.UIThread.CheckAccess()) Append(); else Dispatcher.UIThread.Post(Append);
    }
    public void Save() { try { store.Save(preferences); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Write("Could not save preferences: " + ex.GetType().Name); } }
    private void Select(OutputFormat format) { preferences.Format = format; Refresh(); }
    private bool Supported(ImageInfo? image, OutputFormat target)
    {
        if (image is null) return false;
        try { ConversionPlanner.Plan(image.Format, image.Disc.Platform, target, engines.DolphinAvailable, engines.WitAvailable, engines.RvzPresetAvailable); return true; }
        catch (ConversionException) { return false; }
    }
    private bool Available(OutputFormat target) => Queue.Any(q => q.Status == "Pending")
        ? Queue.Any(q => q.Status == "Pending" && Supported(q.Image, target))
        : target == OutputFormat.RVZ ? engines.DolphinAvailable && engines.RvzPresetAvailable
        : target == OutputFormat.ISO ? engines.DolphinAvailable || engines.WitAvailable : engines.WitAvailable;
    private void Refresh()
    {
        int supported = Queue.Count(q => q.Status == "Pending" && Supported(q.Image, preferences.Format));
        RouteMessage = $"{supported} pending image(s) ready for {preferences.Format}. WBFS requires Wii; identical-format conversions are unavailable.";
        foreach (string property in new[] { nameof(CanEdit), nameof(CanStart), nameof(IsEmpty), nameof(DolphinMissing), nameof(WitMissing), nameof(IsoAvailable), nameof(RvzAvailable), nameof(CisoAvailable), nameof(WbfsAvailable), nameof(IsIso), nameof(IsRvz), nameof(IsCiso), nameof(IsWbfs) }) Changed(property);
    }
    public void Dispose() { Save(); log?.Dispose(); }
}
