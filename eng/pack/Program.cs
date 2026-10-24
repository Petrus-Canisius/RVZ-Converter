using System.Buffers.Binary;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using RvzConverter.Core;
using Microsoft.NET.HostModel.AppHost;

if (args.Length != 5) throw new ArgumentException("Usage: pack <app-directory> <rid> <output> <windows-launcher-or-none> <engine-directory>");
string source = Path.GetFullPath(args[0]), rid = args[1], output = Path.GetFullPath(args[2]), engines = Path.GetFullPath(args[4]);
var platform = rid.StartsWith("win-") ? DesktopPlatform.Windows : rid.StartsWith("osx-") ? DesktopPlatform.MacOS : DesktopPlatform.Linux;
var arch = rid.EndsWith("arm64") ? Architecture.Arm64 : Architecture.X64;
string version = typeof(EngineService).Assembly.GetName().Version!.ToString(3);
string appExecutable = platform == DesktopPlatform.Windows ? "RvzConverter.exe" : "RvzConverter";
Validate(Path.Combine(source, appExecutable));
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(engines, "tools", "engines.json")));
if (manifest.RootElement.GetProperty("runtime").GetString() != rid) throw new Exception("Engine distribution target mismatch.");
bool witAvailable = manifest.RootElement.GetProperty("wit").GetProperty("available").GetBoolean();
if (witAvailable && (!manifest.RootElement.GetProperty("wit").TryGetProperty("sha1Provider", out var sha1Provider) ||
    !sha1Provider.GetString()!.StartsWith("Mbed TLS 2.28.0", StringComparison.Ordinal)))
    throw new Exception("The WIT distribution must be rebuilt with the reviewed Mbed TLS SHA-1 provider.");
Validate(Path.Combine(engines, "tools", "dolphin", platform == DesktopPlatform.Windows ? "DolphinTool.exe" : "dolphin-tool"));
if (witAvailable) Validate(Path.Combine(engines, "tools", "wit", platform == DesktopPlatform.Windows ? "wit.exe" : "wit"));
foreach (string file in Directory.EnumerateFiles(Path.Combine(engines, "tools"), "*", SearchOption.AllDirectories)
    .Where(f => f.EndsWith(".dll") || f.EndsWith(".so") || f.EndsWith(".dylib"))) Validate(file);
