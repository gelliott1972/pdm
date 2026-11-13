// SwCmd: one SOLIDWORKS task per command, run against the SOLIDWORKS that is already open (it starts one only if
// none is running, and leaves it running). No server, no port: each call attaches over COM, does its job, prints a
// report and exits. Brief: sw\solidworks-mcp-review.md "Suggested instead" (Admin, 7 Nov).
//
// Usage:  SwCmd.exe <command> [arguments] [options]
//   status                                       SOLIDWORKS version and every open document (dirty, read-only)
//   close <file>                                 close one open document WITHOUT saving (refused if it is not open)
//   close-all [--exit]                           close every document WITHOUT saving (then exit SOLIDWORKS with --exit)
//   open <file> [--config <name>]                open (or activate) a file; report load errors, missing references,
//                                                components loaded from outside the file's own tree, rebuild errors
//   check-open <file> [--config <name>]          as open, then close it again without saving
//   unlink-equations <file> [--save] [--checked-out]  equations no longer linked to a .txt: values kept, link and path cleared,
//                                                checked, and with --save read back from disk
//   rebuild <file>                               force a full rebuild of an open (or opened) file; report what's wrong
//   save <file> [--checked-out]                  save an open document (refuses if any other open document that would
//                                                be saved with it is somewhere we may not write)
//   replace <asm> <component> <newFile> [--config <name>] [--all] [--save] [--checked-out]
//                                                replace one top-level component of <asm> (name as in the tree,
//                                                "TIE_80x40x8<23>", or the API's "TIE_80x40x8-23") with <newFile>,
//                                                in <name> or the configuration matching the old one; mates re-attached;
//                                                --all replaces every instance of the same file in <asm>
//   set-config <asm> <component> <config> [--save] [--checked-out]
//                                                change the configuration a top-level component uses
//   delete <asm> <component> [--save] [--checked-out]
//                                                delete one top-level component of <asm>
//   save-virtual <asm> <component> <newFile> [--save] [--checked-out]
//                                                save a virtual (^) component of <asm> to its own file
//   save-as <file> <newFile> [--copy]            save under a new name (never in the vault, never over a file);
//                                                --copy keeps the old one as the open document
//   delete-config <file> <config> [--save] [--checked-out]
//                                                delete a configuration (another one is activated first if needed)
//   move <top> <component path> <target subassembly path> | move <top> <component path> --top   [--save] [--checked-out]
//                                                move a component into another subassembly of <top>, or up to <top>;
//                                                paths as in the tree, e.g. "LS-08B-04_SF02-UPPERv2<1>/PLATE<1>"
//   form-sub <asm> <newFile> --except <component> [--except ...] [--save] [--checked-out]
//                                                Form New Subassembly: every top-level component of <asm> except the ones
//                                                named goes into the new assembly <newFile> (never in the vault, never over
//                                                a file; in the vault only with --round <round.csv> naming it as a "new"
//                                                row, 0804 restructure); checks every component kept its file, configuration, suppression
//                                                and position, and lists mates and pattern features left in <asm>
//   dissolve <asm> <component> [--save] [--checked-out]
//                                                dissolve a top-level subassembly into <asm>, with the same checks
//   form-sub / dissolve --accept-gone <mate name> (repeatable): that mate may be lost (B, Admin 7 Nov: two stale mates);
//                                                any other mate lost, new or changed is still a PROBLEM (no save)
//   cut-list <part> [--config <name>]            cut-list items of a weldment part: bodies and properties (read-only)
//   bodies, place, new-asm, delete-feature, dims, add-config, swap-asm: see Place.cs (plates out of the 0804 weldments, 13 Nov)
//   save-as / new-asm --round <round.csv>: as form-sub, a new file in the vault only as a "new" row of that list
//   tree <asm>                                   the component tree with configurations and suppression (read-only)
//   mass <file> [--config <name>]                mass (kg), volume (m3), surface (m2) of every configuration, read-only
//   pack-and-go <top> <newFolder>                copy every file the top uses (suppressed included, no drawings) to a new
//                                                folder, keeping the path below the drive: C:\PDM-work\LS-08A-04\311v3\X ->
//                                                <newFolder>\PDM-work\LS-08A-04\311v3\X (as the 0811 -r1 packs). Refuses an
//                                                existing folder, M:\/Dropbox, the vault, .pdm, the work copy itself, and
//                                                writes nothing if the top uses a file from outside its work copy
//
// Every command that changes an assembly prints the tree's changes (- removed, + added, ~ changed). SOLIDWORKS can
// change more than the component named: in the 7 Nov test, replacing one tie also replaced its mirrored/patterned
// partner; replace, set-config and delete then report PROBLEMS (exit 1) so the extra change is never silent.
// Lightweight components are resolved first where SOLIDWORKS needs it (save-virtual, move).
// move: SOLIDWORKS refuses to move a component out of a subassembly that is used more than once (tested 7 Nov).
//
// Tested 7 Nov on scratch copies (C's pre-prep pack) in SOLIDWORKS 2026 SP3.2: status (also starting SOLIDWORKS),
// check-open (3208 components, 67 s), tree, save-virtual, save, replace (matched and explicit configuration),
// set-config, delete, delete-config, cut-list, save-as --copy, rebuild, close-all. move: only the refused case.
// pack-and-go (7 Nov, scratch C top): 97 of 97 files written, 3 virtual kept in their assemblies; the pack's references point
// into the pack (SetDocumentSaveToNames refuses even SOLIDWORKS's own names, so its default layout is used).
// export-pdf, drawing-info (text and --json), close (7 Nov, scratch copies of GNRIC-BRW-16 and 08A11-SFA-1T): pass.
// export-pdf white recolour (10 Nov, C:\PDM-work\whitetest copies): 08A11-STA-01 36 recoloured, pdfwhite text/strokes/fills
// 36/72/36 -> 0/0/0; GNRIC-BRP-10 2 recoloured, strokes 2 -> 0; both .SLDDRW unchanged (time, size, MD5); an already
// open drawing refused with exit 1 and no PDF. Only single-sheet drawings tried (every drawing on P:\ has one sheet).
//
// Safety (every command): never M:\ or anything in Dropbox; never anything under .pdm. A command that writes refuses
// the vault (P:\, C:\PDM\) unless --checked-out is given and the file is not read-only on disk (checked out); a new
// file is never created in the vault. A command that changes an assembly treats it as a write even without --save.
// Nothing is saved unless the command says so (save, or --save).
// While a file opens, DialogWatch (DialogWatch.cs) cancels a SOLIDWORKS dialog about a missing linked equations file
// (reported as a problem, except by unlink-equations) and reports any other dialog left waiting for 10 s.
// Exit code: 0 done and clean, 1 done but problems reported, 2 refused or bad arguments, 3 SOLIDWORKS error,
//            4 paused: Grant has asked for SOLIDWORKS (nothing done), 5 SOLIDWORKS held by someone else (nothing done).
//
// Who has SOLIDWORKS (docs\briefs\desk-v2.md section 2), files in C:\code\pdm\desk\sw\ (PDM_DESK_DIR overrides the desk folder):
//   every command takes --as <role> (default "unknown"; "grant" for Grant's Desk job). Each command takes lock.json
//   {holder, purpose, since, kind:"command", pid} and clears it after, unless the caller already holds a round.
//   lock --as <role> [--purpose <text>]    hold SOLIDWORKS for a multi-step round (kind "round"); unlock --as <role>
//   [--force] clears it. While pause.json exists every command except status is refused with 4, unless --as grant;
//   the holder of a round may still save, close and close-all to reach a safe point. A lock held by another role = 5.
//   status (any time) prints the lock and pause state first. A running command is never interrupted.
//   status never starts SOLIDWORKS: with none running it says "not running". No command starts SOLIDWORKS while
//   another role holds the lock (5).
// Build (from tools\SwCmd, after copying the interop DLLs from tools\sw-macros\bin into bin\):
//   csc /platform:x64 /r:Microsoft.VisualBasic.dll /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwCmd.exe SwCmd.cs DialogWatch.cs Place.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class SwCmd
{
    static List<string> problems = new List<string>();
    static List<string> dimArgs = new List<string>(), suppressArgs = new List<string>();   // add-config

    static int Main(string[] args)
    {
        if (args.Length < 1) return Usage();
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pos = new List<string>();
        var excepts = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--except")
            { if (i + 1 >= args.Length) return Usage(); excepts.Add(args[++i]); }
            else if (args[i] == "--dim" || args[i] == "--suppress")
            { if (i + 1 >= args.Length) return Usage(); (args[i] == "--dim" ? dimArgs : suppressArgs).Add(args[++i]); }
            else if (args[i] == "--accept-gone")
            { if (i + 1 >= args.Length) return Usage(); acceptGone.Add(args[++i].Trim()); }
            else if (args[i] == "--config" || args[i] == "--as" || args[i] == "--purpose" || args[i] == "--round" || args[i] == "--src-config" || args[i] == "--from" || args[i] == "--tolerance")
            { if (i + 1 >= args.Length) return Usage(); opts[args[i].Substring(2)] = args[++i]; }
            else if (args[i].StartsWith("--")) opts[args[i].Substring(2)] = "yes";
            else pos.Add(args[i]);
        }
        string cmd = args[0].ToLowerInvariant();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "config", "exit", "all", "save", "checked-out", "copy", "top", "json", "as", "purpose", "force", "round", "src-config", "from", "tolerance" };
        if (opts.ContainsKey("round") && cmd != "form-sub" && cmd != "save-as" && cmd != "new-asm") { Console.Error.WriteLine("--round is only for form-sub, save-as and new-asm"); return 2; }
        if (opts.ContainsKey("tolerance"))
        {
            double tol;
            if (cmd != "place" || !double.TryParse(opts["tolerance"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tol) || tol < 0 || tol > 5)
            { Console.Error.WriteLine("--tolerance <percent, 0..5> is only for place"); return 2; }
            placeTolerance = tol / 100;
        }
        if (opts.ContainsKey("src-config") && cmd != "place") { Console.Error.WriteLine("--src-config is only for place"); return 2; }
        if ((opts.ContainsKey("from") || dimArgs.Count > 0 || suppressArgs.Count > 0) && cmd != "add-config") { Console.Error.WriteLine("--from, --dim, --suppress are only for add-config"); return 2; }
        if (excepts.Count > 0 && cmd != "form-sub") { Console.Error.WriteLine("--except is only for form-sub"); return 2; }
        if (acceptGone.Count > 0 && cmd != "form-sub" && cmd != "dissolve" && cmd != "swap-asm") { Console.Error.WriteLine("--accept-gone is only for form-sub, dissolve and swap-asm"); return 2; }
        foreach (var k in opts.Keys) if (!known.Contains(k)) { Console.Error.WriteLine("Unknown option: --" + k); return 2; }
        bool checkedOut = opts.ContainsKey("checked-out"), save = opts.ContainsKey("save");
        if (opts.ContainsKey("json"))
        {
            if (cmd != "drawing-info") { Console.Error.WriteLine("--json is only for drawing-info"); return 2; }
            jsonOut = Console.Out; Console.SetOut(Console.Error);   // stdout carries only the JSON
        }
        string config = opts.ContainsKey("config") ? opts["config"] : null;

        // Check every path before SOLIDWORKS is touched.
        try
        {
            for (int i = 0; i < pos.Count; i++)
                if (LooksLikePath(cmd, i)) pos[i] = Path.GetFullPath(pos[i]);
        }
        catch (Exception ex) { Console.Error.WriteLine("Bad path: " + ex.Message); return 2; }
        string refusal = null;
        switch (cmd)
        {
            case "status": case "close-all": case "lock": case "unlock": if (pos.Count != 0) return Usage(); break;
            case "close":
                if (pos.Count != 1) return Usage(); refusal = ReadRefusal(pos[0]); break;
            case "open": case "check-open": case "rebuild":
                if (pos.Count != 1) return Usage(); refusal = ReadRefusal(pos[0]); break;
            case "save":
                if (pos.Count != 1) return Usage(); refusal = WriteRefusal(pos[0], checkedOut, false); break;
            case "replace":
                if (pos.Count != 3) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false) ?? ReadRefusal(pos[2]);
                if (refusal == null && !File.Exists(pos[2])) refusal = "no such file: " + pos[2];
                break;
            case "unlink-equations":
                if (pos.Count != 1) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false); break;
            case "set-config": case "delete":
                if (pos.Count != (cmd == "delete" ? 2 : 3)) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false); break;
            case "save-virtual":
                if (pos.Count != 3) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false) ?? WriteRefusal(pos[2], false, true);
                if (refusal == null && File.Exists(pos[2])) refusal = "already exists, not overwritten: " + pos[2];
                break;
            case "save-as":
                if (pos.Count != 2) return Usage();
                refusal = ReadRefusal(pos[0]) ?? NewFileRefusal(pos[1], opts);
                if (refusal == null && File.Exists(pos[1])) refusal = "already exists, not overwritten: " + pos[1];
                if (refusal == null && Kind(pos[0]) != Kind(pos[1])) refusal = "save-as keeps the file type: " + pos[1];
                break;
            case "delete-config":
                if (pos.Count != 2) return Usage(); refusal = WriteRefusal(pos[0], checkedOut, false); break;
            case "move":
                if (pos.Count != (opts.ContainsKey("top") ? 2 : 3)) return Usage(); refusal = WriteRefusal(pos[0], checkedOut, false); break;
            case "cut-list": case "tree": case "mass": case "bodies":
                if (pos.Count != 1) return Usage(); refusal = ReadRefusal(pos[0]); break;
            case "export-pdf":
                if (pos.Count != 2) return Usage();
                refusal = ReadRefusal(pos[0]) ?? WriteRefusal(pos[1], false, true);
                if (refusal == null && File.Exists(pos[1])) refusal = "already exists, not overwritten: " + pos[1];
                if (refusal == null && Kind(pos[0]) != (int)swDocumentTypes_e.swDocDRAWING) refusal = "export-pdf needs a drawing: " + pos[0];
                if (refusal == null && !pos[1].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) refusal = "the output must be a .pdf: " + pos[1];
                break;
            case "drawing-info":
                if (pos.Count != 1) return Usage(); refusal = ReadRefusal(pos[0]);
                if (refusal == null && Kind(pos[0]) != (int)swDocumentTypes_e.swDocDRAWING) refusal = "drawing-info needs a drawing: " + pos[0];
                break;
            case "form-sub":
                if (pos.Count != 2 || excepts.Count == 0) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false)
                    ?? (InVault(pos[1]) && opts.ContainsKey("round") ? RoundNewRefusal(opts["round"], pos[1]) : WriteRefusal(pos[1], false, true));
                if (refusal == null && File.Exists(pos[1])) refusal = "already exists, not overwritten: " + pos[1];
                if (refusal == null && (Kind(pos[0]) != (int)swDocumentTypes_e.swDocASSEMBLY || Kind(pos[1]) != (int)swDocumentTypes_e.swDocASSEMBLY))
                    refusal = "form-sub needs an assembly and a new .SLDASM";
                break;
            case "dissolve":
                if (pos.Count != 2) return Usage(); refusal = WriteRefusal(pos[0], checkedOut, false); break;
            case "place":
                if (pos.Count != 4) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false) ?? ReadRefusal(pos[1]) ?? ReadRefusal(pos[2]);
                if (refusal == null && !File.Exists(pos[1])) refusal = "no such file: " + pos[1];
                if (refusal == null && !File.Exists(pos[2])) refusal = "no such file: " + pos[2];
                if (refusal == null && (Kind(pos[0]) != (int)swDocumentTypes_e.swDocASSEMBLY || Kind(pos[1]) != (int)swDocumentTypes_e.swDocPART || Kind(pos[2]) != (int)swDocumentTypes_e.swDocPART))
                    refusal = "place needs <asm.SLDASM> <part.SLDPRT> <srcPart.SLDPRT> <body>";
                break;
            case "new-asm":
                if (pos.Count != 2) return Usage();
                refusal = NewFileRefusal(pos[0], opts) ?? ReadRefusal(pos[1]);
                if (refusal == null && File.Exists(pos[0])) refusal = "already exists, not overwritten: " + pos[0];
                if (refusal == null && !File.Exists(pos[1])) refusal = "no such file: " + pos[1];
                if (refusal == null && (Kind(pos[0]) != (int)swDocumentTypes_e.swDocASSEMBLY || Kind(pos[1]) != (int)swDocumentTypes_e.swDocPART))
                    refusal = "new-asm needs a new .SLDASM and a .SLDPRT";
                break;
            case "swap-asm":
                if (pos.Count != 4) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false) ?? ReadRefusal(pos[1]) ?? ReadRefusal(pos[2]) ?? ReadRefusal(pos[3]);
                for (int k = 1; k < 4 && refusal == null; k++) if (!File.Exists(pos[k])) refusal = "no such file: " + pos[k];
                if (refusal == null && (Kind(pos[0]) != (int)swDocumentTypes_e.swDocASSEMBLY || Kind(pos[1]) != (int)swDocumentTypes_e.swDocPART
                    || Kind(pos[2]) != (int)swDocumentTypes_e.swDocASSEMBLY || Kind(pos[3]) != (int)swDocumentTypes_e.swDocPART))
                    refusal = "swap-asm needs <asm.SLDASM> <oldPart.SLDPRT> <newAsm.SLDASM> <innerPart.SLDPRT>";
                break;
            case "dims":
                if (pos.Count != 1) return Usage(); refusal = ReadRefusal(pos[0]);
                if (refusal == null && Kind(pos[0]) != (int)swDocumentTypes_e.swDocPART) refusal = "dims needs a part";
                break;
            case "add-config":
                if (pos.Count != 2 || !opts.ContainsKey("from")) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false);
                if (refusal == null && Kind(pos[0]) != (int)swDocumentTypes_e.swDocPART) refusal = "add-config is for parts";
                break;
            case "delete-feature":
                if (pos.Count < 2) return Usage();
                refusal = WriteRefusal(pos[0], checkedOut, false); break;
            case "pack-and-go":
                if (pos.Count != 2) return Usage();
                refusal = ReadRefusal(pos[0]) ?? PackTargetRefusal(pos[0], pos[1]);
                break;
            default: return Usage();
        }
        if (refusal != null) { Console.Error.WriteLine("REFUSED: " + refusal); return 2; }

        // Who has SOLIDWORKS (desk-v2 section 2): lock / unlock need no SOLIDWORKS; status always runs; every other
        // command is refused while Grant has asked for SOLIDWORKS (4) or someone else holds it (5).
        string role = (opts.ContainsKey("as") ? opts["as"] : "unknown").Trim().ToLowerInvariant();
        if (role == "" || role.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'))) { Console.Error.WriteLine("Bad --as role: " + role); return 2; }
        if (cmd == "lock") return LockRound(role, opts.ContainsKey("purpose") ? opts["purpose"] : "");
        if (cmd == "unlock") return Unlock(role, opts.ContainsKey("force"));
        if (cmd != "status")
        {
            int g = Gate(cmd, role, opts.ContainsKey("purpose") ? opts["purpose"] : "SwCmd " + string.Join(" ", args));
            if (g != 0) return g;
        }
        formSubExcepts = excepts;
        try { return Run(cmd, pos, opts, config, save, checkedOut); }
        finally { ReleaseCommandLock(); }
    }

    static int Run(string cmd, List<string> pos, Dictionary<string, string> opts, string config, bool save, bool checkedOut)
    {
        // status never starts SOLIDWORKS (7 Nov: a status between SwResave's per-file starts started a hidden one, and
        // SwResave then refused): with no SOLIDWORKS process it reports the lock and "not running".
        bool running = System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Length > 0;
        if (cmd == "status" && !running)
        {
            PrintLockState();
            Console.WriteLine("SOLIDWORKS: not running (status does not start it)");
            return 0;
        }
        // No command starts SOLIDWORKS while another role holds the lock (the Gate already refuses those; this is the
        // last check, against a lock taken between the Gate and here).
        if (!running)
        {
            var held = ReadLock();
            string me = (opts.ContainsKey("as") ? opts["as"] : "unknown").Trim().ToLowerInvariant();
            if (held != null && !Stale(held) && (held.Unreadable || held.Holder != me))
            { Console.Error.WriteLine("IN USE: SOLIDWORKS is not running and is held by " + (held.Unreadable ? "someone (lock unreadable)" : Describe(held)) + "; not starting it. Nothing was done."); return 5; }
        }
        ISldWorks sw;
        try { sw = Attach(); }
        catch (Exception ex) { Console.Error.WriteLine("Could not reach SOLIDWORKS: " + ex.Message); return 3; }
        try
        {
            switch (cmd)
            {
                case "status": return Status(sw);
                case "close": return CloseOne(sw, pos[0]);
                case "close-all": return CloseAll(sw, opts.ContainsKey("exit"));
                case "open": return Open(sw, pos[0], config, false);
                case "check-open": return Open(sw, pos[0], config, true);
                case "rebuild": return Rebuild(sw, pos[0]);
                case "save": return Save(sw, pos[0], checkedOut);
                case "replace": return Replace(sw, pos[0], pos[1], pos[2], config, opts.ContainsKey("all"), save, checkedOut);
                case "set-config": return SetConfig(sw, pos[0], pos[1], pos[2], save, checkedOut);
                case "unlink-equations": return UnlinkEquations(sw, pos[0], save, checkedOut);
                case "delete": return Delete(sw, pos[0], pos[1], save, checkedOut);
                case "save-virtual": return SaveVirtual(sw, pos[0], pos[1], pos[2], save, checkedOut);
                case "save-as": return SaveAs(sw, pos[0], pos[1], opts.ContainsKey("copy"));
                case "delete-config": return DeleteConfig(sw, pos[0], pos[1], save, checkedOut);
                case "move": return Move(sw, pos[0], pos[1], opts.ContainsKey("top") ? null : pos[2], save, checkedOut);
                case "cut-list": return CutList(sw, pos[0], config);
                case "tree": return Tree(sw, pos[0]);
                case "mass": openReadOnly = true; return Mass(sw, pos[0], config);
                case "bodies": openReadOnly = true; return Bodies(sw, pos[0], config);
                case "place": return Place(sw, pos[0], pos[1], pos[2], pos[3], config, opts.ContainsKey("src-config") ? opts["src-config"] : null, save, checkedOut);
                case "new-asm": return NewAsm(sw, pos[0], pos[1], config);
                case "dims": return Dims(sw, pos[0], config);
                case "swap-asm": return SwapAsm(sw, pos[0], pos[1], pos[2], pos[3], save, checkedOut);
                case "add-config": return AddConfig(sw, pos[0], pos[1], opts["from"], dimArgs, suppressArgs, save, checkedOut);
                case "delete-feature": return DeleteFeature(sw, pos[0], pos.Skip(1).ToList(), save, checkedOut);
                case "pack-and-go": return PackAndGo(sw, pos[0], pos[1]);
                case "form-sub": return FormSub(sw, pos[0], pos[1], formSubExcepts, save, checkedOut);
                case "dissolve": return Dissolve(sw, pos[0], pos[1], save, checkedOut);
                case "export-pdf": openReadOnly = true; return ExportPdf(sw, pos[0], pos[1]);
                case "drawing-info": openReadOnly = true; return DrawingInfo(sw, pos[0]);
            }
        }
        catch (COMException ex) { Console.Error.WriteLine("SOLIDWORKS error: " + ex.Message); return 3; }
        finally { RestoreReadOnlyRefs(sw); }
        return Usage();
    }

    // form-sub and dissolve save with SOLIDWORKS' "Don't prompt to save read-only referenced documents (discard changes)"
    // (External References) on, then put the option back. Without it, saving the new subassembly asks Save As for every
    // read-only referenced file SOLIDWORKS holds as modified (rehearsal, 8 Nov: ~90 steel and fastener files not checked
    // out). Those files stay as they are on disk. DialogWatch runs too and cancels any Save As that still comes up.
    static bool? readOnlyRefsWas;
    static void DiscardReadOnlyRefs(ISldWorks sw)
    {
        int opt = (int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave;
        if (!readOnlyRefsWas.HasValue) readOnlyRefsWas = sw.GetUserPreferenceToggle(opt);
        sw.SetUserPreferenceToggle(opt, true);
        Console.WriteLine("Read-only referenced files: not prompted, not saved (SOLIDWORKS option on for this command; was "
            + (readOnlyRefsWas.Value ? "on" : "off") + ")" + (sw.GetUserPreferenceToggle(opt) ? "" : " -- SOLIDWORKS DID NOT TAKE IT"));
        if (!sw.GetUserPreferenceToggle(opt)) problems.Add("could not turn on 'Don't prompt to save read-only referenced documents'");
        DialogWatch.Start();
    }
    static void RestoreReadOnlyRefs(ISldWorks sw)
    {
        if (!readOnlyRefsWas.HasValue) return;
        try { sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave, readOnlyRefsWas.Value); } catch { }
        readOnlyRefsWas = null;
    }

    // ---------- who has SOLIDWORKS: desk\sw\lock.json and pause.json (docs\briefs\desk-v2.md section 2) ----------
    // lock.json {holder, purpose, since, kind, pid}: kind "command" is taken and cleared around one SwCmd call (pid =
    // that SwCmd; a lock whose process is gone is stale and taken over); kind "round" is taken with `lock` and kept
    // across commands until `unlock`. pause.json (written by the Desk) = Grant wants SOLIDWORKS.

    static string DeskSw
    {
        get
        {
            string d = System.Environment.GetEnvironmentVariable("PDM_DESK_DIR");
            return Path.Combine(string.IsNullOrEmpty(d) ? @"C:\code\pdm\desk" : d, "sw");
        }
    }
    static string LockFile { get { return Path.Combine(DeskSw, "lock.json"); } }
    static string PauseFile { get { return Path.Combine(DeskSw, "pause.json"); } }
    static bool ownsCommandLock = false;

    class LockInfo { public string Holder, Purpose, Since, Kind; public int Pid; public bool Unreadable; }

    static LockInfo ReadLock()
    {
        if (!File.Exists(LockFile)) return null;
        string t;
        try { t = File.ReadAllText(LockFile); } catch { return new LockInfo { Holder = "?", Unreadable = true }; }
        Func<string, string> f = name =>
        {
            var m = System.Text.RegularExpressions.Regex.Match(t, "\"" + name + "\"\\s*:\\s*(\"((?:[^\"\\\\]|\\\\.)*)\"|(-?\\d+))");
            return !m.Success ? null : (m.Groups[2].Success && m.Groups[1].Value.StartsWith("\"") ? m.Groups[2].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : m.Groups[3].Value);
        };
        int pid; int.TryParse(f("pid") ?? "", out pid);
        var li = new LockInfo { Holder = (f("holder") ?? "?").ToLowerInvariant(), Purpose = f("purpose") ?? "", Since = f("since") ?? "", Kind = f("kind") ?? "round", Pid = pid };
        if (li.Holder == "?") li.Unreadable = true;
        return li;
    }

    static bool Stale(LockInfo l)
    {
        if (l == null || l.Kind != "command" || l.Pid <= 0) return false;
        try { System.Diagnostics.Process.GetProcessById(l.Pid); return false; } catch (ArgumentException) { return true; }
    }

    static string LockJson(string holder, string purpose, string kind)
    {
        return "{\"holder\":" + J(holder) + ",\"purpose\":" + J(purpose) + ",\"since\":" + J(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
            + ",\"kind\":" + J(kind) + ",\"pid\":" + (kind == "command" ? System.Diagnostics.Process.GetCurrentProcess().Id.ToString() : "0") + "}";
    }

    // Takes the lock only if nobody has it (the rename fails if lock.json exists, so two callers can't both win).
    static bool TryCreateLock(string json)
    {
        Directory.CreateDirectory(DeskSw);
        string tmp = LockFile + "." + System.Diagnostics.Process.GetCurrentProcess().Id + ".tmp";
        File.WriteAllText(tmp, json);
        try { File.Move(tmp, LockFile); return true; }
        catch (IOException) { try { File.Delete(tmp); } catch { } return false; }
    }

    static void ReplaceLock(string json)
    {
        string tmp = LockFile + "." + System.Diagnostics.Process.GetCurrentProcess().Id + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, LockFile, true);
        File.Delete(tmp);
    }

    static string Describe(LockInfo l)
    {
        return l.Holder + (l.Purpose != "" ? " (" + l.Purpose + ")" : "") + (l.Since != "" ? " since " + l.Since : "") + (l.Kind == "command" ? ", one command" : ", a round");
    }

    // Before a command: 4 = paused for Grant, 5 = SOLIDWORKS held by someone else, 0 = go (lock taken if free).
    // While paused, the holder of a round may still save, close and close-all, to reach its safe point.
    static readonly HashSet<string> SafePointCommands = new HashSet<string> { "save", "close", "close-all" };

    static int Gate(string cmd, string role, string purpose)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var l = ReadLock();
            if (Stale(l)) { try { File.Delete(LockFile); } catch { } l = null; }
            bool paused = File.Exists(PauseFile);
            if (paused && role != "grant" && !(l != null && l.Holder == role && l.Kind == "round" && SafePointCommands.Contains(cmd)))
            {
                Console.Error.WriteLine("PAUSED: paused: Grant has asked for SOLIDWORKS (" + PauseFile + "). Nothing was done."
                    + (l != null && l.Holder == role && l.Kind == "round" ? " You hold a round: save or close-all to a safe point, then unlock." : ""));
                return 4;
            }
            if (l != null && l.Unreadable) { Console.Error.WriteLine("IN USE: " + LockFile + " can't be read; nothing was done."); return 5; }
            if (l != null && l.Holder != role) { Console.Error.WriteLine("IN USE: SOLIDWORKS is held by " + Describe(l) + ". Nothing was done."); return 5; }
            if (l != null && l.Kind == "command" && l.Pid != System.Diagnostics.Process.GetCurrentProcess().Id)
            { Console.Error.WriteLine("IN USE: another SwCmd for " + role + " is running (process " + l.Pid + "). Nothing was done."); return 5; }
            if (l != null) return 0;   // the caller's own round: it keeps the lock
            if (TryCreateLock(LockJson(role, purpose, "command"))) { ownsCommandLock = true; return 0; }
        }
        Console.Error.WriteLine("IN USE: could not take " + LockFile + ". Nothing was done.");
        return 5;
    }

    static void ReleaseCommandLock()
    {
        if (!ownsCommandLock) return;
        var l = ReadLock();
        if (l != null && l.Kind == "command" && l.Pid == System.Diagnostics.Process.GetCurrentProcess().Id) { try { File.Delete(LockFile); } catch { } }
        ownsCommandLock = false;
    }

    static int LockRound(string role, string purpose)
    {
        var l = ReadLock();
        if (Stale(l)) { try { File.Delete(LockFile); } catch { } l = null; }
        if (File.Exists(PauseFile) && role != "grant") { Console.Error.WriteLine("PAUSED: paused: Grant has asked for SOLIDWORKS. No lock taken."); return 4; }
        if (l != null && (l.Unreadable || l.Holder != role)) { Console.Error.WriteLine("IN USE: SOLIDWORKS is held by " + Describe(l) + ". No lock taken."); return 5; }
        string json = LockJson(role, purpose ?? "", "round");
        if (l != null) { ReplaceLock(json); Console.WriteLine("Lock renewed for " + role + ": " + (purpose ?? "")); return 0; }
        if (!TryCreateLock(json)) { Console.Error.WriteLine("IN USE: someone took the lock first. No lock taken."); return 5; }
        Console.WriteLine("Locked SOLIDWORKS for " + role + ": " + (purpose ?? ""));
        return 0;
    }

    static int Unlock(string role, bool force)
    {
        var l = ReadLock();
        if (l == null) { Console.WriteLine("No lock to clear."); return 0; }
        if (l.Holder != role && !force && !Stale(l)) { Console.Error.WriteLine("REFUSED: the lock is held by " + Describe(l) + "; only it can unlock (or --force)."); return 5; }
        File.Delete(LockFile);
        Console.WriteLine("Unlocked (was " + Describe(l) + ")" + (File.Exists(PauseFile) ? ". Grant has asked for SOLIDWORKS (pause.json)." : ""));
        return 0;
    }

    static bool LooksLikePath(string cmd, int i)
    {
        switch (cmd)
        {
            case "replace": case "save-virtual": return i == 0 || i == 2;
            case "set-config": case "delete": case "delete-config": case "move": case "dissolve": case "delete-feature": case "add-config": return i == 0;
            case "place": return i <= 2;
            default: return true;
        }
    }

    static int Usage()
    {
        Console.Error.WriteLine("Usage: SwCmd status | close <file> | close-all [--exit] | open <file> [--config <name>] | check-open <file> [--config <name>]");
        Console.Error.WriteLine("       | rebuild <file> | save <file> [--checked-out]");
        Console.Error.WriteLine("       | replace <asm> <component> <newFile> [--config <name>] [--all] [--save] [--checked-out]");
        Console.Error.WriteLine("       | unlink-equations <file> [--save] | set-config <asm> <component> <config> [--save] [--checked-out]");
        Console.Error.WriteLine("       | delete <asm> <component> [--save] [--checked-out]");
        Console.Error.WriteLine("       | save-virtual <asm> <component> <newFile> [--save] [--checked-out]");
        Console.Error.WriteLine("       | save-as <file> <newFile> [--copy] | delete-config <file> <config> [--save] [--checked-out]");
        Console.Error.WriteLine("       | move <top> <component path> <target subassembly path> | move <top> <component path> --top  [--save] [--checked-out]");
        Console.Error.WriteLine("       | form-sub <asm> <newFile> --except <component> [--except ...] [--round <round.csv>] [--accept-gone <mate> ...] [--save] [--checked-out]");
        Console.Error.WriteLine("       | dissolve <asm> <component> [--accept-gone <mate> ...] [--save] [--checked-out]");
        Console.Error.WriteLine("       | cut-list <part> [--config <name>] | tree <asm> | mass <file> [--config <name>] | pack-and-go <top> <newFolder>");
        Console.Error.WriteLine("       | bodies <part> [--config <name>] | dims <part> [--config <name>]");
        Console.Error.WriteLine("       | swap-asm <asm> <oldPart> <newAsm> <innerPart> [--accept-gone <mate> ...] [--save] [--checked-out]");
        Console.Error.WriteLine("       | add-config <part> <newCfg> --from <cfg> [--dim <name>=<mm> ...] [--suppress <feature> ...] [--save] [--checked-out]");
        Console.Error.WriteLine("       | place <asm> <part> <srcPart> <body> [--config <cfg>] [--src-config <cfg>] [--tolerance <percent>] [--save] [--checked-out]");
        Console.Error.WriteLine("       | new-asm <newAsm> <part> [--config <cfg>] [--round <round.csv>] | delete-feature <part> <feature> [<feature> ...] [--save] [--checked-out]");
        Console.Error.WriteLine("       | save-as ... [--round <round.csv>]: a new vault file only as a \"new\" row of the round list");
        Console.Error.WriteLine("       | export-pdf <drawing> <out.pdf> | drawing-info <drawing> [--json]");
        Console.Error.WriteLine("       | lock --as <role> [--purpose <text>] | unlock --as <role> [--force]");
        Console.Error.WriteLine("  every command: [--as <role>] (default unknown; grant = Grant's Desk job)");
        return 2;
    }

    // ---------- safety ----------

    static bool InVault(string p)
    {
        return p.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || p.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase)
            || p.Equals(@"C:\PDM", StringComparison.OrdinalIgnoreCase);
    }

    static string ReadRefusal(string p)
    {
        if (p.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase) || p.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0)
            return "the live Dropbox files (M:\\) are never opened by this tool: " + p;
        if (p.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0 || p.EndsWith(@"\.pdm", StringComparison.OrdinalIgnoreCase))
            return ".pdm is never touched: " + p;
        return null;
    }

    // form-sub --round <round.csv>: the one exception to "no new file in the vault": the new file must be a "new" row
    // of that round list (0804 restructure: the STA-01s are formed in the checked-out tops on P:\ in the agreed run).
    static string RoundNewRefusal(string roundCsv, string newFile)
    {
        string r = ReadRefusal(newFile);
        if (r != null) return r;
        roundCsv = Path.GetFullPath(roundCsv);
        if (!File.Exists(roundCsv)) return "no such round list: " + roundCsv;
        string rel = newFile.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) ? newFile.Substring(3)
            : newFile.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase) ? newFile.Substring(7) : null;
        if (rel == null) return "not a vault path: " + newFile;
        var lines = File.ReadAllLines(roundCsv);
        var head = lines[0].TrimStart('\uFEFF').Split(',').ToList();
        int ia = head.IndexOf("action"), inew = head.IndexOf("new_path");
        if (ia < 0 || inew < 0) return "the round list needs columns action, new_path: " + roundCsv;
        foreach (var l in lines.Skip(1))
        {
            var c = l.Split(',');
            if (c.Length > Math.Max(ia, inew) && c[ia] == "new" && string.Equals(c[inew], rel, StringComparison.OrdinalIgnoreCase)) return null;
        }
        return "not a \"new\" row of " + roundCsv + ": " + newFile;
    }

    // A new file: never in the vault, except as a "new" row of --round <round.csv> (0804 restructure; plates-0804).
    static string NewFileRefusal(string p, Dictionary<string, string> opts)
    {
        return InVault(p) && opts.ContainsKey("round") ? RoundNewRefusal(opts["round"], p) : WriteRefusal(p, false, true);
    }

    static string WriteRefusal(string p, bool checkedOut, bool newFile)
    {
        string r = ReadRefusal(p);
        if (r != null) return r;
        if (!InVault(p)) return null;
        if (newFile) return "no new file is created in the vault (the importer adds new files): " + p;
        if (!checkedOut) return "a vault file is written only with --checked-out: " + p;
        if (!File.Exists(p)) return "no such vault file: " + p;
        if ((File.GetAttributes(p) & FileAttributes.ReadOnly) != 0) return "the vault file is read-only, so it is not checked out: " + p;
        return null;
    }

    // ---------- SOLIDWORKS ----------

    static ISldWorks Attach()
    {
        // The running-object table is not always visible from here (a shell at another integrity level gets
        // MK_E_UNAVAILABLE), but creating the COM object connects to a SOLIDWORKS that is already running, so the
        // process list decides what to say, and a second process appearing is reported.
        object o = null;
        try { o = Marshal.GetActiveObject("SldWorks.Application"); } catch (COMException) { }
        if (o != null) { Console.WriteLine("Attached to the running SOLIDWORKS."); return (ISldWorks)o; }
        var before = System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Select(p => p.Id).ToList();
        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        var after = System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Select(p => p.Id).ToList();
        var started = after.Except(before).ToList();
        if (before.Count == 0) { Console.WriteLine("No SOLIDWORKS was running: started one (it stays open)."); sw.Visible = true; }
        else if (started.Count == 0) Console.WriteLine("Attached to the running SOLIDWORKS (process " + string.Join(", ", before) + ").");
        else Console.WriteLine("WARNING: SOLIDWORKS was running (process " + string.Join(", ", before) + ") but a second one started ("
            + string.Join(", ", started) + "); this command works in the new one.");
        return sw;
    }

    static Type NewestSolidWorks()
    {
        int best = -1;
        using (var root = Microsoft.Win32.Registry.ClassesRoot)
            foreach (var name in root.GetSubKeyNames())
            {
                int v;
                if (name.StartsWith("SldWorks.Application.", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(name.Substring("SldWorks.Application.".Length), out v) && v > best) best = v;
            }
        return Type.GetTypeFromProgID(best >= 0 ? "SldWorks.Application." + best : "SldWorks.Application");
    }

    static int Kind(string f)
    {
        switch (Path.GetExtension(f).ToLowerInvariant())
        {
            case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
            case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
            default: return 0;
        }
    }

    static List<ModelDoc2> OpenDocs(ISldWorks sw)
    {
        var list = new List<ModelDoc2>();
        var d = sw.GetFirstDocument() as ModelDoc2;
        while (d != null) { list.Add(d); d = d.GetNext() as ModelDoc2; }
        return list;
    }

    static ModelDoc2 FindOpen(ISldWorks sw, string path)
    {
        return OpenDocs(sw).FirstOrDefault(d => string.Equals(d.GetPathName(), path, StringComparison.OrdinalIgnoreCase));
    }

    static void PrintLockState()
    {
        var l = ReadLock();
        Console.WriteLine("Lock: " + (l == null ? "free" : Describe(l) + (Stale(l) ? " [stale: its SwCmd has ended]" : "")) + "; pause: "
            + (File.Exists(PauseFile) ? "Grant has asked for SOLIDWORKS" : "none"));
    }

    static int Status(ISldWorks sw)
    {
        PrintLockState();
        Console.WriteLine("SOLIDWORKS " + sw.RevisionNumber());
        var docs = OpenDocs(sw);
        Console.WriteLine("Open documents: " + docs.Count);
        foreach (var d in docs)
            Console.WriteLine("  " + (d.GetSaveFlag() ? "[unsaved] " : "") + (d.IsOpenedReadOnly() ? "[read-only] " : "")
                + (d.Visible ? "" : "[hidden] ") + (d.GetPathName() == "" ? d.GetTitle() : d.GetPathName()));
        return 0;
    }

    // Close one open document without saving (App, 7 Nov); refuses (exit 2) if it is not open. Other documents stay open.
    static int CloseOne(ISldWorks sw, string path)
    {
        var doc = FindOpen(sw, path);
        if (doc == null) { Console.Error.WriteLine("REFUSED: not open in SOLIDWORKS: " + path); return 2; }
        bool dirty = doc.GetSaveFlag();
        sw.CloseDoc(doc.GetTitle());
        bool gone = FindOpen(sw, path) == null;
        Console.WriteLine((gone ? "Closed " : "CLOSE FAILED ") + path + " without saving" + (dirty ? " (its unsaved changes are discarded)" : ""));
        if (!gone) problems.Add("still open: " + path + " (another open document may be using it)");
        return Finish();
    }

    static int CloseAll(ISldWorks sw, bool exit)
    {
        var docs = OpenDocs(sw);
        int unsaved = docs.Count(d => d.GetSaveFlag());
        sw.CloseAllDocuments(true);
        int left = OpenDocs(sw).Count;
        Console.WriteLine("Closed " + (docs.Count - left) + " documents without saving (" + unsaved + " had unsaved changes, now discarded)."
            + (left > 0 ? " Still open: " + left + "." : ""));
        if (exit)
        {
            if (left > 0) { Console.Error.WriteLine("Not exiting: documents are still open."); return 1; }
            // ExitApp alone often leaves SOLIDWORKS running with no window (7 Nov, three times); as SwCheck does, wait,
            // then end the process. Only reached with every document closed, so nothing can be lost.
            var procs = System.Diagnostics.Process.GetProcessesByName("SLDWORKS").ToList();
            sw.ExitApp();
            Console.WriteLine("SOLIDWORKS asked to exit.");
            var deadline = DateTime.Now.AddSeconds(30);
            while (DateTime.Now < deadline && procs.Any(pr => { try { return !pr.HasExited; } catch { return false; } })) System.Threading.Thread.Sleep(1000);
            foreach (var pr in procs)
            {
                try { if (!pr.HasExited) { pr.Kill(); pr.WaitForExit(10000); Console.WriteLine("It hadn't exited after 30 s with nothing open: ended process " + pr.Id + "."); } }
                catch (Exception ex) { problems.Add("could not end SOLIDWORKS process " + pr.Id + ": " + ex.Message); }
            }
            Console.WriteLine(System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Length == 0 ? "SOLIDWORKS has exited." : "SOLIDWORKS is STILL running.");
            return problems.Count > 0 ? 1 : 0;
        }
        return left > 0 ? 1 : 0;
    }

    // Open (or activate) a file and report on it.
    static bool openReadOnly = false;   // export-pdf, drawing-info: a file this command opens is opened read-only

    static ModelDoc2 OpenOrActivate(ISldWorks sw, string path, string config, out bool wasOpen)
    {
        var doc = FindOpen(sw, path);
        wasOpen = doc != null;
        int err = 0, warn = 0;
        if (doc == null)
        {
            if (!File.Exists(path)) { problems.Add("no such file: " + path); return null; }
            int options = (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (openReadOnly ? (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly : 0);
            DialogWatch.Start();
            doc = sw.OpenDoc6(path, Kind(path), options, config ?? "", ref err, ref warn);
            foreach (var e in DialogWatch.Stop())
                if (expectEquationDialog && e.StartsWith("CANCELLED")) Console.WriteLine("  (" + e + ")"); else problems.Add(e);
            if (doc == null) { problems.Add("could not open " + path + ": " + FlagNames(typeof(swFileLoadError_e), err)); return null; }
            Console.WriteLine("Opened " + path + (config != null ? " (" + config + ")" : ""));
            if (err != 0) problems.Add("load errors: " + FlagNames(typeof(swFileLoadError_e), err));
            int quiet = (int)swFileLoadWarning_e.swFileLoadWarning_ReadOnly | (int)swFileLoadWarning_e.swFileLoadWarning_AlreadyOpen
                | (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen;
            if ((warn & (int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen) != 0) Console.WriteLine("  (needs a rebuild: it was saved out of date)");
            if ((warn & ~quiet) != 0) problems.Add("load warnings: " + FlagNames(typeof(swFileLoadWarning_e), warn & ~quiet));
            if ((warn & (int)swFileLoadWarning_e.swFileLoadWarning_ReadOnly) != 0) Console.WriteLine("  (opened read-only)");
        }
        else Console.WriteLine("Already open: " + path);
        int aerr = 0;
        sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref aerr);
        if (wasOpen && config != null && !string.Equals(((Configuration)doc.GetActiveConfiguration()).Name, config, StringComparison.OrdinalIgnoreCase))
        {
            if (doc.ShowConfiguration2(config)) Console.WriteLine("  switched to configuration " + config);
            else problems.Add("could not show configuration " + config);
        }
        return doc;
    }

    static int Open(ISldWorks sw, string path, string config, bool closeAfter)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, config, out wasOpen);
        if (doc != null)
        {
            ReportComponents(doc, path);
            ReportWhatsWrong(doc);
            if (closeAfter && !wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed it again without saving."); }
            else if (closeAfter) Console.WriteLine("It was already open before, so it is left open.");
        }
        return Finish();
    }

    // Components that are missing, suppressed because SOLIDWORKS couldn't find them, or loaded from outside the
    // top's own folder tree (the work copy root is taken as the folder two levels above SC08 etc. is not known, so
    // the rule is: a component outside the top file's drive-and-first-two-folders is listed).
    static void ReportComponents(ModelDoc2 doc, string path)
    {
        if (doc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY) return;
        var asm = (AssemblyDoc)doc;
        var comps = (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>().ToList();
        string root = TreeRoot(path);
        int missing = 0, outside = 0, suppressed = 0, lightweight = 0;
        var seenOutside = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in comps)
        {
            string p = c.GetPathName() ?? "";
            int s = c.GetSuppression2();
            if (s == (int)swComponentSuppressionState_e.swComponentSuppressed) { suppressed++; continue; }
            if (s == (int)swComponentSuppressionState_e.swComponentLightweight || s == (int)swComponentSuppressionState_e.swComponentFullyLightweight) lightweight++;
            if (c.IsVirtual) continue;
            if (p == "" || !File.Exists(p)) { missing++; problems.Add("missing file for component " + c.Name2 + ": " + p); continue; }
            if (root != null && !p.StartsWith(root, StringComparison.OrdinalIgnoreCase) && seenOutside.Add(p))
            { outside++; Console.WriteLine("  loaded from outside " + root + ": " + p); }
        }
        Console.WriteLine("Components: " + comps.Count + " (" + suppressed + " suppressed, " + lightweight + " lightweight), "
            + missing + " missing, " + outside + " files from outside " + (root ?? "?"));
    }

    // C:\PDM-work\LS-08B-04\... -> C:\PDM-work\LS-08B-04\ ; P:\APEX\... -> P:\ ; otherwise the file's own folder.
    static string TreeRoot(string path)
    {
        var parts = path.Split('\\');
        if (InVault(path)) return path.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) ? @"P:\" : @"C:\PDM\";
        int m = path.IndexOf(@"\311v3\", StringComparison.OrdinalIgnoreCase);   // a work copy or Pack and Go: the folder above 311v3
        if (m > 0) return path.Substring(0, m + 1);
        if (parts.Length > 3) return string.Join("\\", parts.Take(3)) + "\\";
        return Path.GetDirectoryName(path) + "\\";
    }

    static void ReportWhatsWrong(ModelDoc2 doc)
    {
        int n = doc.Extension.GetWhatsWrongCount();
        if (n == 0) { Console.WriteLine("What's wrong: nothing."); return; }
        object feats, codes, warns;
        doc.Extension.GetWhatsWrong(out feats, out codes, out warns);
        var f = feats as object[] ?? new object[0];
        var c = codes as int[] ?? new int[0];
        var w = warns as bool[] ?? new bool[0];
        for (int i = 0; i < f.Length; i++)
        {
            var feat = f[i] as Feature;
            string line = (i < w.Length && w[i] ? "warning" : "error") + ": " + (feat != null ? feat.Name + " (" + feat.GetTypeName2() + ")" : "?")
                + (i < c.Length ? " " + FlagName(typeof(swFeatureError_e), c[i]) : "");
            if (i < w.Length && w[i]) Console.WriteLine("  " + line); else problems.Add(line);
        }
    }

    static int Rebuild(ISldWorks sw, string path)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc != null)
        {
            bool ok = doc.ForceRebuild3(false);
            Console.WriteLine("Rebuilt (all configurations' active one, every level): " + (ok ? "ok" : "SOLIDWORKS reported a failure"));
            if (!ok) problems.Add("ForceRebuild3 returned false");
            ReportWhatsWrong(doc);
            Console.WriteLine("Not saved.");
        }
        return Finish();
    }

    // Save one open document. Assemblies can carry other modified documents with them, so every other modified
    // open document must also be somewhere we may write, or nothing is saved.
    static int Save(ISldWorks sw, string path, bool checkedOut)
    {
        var doc = FindOpen(sw, path);
        if (doc == null) { Console.Error.WriteLine("Not open in SOLIDWORKS: " + path); return 2; }
        return SaveDoc(sw, doc, checkedOut) ? Finish() : 2;
    }

    // Why another modified open document would stop a save, or null. While DiscardReadOnlyRefs has SOLIDWORKS' "don't
    // prompt to save read-only referenced documents (discard changes)" on, a modified document that is read-only on disk
    // is not saved by SOLIDWORKS, so it doesn't count (real 0804 run, 8 Nov: form-sub A was refused for the read-only,
    // modified FAST-HEX-8.8-HDG; the rehearsal on a copy outside the vault never reached this check).
    static string OtherModifiedRefusal(ISldWorks sw, bool checkedOut)
    {
        int discarded = 0;
        foreach (var d in OpenDocs(sw))
        {
            string p = d.GetPathName();
            if (!d.GetSaveFlag() || p == "") continue;
            if (readOnlyRefsWas.HasValue && File.Exists(p) && (File.GetAttributes(p) & FileAttributes.ReadOnly) != 0) { discarded++; continue; }
            string why = WriteRefusal(p, checkedOut, false);
            if (why != null) return why;
        }
        if (discarded > 0) Console.WriteLine("  " + discarded + " other modified open document(s) are read-only on disk: SOLIDWORKS discards their changes (option on), they are not written");
        return null;
    }

    static bool SaveDoc(ISldWorks sw, ModelDoc2 doc, bool checkedOut)
    {
        string why = OtherModifiedRefusal(sw, checkedOut);
        if (why != null) { Console.Error.WriteLine("REFUSED, nothing saved: another modified open document can't be written: " + why); return false; }
        int err = 0, warn = 0;
        bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref err, ref warn);
        Console.WriteLine((ok ? "Saved " : "SAVE FAILED ") + doc.GetPathName()
            + (err != 0 ? " errors: " + FlagNames(typeof(swFileSaveError_e), err) : "")
            + (warn != 0 ? " warnings: " + FlagNames(typeof(swFileSaveWarning_e), warn) : ""));
        if (!ok || err != 0) problems.Add("save failed on " + doc.GetPathName());
        return true;
    }

    // Top-level components only: "TIE_80x40x8<23>" (tree) or "TIE_80x40x8-23" (API).
    static Component2 FindComponent(AssemblyDoc asm, string name, bool quiet = false)
    {
        string want = name.Trim();
        if (want.EndsWith(">") && want.LastIndexOf('<') > 0)
        {
            int i = want.LastIndexOf('<');
            want = want.Substring(0, i) + "-" + want.Substring(i + 1, want.Length - i - 2);
        }
        var comps = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>().ToList();
        var hit = comps.FirstOrDefault(c => string.Equals(c.Name2, want, StringComparison.OrdinalIgnoreCase));
        if (hit == null && !quiet)
        {
            problems.Add("no top-level component named " + name + " (looked for " + want + "); a component inside a subassembly must be "
                + "changed in that subassembly");
            var near = comps.Where(c => c.Name2.StartsWith(want.Split('-')[0], StringComparison.OrdinalIgnoreCase)).Select(c => c.Name2).Take(12).ToList();
            if (near.Count > 0) Console.WriteLine("  top-level components with that file name: " + string.Join(", ", near));
        }
        return hit;
    }

    static AssemblyDoc OpenAssembly(ISldWorks sw, string path)
    {
        if (Kind(path) != (int)swDocumentTypes_e.swDocASSEMBLY) { problems.Add("not an assembly: " + path); return null; }
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        return doc as AssemblyDoc;
    }

    static bool Select(ModelDoc2 doc, Component2 c)
    {
        doc.ClearSelection2(true);
        var sel = (SelectionMgr)doc.SelectionManager;
        var data = sel.CreateSelectData();
        bool ok = c.Select4(false, data, false);
        if (!ok) problems.Add("could not select " + c.Name2);
        return ok;
    }

    // Every component (all levels) as "name -> file <configuration>", to show exactly what a command changed:
    // SOLIDWORKS can change more than the one named (a mirrored or patterned copy follows its seed).
    static Dictionary<string, string> Snapshot(AssemblyDoc asm)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>())
            d[c.Name2] = (c.GetPathName() ?? "") + " <" + c.ReferencedConfiguration + ">"
                + (c.GetSuppression2() == (int)swComponentSuppressionState_e.swComponentSuppressed ? " [suppressed]" : "");
        return d;
    }

    // Prints what changed between two snapshots; returns how many top-level components were removed or changed.
    static int Diff(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        int top = 0;
        var lines = new List<string>();
        foreach (var kv in before)
        {
            string now;
            if (!after.TryGetValue(kv.Key, out now)) { lines.Add("  - " + kv.Key + ": " + kv.Value); if (kv.Key.IndexOf('/') < 0) top++; }
            else if (now != kv.Value) { lines.Add("  ~ " + kv.Key + ": " + kv.Value + " -> " + now); if (kv.Key.IndexOf('/') < 0) top++; }
        }
        foreach (var kv in after)
            if (!before.ContainsKey(kv.Key)) lines.Add("  + " + kv.Key + ": " + kv.Value);
        Console.WriteLine("Changes in the tree (" + lines.Count + " lines; - removed, + added, ~ changed):");
        foreach (var l in lines.Where(l => l.Substring(4).Split(':')[0].IndexOf('/') < 0)) Console.WriteLine(l);
        int nested = lines.Count(l => l.Substring(4).Split(':')[0].IndexOf('/') >= 0);
        if (nested > 0) Console.WriteLine("  (and " + nested + " lines for components inside the changed ones)");
        return top;
    }

    static void ExpectOne(int changedTop, string what, bool all)
    {
        if (!all && changedTop > 1)
            problems.Add(what + " changed " + changedTop + " top-level components, not one: SOLIDWORKS also changed the ones listed above "
                + "(mirrored or patterned copies follow their seed). Check them, or undo by closing without saving.");
    }

    static int AfterChange(ISldWorks sw, ModelDoc2 doc, bool save, bool checkedOut)
    {
        doc.ForceRebuild3(false);
        ReportWhatsWrong(doc);
        if (save) { if (!SaveDoc(sw, doc, checkedOut)) return 2; }
        else Console.WriteLine("Not saved (the change is in SOLIDWORKS only; run save, or pass --save).");
        return Finish();
    }

    static int Replace(ISldWorks sw, string asmPath, string comp, string newFile, string config, bool all, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null) return Finish();
        var doc = (ModelDoc2)asm;
        var c = FindComponent(asm, comp);
        if (c == null) return Finish();
        string oldFile = c.GetPathName(), oldCfg = c.ReferencedConfiguration;
        var before = Snapshot(asm);
        if (!Select(doc, c)) return Finish();
        int choice = config != null ? (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_ManuallySelect
                                    : (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName;
        bool ok = asm.ReplaceComponents2(newFile, config ?? "", all, choice, true);
        Console.WriteLine((ok ? "Replaced " : "REPLACE FAILED ") + comp + (all ? " (all instances)" : "") + ": " + oldFile + " <" + oldCfg + "> -> "
            + newFile + (config != null ? " <" + config + ">" : " (configuration matched by name)"));
        if (!ok) { problems.Add("ReplaceComponents2 returned false"); return Finish(); }
        var after = Snapshot(asm);
        ExpectOne(Diff(before, after), "replace", all);
        var added = after.Where(kv => !before.ContainsKey(kv.Key) && kv.Key.IndexOf('/') < 0).ToList();
        if (added.Count == 0) problems.Add("no component now uses " + newFile);
        if (config != null && added.Any(kv => kv.Value.IndexOf("<" + config + ">", StringComparison.OrdinalIgnoreCase) < 0))
            problems.Add("a replaced component is not in configuration " + config);
        return AfterChange(sw, doc, save, checkedOut);
    }

    static int SetConfig(ISldWorks sw, string asmPath, string comp, string config, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null) return Finish();
        var c = FindComponent(asm, comp);
        if (c == null) return Finish();
        string old = c.ReferencedConfiguration;
        var before = Snapshot(asm);
        c.ReferencedConfiguration = config;
        Console.WriteLine("Configuration of " + c.Name2 + ": " + old + " -> " + c.ReferencedConfiguration);
        if (!string.Equals(c.ReferencedConfiguration, config, StringComparison.OrdinalIgnoreCase))
            problems.Add("SOLIDWORKS did not take configuration " + config + " (does the file have it?)");
        ((ModelDoc2)asm).ForceRebuild3(false);
        ExpectOne(Diff(before, Snapshot(asm)), "set-config", false);
        return AfterChange(sw, (ModelDoc2)asm, save, checkedOut);
    }

    static int Delete(ISldWorks sw, string asmPath, string comp, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null) return Finish();
        var doc = (ModelDoc2)asm;
        var c = FindComponent(asm, comp);
        if (c == null) return Finish();
        string name = c.Name2, file = c.GetPathName();
        var before = Snapshot(asm);
        if (!Select(doc, c)) return Finish();
        bool ok = doc.Extension.DeleteSelection2(0);
        bool gone = FindComponent(asm, name, true) == null;
        Console.WriteLine((ok && gone ? "Deleted " : "DELETE FAILED ") + name + " (" + file + ")");
        if (!ok || !gone) { problems.Add("component " + name + " is still there"); return Finish(); }
        ExpectOne(Diff(before, Snapshot(asm)), "delete", false);
        return AfterChange(sw, doc, save, checkedOut);
    }

    static int SaveVirtual(ISldWorks sw, string asmPath, string comp, string newFile, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null) return Finish();
        var c = FindComponent(asm, comp);
        if (c == null) return Finish();
        if (!c.IsVirtual) { problems.Add(c.Name2 + " is not a virtual component"); return Finish(); }
        if (Kind(newFile) != Kind(c.GetPathName() ?? "")) { problems.Add("the new file must keep the type of " + c.GetPathName()); return Finish(); }
        var before = Snapshot(asm);
        int state = c.GetSuppression2();
        if (state != (int)swComponentSuppressionState_e.swComponentFullyResolved && state != (int)swComponentSuppressionState_e.swComponentResolved)
        {
            int r = c.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
            Console.WriteLine("  resolved it first (was " + FlagName(typeof(swComponentSuppressionState_e), state) + ", result " + r + ")");
        }
        bool ok = c.SaveVirtualComponent(newFile);
        bool exists = File.Exists(newFile);
        if (!ok || !exists) Console.WriteLine("  SaveVirtualComponent returned " + ok + "; file there: " + exists + "; component file now: " + c.GetPathName());
        Console.WriteLine((ok && exists ? "Saved virtual " : "SAVE-VIRTUAL FAILED ") + c.Name2 + " -> " + newFile);
        if (!ok || !exists) { problems.Add("SaveVirtualComponent did not write " + newFile); return Finish(); }
        var again = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>()
            .Where(x => string.Equals(x.GetPathName(), newFile, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name2).ToList();
        Console.WriteLine("  components now using it: " + (again.Count > 0 ? string.Join(", ", again) : "none found (check the tree)"));
        Diff(before, Snapshot(asm));
        return AfterChange(sw, (ModelDoc2)asm, save, checkedOut);
    }

    // Save an open (or opened) file under a new name. Without --copy SOLIDWORKS then works on the new file (open
    // assemblies that use it follow the new name once they are saved); with --copy the open document stays the old one.
    static int SaveAs(ISldWorks sw, string path, string newFile, bool copy)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        int err = 0, warn = 0;
        int options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (copy ? (int)swSaveAsOptions_e.swSaveAsOptions_Copy : 0);
        bool ok = doc.Extension.SaveAs3(newFile, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, options, null, null, ref err, ref warn);
        bool exists = File.Exists(newFile);
        Console.WriteLine((ok && exists ? "Saved as " : "SAVE-AS FAILED ") + newFile + (copy ? " (copy; " + path + " stays open)" : " (now open in place of " + path + ")")
            + (err != 0 ? " errors: " + FlagNames(typeof(swFileSaveError_e), err) : "")
            + (warn != 0 ? " warnings: " + FlagNames(typeof(swFileSaveWarning_e), warn) : ""));
        if (!ok || !exists || err != 0) problems.Add("save-as did not write " + newFile);
        return Finish();
    }

    static List<string> ConfigNames(ModelDoc2 doc)
    {
        return ((doc.GetConfigurationNames() as object[]) ?? new object[0]).Select(o => (string)o).ToList();
    }

    static int DeleteConfig(ISldWorks sw, string path, string config, bool save, bool checkedOut)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        var names = ConfigNames(doc);
        var hit = names.FirstOrDefault(n => string.Equals(n, config, StringComparison.OrdinalIgnoreCase));
        if (hit == null) { problems.Add("no configuration " + config + " in " + path + " (has: " + string.Join(", ", names) + ")"); return Finish(); }
        var active = ((Configuration)doc.GetActiveConfiguration()).Name;
        if (string.Equals(active, hit, StringComparison.OrdinalIgnoreCase))
        {
            var other = names.FirstOrDefault(n => !string.Equals(n, hit, StringComparison.OrdinalIgnoreCase)
                && !n.StartsWith(hit + "<", StringComparison.OrdinalIgnoreCase) && n.IndexOf("<As Welded>", StringComparison.OrdinalIgnoreCase) < 0);
            if (other == null || !doc.ShowConfiguration2(other)) { problems.Add(hit + " is the active configuration and no other could be activated"); return Finish(); }
            Console.WriteLine("Activated " + other + " first (" + hit + " was active).");
        }
        bool ok = doc.DeleteConfiguration2(hit);
        var after = ConfigNames(doc);
        bool gone = !after.Any(n => string.Equals(n, hit, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine((ok && gone ? "Deleted configuration " : "DELETE-CONFIG FAILED ") + hit + "; " + after.Count + " left");
        var lostChildren = names.Except(after, StringComparer.OrdinalIgnoreCase).Where(n => !string.Equals(n, hit, StringComparison.OrdinalIgnoreCase)).ToList();
        if (lostChildren.Count > 0) Console.WriteLine("  also gone with it (derived): " + string.Join(", ", lostChildren));
        if (!ok || !gone) { problems.Add("configuration " + hit + " is still there"); return Finish(); }
        return AfterChange(sw, doc, save, checkedOut);
    }

    // Any component of the open top by its full tree path: "SubAsm<1>/Part<2>" or "SubAsm-1/Part-2".
    static Component2 FindAny(AssemblyDoc asm, string path)
    {
        string want = string.Join("/", path.Split('/').Select(ApiName));
        var comps = (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>();
        var hit = comps.FirstOrDefault(c => string.Equals(c.Name2, want, StringComparison.OrdinalIgnoreCase));
        if (hit == null) problems.Add("no component " + path + " (looked for " + want + ")");
        return hit;
    }

    static string ApiName(string s)
    {
        s = s.Trim();
        if (s.EndsWith(">") && s.LastIndexOf('<') > 0)
        {
            int i = s.LastIndexOf('<');
            s = s.Substring(0, i) + "-" + s.Substring(i + 1, s.Length - i - 2);
        }
        return s;
    }

    // Move a component (with its mates where SOLIDWORKS can keep them) into another subassembly of the same top,
    // or up to the top itself (--top). R1: B's LED plate out of SF02-UPPERv2 into the top.
    static int Move(ISldWorks sw, string topPath, string compPath, string targetPath, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, topPath);
        if (asm == null) return Finish();
        var doc = (ModelDoc2)asm;
        var c = FindAny(asm, compPath);
        Component2 target = targetPath == null ? null : FindAny(asm, targetPath);
        if (c == null || (targetPath != null && target == null)) return Finish();
        string file = c.GetPathName();
        var before = Snapshot(asm);
        foreach (var x in new[] { c, target })
            if (x != null && x.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentFullyResolved)
                x.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
        // The top level is passed as the root component of the active configuration (the AssemblyDoc itself is refused).
        object to = target != null ? (object)target : ((Configuration)doc.GetActiveConfiguration()).GetRootComponent3(true);
        bool ok = asm.ReorganizeComponents(new object[] { c }, to);
        if (!ok && target == null)
        {
            ok = asm.ReorganizeComponents(new object[] { c }, null);
            if (ok) Console.WriteLine("  (moved with a null target)");
        }
        Console.WriteLine((ok ? "Moved " : "MOVE FAILED ") + compPath + " (" + file + ") -> " + (targetPath ?? "the top level of " + topPath));
        if (!ok) { problems.Add("ReorganizeComponents returned false"); return Finish(); }
        var now = (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>()
            .Where(x => string.Equals(x.GetPathName(), file, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name2).ToList();
        Console.WriteLine("  components now using that file: " + string.Join(", ", now));
        Diff(before, Snapshot(asm));
        Console.WriteLine("  the subassembly it left and the top both change: save both (save <top> saves the open documents it carries)");
        return AfterChange(sw, doc, save, checkedOut);
    }

    // ---------- form-sub and dissolve (0804 restructure, SW 7 Nov) ----------
    static List<string> formSubExcepts = new List<string>();

    // Every component (all levels) as a "file <config> [suppressed] @ position" signature, so components can be matched
    // when SOLIDWORKS renames or renumbers them. Position: the component's transform in the top, rounded to 1e-5 (m).
    static List<string> Signatures(AssemblyDoc asm, Func<Component2, bool> skip)
    {
        var list = new List<string>();
        foreach (var c in (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>())
            if (!skip(c)) list.Add(Signature(c));
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    static string Signature(Component2 c)
    {
        string pos = "?";
        try
        {
            var t = c.Transform2;
            var a = t == null ? null : t.ArrayData as double[];
            if (a != null && a.Length >= 12)
                pos = string.Join(",", a.Take(12).Select(v => Math.Round(v, 5).ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture).Replace("-0.00000", "0.00000")));
        }
        catch { }
        return (c.GetPathName() ?? "").ToUpperInvariant() + " <" + c.ReferencedConfiguration + ">"
            + (c.GetSuppression2() == (int)swComponentSuppressionState_e.swComponentSuppressed ? " [suppressed]" : "") + " @ " + pos;
    }

    // Lists what is in one list and not the other (multiset), as problems; returns the number of differences.
    static int CompareSignatures(List<string> before, List<string> after, string what)
    {
        var left = before.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var right = after.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        int n = 0;
        foreach (var k in left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase))
        {
            int l, r; left.TryGetValue(k, out l); right.TryGetValue(k, out r);
            if (l == r) continue;
            n += Math.Abs(l - r);
            if (n <= 40) problems.Add(what + ": " + (l > r ? (l - r) + " before, not after: " : (r - l) + " after, not before: ") + k);
        }
        if (n > 40) problems.Add(what + ": " + n + " differences in all (first 40 listed)");
        Console.WriteLine(what + ": " + before.Count + " components before, " + after.Count + " after, "
            + (n == 0 ? "every one with the same file, configuration, suppression and position" : n + " DIFFERENT"));
        return n;
    }

    // Mates (MateGroup) of one document, each with the components it uses (as seen from the top, "Sub-1/Part-1";
    // "" for the assembly's own planes and origin).
    static List<KeyValuePair<string, List<string>>> Mates(ModelDoc2 doc) { return MatesFrom(doc.FirstFeature() as Feature); }

    // A subassembly's mates as seen in the top (Component2.FirstFeature: features in the assembly's context), so the
    // components they tie are named as in the top ("Sub-1/Part-1").
    static List<KeyValuePair<string, List<string>>> MatesOf(Component2 c) { return MatesFrom(c.FirstFeature() as Feature); }

    // Each mate as "type | the components it ties, by file, configuration and position" (names change on a dissolve),
    // with a readable label. "(planes)" = the assembly's own planes or origin; a name kept as [name] had no signature.
    static List<KeyValuePair<string, string>> MateKeys(List<KeyValuePair<string, List<string>>> mates, Dictionary<string, string> sig)
    {
        var list = new List<KeyValuePair<string, string>>();
        foreach (var m in mates)
        {
            int p = m.Key.LastIndexOf(" (");
            string type = p > 0 ? m.Key.Substring(p) : m.Key;
            var parts = m.Value.Select(n => n == "" ? "(planes)" : sig.ContainsKey(n) ? sig[n] : "[" + n + "]").OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            string ents;
            mateEntities.TryGetValue(m.Key + "|" + string.Join(";", m.Value), out ents);
            list.Add(new KeyValuePair<string, string>(type + " | " + string.Join(" + ", parts),
                m.Key + " [" + string.Join("; ", m.Value.Select(n => n == "" ? "(planes)" : n)) + "]" + (string.IsNullOrEmpty(ents) ? "" : " {" + ents + "}")));
        }
        return list;
    }

    static Dictionary<string, string> SigMap(AssemblyDoc asm)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>()) d[c.Name2] = Signature(c);
        return d;
    }

    // Prints the mates in one list and not the other (multiset by key); returns how many are gone.
    static int MateDiff(List<KeyValuePair<string, string>> before, List<KeyValuePair<string, string>> after)
    {
        var left = before.ToList();
        var right = after.ToList();
        foreach (var a in after.ToList())
        {
            int i = left.FindIndex(b => string.Equals(b.Key, a.Key, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) { left.RemoveAt(i); right.Remove(a); }
        }
        foreach (var b in left) Console.WriteLine("  mate gone:  " + b.Value);
        foreach (var a in right) Console.WriteLine("  mate new or changed:  " + a.Value);
        lastGone = left.Select(b => b.Value).ToList();
        lastNew = right.Select(a => a.Value).ToList();
        return left.Count;
    }

    static List<string> lastGone = new List<string>(), lastNew = new List<string>();
    static List<string> acceptGone = new List<string>();   // --accept-gone <mate name>, repeatable (Admin's call for B, 7 Nov)

    // After MateDiff: every gone mate must be named with --accept-gone, nothing may be new or changed, every accepted
    // name must be one that went, and the counts must differ by exactly the accepted ones. Anything else is a PROBLEM.
    static void CheckMates(int countBefore, int countAfter)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in lastGone)
        {
            int p = g.IndexOf(" (");
            string nm = p > 0 ? g.Substring(0, p) : g;
            if (acceptGone.Contains(nm, StringComparer.OrdinalIgnoreCase)) { used.Add(nm); Console.WriteLine("  accepted: " + g); }
            else problems.Add("mate gone: " + g);
        }
        foreach (var n in lastNew) problems.Add("mate new or changed: " + n);
        foreach (var a in acceptGone.Where(a => !used.Contains(a))) problems.Add("--accept-gone " + a + ": no mate of that name went");
        if (countBefore - countAfter != used.Count)
            problems.Add("mates: " + countBefore + " before, " + countAfter + " after, " + used.Count + " accepted as gone");
    }

    // What each mate entity is (face, edge, plane by name ...), for the "mate gone" lines.
    static Dictionary<string, string> mateEntities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    static string EntityText(MateEntity2 e)
    {
        if (e == null) return "?";
        string t = "?";
        try { t = FlagName(typeof(swSelectType_e), e.ReferenceType2).Replace("swSel", ""); } catch { }
        string n = "";
        try { var feat = e.Reference as Feature; if (feat != null) n = " \"" + feat.Name + "\""; } catch { }
        if (n == "") try { var ent = e.Reference as Entity; string mn = ent == null ? null : ent.ModelName; if (!string.IsNullOrEmpty(mn)) n = " \"" + mn + "\""; } catch { }
        // Who owns the entity, as SOLIDWORKS reports it (SW, 7 Nov: two lost face mates name the top as their component).
        string owner = "";
        try
        {
            var ent = e.Reference as Entity;
            if (ent == null) owner = " owner: no entity";
            else
            {
                var oc = ent.GetComponent() as Component2;
                owner = " owner: " + (oc != null ? oc.Name2 : "none");
                var face = e.Reference as Face2;
                var body = face == null ? null : face.GetBody() as Body2;
                if (body != null) owner += ", body \"" + body.Name + "\"";
            }
        }
        catch (Exception ex) { owner = " owner: ? (" + ex.GetType().Name + ")"; }
        // The geometry SOLIDWORKS keeps for the entity (a planar face: a point and its normal, in the top's coordinates),
        // there even when the face itself is gone (SW, 7 Nov).
        string geo = "";
        try
        {
            var pa = e.EntityParams as double[];
            if (pa != null && pa.Length > 0) geo = " params [" + string.Join(", ", pa.Select(v => Math.Round(v, 5).ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
        }
        catch { }
        try { var rc = e.ReferenceComponent as Component2; if (rc != null && rc.IsRoot()) owner += " (the mate's component is the top itself)"; } catch { }
        return t + n + owner + geo;
    }

    static List<KeyValuePair<string, List<string>>> MatesFrom(Feature first)
    {
        var list = new List<KeyValuePair<string, List<string>>>();
        for (var f = first; f != null; f = f.GetNextFeature() as Feature)
        {
            if (f.GetTypeName2() != "MateGroup") continue;
            for (var m = f.GetFirstSubFeature() as Feature; m != null; m = m.GetNextSubFeature() as Feature)
            {
                // Mate folders (FtrFolder) and their end markers are not mates (SW's B trial, 7 Nov).
                if (m.GetTypeName2() == "FtrFolder" || m.Name.StartsWith("___EndTag___")) continue;
                var comps = new List<string>();
                var ents = new List<string>();
                var mate = m.GetSpecificFeature2() as Mate2;
                if (mate != null)
                    for (int i = 0; i < mate.GetMateEntityCount(); i++)
                    {
                        var e = mate.MateEntity(i);
                        var rc = e == null ? null : e.ReferenceComponent as Component2;
                        comps.Add(rc == null ? "" : rc.Name2);
                        ents.Add(EntityText(e));
                    }
                string key = m.Name + " (" + m.GetTypeName2() + ")";
                string state = "";
                try { bool warn; int code = m.GetErrorCode2(out warn); if (code != 0) state += "; " + (warn ? "warning " : "ERROR ") + FlagName(typeof(swFeatureError_e), code); } catch { }
                try { if (m.IsSuppressed()) state += "; SUPPRESSED"; } catch { }
                mateEntities[key + "|" + string.Join(";", comps)] = string.Join("; ", ents)
                    + (mate != null ? "; alignment " + FlagName(typeof(swMateAlign_e), mate.Alignment) : "") + (state == "" ? "; no error, not suppressed" : state);
                list.Add(new KeyValuePair<string, List<string>>(key, comps));
            }
        }
        return list;
    }

    // Component pattern and mirror features of one document.
    static List<string> Patterns(ModelDoc2 doc)
    {
        var list = new List<string>();
        for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
        {
            string t = f.GetTypeName2();
            if (t.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Mirror", StringComparison.OrdinalIgnoreCase) >= 0)
                list.Add(f.Name + " (" + t + ")");
        }
        return list;
    }

    static void OneConfiguration(ModelDoc2 doc, string path)
    {
        var names = ConfigNames(doc);
        if (names.Count > 1)
            problems.Add(path + " has " + names.Count + " configurations (" + string.Join(", ", names) + "): only the active one, "
                + ((Configuration)doc.GetActiveConfiguration()).Name + ", was checked; check the others before saving");
    }

    static bool InsideOf(string name, string prefix) { return name.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase); }

    static bool IsKeptName(string name, HashSet<string> keepNames) { return keepNames.Contains(name) || keepNames.Any(k => InsideOf(name, k)); }

    static int FormSub(ISldWorks sw, string asmPath, string newFile, List<string> excepts, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null) return Finish();
        var doc = (ModelDoc2)asm;
        OneConfiguration(doc, asmPath);
        DiscardReadOnlyRefs(sw);
        // Lightweight components give no component for their faces in mates: resolve them first (SW, 7 Nov).
        int lw = asm.ResolveAllLightWeightComponents(false);
        Console.WriteLine("Resolved lightweight components (result " + lw + ").");
        var keep = new List<Component2>();
        foreach (var e in excepts) { var c = FindComponent(asm, e); if (c == null) return Finish(); keep.Add(c); }
        var keepNames = new HashSet<string>(keep.Select(c => c.Name2), StringComparer.OrdinalIgnoreCase);
        var tops = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>().ToList();
        var move = tops.Where(c => !keepNames.Contains(c.Name2)).ToList();
        if (move.Count == 0) { problems.Add("nothing to move: every top-level component is excepted"); return Finish(); }
        Func<Component2, bool> isKept = c => IsKeptName(c.Name2, keepNames);
        var keptBefore = Signatures(asm, c => !isKept(c));
        var movedBefore = Signatures(asm, isKept);
        var snapBefore = Snapshot(asm);
        var matesBefore = Mates(doc);
        var mateKeysBefore = MateKeys(matesBefore, SigMap(asm));
        var patternsBefore = Patterns(doc);
        Console.WriteLine("Top-level components: " + tops.Count + "; kept in the top: " + string.Join(", ", keepNames) + "; into " + Path.GetFileName(newFile) + ": " + move.Count);
        Console.WriteLine("Before: " + matesBefore.Count + " mates and " + patternsBefore.Count + " pattern/mirror features in the top"
            + (patternsBefore.Count > 0 ? " (" + string.Join(", ", patternsBefore) + ")" : ""));

        doc.ClearSelection2(true);
        var sel = (SelectionMgr)doc.SelectionManager;
        int selected = 0;
        foreach (var c in move)
        {
            var data = sel.CreateSelectData();
            if (c.Select4(true, data, false)) selected++; else problems.Add("could not select " + c.Name2);
        }
        Console.WriteLine("Selected " + selected + " of " + move.Count + " (SOLIDWORKS counts " + sel.GetSelectedObjectCount2(-1) + ").");
        if (selected != move.Count) { doc.ClearSelection2(true); return Finish(); }
        // Component pattern and mirror features of the top go too (SOLIDWORKS refuses to form a subassembly from pattern
        // instances alone; SW's B trial, 7 Nov). If one patterns a kept component, the checks below report it.
        int feats = 0;
        for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
        {
            string t = f.GetTypeName2();
            if (t.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) < 0 && t.IndexOf("Mirror", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (f.Select2(true, -1)) { feats++; Console.WriteLine("  also selected the feature " + f.Name + " (" + t + ")"); }
            else problems.Add("could not select the feature " + f.Name + " (" + t + ")");
        }
        if (feats > 0) Console.WriteLine("Selected " + feats + " pattern/mirror feature(s) as well (SOLIDWORKS counts " + sel.GetSelectedObjectCount2(-1) + ").");
        var topNamesBefore = new HashSet<string>(tops.Select(c => c.Name2), StringComparer.OrdinalIgnoreCase);
        bool ok = asm.MakeAssemblyFromSelectedComponents(newFile);
        doc.ClearSelection2(true);
        // SOLIDWORKS 2026 ignores the file name and makes a virtual "Assem1^<top>" (SW's trial, 7 Nov): the one new
        // top-level component is then saved to newFile, as save-virtual does.
        var fresh = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>().Where(c => !topNamesBefore.Contains(c.Name2)).ToList();
        if (ok && fresh.Count == 1 && fresh[0].IsVirtual)
        {
            var v = fresh[0];
            Console.WriteLine("SOLIDWORKS made a virtual subassembly (" + v.Name2 + "); saving it to " + newFile);
            int st = v.GetSuppression2();
            if (st != (int)swComponentSuppressionState_e.swComponentFullyResolved && st != (int)swComponentSuppressionState_e.swComponentResolved)
                v.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
            bool saved = v.SaveVirtualComponent(newFile);
            Console.WriteLine((saved && File.Exists(newFile) ? "  saved " : "  SAVE-VIRTUAL FAILED ") + newFile);
            if (!saved || !File.Exists(newFile)) { problems.Add("SaveVirtualComponent did not write " + newFile + "; close without saving"); return Finish(); }
        }
        else if (ok && fresh.Count != 1) problems.Add("expected one new top-level component, found " + fresh.Count + ": " + string.Join(", ", fresh.Select(c => c.Name2)));
        var made = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>()
            .Where(c => string.Equals(c.GetPathName(), newFile, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine((ok && made.Count == 1 ? "Formed " : "FORM-SUB FAILED ") + newFile
            + (made.Count == 1 ? " as " + made[0].Name2 : " (components using it: " + made.Count + ")"));
        if (!ok || made.Count != 1)
        {
            problems.Add("MakeAssemblyFromSelectedComponents returned " + ok + ", " + made.Count + " components use " + newFile + "; close without saving"
                + (ok ? "" : " (the API gives no reason; Form New Subassembly by hand on the same selection shows SOLIDWORKS' message)"));
            return Finish();
        }
        string sub = made[0].Name2;

        // Every component that was meant to move is now inside the new one, unchanged; the kept ones are unchanged.
        var topsAfter = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>().Select(c => c.Name2).ToList();
        foreach (var t in topsAfter.Where(t => !keepNames.Contains(t) && !string.Equals(t, sub, StringComparison.OrdinalIgnoreCase)))
            problems.Add("still at the top level of " + Path.GetFileName(asmPath) + ", not in " + sub + ": " + t);
        foreach (var k in keepNames.Where(k => !topsAfter.Contains(k, StringComparer.OrdinalIgnoreCase)))
            problems.Add("the kept component " + k + " is no longer at the top level");
        CompareSignatures(keptBefore, Signatures(asm, c => !isKept(c)), "Kept in the top");
        CompareSignatures(movedBefore, Signatures(asm, c => !InsideOf(c.Name2, sub)), "Into " + sub);
        var snapAfter = Snapshot(asm);
        int renamed = snapBefore.Keys.Count(k => !IsKeptName(k, keepNames) && !snapAfter.ContainsKey(sub + "/" + k));
        Console.WriteLine("Names: " + (renamed == 0 ? "every moved component keeps its name under " + sub
            : renamed + " moved components have a new name or number under " + sub + " (matched by file and position above)"));

        // Mates and patterns: what stayed in the top, and what the new assembly holds.
        var matesAfter = Mates(doc);
        var subDoc = made[0].GetModelDoc2() as ModelDoc2;
        var subMates = subDoc == null ? null : Mates(subDoc);
        var patternsAfter = Patterns(doc);
        var subPatterns = subDoc == null ? new List<string>() : Patterns(subDoc);
        Console.WriteLine("After: " + matesAfter.Count + " mates in the top, " + (subMates == null ? "?" : subMates.Count.ToString()) + " in " + sub
            + "; pattern/mirror features: " + patternsAfter.Count + " in the top, " + subPatterns.Count + " in " + sub);
        Console.WriteLine("Mates matched by type and the components they tie (file, configuration, position), top + " + sub + " seen from the top:");
        int goneMates = MateDiff(mateKeysBefore, MateKeys(matesAfter.Concat(MatesOf(made[0])).ToList(), SigMap(asm)));
        Console.WriteLine("  " + goneMates + " gone" + (goneMates == 0 ? "; every mate is still there" : " (listed above with their names before)"));
        CheckMates(matesBefore.Count, matesAfter.Count + (subMates == null ? MatesOf(made[0]).Count : subMates.Count));
        foreach (var m in matesAfter)
        {
            bool steel = m.Value.Any(n => InsideOf(n, sub) || string.Equals(n, sub, StringComparison.OrdinalIgnoreCase));
            bool other = m.Value.Any(n => n == "" || IsKeptName(n, keepNames));
            if (steel && !other) problems.Add("mate left in the top between components that are all in " + sub + ": " + m.Key + " [" + string.Join("; ", m.Value) + "]");
            else if (steel) Console.WriteLine("  mate kept in the top (it ties " + sub + " to the top or a kept component): " + m.Key + " [" + string.Join("; ", m.Value) + "]");
        }
        foreach (var p in patternsAfter) problems.Add("pattern/mirror feature left in the top: " + p);
        if (subDoc == null) problems.Add("could not reach " + sub + "'s document to check its mates and patterns");
        return AfterSubChange(sw, doc, subDoc, save, checkedOut);
    }

    static int Dissolve(ISldWorks sw, string asmPath, string comp, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null) return Finish();
        var doc = (ModelDoc2)asm;
        OneConfiguration(doc, asmPath);
        DiscardReadOnlyRefs(sw);
        // Lightweight components give no component for their faces in mates: resolve them first (SW, 7 Nov).
        int lw = asm.ResolveAllLightWeightComponents(false);
        Console.WriteLine("Resolved lightweight components (result " + lw + ").");
        var c = FindComponent(asm, comp);
        if (c == null) return Finish();
        string name = c.Name2, file = c.GetPathName();
        if (Kind(file ?? "") != (int)swDocumentTypes_e.swDocASSEMBLY) { problems.Add(name + " is not a subassembly"); return Finish(); }
        if (c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentFullyResolved)
            c.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
        var subDoc = c.GetModelDoc2() as ModelDoc2;
        int subMates = subDoc == null ? -1 : Mates(subDoc).Count;
        var before = Signatures(asm, x => string.Equals(x.Name2, name, StringComparison.OrdinalIgnoreCase));
        int matesBefore = Mates(doc).Count;
        var sigBefore = SigMap(asm);
        var subInTop = MatesOf(c);
        if (subMates >= 0 && subInTop.Count != subMates) Console.WriteLine("  (" + name + "'s mates: " + subMates + " in its file, " + subInTop.Count + " seen from the top)");
        var keysBefore = MateKeys(Mates(doc).Concat(subInTop).ToList(), sigBefore);
        int tops = (asm.GetComponents(true) as object[] ?? new object[0]).Length;
        if (!Select(doc, c)) return Finish();
        bool ok = asm.DissolveSubAssembly();
        doc.ClearSelection2(true);
        bool gone = FindComponent(asm, name, true) == null;
        Console.WriteLine((ok && gone ? "Dissolved " : "DISSOLVE FAILED ") + name + " (" + file + ") into " + Path.GetFileName(asmPath));
        if (!ok || !gone)
        {
            problems.Add("DissolveSubAssembly returned " + ok + "; " + name + (gone ? " is gone" : " is still there") + "; close without saving");
            return Finish();
        }
        CompareSignatures(before, Signatures(asm, x => false), "Components (all levels, " + name + " itself left out)");
        int matesAfter = Mates(doc).Count;
        Console.WriteLine("Top-level components: " + tops + " before, " + (asm.GetComponents(true) as object[] ?? new object[0]).Length + " after; mates in the top: "
            + matesBefore + " before + " + (subMates < 0 ? "?" : subMates.ToString()) + " in " + name + " = " + matesAfter + " after");
        Console.WriteLine("Mates matched by type and the components they tie (file, configuration, position):");
        int goneMates = MateDiff(keysBefore, MateKeys(Mates(doc), SigMap(asm)));
        Console.WriteLine("  " + goneMates + " gone" + (goneMates == 0 ? "; every mate is still there" : " (listed above with their names before the dissolve)"));
        CheckMates(matesBefore + (subMates >= 0 ? subMates : subInTop.Count), matesAfter);
        foreach (var p in Patterns(doc)) Console.WriteLine("  pattern/mirror feature in the top: " + p);
        Console.WriteLine("  " + Path.GetFileName(file) + " itself is not changed or deleted; the top no longer uses it.");
        return AfterSubChange(sw, doc, null, save, checkedOut);
    }

    // Rebuild, what's wrong, then with --save (and no problems) the new subassembly, if any, and the top.
    static int AfterSubChange(ISldWorks sw, ModelDoc2 doc, ModelDoc2 subDoc, bool save, bool checkedOut)
    {
        doc.ForceRebuild3(false);
        ReportWhatsWrong(doc);
        if (!save) { Console.WriteLine("Not saved (the change is in SOLIDWORKS only; run save, or pass --save)."); return Finish(); }
        if (problems.Count > 0) { Console.WriteLine("Not saved: problems above (close without saving, or save after checking)."); return Finish(); }
        // The top's save check runs before anything is saved, so a refusal leaves neither file saved here.
        string why = OtherModifiedRefusal(sw, checkedOut);
        if (why != null) { problems.Add("nothing saved: another modified open document can't be written: " + why); return Finish(); }
        if (subDoc != null)
        {
            int err = 0, warn = 0;
            bool ok = subDoc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref err, ref warn);
            bool there = File.Exists(subDoc.GetPathName());
            Console.WriteLine((ok && there ? "Saved " : "SAVE FAILED ") + subDoc.GetPathName() + (err != 0 ? " errors: " + FlagNames(typeof(swFileSaveError_e), err) : ""));
            if (!ok || !there) { problems.Add("the new subassembly was not saved"); return Finish(); }
        }
        if (!SaveDoc(sw, doc, checkedOut)) return 2;
        return Finish();
    }

    // Cut list of a part in one configuration: each cut-list item, its body count and properties.
    static int CutList(ISldWorks sw, string path, string config)
    {
        if (Kind(path) != (int)swDocumentTypes_e.swDocPART) { Console.Error.WriteLine("cut-list needs a part: " + path); return 2; }
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, config, out wasOpen);
        if (doc == null) return Finish();
        string activeNow = ((Configuration)doc.GetActiveConfiguration()).Name;
        if (config != null && !string.Equals(activeNow, config, StringComparison.OrdinalIgnoreCase) && !doc.ShowConfiguration2(config))
        {
            problems.Add("could not show configuration " + config + " (has: " + string.Join(", ", ConfigNames(doc)) + ")");
            if (!wasOpen) sw.CloseDoc(doc.GetTitle());
            return Finish();
        }
        Console.WriteLine("Configuration: " + ((Configuration)doc.GetActiveConfiguration()).Name);
        int items = 0;
        var f = doc.FirstFeature() as Feature;
        while (f != null)
        {
            if (f.GetTypeName2() == "SolidBodyFolder")
            {
                var bf = f.GetSpecificFeature2() as BodyFolder;
                if (bf != null) bf.UpdateCutList();
                var sub = f.GetFirstSubFeature() as Feature;
                while (sub != null)
                {
                    if (sub.GetTypeName2() == "CutListFolder")
                    {
                        items++;
                        var folder = sub.GetSpecificFeature2() as BodyFolder;
                        int n = folder != null ? folder.GetBodyCount() : 0;
                        Console.WriteLine("  " + sub.Name + ": " + n + " bod" + (n == 1 ? "y" : "ies"));
                        var bodies = folder != null ? (folder.GetBodies() as object[] ?? new object[0]) : new object[0];
                        foreach (var b in bodies.Cast<Body2>()) Console.WriteLine("      body " + b.Name);
                        var pm = sub.CustomPropertyManager;
                        foreach (var name in ((pm.GetNames() as object[]) ?? new object[0]).Cast<string>())
                        {
                            string val, res; bool wasRes, link;
                            pm.Get6(name, false, out val, out res, out wasRes, out link);
                            Console.WriteLine("      " + name + " = " + res + (val != res ? "   (" + val + ")" : ""));
                        }
                    }
                    sub = sub.GetNextSubFeature() as Feature;
                }
            }
            f = f.GetNextFeature() as Feature;
        }
        if (items == 0) Console.WriteLine("No cut list (not a weldment, or one body without a cut-list folder).");
        if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed it again without saving."); }
        return Finish();
    }

    static int Tree(ISldWorks sw, string path)
    {
        var asm = OpenAssembly(sw, path);
        if (asm == null) return Finish();
        var top = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>().ToList();
        Console.WriteLine(path + " <" + ((Configuration)((ModelDoc2)asm).GetActiveConfiguration()).Name + ">");
        foreach (var c in top) TreeLine(c, 1);
        return Finish();
    }

    static void TreeLine(Component2 c, int depth)
    {
        int s = c.GetSuppression2();
        string name = c.Name2.Substring(c.Name2.LastIndexOf('/') + 1);
        int dash = name.LastIndexOf('-');
        string shown = dash > 0 ? name.Substring(0, dash) + "<" + name.Substring(dash + 1) + ">" : name;
        Console.WriteLine(new string(' ', depth * 2) + shown + " <" + c.ReferencedConfiguration + ">"
            + (s == (int)swComponentSuppressionState_e.swComponentSuppressed ? " [suppressed]" : "")
            + (c.IsVirtual ? " [virtual]" : "") + "   " + c.GetPathName());
        foreach (var k in (c.GetChildren() as object[] ?? new object[0]).Cast<Component2>()) TreeLine(k, depth + 1);
    }

    // Pack and Go: every file the top uses (suppressed components included, drawings and simulation results not),
    // copied to <newFolder> with the whole folder structure below the drive kept, as the 0811 -r1 packs:
    // C:\PDM-work\LS-08A-04\311v3\X goes to <newFolder>\PDM-work\LS-08A-04\311v3\X. Refuses a folder that exists, or one inside M:\ / Dropbox, the vault, .pdm or the
    // work copy itself; refuses (writes nothing) if the top uses any file from outside its work copy.
    static string PackTargetRefusal(string top, string target)
    {
        string r = WriteRefusal(target, false, true);
        if (r != null) return r;
        if (Directory.Exists(target) || File.Exists(target)) return "the target already exists, nothing is overwritten: " + target;
        string root = TreeRoot(top);
        string t = target.TrimEnd('\\') + "\\";
        if (t.StartsWith(root, StringComparison.OrdinalIgnoreCase) || root.StartsWith(t, StringComparison.OrdinalIgnoreCase))
            return "the target must be outside the work copy " + root + ": " + target;
        if (Kind(top) != (int)swDocumentTypes_e.swDocASSEMBLY) return "pack-and-go needs an assembly: " + top;
        return null;
    }

    static int PackAndGo(ISldWorks sw, string top, string target)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, top, null, out wasOpen);
        if (doc == null) return Finish();
        string root = TreeRoot(top);
        var pg = doc.Extension.GetPackAndGo();
        pg.IncludeDrawings = false;
        pg.IncludeSimulationResults = false;
        pg.IncludeToolboxComponents = true;
        pg.IncludeSuppressed = true;
        pg.FlattenToSingleFolder = false;
        object namesObj, saveObj, statusObj;
        pg.GetDocumentNames(out namesObj);
        // The documented order: set the folder first, then read SOLIDWORKS's own save-to names and override them.
        Directory.CreateDirectory(target);
        if (!pg.SetSaveToName(true, target)) { problems.Add("SetSaveToName refused " + target); return Finish(); }
        pg.GetDocumentSaveToNames(out saveObj, out statusObj);
        var names = ((namesObj as object[]) ?? new object[0]).Select(o => (string)o).ToArray();
        var defaults = ((saveObj as object[]) ?? new object[0]).Select(o => (string)o).ToArray();
        var status = statusObj as int[] ?? new int[0];
        var targets = new string[names.Length];
        var virtualAt = new bool[names.Length];
        var outside = new List<string>();
        int virt = 0;
        for (int i = 0; i < names.Length; i++)
        {
            bool isVirtual = (i < status.Length && status[i] == (int)swPackAndGoDocumentStatus_e.swPackAndGoDocumentStatus_Virtual)
                || Path.GetFileName(names[i]).IndexOf('^') >= 0;   // a nested virtual component is not always marked
            if (isVirtual) { virt++; virtualAt[i] = true; targets[i] = i < defaults.Length ? defaults[i] : names[i]; continue; }
            if (!names[i].StartsWith(root, StringComparison.OrdinalIgnoreCase)) { outside.Add(names[i]); continue; }
            // SOLIDWORKS keeps the whole path below the drive (C:\PDM-work\LS-08A-04\311v3\X -> <target>\PDM-work\LS-08A-04\311v3\X,
            // as the 0811 -r1 packs); its own names are used (SetDocumentSaveToNames refuses even them, 7 Nov test).
            targets[i] = i < defaults.Length ? defaults[i] : null;
            if (targets[i] == null || !targets[i].StartsWith(target.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) { outside.Add(names[i] + " -> " + targets[i]); continue; }
        }
        Console.WriteLine("Pack and Go of " + top + ": " + names.Length + " documents (" + virt + " virtual, kept inside their assembly)");
        if (outside.Count > 0)
        {
            foreach (var o in outside.Take(30)) problems.Add("used from outside the work copy " + root + ": " + o);
            problems.Add("nothing written: every file must come from the work copy");
            return Finish();
        }
        var result = doc.Extension.SavePackAndGo(pg) as int[] ?? new int[0];
        int written = 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (virtualAt[i]) continue;   // virtual: saved inside its assembly
            if (File.Exists(targets[i])) written++;
            else problems.Add("not written: " + targets[i] + (i < result.Length ? " (status " + result[i] + ")" : ""));
        }
        var bad = result.Where(r => r != 0).Count();
        Console.WriteLine("Written: " + written + " of " + (names.Length - virt) + " files to " + target + (bad > 0 ? "; " + bad + " non-zero save statuses" : ""));
        foreach (var dir in targets.Where((x, k) => x != null && !virtualAt[k])
                     .Select(x => Path.GetDirectoryName(x).Substring(target.TrimEnd('\\').Length)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Take(40))
            Console.WriteLine("  " + (dir == "" ? "\\" : dir));
        if (!wasOpen) Console.WriteLine("The top stays open (close-all closes it without saving).");
        return Finish();
    }

    // Export every sheet of a drawing to one PDF (release brief, 7 Nov). Opened read-only by this command, white
    // annotations recoloured in memory (RecolourWhite), closed without saving. A drawing already open is refused (10 Nov).
    static int ExportPdf(ISldWorks sw, string drawing, string pdf)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, drawing, null, out wasOpen);
        if (doc == null) return Finish();
        var drw = (DrawingDoc)doc;
        var sheets = ((drw.GetSheetNames() as object[]) ?? new object[0]).Cast<string>().ToList();
        var data = (ExportPdfData)sw.GetExportFileData((int)swExportDataFileType_e.swExportPdfData);
        data.ViewPdfAfterSaving = false;
        data.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);
        Directory.CreateDirectory(Path.GetDirectoryName(pdf));
        // A drawing already open is refused (Admin, 10 Nov): its white annotations can't be recoloured without changing
        // the document the user has open. Nothing is exported; close it in SOLIDWORKS (or close-all) and run again.
        if (wasOpen) { problems.Add("REFUSED: drawing already open in SOLIDWORKS, so its white annotations can't be recoloured; no PDF written (close it and run again)"); return Finish(); }
        RecolourWhite(doc);
        int err = 0, warn = 0;
        int options = (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_UpdateInactiveViews;
        bool ok = doc.Extension.SaveAs3(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, options, data, null, ref err, ref warn);
        long size = File.Exists(pdf) ? new FileInfo(pdf).Length : 0;
        int pages = size > 0 ? CountPdfPages(pdf) : 0;
        Console.WriteLine((ok && size > 0 ? "Exported " : "EXPORT FAILED ") + drawing + " -> " + pdf + ": " + sheets.Count + " sheets, "
            + pages + " PDF pages, " + size + " bytes" + (err != 0 ? "; errors: " + FlagNames(typeof(swFileSaveError_e), err) : "")
            + (warn != 0 ? "; warnings: " + FlagNames(typeof(swFileSaveWarning_e), warn) : ""));
        if (!ok || size == 0) problems.Add("no PDF written");
        else if (pages != sheets.Count) problems.Add("the PDF has " + pages + " pages for " + sheets.Count + " sheets");
        if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed the drawing again without saving."); }
        return Finish();
    }

    // Annotations whose own colour is pure white vanish on paper (41 of the 0A drawings, sw\0A-white-pdfs.md, 10 Nov).
    // In memory only, just before the PDF: every sheet (GetViews gives one array per sheet, sheet view first), every
    // view, every annotation coloured 0xFFFFFF gets its layer's colour, or black if it has no layer or the layer is
    // white too. The drawing is closed without saving, so the file on disk never changes.
    static void RecolourWhite(ModelDoc2 doc)
    {
        const int White = 0xFFFFFF;
        var lm = doc.GetLayerManager() as LayerMgr;
        int n = 0, failed = 0;
        var perSheet = ((DrawingDoc)doc).GetViews() as object[];
        foreach (var s in perSheet ?? new object[0])
            foreach (var o in (s as object[]) ?? new object[0])
            {
                var view = o as View;
                if (view == null) continue;
                var ann = view.GetFirstAnnotation3() as Annotation;
                while (ann != null)
                {
                    try
                    {
                        if (ann.Color == White)
                        {
                            Layer layer = null;
                            string ln = ann.Layer;
                            if (lm != null && !string.IsNullOrEmpty(ln)) layer = lm.GetLayer(ln) as Layer;
                            int c = layer != null && layer.Color != White ? layer.Color : 0;
                            ann.Color = c;
                            if (ann.Color == White) failed++; else n++;
                        }
                    }
                    catch (Exception) { failed++; }
                    ann = ann.GetNext3() as Annotation;
                }
            }
        Console.WriteLine("Recoloured " + n + " white annotation(s) for the PDF (not saved)");
        if (failed > 0) problems.Add(failed + " white annotation(s) could not be recoloured: they may not show on paper");
    }

    // Page objects in a PDF ("/Type /Page", not "/Pages"); good enough to compare with the sheet count.
    static int CountPdfPages(string pdf)
    {
        string text = System.Text.Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(pdf));
        return System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page(?![s\w])").Count;
    }

    // What a drawing shows for its revision, per sheet: the title-block notes linked to a revision property (as
    // displayed), the Revision-Sheet<n> property they read, the revision table, and the models the views show.
    // Read-only: sheets are activated and the sheet format is opened for edit in memory only, never saved.
    // Text output, tab-separated: DRAWING, PROP, SHEET, NOTE, ROW, MODEL lines, then OK / PROBLEMS.
    // --json: stdout is one JSON object (App's release step, 7 Nov); every other line goes to stderr:
    //   { "drawing", "sheets": [ { "number", "name", "rev", "revSource", "revisionSheetProperty", "format", "revTableCurrent" } ],
    //     "revTable": [ { "rev", "description", "date", "by", "checked", "cells": [...] } ]  (first sheet with a table, oldest first),
    //     "views": [ { "sheet", "model", "configuration" } ], "problems": [ ... ] }
    static TextWriter jsonOut;   // set when --json: the real stdout

    static int DrawingInfo(ISldWorks sw, string drawing)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, drawing, null, out wasOpen);
        if (doc == null) return FinishJson(drawing, null, null, null);
        if (wasOpen && doc.GetSaveFlag()) problems.Add("the drawing was already open with unsaved changes: this shows them, not the saved file");
        var drw = (DrawingDoc)doc;
        var cpm = doc.Extension.CustomPropertyManager[""];
        Func<string, string> prop = name =>
        {
            string raw, val; bool wasResolved, linked;
            int r = cpm.Get6(name, false, out raw, out val, out wasResolved, out linked);
            return r == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent ? null : val;
        };
        var sheets = ((drw.GetSheetNames() as object[]) ?? new object[0]).Cast<string>().ToList();
        string active = ((Sheet)drw.GetCurrentSheet()).GetName();
        Console.WriteLine("DRAWING\t" + drawing + "\tsheets=" + sheets.Count);
        foreach (var name in ((cpm.GetNames() as object[]) ?? new object[0]).Cast<string>().Where(n => n.IndexOf("rev", StringComparison.OrdinalIgnoreCase) >= 0))
            Console.WriteLine("PROP\t" + name + "\t" + (prop(name) ?? ""));
        var jSheets = new List<string>(); var jViews = new List<string>(); List<string> jTable = null;
        for (int i = 0; i < sheets.Count; i++)
        {
            int n = i + 1;
            drw.ActivateSheet(sheets[i]);
            var sheet = (Sheet)drw.GetCurrentSheet();
            var rt = sheet.RevisionTable;
            string pv = prop("Revision-Sheet" + n);
            var notes = new List<string[]>();
            drw.EditTemplate();
            CollectRevNotes((View)drw.GetFirstView(), n, "format", notes);
            drw.EditSheet();
            CollectRevNotes((View)drw.GetFirstView(), n, "sheet", notes);
            var shown = notes.Select(x => x[1]).Distinct().ToList();
            Console.WriteLine("SHEET\t" + n + "\t" + sheets[i] + "\tREV=" + string.Join(" / ", shown) + "\tRevision-Sheet" + n + "=" + (pv ?? "(none)")
                + "\ttable=" + (rt == null ? "none" : rt.CurrentRevision));
            if (shown.Count == 0) problems.Add("sheet " + n + " (" + sheets[i] + "): no title-block note shows a revision");
            else if (shown.Count > 1) problems.Add("sheet " + n + ": revision notes disagree: " + string.Join(" / ", shown));
            else if (pv != null && !string.Equals(shown[0].Trim(), pv.Trim(), StringComparison.OrdinalIgnoreCase))
                problems.Add("sheet " + n + ": the title block shows " + shown[0] + " but Revision-Sheet" + n + " is " + pv);
            jSheets.Add("{\"number\":" + n + ",\"name\":" + J(sheets[i]) + ",\"rev\":" + J(shown.Count == 1 ? shown[0] : (shown.Count == 0 ? null : string.Join(" / ", shown)))
                + ",\"revSource\":" + J(notes.Count > 0 ? notes[0][0] : null) + ",\"revisionSheetProperty\":" + J(pv)
                + ",\"format\":" + J(sheet.GetTemplateName()) + ",\"revTableCurrent\":" + J(rt == null ? null : rt.CurrentRevision) + "}");
            if (rt != null)
            {
                var t = (TableAnnotation)rt;
                var rows = new List<string[]>();
                for (int r = 0; r < t.RowCount; r++)
                {
                    var cells = Enumerable.Range(0, t.ColumnCount).Select(c => Clean(t.Text[r, c])).ToArray();
                    rows.Add(cells);
                    Console.WriteLine("ROW\t" + n + "\t" + r + "\t" + string.Join(" | ", cells));
                }
                if (jTable == null && rows.Count > 0) jTable = RevRowsJson(rows, rt.CurrentRevision);
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var v = ((View)drw.GetFirstView()).GetNextView() as View;
            while (v != null)
            {
                string m = v.GetReferencedModelName() ?? "", cfg = v.ReferencedConfiguration ?? "";
                if (m != "" && seen.Add(m + "|" + cfg))
                {
                    Console.WriteLine("MODEL\t" + n + "\t" + m + "\t" + cfg);
                    jViews.Add("{\"sheet\":" + n + ",\"model\":" + J(m) + ",\"configuration\":" + J(cfg) + "}");
                }
                v = v.GetNextView() as View;
            }
        }
        drw.ActivateSheet(active);
        if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed the drawing again without saving."); }
        return FinishJson(drawing, jSheets, jTable ?? new List<string>(), jViews);
    }

    // The revision table's rows as JSON, oldest first. A row whose cells look like headers names the columns; the
    // rows are read in the order that ends with the current revision.
    static List<string> RevRowsJson(List<string[]> rows, string current)
    {
        int head = rows.FindIndex(r => r.Any(c => c.Trim().ToUpperInvariant().StartsWith("REV")) && r.Any(c => c.Trim().ToUpperInvariant().StartsWith("DESC")));
        string[] h = head >= 0 ? rows[head].Select(c => c.Trim().ToUpperInvariant()).ToArray() : new string[0];
        Func<string, int> col = w => Array.FindIndex(h, x => x.StartsWith(w));
        int cRev = head >= 0 ? col("REV") : 0, cDesc = col("DESC"), cDate = col("DATE"), cBy = Math.Max(col("BY"), col("DRAWN")), cChk = Math.Max(col("CHK"), col("CHECK"));
        var data = rows.Where((r, i) => i != head && r.Any(c => c.Trim() != "")).ToList();
        if (data.Count > 1 && current != null && cRev >= 0 && cRev < data[0].Length && data[0][cRev].Trim() == current.Trim()) data.Reverse();
        Func<string[], int, string> cell = (r, k) => k >= 0 && k < r.Length ? r[k] : null;
        return data.Select(r => "{\"rev\":" + J(cell(r, cRev)) + ",\"description\":" + J(cell(r, cDesc)) + ",\"date\":" + J(cell(r, cDate))
            + ",\"by\":" + J(cell(r, cBy)) + ",\"checked\":" + J(cell(r, cChk)) + ",\"cells\":[" + string.Join(",", r.Select(J)) + "]}").ToList();
    }

    static int FinishJson(string drawing, List<string> sheets, List<string> table, List<string> views)
    {
        int code = Finish();
        if (jsonOut != null)
        {
            jsonOut.WriteLine("{\"drawing\":" + J(drawing) + ",\"sheets\":[" + string.Join(",", sheets ?? new List<string>()) + "],\"revTable\":["
                + string.Join(",", table ?? new List<string>()) + "],\"views\":[" + string.Join(",", views ?? new List<string>()) + "],\"problems\":["
                + string.Join(",", problems.Select(J)) + "]}");
            jsonOut.Flush();
        }
        return code;
    }

    static string J(string s)
    {
        if (s == null) return "null";
        var b = new System.Text.StringBuilder("\"");
        foreach (char ch in s)
        {
            if (ch == '"') b.Append("\\\"");
            else if (ch == '\\') b.Append("\\\\");
            else if (ch < ' ') b.Append("\\u" + ((int)ch).ToString("x4"));
            else b.Append(ch);
        }
        return b.Append('"').ToString();
    }

    static void CollectRevNotes(View v, int n, string where, List<string[]> notes)
    {
        foreach (var note in (v.GetNotes() as object[] ?? new object[0]).Cast<Note>())
        {
            string link = note.PropertyLinkedText ?? "", text = note.GetText() ?? "";
            if (link.IndexOf("Revision", StringComparison.OrdinalIgnoreCase) < 0) continue;
            Console.WriteLine("NOTE\t" + n + "\t" + where + "\tlink=" + Clean(link) + "\tshows=" + Clean(text));
            notes.Add(new[] { Clean(link), Clean(text) });
        }
    }

    static string Clean(string s) { return (s ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " "); }

    // Mass, volume and surface area of every configuration (or one), in SI units (kg, m3, m2), read-only: each
    // configuration is shown and rebuilt in memory, never saved. For comparing two versions of a file (open them one
    // after the other, never together: same name). Output: MASS <config> <kg> <m3> <m2> lines, then OK / PROBLEMS.
    static int Mass(ISldWorks sw, string path, string config)
    {
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        if (doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING) { problems.Add("mass needs a part or assembly"); return Finish(); }
        if (wasOpen && doc.GetSaveFlag()) problems.Add("the file was already open with unsaved changes: this measures them, not the saved file");
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        var names = config != null ? new List<string> { config } : ConfigNames(doc);
        foreach (var c in names)
        {
            if (!string.Equals(((Configuration)doc.GetActiveConfiguration()).Name, c, StringComparison.OrdinalIgnoreCase) && !doc.ShowConfiguration2(c))
            { problems.Add("could not show configuration " + c); continue; }
            doc.ForceRebuild3(false);
            var mp = doc.Extension.CreateMassProperty() as MassProperty;
            if (mp == null) { problems.Add(c + ": no mass properties"); continue; }
            mp.UseSystemUnits = true;
            Console.WriteLine("MASS\t" + c + "\t" + mp.Mass.ToString("G9", System.Globalization.CultureInfo.InvariantCulture)
                + "\t" + mp.Volume.ToString("G9", System.Globalization.CultureInfo.InvariantCulture)
                + "\t" + mp.SurfaceArea.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
            if (mp.Mass <= 0) problems.Add(c + ": mass is " + mp.Mass);
        }
        doc.ShowConfiguration2(active);
        if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed it again without saving."); }
        return Finish();
    }

    // Unlink a part's or assembly's equations from an external .txt (0804, 7 Nov): the equations and their values stay
    // in the file, the link and the file path are cleared. Checked before and after (count, each equation, each value),
    // and with --save read back from disk (re-opened read-only). The missing-file dialog SOLIDWORKS shows on opening is
    // cancelled by DialogWatch, which keeps the values the file has.
    static bool expectEquationDialog = false;

    static List<string> EquationList(ModelDoc2 doc)
    {
        var em = doc.GetEquationMgr() as EquationMgr;
        var r = new List<string>();
        if (em == null) return r;
        for (int i = 0; i < em.GetCount(); i++)
            r.Add(em.Equation[i] + " = " + em.Value[i].ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
        return r;
    }

    static int UnlinkEquations(ISldWorks sw, string path, bool save, bool checkedOut)
    {
        expectEquationDialog = true;
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        if (doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING) { problems.Add("unlink-equations needs a part or assembly"); return Finish(); }
        var em = doc.GetEquationMgr() as EquationMgr;
        if (em == null) { problems.Add("no equation manager"); return Finish(); }
        Console.WriteLine("Equations: " + em.GetCount() + "; linked to a file: " + (em.LinkToFile ? "yes" : "no") + "; file: " + (em.FilePath ?? ""));
        if (!em.LinkToFile && string.IsNullOrEmpty(em.FilePath)) { Console.WriteLine("Nothing to do: not linked."); return Finish(); }
        var before = EquationList(doc);
        em.LinkToFile = false;
        try { em.FilePath = ""; } catch (Exception ex) { problems.Add("could not clear the file path: " + ex.Message); }
        doc.ForceRebuild3(false);
        var after = EquationList(doc);
        Console.WriteLine("Now: linked " + (em.LinkToFile ? "YES" : "no") + "; file: '" + (em.FilePath ?? "") + "'");
        if (em.LinkToFile) problems.Add("SOLIDWORKS kept the link on");
        if (!string.IsNullOrEmpty(em.FilePath)) problems.Add("the file path is still set: " + em.FilePath);
        CompareEquations(before, after, "after unlinking");
        if (!save) { Console.WriteLine("Not saved (the change is in SOLIDWORKS only; run save, or pass --save)."); return Finish(); }
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); return Finish(); }
        ReportWhatsWrong(doc);
        if (!SaveDoc(sw, doc, checkedOut)) return 2;
        sw.CloseDoc(doc.GetTitle());
        int err = 0, warn = 0;
        DialogWatch.Start();
        var back = sw.OpenDoc6(path, Kind(path), (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly, "", ref err, ref warn);
        foreach (var e in DialogWatch.Stop()) problems.Add("on re-opening: " + e);
        if (back == null) { problems.Add("could not re-open it to read back"); return Finish(); }
        var em2 = back.GetEquationMgr() as EquationMgr;
        Console.WriteLine("Read back from disk: linked " + (em2.LinkToFile ? "YES" : "no") + "; file: '" + (em2.FilePath ?? "") + "'; equations " + em2.GetCount());
        if (em2.LinkToFile || !string.IsNullOrEmpty(em2.FilePath)) problems.Add("on disk the equations are still linked");
        CompareEquations(before, EquationList(back), "on disk");
        sw.CloseDoc(back.GetTitle());
        if (wasOpen) Console.WriteLine("(It was open before; it is now closed, saved.)");
        return Finish();
    }

    static void CompareEquations(List<string> a, List<string> b, string when)
    {
        if (a.Count != b.Count) { problems.Add("equations " + when + ": " + a.Count + " before, " + b.Count + " now"); return; }
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) problems.Add("equation " + (i + 1) + " " + when + ": " + a[i] + " -> " + b[i]);
        if (a.Count == b.Count && Enumerable.Range(0, a.Count).All(i => a[i] == b[i])) Console.WriteLine("Equations " + when + ": all " + a.Count + " the same, with the same values.");
    }

    // ---------- reporting ----------

    static int Finish()
    {
        foreach (var e in DialogWatch.Stop()) problems.Add(e);
        if (problems.Count == 0) { Console.WriteLine("OK"); return 0; }
        Console.WriteLine("PROBLEMS (" + problems.Count + "):");
        foreach (var p in problems) Console.WriteLine("  " + p);
        return 1;
    }

    static string FlagName(Type e, int v)
    {
        return Enum.IsDefined(e, v) ? Enum.GetName(e, v) : v.ToString();
    }

    static string FlagNames(Type e, int v)
    {
        if (v == 0) return "none";
        var names = new List<string>();
        foreach (var n in Enum.GetNames(e))
        {
            int f = (int)Enum.Parse(e, n);
            if (f != 0 && (v & f) == f) names.Add(n.Substring(n.LastIndexOf('_') + 1));
        }
        return names.Count > 0 ? string.Join(", ", names) + " (" + v + ")" : v.ToString();
    }
}
