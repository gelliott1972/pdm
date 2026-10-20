// DmToolbox: show or clear the Toolbox flag on SOLIDWORKS parts using the Document Manager.
// Phase 0, Test 2. Clearing makes SOLIDWORKS treat the file as an ordinary part, so it is
// resolved like any other reference instead of through the Toolbox folder.
// Usage: DmToolbox.exe show|clear <file> [<file> ...]
// Refuses any path inside a Dropbox folder, so live project files can't be changed by mistake.
// The licence key is read from the SW_DM_KEY environment variable and is never printed.
// Written in C# 5 so it compiles with the .NET Framework compiler built into Windows.

using System;
using System.IO;
using SolidWorks.Interop.swdocumentmgr;

static class DmToolbox
{
    static int Main(string[] args)
    {
        if (args.Length < 2 || (args[0] != "show" && args[0] != "clear"))
        {
            Console.Error.WriteLine("Usage: DmToolbox show|clear <file> [<file> ...]");
            return 2;
        }
        bool clear = args[0] == "clear";

        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key))
        {
            Console.Error.WriteLine("SW_DM_KEY is not set.");
            return 2;
        }
        ISwDMApplication app;
        try { app = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key); }
        catch (Exception ex) { Console.Error.WriteLine("Could not start Document Manager (check the key): " + ex.GetType().Name); return 3; }
        if (app == null) { Console.Error.WriteLine("Document Manager returned no application (key rejected?)."); return 3; }

        int failures = 0;
        for (int i = 1; i < args.Length; i++)
        {
            string path = Path.GetFullPath(args[i]);
            string name = Path.GetFileName(path);
            if (path.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.Error.WriteLine("REFUSED (inside Dropbox): " + path);
                failures++;
                continue;
            }
            if (!path.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                Console.Error.WriteLine("Not a part file: " + path);
                failures++;
                continue;
            }

            SwDmDocumentOpenError err;
            var doc = app.GetDocument(path, SwDmDocumentType.swDmDocumentPart, !clear, out err) as ISwDMDocument31;
            if (doc == null)
            {
                Console.Error.WriteLine("Could not open " + name + ": " + err);
                failures++;
                continue;
            }
            try
            {
                var before = doc.ToolboxPart;
                if (!clear || before == SwDmToolboxPartType.swDmNotAToolboxPart)
                {
                    Console.WriteLine(name + ": " + Short(before) + (clear ? " (no change needed)" : ""));
                    continue;
                }
                doc.ToolboxPart = SwDmToolboxPartType.swDmNotAToolboxPart;
                var saveErr = doc.Save();
                if (saveErr != SwDmDocumentSaveError.swDmDocumentSaveErrorNone)
                {
                    Console.Error.WriteLine("Save failed on " + name + ": " + saveErr);
                    failures++;
                    continue;
                }
                Console.WriteLine(name + ": " + Short(before) + " -> " + Short(SwDmToolboxPartType.swDmNotAToolboxPart));
            }
            finally
            {
                try { doc.CloseDoc(); } catch { }
            }
        }
        return failures == 0 ? 0 : 1;
    }

    static string Short(SwDmToolboxPartType t)
    {
        return t.ToString().Replace("swDm", "");
    }
}
