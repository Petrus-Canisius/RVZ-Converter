using System.Runtime.InteropServices;
using System.Text;

namespace RvzConverter.Core;

/// <summary>Reads owned Windows error dialogs. Never clicks or accepts them.</summary>
internal static class EngineDialogWatcher
{
    public static string? ReadErrorDialog(int processId)
    {
        if (!OperatingSystem.IsWindows()) return null;
        string? result = null;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != (uint)processId || !IsWindowVisible(window)) return true;
            var className = new StringBuilder(256); GetClassName(window, className, className.Capacity);
            if (className.ToString() != "#32770") return true;
            var text = new List<string> { Text(window) };
            EnumChildWindows(window, (child, _) => { string value = Text(child); if (value.Length > 0) text.Add(value); return true; }, IntPtr.Zero);
            result = string.Join(" | ", text).Replace('\r', ' ').Replace('\n', ' ');
            return false;
        }, IntPtr.Zero);
        return result;
    }
    private static string Text(IntPtr window) { var text = new StringBuilder(4096); GetWindowText(window, text, text.Capacity); return text.ToString(); }
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
}
