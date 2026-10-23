using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RvzConverter.Core;

internal static class ProcessTree
{
    public static List<Process> CaptureDescendants(int parent)
    {
        var result = new List<Process>();
        if (!OperatingSystem.IsWindows()) return CaptureUnix(parent);
        nint snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == -1) throw new IOException("Cannot enumerate the engine process tree.");
        try
        {
            var links = new List<(int Id, int Parent)>();
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (Process32First(snapshot, ref entry))
            {
                do { links.Add(((int)entry.ProcessId, (int)entry.ParentProcessId)); }
                while (Process32Next(snapshot, ref entry));
            }
            var ids = new HashSet<int> { parent };
            bool found;
            do
            {
                found = false;
                foreach (var link in links)
                    if (ids.Contains(link.Parent) && ids.Add(link.Id))
                    {
                        found = true;
                        try { var process = Process.GetProcessById(link.Id); _ = process.Handle; result.Add(process); }
                        catch (ArgumentException) { }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception ex) { foreach (var captured in result) captured.Dispose(); throw new IOException("Cannot retain a descendant process handle. Task files must be preserved.", ex); }
                    }
            } while (found);
        }
        finally { CloseHandle(snapshot); }
        return result;
    }

    private static List<Process> CaptureUnix(int parent)
    {
        var start = new ProcessStartInfo("/bin/ps") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("-axo"); start.ArgumentList.Add("pid=,ppid=");
        using var ps = Process.Start(start) ?? throw new IOException("Cannot start process-tree inspection.");
        var stdout = ps.StandardOutput.ReadToEndAsync(); var stderr = ps.StandardError.ReadToEndAsync();
        if (!ps.WaitForExit(5000)) { ps.Kill(true); ps.WaitForExit(); throw new IOException("Process-tree inspection timed out."); }
        Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
        if (ps.ExitCode != 0) throw new IOException("Process-tree inspection failed.");
        var links = stdout.Result.Split('\n').Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length == 2).Select(fields => (Id: int.Parse(fields[0]), Parent: int.Parse(fields[1]))).ToArray();
        var ids = new HashSet<int> { parent }; var result = new List<Process>(); bool changed;
        do
        {
            changed = false;
            foreach (var link in links)
                if (ids.Contains(link.Parent) && ids.Add(link.Id))
                {
                    changed = true;
                    try { var child = Process.GetProcessById(link.Id); _ = child.Handle; result.Add(child); }
                    catch (ArgumentException) { }
                    catch (InvalidOperationException) { }
                }
        } while (changed);
        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public nuint DefaultHeapId;
        public uint ModuleId, Threads, ParentProcessId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string? ExeFile;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32First(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32Next(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
