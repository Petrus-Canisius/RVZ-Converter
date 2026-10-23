using System.Formats.Tar;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using RvzConverter.Core;

if (args.Length is not (3 or 4)) throw new ArgumentException("Usage: pack <publish-directory> <rid> <package-directory> [windows-launcher]");
string source = Path.GetFullPath(args[0]), rid = args[1], output = Path.GetFullPath(args[2]);
Directory.CreateDirectory(output);
var platform = rid.StartsWith("win-") ? DesktopPlatform.Windows : rid.StartsWith("osx-") ? DesktopPlatform.MacOS : DesktopPlatform.Linux;
var arch = rid.EndsWith("arm64") ? Architecture.Arm64 : Architecture.X64;
string executable = platform == DesktopPlatform.Windows ? "RvzConverter.exe" : "RvzConverter";
if (ExecutableCompatibility.Problem(Path.Combine(source, executable), platform, arch, false) is { } problem) throw new Exception(problem);
if (platform == DesktopPlatform.Windows)
{
    var metadata = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(source, executable));
    if (metadata.ProductName != "RVZ Converter" || metadata.CompanyName != "Petrus-Canisius" || metadata.FileVersion?.StartsWith("2.0.1", StringComparison.Ordinal) != true) throw new Exception("Executable publication metadata is incorrect.");
}
if (Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Any(f => Path.GetFileName(f).ToLowerInvariant() is "wit" or "wit.exe" or "dolphin-tool" or "dolphintool.exe" or "cygwin1.dll")) throw new Exception("Third-party engine found in package.");
foreach (var file in Directory.EnumerateFiles(source).Where(f => f.EndsWith(".so") || f.EndsWith(".dylib")))
    if (ExecutableCompatibility.Problem(file, platform, arch, false) is { } error) throw new Exception($"{file}: {error}");
