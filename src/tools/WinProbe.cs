using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

// Diagnostic + mitigation for the CMO start-up error dialog.
//
//   probe            list every top-level window: hwnd, pid, class, title, visibility
//   probe <pid>      same, filtered to one process
//   close <pid>      click the default button of any dialog owned by <pid> and close it
//
// The localizer's auto-dismiss matched on the English window title, which the
// localizer itself translates to Chinese, so the match broke as soon as the
// dictionary landed.  This tool matches on window class instead and is used to
// determine the real class/PID before fixing the engine.
class WinProbe
{
    delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    const uint BM_CLICK = 0x00F5;
    const uint WM_CLOSE = 0x0010;

    static uint filter = 0;
    static bool closeMode = false;
    static int closed = 0;

    static bool Callback(IntPtr hWnd, IntPtr lParam)
    {
        uint pid;
        GetWindowThreadProcessId(hWnd, out pid);
        if (filter != 0 && pid != filter) return true;

        var cls = new StringBuilder(256);
        GetClassName(hWnd, cls, cls.Capacity);
        var txt = new StringBuilder(512);
        GetWindowText(hWnd, txt, txt.Capacity);
        bool vis = IsWindowVisible(hWnd);

        if (closeMode)
        {
            // A dialog is a "#32770" window; click its buttons then close it.
            if (cls.ToString() == "#32770")
            {
                EnumChildWindows(hWnd, delegate(IntPtr c, IntPtr p)
                {
                    var bt = new StringBuilder(128);
                    GetWindowText(c, bt, bt.Capacity);
                    string s = bt.ToString();
                    if (s.Length > 0)
                    {
                        Console.WriteLine("    button: " + s);
                        SendMessage(c, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    }
                    return true;
                }, IntPtr.Zero);
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                Console.WriteLine("  closed dialog: " + txt);
                closed++;
            }
            return true;
        }

        Console.WriteLine(string.Format("{0,-12} pid={1,-7} vis={2,-6} class={3,-34} title={4}",
            hWnd.ToString(), pid, vis, cls.ToString(), txt.ToString()));
        return true;
    }

    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "probe";
        if (args.Length > 1) filter = uint.Parse(args[1]);
        closeMode = mode == "close";
        EnumWindows(Callback, IntPtr.Zero);
        if (closeMode) Console.WriteLine("dialogs closed: " + closed);
        return 0;
    }
}
