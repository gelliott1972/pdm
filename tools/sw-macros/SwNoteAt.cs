// SwNoteAt: read-only (LS-08B-11 C16, 25 Nov 2026): for one annotation of a drawing (by name, e.g. DetailItem534), print
// its sheet and view, its text, and what it is attached to (entity type and the component, with that component's path).
// Uses the drawing if it is open already, else opens it read-only; never saves.
// Usage: SwNoteAt.exe <drawing> <annotation name>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwNoteAt.exe SwNoteAt.cs

using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwNoteAt
{
    static string S(Func<string> f) { try { return f() ?? ""; } catch (Exception x) { return "(" + x.GetType().Name + ")"; } }

    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]);
        if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        var doc = sw.GetOpenDocumentByName(file) as ModelDoc2;
        bool opened = false; int e = 0, w = 0;
        if (doc == null)
        {
            doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocDRAWING, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
            opened = true;
        }
        if (doc == null) { Console.WriteLine("FAILED\topen " + e); return 3; }
        var drw = (DrawingDoc)doc;
        int found = 0;
        foreach (string sh in (string[])drw.GetSheetNames())
        {
            drw.ActivateSheet(sh);
            for (var v = (View)drw.GetFirstView(); v != null; v = (View)v.GetNextView())
            {
                foreach (Annotation an in (object[])v.GetAnnotations() ?? new object[0])
                {
                    if (an.GetName() != a[1]) continue;
                    found++;
                    string text = "";
                    var spec = an.GetSpecificAnnotation();
                    var note = spec as Note; if (note != null) text = S(() => note.GetText());
                    Console.WriteLine("ANNOTATION\t" + a[1] + "\tsheet " + sh + "\tview " + v.Name + "\ttype " + (swAnnotationType_e)an.GetType() + "\tdangling " + an.IsDangling() + "\ttext " + text.Replace("\r", " ").Replace("\n", " "));
                    var ents = an.GetAttachedEntities3() as object[];
                    var types = an.GetAttachedEntityTypes() as int[];
                    Console.WriteLine("ATTACHED\t" + (ents == null ? 0 : ents.Length));
                    for (int i = 0; ents != null && i < ents.Length; i++)
                    {
                        var en = ents[i] as Entity;
                        string comp = en == null ? "-" : S(() => { var c = (Component2)en.GetComponent(); return c == null ? "-" : c.Name2 + "  " + c.GetPathName(); });
                        Console.WriteLine("  ENTITY\t" + (types != null && i < types.Length ? ((swSelectType_e)types[i]).ToString() : "?") + "\t" + comp);
                    }
                    if (note != null && note.IsBomBalloon()) Console.WriteLine("  BALLOON\tbom item " + S(() => note.GetBomBalloonText(true)));
                }
            }
        }
        Console.WriteLine("FOUND\t" + found);
        if (opened) sw.CloseDoc(doc.GetTitle());
        return found > 0 ? 0 : 1;
    }
}