string archive = Path.Combine(output, "RVZConverter-" + rid + (platform == DesktopPlatform.Linux ? ".tar.gz" : ".zip"));
Directory.CreateDirectory(output);
if (File.Exists(archive)) File.Move(archive, archive + ".previous-" + Guid.NewGuid().ToString("N"));
string layout = Path.Combine(output, ".layout-" + rid + "-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(layout);
try
{
    string app, resources;
    string launch = platform switch { DesktopPlatform.Windows => "RVZConverter.exe", DesktopPlatform.MacOS => "RVZ Converter.app", _ => "RVZConverter" };
    if (platform == DesktopPlatform.MacOS)
    {
        string bundle = Path.Combine(layout, launch);
        resources = Path.Combine(bundle, "Contents", "Resources");
        app = Path.Combine(resources, "app");
        Directory.CreateDirectory(resources);
        string macEntry = Path.Combine(bundle, "Contents", "MacOS", "RvzConverter");
        Directory.CreateDirectory(Path.GetDirectoryName(macEntry)!);
        using var runtime = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "RvzConverter.runtimeconfig.json")));
        string runtimeVersion = runtime.RootElement.GetProperty("runtimeOptions").GetProperty("includedFrameworks")[0].GetProperty("version").GetString()!;
        string nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        string hostRelative = Path.Combine(runtimeVersion, "runtimes", rid, "native", "apphost");
        // Native SDK installs supply their own architecture's host under packs.
        // Cross-target restores supply it through the NuGet package cache.
        string sdkRoot = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        string[] templates = [Path.Combine(sdkRoot, "packs", "Microsoft.NETCore.App.Host." + rid, hostRelative),
            Path.Combine(nuget, "microsoft.netcore.app.host." + rid, hostRelative)];
        string template = templates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("The matching .NET apphost template is missing from both the SDK and NuGet cache.");
        Validate(template);
        HostWriter.CreateAppHost(template, macEntry, "../Resources/app/RvzConverter.dll", enableMacOSCodeSign: false);
        MakeMacIcon(Path.Combine(source, "App.ico"), Path.Combine(resources, "App.icns"));
        File.WriteAllText(Path.Combine(bundle, "Contents", "Info.plist"), $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.petrus-canisius.rvzconverter</string>
<key>CFBundleName</key><string>RVZ Converter</string>
<key>CFBundleExecutable</key><string>RvzConverter</string>
<key>CFBundleIconFile</key><string>App.icns</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>{version}</string>
<key>CFBundleVersion</key><string>{version}</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSHumanReadableCopyright</key><string>Created by Petrus-Canisius</string>
</dict></plist>
""");
    }
    else { app = Path.Combine(layout, "app"); resources = layout; }
    Directory.CreateDirectory(app);
    foreach (string input in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(source, input), normalized = relative.Replace('\\', '/');
        if (normalized.StartsWith("docs/") || normalized.StartsWith("licenses/") || normalized is "README.md" or "THIRD-PARTY-NOTICES.md") continue;
        Copy(input, Path.Combine(app, relative));
    }
    CopyDirectory(Path.Combine(source, "docs"), Path.Combine(resources, "docs"));
    CopyDirectory(Path.Combine(source, "licenses"), Path.Combine(resources, "licenses"));
    CopyDirectory(Path.Combine(engines, "licenses"), Path.Combine(resources, "licenses"));
    CopyDirectory(Path.Combine(engines, "tools"), Path.Combine(resources, "tools"));
    Copy(Path.Combine(source, "THIRD-PARTY-NOTICES.md"), Path.Combine(resources, "licenses", "THIRD-PARTY-NOTICES.md"));
    string engineText = witAvailable ? "Includes native DolphinTool 2506 and WIT 3.05a+rvz1." : "Includes native DolphinTool 2506. Native WIT is unavailable on Windows ARM64. CISO and WBFS output are unavailable in this package.";
    File.WriteAllText(Path.Combine(layout, "README.txt"), $"RVZ Converter {version}\nCreated by Petrus-Canisius\n\nExtract the complete archive before running it. Open {launch} to start.\nKeep all extracted directories together. Do not run from inside an archive.\n\n{engineText}\nThe engines are independent projects. Their licenses and notices are included.\nCorresponding source is distributed as the EngineSources-{rid}.tar.gz release asset.\nThe application includes .NET. No separate .NET installation is required.\n\nSee the included docs for system requirements and optional external engine recovery.\n" + (platform == DesktopPlatform.MacOS ? "No Developer ID signature. Not notarized. Ad-hoc signatures only. Gatekeeper may block this app. See docs/PLATFORMS.md inside the app bundle.\n" : ""));
    if (platform == DesktopPlatform.Windows)
    {
        Validate(args[3]);
        var metadata = FileVersionInfo.GetVersionInfo(args[3]);
        if (metadata.CompanyName != "Petrus-Canisius" || metadata.ProductName != "RVZ Converter" || !metadata.FileVersion!.StartsWith(version)) throw new Exception("Launcher metadata mismatch.");
        Copy(args[3], Path.Combine(layout, launch));
    }
    else if (platform == DesktopPlatform.Linux)
    {
        File.WriteAllText(Path.Combine(layout, launch), "#!/bin/sh\nset -eu\npackage_root=$(CDPATH= cd -- \"$(dirname -- \"$0\")\" && pwd)\nexec \"$package_root/app/RvzConverter\" \"$@\"\n");
    }
    else if (OperatingSystem.IsMacOS())
    {
        foreach (string file in Directory.EnumerateFiles(layout, "*", SearchOption.AllDirectories).Where(IsMacNative))
            Run("codesign", ["--force", "--sign", "-", file]);
        Run("codesign", ["--force", "--sign", "-", Path.Combine(layout, launch)]);
        Run("codesign", ["--verify", "--deep", "--strict", Path.Combine(layout, launch)]);
    }
    if (platform == DesktopPlatform.Linux)
    {
        using var stream = File.Create(archive); using var gzip = new GZipStream(stream, CompressionLevel.Optimal); using var tar = new TarWriter(gzip, TarEntryFormat.Pax);
        foreach (string file in Directory.EnumerateFiles(layout, "*", SearchOption.AllDirectories).Order())
        {
            string relative = Path.GetRelativePath(layout, file).Replace('\\', '/'); using var data = File.OpenRead(file);
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, relative) { DataStream = data, Mode = Mode(relative) });
        }
    }
    else Zip(layout, archive);
}
finally
{
    if (Path.GetFullPath(layout).StartsWith(Path.GetFullPath(output) + Path.DirectorySeparatorChar) && Path.GetFileName(layout).StartsWith(".layout-")) Directory.Delete(layout, true);
}
using (var stream = File.OpenRead(archive)) File.WriteAllText(archive + ".sha256", Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() + "  " + Path.GetFileName(archive) + "\n");
Console.WriteLine("Package and native engine architectures verified: " + archive);
void Validate(string path) { if (ExecutableCompatibility.Problem(path, platform, arch, false) is { } problem) throw new Exception($"{Path.GetFileName(path)}: {problem}"); }
static void Copy(string from, string to) { Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(from, to, false); }
static void CopyDirectory(string from, string to) { foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories)) Copy(file, Path.Combine(to, Path.GetRelativePath(from, file))); }
static UnixFileMode Mode(string path) => Path.GetFileName(path) is "RVZConverter" or "RvzConverter" or "dolphin-tool" or "wit" or "createdump" || path.EndsWith(".sh") || path.EndsWith(".so") || path.EndsWith(".dylib") ? (UnixFileMode)493 : (UnixFileMode)420;
static bool IsMacNative(string file) { using var f = File.OpenRead(file); Span<byte> magic = stackalloc byte[4]; return f.Read(magic) == 4 && BinaryPrimitives.ReadUInt32BigEndian(magic) is 0xcffaedfe or 0xfeedfacf or 0xcafebabe or 0xcafebabf; }
static void Run(string executable, string[] arguments) { var start = new ProcessStartInfo(executable) { UseShellExecute = false }; foreach (string argument in arguments) start.ArgumentList.Add(argument); using var process = Process.Start(start)!; process.WaitForExit(); if (process.ExitCode != 0) throw new Exception($"{executable} failed: {process.ExitCode}"); }
static void MakeMacIcon(string source, string destination)
{
    byte[] ico = File.ReadAllBytes(source); int count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4));
    int entry = Enumerable.Range(0, count).OrderByDescending(i => ico[6 + i * 16] == 0 ? 256 : ico[6 + i * 16]).First() * 16 + 6;
    int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(entry + 8)), offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(entry + 12));
    byte[] icns = new byte[length + 16]; "icns"u8.CopyTo(icns); BinaryPrimitives.WriteInt32BigEndian(icns.AsSpan(4), icns.Length); "ic08"u8.CopyTo(icns.AsSpan(8)); BinaryPrimitives.WriteInt32BigEndian(icns.AsSpan(12), length + 8); ico.AsSpan(offset, length).CopyTo(icns.AsSpan(16)); File.WriteAllBytes(destination, icns);
}
static void Zip(string source, string destination)
{
    using (var zip = ZipFile.Open(destination, ZipArchiveMode.Create))
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Order())
        {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/'); var entry = zip.CreateEntry(relative, CompressionLevel.Optimal); entry.ExternalAttributes = ((int)Mode(relative) | 0x8000) << 16; using var input = File.OpenRead(file); using var target = entry.Open(); input.CopyTo(target);
        }
    using var archive = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite);
    byte[] tail = new byte[(int)Math.Min(65557, archive.Length)]; archive.Position = archive.Length - tail.Length; archive.ReadExactly(tail); int end = tail.Length - 22;
    while (end >= 0 && BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end)) != 0x06054b50) end--;
    if (end < 0) throw new Exception("ZIP directory missing.");
    long central = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 16)); int entries = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end + 10));
    for (int i = 0; i < entries; i++) { byte[] header = new byte[46]; archive.Position = central; archive.ReadExactly(header); archive.Position = central + 5; archive.WriteByte(3); central += 46 + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28)) + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30)) + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(32)); }
}
