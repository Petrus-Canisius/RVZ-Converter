using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RvzConverter.Core;

namespace RvzConverter.App;

public partial class MainWindow : Window
{
    public MainViewModel Model { get; }
    private bool exiting;
    public MainWindow()
    {
        InitializeComponent(); Model = new(); DataContext = Model;
        QueueList.AddHandler(DragDrop.DropEvent, Drop);
        QueueList.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = Model.CanEdit && e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
        Opened += async (_, _) => { await Model.DiscoverAsync(); await SmokeIfRequested(); };
        Closing += (_, e) =>
        {
            if (Model.IsRunning) { e.Cancel = true; Model.Cancel(); exiting = true; }
            else Model.Dispose();
        };
        Model.PropertyChanged += (_, e) => { if (exiting && e.PropertyName == nameof(Model.IsRunning) && !Model.IsRunning) Close(); };
    }
    private async void AddClick(object? sender, RoutedEventArgs e) => await Guard(async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Add disc images", AllowMultiple = true, FileTypeFilter = [new("Disc images") { Patterns = ["*.iso", "*.rvz", "*.gcz", "*.wia", "*.ciso", "*.wbfs"] }, FilePickerFileTypes.All] });
        Model.Add(files.Select(f => f.TryGetLocalPath()).OfType<string>());
    });
    private void RemoveClick(object? sender, RoutedEventArgs e) => Model.Remove(QueueList.SelectedItems?.OfType<QueueItem>().ToArray() ?? []);
    private void Drop(object? sender, DragEventArgs e) { if (Model.CanEdit) Model.Add(e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>() ?? []); }
    private async Task Browse(bool temporary)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = temporary ? "Select temporary folder" : "Select destination folder", AllowMultiple = false });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) { if (temporary) Model.TemporaryDirectory = path; else Model.Destination = path; Model.Save(); }
    }
    private async void DestinationClick(object? sender, RoutedEventArgs e) => await Guard(() => Browse(false));
    private async void TemporaryClick(object? sender, RoutedEventArgs e) => await Guard(() => Browse(true));
    private async Task Locate(EngineKind kind)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = kind == EngineKind.Dolphin ? "Locate DolphinTool" : "Locate WIT", AllowMultiple = false });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await Model.LocateAsync(kind, path);
    }
    private async void DolphinClick(object? sender, RoutedEventArgs e) => await Guard(() => Locate(EngineKind.Dolphin));
    private async void WitClick(object? sender, RoutedEventArgs e) => await Guard(() => Locate(EngineKind.Wit));
    private async void RecheckClick(object? sender, RoutedEventArgs e) => await Guard(Model.DiscoverAsync);
    private async void StartClick(object? sender, RoutedEventArgs e) => await Guard(Model.StartAsync);
    private void CancelClick(object? sender, RoutedEventArgs e) => Model.Cancel();
    private async void OpenOutputClick(object? sender, RoutedEventArgs e) => await Guard(async () =>
    {
        Directory.CreateDirectory(Model.Destination);
        var folder = await StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(Model.Destination)));
        if (folder is null || !await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(Model.Destination))) Model.Progress.Stop("Could not open the output folder with the system file manager.");
    });
    private async void ExportClick(object? sender, RoutedEventArgs e) => await Guard(async () =>
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Export conversion log", SuggestedFileName = $"rvz-converter-{DateTime.Now:yyyyMMdd-HHmmss}.log", ShowOverwritePrompt = true });
        if (file?.TryGetLocalPath() is not { } path) return;
        // Export also refuses to overwrite an existing file.
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await using var writer = new StreamWriter(stream); await writer.WriteAsync(Model.LogText);
    });
    private async void AboutClick(object? sender, RoutedEventArgs e) => await new AboutWindow().ShowDialog(this);
    private async Task Guard(Func<Task> action) { try { await action(); } catch (Exception ex) { Model.Progress.Stop("Operation failed: " + ex.Message); Model.Write(Model.Progress.Operation); } }
    // A native-window smoke mode for CI/manual review, never used during normal operation.
    private async Task SmokeIfRequested()
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--smoke-ui");
        if (index < 0 || index + 1 >= args.Length) return;
        string output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
        try
        {
            foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
            {
                Width = 900; Height = 790; await Task.Delay(120);
                var bitmap = new RenderTargetBitmap(new PixelSize((int)(Bounds.Width * scale), (int)(Bounds.Height * scale)), new Vector(96 * scale, 96 * scale));
                bitmap.Render(this); bitmap.Save(Path.Combine(output, $"main-{scale:0.##}.png")); bitmap.Dispose();
            }
            string fixture = Path.Combine(output, "Synthetic non-game image [é 日本語] with a long filename for queue alignment.iso");
            ProbeImages.WriteIso(fixture); Model.Add([fixture]); Model.IsRvz = true;
            string invalid = Path.Combine(output, "Invalid image fixture.iso"); File.WriteAllBytes(invalid, new byte[128]); Model.Add([invalid]);
            await Task.Delay(120); Capture("queue-and-error.png");
            Width = 660; Height = 580; Model.Progress.Update(new("test", ConversionState.Converting, "Step 2 of 2: final CISO conversion (synthetic UI fixture)", TimeSpan.Zero, 42), 2, 5);
            await Task.Delay(150); Capture("compact-progress.png");
            Model.Progress.Wait("Validating container readability (synthetic UI fixture)…");
            // The native theme's marquee begins outside the track. Capture a visible frame.
            await Task.Delay(args.Contains("--hold-ui") ? 20000 : 1050); Capture("indeterminate.png");
            var about = new AboutWindow(); about.Show(this); await Task.Delay(150);
            using (var image = new RenderTargetBitmap(new PixelSize((int)about.Bounds.Width, (int)about.Bounds.Height))) { image.Render(about); image.Save(Path.Combine(output, "about.png")); }
            about.Close(); File.WriteAllText(Path.Combine(output, "native-launch.txt"), $"Opened Avalonia native windows on {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}.\nRender checks at 100/125/150/200 percent; compact layout; real engine discovery. Progress fixture is simulated.\n");
            void Capture(string name) { using var image = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height)); image.Render(this); image.Save(Path.Combine(output, name)); }
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); }
        finally { Dispatcher.UIThread.Post(Close); }
    }
}
