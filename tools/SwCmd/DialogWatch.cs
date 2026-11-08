// DialogWatch: while SwCmd or SwCheck waits on SOLIDWORKS (opening a file), a background thread looks at SOLIDWORKS's
// dialog boxes. A dialog about a missing linked equations file (its title or text mentions "equation" or ".txt") is
// cancelled, as Grant did by hand (7 Nov, 0804 A): SOLIDWORKS then keeps the equation values it has, and nothing is
// written. A Save As box (SOLIDWORKS asking where to save a read-only referenced file during a save) is cancelled and
// reported. Every other dialog is left alone and reported once, so a caller sees why SOLIDWORKS is waiting.
// Shared by tools\SwCmd and tools\SwCheck (compiled into both). C# 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

static class DialogWatch
{
    delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, StringBuilder l);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    const uint WM_GETTEXT = 0x000D, WM_COMMAND = 0x0111, BM_CLICK = 0x00F5;
    const int IDCANCEL = 2;

    static Thread thread;
    static volatile bool running;
    static readonly object gate = new object();
    static readonly List<string> events = new List<string>();     // what was cancelled or seen, for the caller's report
    static readonly HashSet<IntPtr> reported = new HashSet<IntPtr>();
    static readonly Dictionary<IntPtr, DateTime> firstSeen = new Dictionary<IntPtr, DateTime>();

    public static void Start()
    {
        if (running) return;
        running = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "DialogWatch" };
        thread.Start();
    }

    // Stops watching and returns what happened since Start (cancelled dialogs first marked "CANCELLED").
    public static List<string> Stop()
    {
        running = false;
        if (thread != null) thread.Join(2000);
        thread = null;
        lock (gate) { var r = events.ToList(); events.Clear(); reported.Clear(); firstSeen.Clear(); return r; }
    }

    static string Text(IntPtr h)
    {
        var sb = new StringBuilder(1024);
        SendMessage(h, WM_GETTEXT, (IntPtr)sb.Capacity, sb);
        return sb.ToString();
    }

    static string Class(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, sb.Capacity); return sb.ToString(); }

    static void Loop()
    {
        while (running)
        {
            try { Scan(); } catch { }
            Thread.Sleep(400);
        }
    }

    static void Scan()
    {
        var pids = new HashSet<uint>(Process.GetProcessesByName("SLDWORKS").Select(p => (uint)p.Id));
        if (pids.Count == 0) return;
        var dialogs = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pids.Contains(pid) && IsWindowVisible(h) && Class(h) == "#32770") dialogs.Add(h);
            return true;
        }, IntPtr.Zero);
        foreach (var d in dialogs)
        {
            string title = Text(d);
            var texts = new List<string>(); IntPtr cancel = IntPtr.Zero;
            EnumChildWindows(d, (c, l) =>
            {
                string t = Text(c).Trim();
                if (t != "") texts.Add(t);
                string k = Class(c);
                if (k == "Button" && t.Replace("&", "").Equals("Cancel", StringComparison.OrdinalIgnoreCase)) cancel = c;
                return true;
            }, IntPtr.Zero);
            string all = (title + " | " + string.Join(" | ", texts)).Replace("\r", " ").Replace("\n", " ");
            if (all.Length > 400) all = all.Substring(0, 400) + "...";
            bool equations = all.IndexOf("equation", StringComparison.OrdinalIgnoreCase) >= 0 || all.IndexOf(".txt", StringComparison.OrdinalIgnoreCase) >= 0;
            // A Save As box during a save: SOLIDWORKS wants to save a referenced file it can't write (read-only, i.e. not
            // checked out). Cancelled, so nothing is written there; the caller treats it as a problem (rehearsal, 8 Nov:
            // form-sub's SaveVirtualComponent asked this for ~90 read-only steel and fastener files, one at a time).
            bool saveAs = title.Replace("&", "").Trim().Equals("Save As", StringComparison.OrdinalIgnoreCase);
            lock (gate)
            {
                if (saveAs)
                {
                    if (reported.Contains(d)) continue;
                    string file = "";
                    EnumChildWindows(d, (c, l) => { if (Class(c) == "Edit") { string t = Text(c).Trim(); if (t != "") { file = t; return false; } } return true; }, IntPtr.Zero);
                    if (cancel != IntPtr.Zero) PostMessage(cancel, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    else PostMessage(d, WM_COMMAND, (IntPtr)IDCANCEL, IntPtr.Zero);
                    reported.Add(d);
                    string e = "CANCELLED a SOLIDWORKS Save As for " + (file == "" ? "(no file name shown)" : file) + " (a referenced file it could not write; nothing written there)";
                    events.Add(e); Console.Error.WriteLine(e);
                }
                else if (equations)
                {
                    if (reported.Contains(d)) continue;
                    if (cancel != IntPtr.Zero) PostMessage(cancel, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    else PostMessage(d, WM_COMMAND, (IntPtr)IDCANCEL, IntPtr.Zero);
                    reported.Add(d);
                    string e = "CANCELLED a SOLIDWORKS dialog about a linked equations file: " + all;
                    events.Add(e); Console.Error.WriteLine(e);
                }
                else
                {
                    DateTime t0;
                    if (!firstSeen.TryGetValue(d, out t0)) { firstSeen[d] = DateTime.Now; continue; }
                    if (reported.Contains(d) || (DateTime.Now - t0).TotalSeconds < 10) continue;
                    reported.Add(d);
                    string e = "SOLIDWORKS is waiting on a dialog (left alone; answer it in SOLIDWORKS): " + all;
                    events.Add(e); Console.Error.WriteLine(e);
                }
            }
        }
    }
}