string name = "RVZConverter-" + rid;
string archive = Path.Combine(output, name + (platform == DesktopPlatform.Linux ? ".tar.gz" : ".zip"));
if (File.Exists(archive)) File.Move(archive, archive + ".previous-" + Guid.NewGuid().ToString("N"), false);
if (platform == DesktopPlatform.Windows)
{
    if (args.Length != 4) throw new ArgumentException("Windows packages require a native launcher executable.");
    string launcher = Path.GetFullPath(args[3]);
    if (ExecutableCompatibility.Problem(launcher, platform, arch, false) is { } launcherProblem) throw new Exception($"Launcher architecture mismatch: {launcherProblem}");
    var launcherMetadata = System.Diagnostics.FileVersionInfo.GetVersionInfo(launcher);
    if (launcherMetadata.ProductName != "RVZ Converter" || launcherMetadata.CompanyName != "Petrus-Canisius" || launcherMetadata.FileVersion?.StartsWith("2.0.1", StringComparison.Ordinal) != true) throw new Exception("Launcher publication metadata is incorrect.");
    string layout = Path.Combine(output, ".windows-layout-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(layout);
    try
    {
        File.Copy(launcher, Path.Combine(layout, "RVZConverter.exe"), false);
        File.WriteAllText(Path.Combine(layout, "README.txt"), "RVZ Converter\r\n\r\nOpen RVZConverter.exe to start.\r\n\r\nExtract the complete archive before running it. Keep the app directory beside RVZConverter.exe.\r\n\r\nInstall DolphinTool and Wiimms ISO Tools (WIT) separately. Keep each complete engine distribution and its dependencies under tools\\dolphin\\ or tools\\wit\\ beside the launcher. The application also lets you locate installed engine executables.\r\n");
        string appDirectory = Path.Combine(layout, "app");
        foreach (string input in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, input);
            string normalized = relative.Replace('\\', '/');
            if (normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("licenses/", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("README.md", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("THIRD-PARTY-NOTICES.md", StringComparison.OrdinalIgnoreCase)) continue;
            string destination = Path.Combine(appDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(input, destination, false);
        }
        string docs = Path.Combine(source, "docs");
        if (!Directory.Exists(docs)) throw new Exception("Published detailed documentation is missing.");
        CopyDirectory(docs, Path.Combine(layout, "docs"));
        string licenses = Path.Combine(source, "licenses");
        if (!Directory.Exists(licenses)) throw new Exception("Published license notices are missing.");
        CopyDirectory(licenses, Path.Combine(layout, "licenses"));
        string thirdParty = Path.Combine(source, "THIRD-PARTY-NOTICES.md");
        if (!File.Exists(thirdParty)) throw new Exception("Third-party notices are missing.");
        File.Copy(thirdParty, Path.Combine(layout, "licenses", "THIRD-PARTY-NOTICES.md"), false);
        Zip(layout, archive, "");
    }
    finally
    {
        string full = Path.GetFullPath(layout);
        string boundary = Path.GetFullPath(output) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith(".windows-layout-", StringComparison.Ordinal)) throw new Exception("Refusing to clean an unowned package layout.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}
else if (platform == DesktopPlatform.MacOS)
{
    string bundle = Path.Combine(output, name + ".app");
    if (Directory.Exists(bundle)) Directory.Move(bundle, bundle + ".previous-" + Guid.NewGuid().ToString("N"));
    string payload = Path.Combine(bundle, "Contents", "MacOS"); Directory.CreateDirectory(payload);
    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) { string dest = Path.Combine(payload, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(file, dest, false); }
    Directory.CreateDirectory(Path.Combine(bundle, "Contents", "Resources"));
    // Preserve the original PNG icon embedded in the ICO, in a native ICNS wrapper.
    byte[] ico = File.ReadAllBytes(Path.Combine(source, "App.ico"));
    int count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4));
    int iconEntry = Enumerable.Range(0, count).OrderByDescending(i => ico[6 + i * 16] == 0 ? 256 : ico[6 + i * 16]).First() * 16 + 6;
    int iconLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(iconEntry + 8));
    int iconOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(iconEntry + 12));
    byte[] icns = new byte[iconLength + 16]; "icns"u8.CopyTo(icns); BinaryPrimitives.WriteInt32BigEndian(icns.AsSpan(4), icns.Length);
    "ic08"u8.CopyTo(icns.AsSpan(8)); BinaryPrimitives.WriteInt32BigEndian(icns.AsSpan(12), iconLength + 8); ico.AsSpan(iconOffset, iconLength).CopyTo(icns.AsSpan(16));
    File.WriteAllBytes(Path.Combine(bundle, "Contents", "Resources", "App.icns"), icns);
    File.WriteAllText(Path.Combine(bundle, "Contents", "Info.plist"), """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.petrus-canisius.rvzconverter</string>
<key>CFBundleName</key><string>RVZ Converter</string>
<key>CFBundleExecutable</key><string>RvzConverter</string>
<key>CFBundleIconFile</key><string>App.icns</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>2.0.1</string>
<key>CFBundleVersion</key><string>2.0.1</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSHumanReadableCopyright</key><string>Created by Petrus-Canisius</string>
</dict></plist>
""");
    File.WriteAllText(Path.Combine(payload, "UNSIGNED.txt"), "Unsigned, not notarized. Cross-published; native macOS launch NOT RUN on the Windows build machine. Gatekeeper may block this bundle. See docs/PLATFORMS.md.\n");
    Zip(bundle, archive, name + ".app");
}
else
{
    using var file = new FileStream(archive, FileMode.CreateNew); using var gzip = new GZipStream(file, CompressionLevel.Optimal); using var tar = new TarWriter(gzip, TarEntryFormat.Pax);
    foreach (string input in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Order())
    {
        using var data = File.OpenRead(input); string relative = Path.GetRelativePath(source, input).Replace('\\', '/');
        var entry = new PaxTarEntry(TarEntryType.RegularFile, "RVZConverter/" + relative) { DataStream = data, Mode = Mode(relative) }; tar.WriteEntry(entry);
    }
}
using (var file = File.OpenRead(archive)) File.WriteAllText(archive + ".sha256", Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant() + "  " + Path.GetFileName(archive) + "\n");
Console.WriteLine("Package created and architecture checked: " + archive);
static UnixFileMode Mode(string relative) => relative.EndsWith("/RvzConverter", StringComparison.Ordinal) || relative == "RvzConverter" || relative.EndsWith("createdump", StringComparison.Ordinal) ? (UnixFileMode)Convert.ToInt32("755", 8) : (UnixFileMode)Convert.ToInt32("644", 8);
static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (string input in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        string output = Path.Combine(destination, Path.GetRelativePath(source, input));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.Copy(input, output, false);
    }
}
static void Zip(string directory, string archivePath, string prefix)
{
    using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
    {
    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order())
    {
        string relative = Path.GetRelativePath(directory, file).Replace('\\', '/'); var entry = zip.CreateEntry(string.IsNullOrEmpty(prefix) ? relative : prefix + "/" + relative, CompressionLevel.Optimal);
        entry.ExternalAttributes = ((int)Mode(relative) | 0x8000) << 16;
        using var input = File.OpenRead(file); using var stream = entry.Open(); input.CopyTo(stream);
    }
    }
    // ZipArchive otherwise records Windows as the creator on a Windows build host;
    // mark Unix mode fields explicitly so native macOS extraction retains execute bits.
    using var archiveFile = new FileStream(archivePath, FileMode.Open, FileAccess.ReadWrite);
    byte[] tail = new byte[(int)Math.Min(65557, archiveFile.Length)]; archiveFile.Position = archiveFile.Length - tail.Length; archiveFile.ReadExactly(tail);
    int end = tail.Length - 22;
    while (end >= 0 && BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end)) != 0x06054b50) end--;
    if (end < 0) throw new InvalidDataException("ZIP directory record missing.");
    long central = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 16));
    int entries = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end + 10));
    for (int i = 0; i < entries; i++)
    {
        byte[] header = new byte[46]; archiveFile.Position = central; archiveFile.ReadExactly(header);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 0x02014b50) throw new InvalidDataException("Invalid ZIP central directory.");
        archiveFile.Position = central + 5; archiveFile.WriteByte(3);
        central += 46 + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28)) + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30)) + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(32));
    }
}
