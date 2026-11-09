using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Pdm;

// The page's tray icon (Grant, 9 Nov, 0.1.12), in place of the console window: Open PDM page, Update when one has
// downloaded, Open log, Exit. What the console used to show goes to a log file (with the time), so an error or a crash
// can still be seen. Run from a terminal, the output shows there too and the terminal stays.
public static class Tray
{
    public static string LogFile { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDM-logs", "pdm-page.log");
    static NotifyIcon? icon;

    [DllImport("kernel32.dll")] static extern bool FreeConsole();
    [DllImport("kernel32.dll")] static extern uint GetConsoleProcessList(uint[] list, uint count);

    // Output to the log file; the console window closes when it's this app's own (started from the Start menu).
    public static void LogToFile()
    {
        TextWriter? console = Console.Out;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 5_000_000) File.Move(LogFile, LogFile + ".old", overwrite: true);
            var file = new StreamWriter(new FileStream(LogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
            if (GetConsoleProcessList(new uint[2], 2) == 1) { FreeConsole(); console = null; }
            var log = new LogWriter(file, console);
            Console.SetOut(log); Console.SetError(log);
        }
        catch (IOException) { } // no log file: the console stays as it was
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.WriteLine($"PDM stopped on an error: {e.ExceptionObject}");
            try { MessageBox.Show($"PDM stopped on an error:\n\n{(e.ExceptionObject as Exception)?.Message}\n\nDetails are in {LogFile}", "PDM", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        };
    }

    // The icon and its menu, on a thread of their own. busy: something is running (an update or exit asks first).
    public static void Start(string url, Func<string?> updateReady, Func<bool> busy, Action applyUpdate)
    {
        var t = new Thread(() =>
        {
            Application.EnableVisualStyles();
            icon = new NotifyIcon { Icon = AppIcon(), Text = "PDM", Visible = true };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open PDM page", null, (_, _) => Open(url));
            var update = menu.Items.Add("Update", null, (_, _) =>
            {
                if (busy()) { icon.ShowBalloonTip(5000, "PDM", "PDM is busy (a check-in, check-out or download). Update when it has finished.", ToolTipIcon.Info); return; }
                Hide(); applyUpdate();
            });
            menu.Items.Add("Open log", null, (_, _) => { try { Process.Start(new ProcessStartInfo(LogFile) { UseShellExecute = true }); } catch { } });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) =>
            {
                if (busy() && MessageBox.Show("PDM is in the middle of a check-in, check-out or download. Exit anyway?", "PDM",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                Console.WriteLine("Exit from the tray.");
                Hide(); Environment.Exit(0);
            });
            menu.Opening += (_, _) => { var v = updateReady(); update.Visible = v != null; update.Text = $"Update to {v} (restarts PDM)"; };
            icon.ContextMenuStrip = menu;
            icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Open(url); };
            Application.Run();
        }) { IsBackground = true, Name = "tray" };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    // Takes the icon away before the app exits (an update restart), so no dead icon is left behind.
    public static void Hide() { try { if (icon != null) icon.Visible = false; } catch { } }

    public static void Open(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } }

    static Icon AppIcon()
    {
        try { if (Environment.ProcessPath is { } p && Icon.ExtractAssociatedIcon(p) is { } i) return i; } catch { }
        return SystemIcons.Application;
    }

    // Each line gets the time; written to the log file and, when kept, the console.
    sealed class LogWriter(StreamWriter file, TextWriter? console) : TextWriter
    {
        readonly StringBuilder line = new();
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char c)
        {
            if (c == '\r') return;
            if (c != '\n') { line.Append(c); return; }
            var text = line.ToString(); line.Clear();
            try { file.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {text}"); } catch (IOException) { }
            console?.WriteLine(text);
        }
    }
}
