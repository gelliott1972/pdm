// DmEdit: small Document Manager edits on copies of SOLIDWORKS files, to prove what the importer relies on.
// Commands:
//   refs        <file>                               list stored component paths (per configuration) and references
//   getprop     <file> <config|@file> <name>         read a custom property
//   setprop     <file> <config|@file> <name> <value> add or change a text custom property, then save
//   delprop     <file> <config|@file> <name>         delete a custom property, save, then re-open and check it's gone
//   replace-ref <file> <oldFileName> <newPath>       ReplaceReference for every stored path with that file name, then save
//   replace-comp <asm> <oldFileName> <newPath> [<oldCfg>=<newCfg> ...]
//                                                    Component.Replace in every configuration, mapping the
//                                                    referenced configuration (unmapped names are kept), then save
// Refuses any path inside a Dropbox folder, so live project files can't be changed by mistake, and refuses to
// change anything in the vault (P:\ = C:\PDM): vault files change only by check-out in the PDM.
// The licence key is read from the SW_DM_KEY environment variable and is never printed.
// Written in C# 5 so it compiles with the .NET Framework compiler built into Windows.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.swdocumentmgr;

static class DmEdit
{
    static ISwDMApplication app;

    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: DmEdit refs|getprop|setprop|delprop|replace-ref|replace-comp <file> ...");
            return 2;
        }
        string cmd = args[0];
        string path = Path.GetFullPath(args[1]);
        bool write = cmd == "setprop" || cmd == "delprop" || cmd == "replace-ref" || cmd == "replace-comp";

        if (path.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Console.Error.WriteLine("REFUSED (inside Dropbox): " + path);
            return 2;
        }
        if (write && (path.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || path.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine("REFUSED (inside the vault): " + path);
            return 2;
        }
        if (!File.Exists(path)) { Console.Error.WriteLine("No such file: " + path); return 2; }

        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        try { app = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key); }
        catch (Exception ex) { Console.Error.WriteLine("Could not start Document Manager (check the key): " + ex.GetType().Name); return 3; }
        if (app == null) { Console.Error.WriteLine("Document Manager returned no application (key rejected?)."); return 3; }

        SwDmDocumentOpenError err;
        var doc = app.GetDocument(path, TypeOf(path), !write, out err) as ISwDMDocument31;
        if (doc == null) { Console.Error.WriteLine("Could not open " + Path.GetFileName(path) + ": " + err); return 3; }
        try
        {
            int rc;
            switch (cmd)
            {
                case "refs": rc = Refs(doc, path); break;
                case "getprop": rc = args.Length == 4 ? GetProp(doc, args[2], args[3]) : Usage(); break;
                case "setprop": rc = args.Length == 5 ? SetProp(doc, args[2], args[3], args[4]) : Usage(); break;
                case "delprop": rc = args.Length == 4 ? DelProp(doc, args[2], args[3]) : Usage(); break;
                case "replace-ref": rc = args.Length == 4 ? ReplaceRef(doc, args[2], Path.GetFullPath(args[3])) : Usage(); break;
                case "replace-comp": rc = args.Length >= 4 ? ReplaceComp(doc, args[2], Path.GetFullPath(args[3]), args.Skip(4).ToArray()) : Usage(); break;
                default: rc = Usage(); break;
            }
            if (rc == 0 && write)
            {
                var saveErr = doc.Save();
                if (saveErr != SwDmDocumentSaveError.swDmDocumentSaveErrorNone)
                {
                    Console.Error.WriteLine("Save failed: " + saveErr);
                    return 1;
                }
                Console.WriteLine("Saved " + Path.GetFileName(path));
            }
            if (rc == 0 && cmd == "delprop")
            {
                doc.CloseDoc();
                doc = app.GetDocument(path, TypeOf(path), true, out err) as ISwDMDocument31;
                if (doc == null) { Console.Error.WriteLine("Could not re-open " + Path.GetFileName(path) + ": " + err); return 1; }
                var names = args[2] == "@file" ? doc.GetCustomPropertyNames() : Config(doc, args[2]) == null ? null : Config(doc, args[2]).GetCustomPropertyNames();
                if (Has(names, args[3])) { Console.Error.WriteLine("Read back: " + args[3] + " is still there at " + args[2]); return 1; }
                Console.WriteLine("Read back: " + args[3] + " is gone at " + args[2]);
            }
            return rc;
        }
        finally
        {
            try { doc.CloseDoc(); } catch { }
        }
    }

    static int Usage()
    {
        Console.Error.WriteLine("Wrong arguments; see the header of DmEdit.cs.");
        return 2;
    }

    static int Refs(ISwDMDocument31 doc, string path)
    {
        var cm = doc.ConfigurationManager;
        if (TypeOf(path) == SwDmDocumentType.swDmDocumentAssembly)
        {
            foreach (var cn in (string[])cm.GetConfigurationNames())
            {
                var cfg = (ISwDMConfiguration18)cm.GetConfigurationByName(cn);
                var arr = cfg.GetComponents() as object[];
                if (arr == null) continue;
                foreach (var o in arr)
                {
                    var c = o as ISwDMComponent12;
                    if (c == null) continue;
                    Console.WriteLine("component\t" + cn + "\t" + Safe(() => c.ConfigurationName) + "\t" + Safe(() => c.PathName)
                        + (Safe(() => c.IsSuppressed() ? "yes" : "") == "yes" ? "\t(suppressed)" : ""));
                }
            }
        }
        var search = app.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly
            | SwDmSearchFilters.SwDmSearchForDrawing | SwDmSearchFilters.SwDmSearchExternalReference
            | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchPartToBaseAssemblyReference);
        object broken, isVirtual, stamps, imported;
        var refs = doc.GetAllExternalReferences5(search, out broken, out isVirtual, out stamps, out imported) as string[];
        var b = broken as int[];
        if (refs != null)
            for (int i = 0; i < refs.Length; i++)
                Console.WriteLine("reference\t" + refs[i] + "\tbroken=" + (b != null && i < b.Length ? b[i].ToString() : "?")
                    + "\texists=" + (File.Exists(refs[i]) ? "yes" : "no"));
        return 0;
    }

    static int GetProp(ISwDMDocument31 doc, string level, string name)
    {
        SwDmCustomInfoType t;
        string val;
        if (level == "@file")
        {
            if (!Has(doc.GetCustomPropertyNames(), name)) { Console.WriteLine("(not set)"); return 1; }
            val = doc.GetCustomProperty2(name, out t);
        }
        else
        {
            var cfg = Config(doc, level);
            if (cfg == null) return 1;
            if (!Has(cfg.GetCustomPropertyNames(), name)) { Console.WriteLine("(not set)"); return 1; }
            val = cfg.GetCustomProperty2(name, out t);
        }
        Console.WriteLine(name + " = " + val);
        return 0;
    }

    static int SetProp(ISwDMDocument31 doc, string level, string name, string value)
    {
        if (level == "@file")
        {
            if (Has(doc.GetCustomPropertyNames(), name)) doc.SetCustomProperty(name, value);
            else doc.AddCustomProperty(name, SwDmCustomInfoType.swDmCustomInfoText, value);
        }
        else
        {
            var cfg = Config(doc, level);
            if (cfg == null) return 1;
            if (Has(cfg.GetCustomPropertyNames(), name)) cfg.SetCustomProperty(name, value);
            else cfg.AddCustomProperty(name, SwDmCustomInfoType.swDmCustomInfoText, value);
        }
        Console.WriteLine("Set " + name + " = " + value + " at " + level);
        return 0;
    }

    static int DelProp(ISwDMDocument31 doc, string level, string name)
    {
        bool ok;
        if (level == "@file")
        {
            if (!Has(doc.GetCustomPropertyNames(), name)) { Console.Error.WriteLine(name + " is not set at @file"); return 1; }
            ok = doc.DeleteCustomProperty(name);
        }
        else
        {
            var cfg = Config(doc, level);
            if (cfg == null) return 1;
            if (!Has(cfg.GetCustomPropertyNames(), name)) { Console.Error.WriteLine(name + " is not set at " + level); return 1; }
            ok = cfg.DeleteCustomProperty(name);
        }
        if (!ok) { Console.Error.WriteLine("Delete refused: " + name + " at " + level); return 1; }
        Console.WriteLine("Deleted " + name + " at " + level);
        return 0;
    }

    // ReplaceReference wants the reference as the file stores it, which is often an old path from another PC,
    // so every stored component path and every resolved reference with the old file name is tried.
    static int ReplaceRef(ISwDMDocument31 doc, string oldName, string newPath)
    {
        if (!File.Exists(newPath)) { Console.Error.WriteLine("New file does not exist: " + newPath); return 1; }
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cm = doc.ConfigurationManager;
        // Only assemblies have components; drawings have no configurations (the Document Manager fails on them).
        if (TypeOf(doc.FullName) == SwDmDocumentType.swDmDocumentAssembly)
            foreach (var cn in (string[])cm.GetConfigurationNames())
            {
                var cfg = (ISwDMConfiguration18)cm.GetConfigurationByName(cn);
                var arr = cfg.GetComponents() as object[];
                if (arr == null) continue;
                foreach (var o in arr)
                {
                    var c = o as ISwDMComponent12;
                    if (c != null && Path.GetFileName(Safe(() => c.PathName)).Equals(oldName, StringComparison.OrdinalIgnoreCase))
                        candidates.Add(c.PathName);
                }
            }
        var search = app.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly
            | SwDmSearchFilters.SwDmSearchExternalReference | SwDmSearchFilters.SwDmSearchInContextReference
            | SwDmSearchFilters.SwDmSearchPartToBaseAssemblyReference);
        object broken, isVirtual, stamps, imported;
        var refs = doc.GetAllExternalReferences5(search, out broken, out isVirtual, out stamps, out imported) as string[];
        if (refs != null)
            foreach (var r in refs)
                if (Path.GetFileName(r).Equals(oldName, StringComparison.OrdinalIgnoreCase)) candidates.Add(r);

        if (candidates.Count == 0) { Console.Error.WriteLine("No reference named " + oldName); return 1; }
        foreach (var c in candidates)
        {
            doc.ReplaceReference(c, newPath);
            Console.WriteLine("ReplaceReference " + c + " -> " + newPath);
        }
        return 0;
    }

    static int ReplaceComp(ISwDMDocument31 doc, string oldName, string newPath, string[] maps)
    {
        if (!File.Exists(newPath)) { Console.Error.WriteLine("New file does not exist: " + newPath); return 1; }
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in maps)
        {
            int i = m.IndexOf('=');
            if (i <= 0) { Console.Error.WriteLine("Bad mapping (want old=new): " + m); return 2; }
            map[m.Substring(0, i)] = m.Substring(i + 1);
        }
        int n = 0;
        var cm = doc.ConfigurationManager;
        foreach (var cn in (string[])cm.GetConfigurationNames())
        {
            var cfg = (ISwDMConfiguration18)cm.GetConfigurationByName(cn);
            var arr = cfg.GetComponents() as object[];
            if (arr == null) continue;
            foreach (var o in arr)
            {
                var c = o as ISwDMComponent12;
                if (c == null || !Path.GetFileName(Safe(() => c.PathName)).Equals(oldName, StringComparison.OrdinalIgnoreCase)) continue;
                string oldCfg = Safe(() => c.ConfigurationName);
                string newCfg;
                if (!map.TryGetValue(oldCfg, out newCfg)) newCfg = oldCfg;
                var res = c.Replace(newPath, newCfg, true);
                Console.WriteLine("Replace in " + cn + ": " + oldName + " [" + oldCfg + "] -> " + Path.GetFileName(newPath) + " [" + newCfg + "]: " + res);
                n++;
            }
        }
        if (n == 0) { Console.Error.WriteLine("No component named " + oldName); return 1; }
        return 0;
    }

    static ISwDMConfiguration18 Config(ISwDMDocument31 doc, string name)
    {
        ISwDMConfiguration18 cfg = null;
        try { cfg = doc.ConfigurationManager.GetConfigurationByName(name) as ISwDMConfiguration18; } catch { }
        if (cfg == null) Console.Error.WriteLine("No configuration named " + name);
        return cfg;
    }

    static bool Has(object names, string name)
    {
        var a = names as string[];
        return a != null && a.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    static SwDmDocumentType TypeOf(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".sldprt": return SwDmDocumentType.swDmDocumentPart;
            case ".sldasm": return SwDmDocumentType.swDmDocumentAssembly;
            case ".slddrw": return SwDmDocumentType.swDmDocumentDrawing;
            default: return SwDmDocumentType.swDmDocumentUnknown;
        }
    }

    static string Safe(Func<string> f)
    {
        try { var s = f(); return s ?? ""; } catch { return ""; }
    }
}
