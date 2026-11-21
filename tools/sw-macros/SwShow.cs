// SwShow: make the running SOLIDWORKS 2026 visible and hand it to the user (21 Nov 2026: a SOLIDWORKS started in the
// background by a tool had no window, so Grant could not see the drawings opened for him). Changes no document.
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /out:bin\SwShow.exe SwShow.cs

using System;
using SolidWorks.Interop.sldworks;

static class SwShow
{
    static int Main()
    {
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        sw.Visible = true;
        sw.UserControl = true;
        sw.FrameState = 1;   // swWindowMaximized
        Console.WriteLine("visible " + sw.Visible + ", documents " + sw.GetDocumentCount());
        return 0;
    }
}
