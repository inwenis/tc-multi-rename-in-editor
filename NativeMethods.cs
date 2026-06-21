using System.Runtime.InteropServices;

namespace TcBatchRename;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(
        uint idAttach,
        uint idAttachTo,
        [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>
    /// Forces <paramref name="hWnd"/> to the foreground with input focus.
    /// Windows blocks a background process from stealing focus from the active
    /// application, so <see cref="System.Windows.Forms.Form.Activate"/> alone does
    /// not give a freshly shown dialog keyboard focus when another app (e.g. the
    /// editor) owns the foreground. Attaching our thread's input queue to the
    /// foreground window's thread lifts that restriction for the duration of the
    /// SetForegroundWindow call.
    /// </summary>
    public static void ForceForegroundWindow(IntPtr hWnd)
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == hWnd)
        {
            return;
        }

        uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        uint currentThread = GetCurrentThreadId();
        bool attached = false;

        if (foregroundThread != 0 && foregroundThread != currentThread)
        {
            attached = AttachThreadInput(foregroundThread, currentThread, true);
        }

        try
        {
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(foregroundThread, currentThread, false);
            }
        }
    }
}
