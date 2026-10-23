using System.Text.Json;

namespace RvzConverter.Core;

public sealed class Preferences
{
    public string Destination { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) is { Length: > 0 } docs ? docs : PlatformPaths.Home, "RVZ Converter Output");
    public string TemporaryDirectory { get; set; } = Path.Combine(PlatformPaths.Directories.Cache, "temporary");
    public string DolphinPath { get; set; } = "";
    public string WitPath { get; set; } = "";
    public OutputFormat Format { get; set; } = OutputFormat.ISO;
    public bool SplitWbfs { get; set; } = true;
}

public sealed class PreferencesStore
{
    public static string DataDirectory => PlatformPaths.Directories.Preferences;
    public string DirectoryPath { get; }
    public PreferencesStore(string? directory = null) => DirectoryPath = directory ?? DataDirectory;
    public Preferences Load(out string? warning)
    {
        warning = null;
        string path = Path.Combine(DirectoryPath, "preferences.json");
        try
        {
            if (!File.Exists(path)) return new();
            var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path));
            if (prefs is null || string.IsNullOrWhiteSpace(prefs.Destination) || string.IsNullOrWhiteSpace(prefs.TemporaryDirectory)
                || prefs.DolphinPath is null || prefs.WitPath is null || !Enum.IsDefined(prefs.Format))
                throw new JsonException("Missing or invalid preference values.");
            return prefs;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = $"Preferences could not be read. Defaults are being used. {ex.Message}";
            return new();
        }
    }

    public void Save(Preferences preferences)
    {
        Directory.CreateDirectory(DirectoryPath);
        string path = Path.Combine(DirectoryPath, "preferences.json");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class SessionLog : IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter writer;
    public string FilePath { get; }
    public SessionLog(string? directory = null)
    {
        directory ??= PlatformPaths.Directories.Logs;
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, $"rvz-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        writer = new StreamWriter(new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    }
    public void Write(string message)
    {
        lock (gate) writer.WriteLine($"{DateTimeOffset.Now:O} {message}");
    }
    public void Dispose() { lock (gate) writer.Dispose(); }
}
