# AutoCAD versions of our drawings: options

14 Nov 2026. Research only: neither SOLIDWORKS nor AutoCAD was started, and nothing was exported. Each claim is marked **[PC]** (checked on this PC), **[SW help]** (SOLIDWORKS 2026 API help installed here), **[web]** (found online, source at the end) or **[inferred]** (my understanding, to be proven by the first test).

## In short

- **Yes.** SOLIDWORKS can save any drawing as a DWG, and the same step that makes the release PDF can make the DWG. That part is free and nearly no work.
- **Straight out of SOLIDWORKS it is a real DWG, but it won't feel native.** Expect odd layer names, a title block made of loose lines and text, tables drawn as lines, and dimensions that may misbehave when someone edits them.
- **AutoCAD 2027 on this PC can tidy that up automatically.** A clean-up script would rename layers to the recipient's standard, fix the text styles, swap in a proper title block, purge and audit. We already drove AutoCAD this way on Jerry's job.
- **Recommendation:** export from SOLIDWORKS at release, then run an AutoCAD clean-up pass. Start with a one-drawing test before building anything.
- **The DWG is a copy, not a master.** SOLIDWORKS stays the source. Anything a recipient changes in the DWG is lost at the next revision, and each revision produces a fresh DWG.
- **The main unknown is what each agency actually wants.** "A DWG to lay under our plans" and "a DWG drawn to our CAD standard" are very different amounts of work. Questions are at the end.

## 1. What "native AutoCAD" probably means

Nobody has told us yet, so this is my reading of what such recipients usually ask for. **[inferred]**

| What they mean | Who typically asks | What they need from us | Effort |
| --- | --- | --- | --- |
| **A. Background to lay under their own drawings** (xref) | Architects, consultants, main contractor's coordination team | 2D linework in model space at true size (1 unit = 1 mm), sensible origin, a few clean layers, no junk. They often don't want our title block at all | Low |
| **B. An editable copy of our sheet** | Agencies that re-issue drawings under their own title block, or mark up and return | Sheets as layouts at the right scale, real dimensions and text (not exploded), title block as a block with fillable fields, fonts they have | Medium |
| **C. A sheet drawn to their CAD standard** | Government or large-client projects with a CAD manual (set layer names, colours, pen weights, fonts, file naming) | Everything in B, plus their layer list, text and dimension styles, and plot style table | Medium to high, per standard |
| **D. A record copy for submission** | Approving bodies that require DWG alongside PDF | Usually any DWG that opens cleanly in an older version and prints like the PDF | Low |
| **E. 3D solids for coordination** | BIM or coordination teams working in AutoCAD, Navisworks or Revit | A DWG containing 3D solids of the structure at true size and position | Low to medium (see section 3) |
| **F. Cutting profiles** | Fabricators (laser or plasma cutting of plates) | DXF at 1:1 of the flat plate outline and holes, nothing else | Low; a different job from drawings |

Why they want it **[inferred]**: they can measure and snap to it, overlay it on their own plans, reuse details, and re-issue under their own border. A PDF lets them do none of that properly. In mainland China, design institutes work almost entirely in AutoCAD (or AutoCAD-compatible programs), often in older file versions and with Chinese SHX fonts, so file version and fonts matter more there.

## 2. Routes compared

| Route | What you get | Quality ("feels native"?) | Effort | Can it run unattended? | Cost | Main risks |
| --- | --- | --- | --- | --- | --- | --- |
| **1. SOLIDWORKS Save As DWG** | The whole sheet as lines, arcs, text and dimensions | Fair. Real DWG, real dimension objects, but SOLIDWORKS-flavoured layers, loose title block, tables as lines **[inferred]** | Very low | Yes, through the same SwCmd tool that exports the release PDF | None | Scale and dimension quirks (below); fonts |
| **2. Route 1 + AutoCAD clean-up pass** | Same content, tidied to a standard | Good. Close to hand-drawn if the title block and layers are done well | Medium once, then low per drawing. More for each extra CAD standard | Yes. AutoCAD 2027 is here with its batch tools **[PC]** | None extra on this PC. Needs an AutoCAD seat wherever releases are run | Clean-up rules need tuning per drawing type; AutoCAD must be on the release machine |
| **3. Export DXF, edit with a library (ezdxf), convert to DWG** | Same content, edited without AutoCAD | Fair to good | Medium | Yes | Free, but needs the ODA File Converter to produce DWG (not installed, licence not checked) | ezdxf writes DXF only, never DWG **[web]**. The free DWG library we tried on Jerry's job (LibreDWG) lost layouts and tables **[PC, Jerry notes]** |
| **4. DraftSight (Dassault's AutoCAD-style program)** | A second DWG editor | Same as route 2 | Medium | Has its own scripting | About US$299 a year per seat (third-party price lists, not confirmed) **[web]** | No advantage while we have AutoCAD. Recipients judge the file in AutoCAD anyway |
| **5. Redraw the views inside AutoCAD from the 3D model** | Drawing views generated by AutoCAD itself | Most native | High: every dimension and note James made is redone | Partly | None | Doubles the drawing work. Not recommended |
| **6. 3D solids DWG** (section 3) | The model as AutoCAD solids, no drawing sheet | Good for coordination | Low to medium | Probably | None | File size; see section 3 |

### Route 1 in detail: the SOLIDWORKS export settings

These are the settings behind File > Save As > DWG > Options. All can be set from a program before saving. Names are from the 2026 API help on this PC (`swconst.chm`, topic "File > Save As > Dxf or Dwg > Options"). **[SW help]**

| Setting | API name | Choices | Suggested start |
| --- | --- | --- | --- |
| DWG version | `swDxfVersion` (integer) | R12, R13, R14, R2000, R2004, R2007, R2010, R2013, R2018. Nothing newer exists; AutoCAD 2027 still uses the 2018 format | R2013 unless told otherwise (opens in anything from 2013 on) |
| Fonts | `swDxfOutputFonts` (integer) | 0 = AutoCAD STANDARD only, 1 = TrueType | 1, then judge in the test |
| Line styles | `swDxfOutputLineStyles` (integer) | 0 = AutoCAD standard styles, 1 = SOLIDWORKS custom styles | 0 |
| Layer and colour mapping on/off | `swDxfMapping` (toggle) | on / off | On, with our map file |
| Map file | `swDxfMappingFiles` (string list) plus `swDxfMappingFileIndex` (integer) | Path to the map file. The help says the index must also be set or the map is ignored | One map file per recipient standard |
| Don't show the mapping box at each save | `swDXFDontShowMap` (toggle) | on / off | On (otherwise an unattended run stops at a dialog) |
| Keep SOLIDWORKS layers | `swDxfUseSolidworksLayers` (toggle) | Map only what has no layer, or let the map override everything | Decide after seeing what layers James uses |
| Scale output 1:1 | `swDxfOutputNoScale` (integer) | 0 = geometry at paper size, 1 = geometry at true size | Test both |
| Splines | `swDxfExportSplinesAsSplines` (toggle) | Splines or polylines | Splines |
| Geometry grouping | `swDxfGeomExportOption` (integer) | Views as blocks, top-level components as blocks, or geometry as polylines | Views as blocks: test it |
| Multi-sheet drawings | `swDxfMultiSheetOption` (integer) | Active sheet only (0), separate files (1), all in one file (2) | Not an issue: James's drawings are one sheet |
| Sheets to paper space | `swDxfExportAllSheetsToPaperSpace` (toggle) | on / off. Off puts everything in model space | Test both |
| Hidden layers | `swDXFExportHiddenLayersOn`, `swDXFExportHiddenLayersWarnIsOn` (toggles) | Export hidden layers or not; silence the warning | Don't export; silence the warning |
| Join line ends | `swDxfEndPointMerge`, `swDxfMergingDistance`, `swDXFHighQualityExport` | Closes tiny gaps | The help says this applies when exporting a **part**; leave off for drawings |

The save itself is the ordinary Save As with a `.dwg` name (`IModelDocExtension.SaveAs3`, present in the 2026 help). For cutting profiles (variant F) there is a separate call that exports a part's faces straight to DXF or DWG at 1:1, `IPartDoc.ExportToDWG2`. **[SW help]**

Things the settings can't fix, to look for in the test:

| Concern | What I expect | Status |
| --- | --- | --- |
| **Scale** | With 1:1 off, a 1:10 sheet comes out as an A3-sized picture, with the steel drawn at one tenth size. With 1:1 on, one view scale is made true size and the border is blown up to suit. The help says the program can't choose which scale or tell sheet scale from view scale. Views at other scales (details, isometrics) end up the wrong size relative to it | **[SW help]** for the limit, rest **[inferred]** |
| **Dimensions** | They arrive as real AutoCAD dimensions, but not tied to the geometry. Old forum reports say values can change by the scale factor when edited in AutoCAD if the export was not 1:1 | **[web]**, old reports; must test |
| **Paper space** | I believe the whole sheet lands in the layout as flat linework, not as a model with a scaled viewport looking at it, which is how an AutoCAD drafter would build it | **[inferred]** |
| **Title block** | Loose lines and text, not a block with fillable fields | **[inferred]** |
| **BOM, cut list, revision table** | Lines and text, not AutoCAD tables | **[inferred]** |
| **Fonts** | TrueType text needs the same font on the recipient's PC or it is swapped and shifts. SOLIDWORKS has a font swap list (`data\drawfontmap.txt`) **[PC]**. We met exactly this on Jerry's job: missing fonts moved text | **[PC]** |

### Route 2 in detail: the AutoCAD clean-up pass

What is on this PC **[PC]**:

| Item | Found |
| --- | --- |
| AutoCAD | **Full AutoCAD 2027.1** (not LT), `C:\Program Files\Autodesk\AutoCAD 2027` |
| Windowless batch program | `accoreconsole.exe` is there |
| Ways to drive it | Automation from outside (registered as `AutoCAD.Application.26`), AutoLISP and script files, and the .NET libraries (`accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll`) |
| Standards checker | `DwgCheckStandards.exe`: checks drawings against a standards file (`.dws`) in batch |
| Autodesk's own "AutoCAD MCP Server" | Installed (version 26.0.460). Autodesk's pages say it is a preview, read-only in plain AutoCAD, and supported only with Autodesk's own assistant **[web]**. Not our route |
| Python | 3.12 with ezdxf 1.4.4 (reads and writes DXF), pywin32 (drives AutoCAD) and PyMuPDF (PDF compare) |
| Not installed | ODA File Converter, DraftSight |

What the pass would do to each exported DWG:

| Step | Detail |
| --- | --- |
| 1. Layers | Rename and merge to the recipient's layer list (or a plain house list: outline, hidden, centre, dimensions, text, title block, table), with their colours and line types |
| 2. Text and dimension styles | One text style in a font the recipient has; one dimension style; set so values stay right if edited |
| 3. Title block | Replace the loose linework with a proper AutoCAD block whose fields (number, description, revision, scale, date) are filled from the same properties as the SOLIDWORKS sheet. The block is drawn once by hand |
| 4. Layout | One layout named by drawing number, A3, with plot settings so it prints like the PDF |
| 5. Tidy | Purge unused items, audit, set units to millimetres, zoom extents, save in the agreed version |
| 6. Check | Plot the DWG to PDF and compare it with the SOLIDWORKS PDF using the picture-compare tool from Jerry's job. Any shifted text or missing line shows up without anyone opening the file |

How to run it: the method proven on Jerry's job is Python driving the normal AutoCAD window and running AutoLISP routines (`C:\code\jerry`, memory note "autocad-automation-gotchas"). The windowless `accoreconsole.exe` is better for unattended batches, but that note records that it has **no** outside-automation objects, so the Jerry routines would not run in it unchanged; a small .NET add-in is the usual way there **[inferred]**. Start with the proven method and move to the windowless one only if volume calls for it.

## 3. 3D solids for coordination

| Route | Status |
| --- | --- |
| AutoCAD opens the SOLIDWORKS file directly (IMPORT) | Older AutoCAD help lists SOLIDWORKS parts and assemblies, but only up to certain SOLIDWORKS versions (the 2020 page says 2003 to 2010 and 2019) **[web]**. This install has a file that looks like a SOLIDWORKS reader (`ODXSW_DLL.dll`) **[PC]**. Whether it reads **2026** files is unknown; I'd assume not until tested |
| SOLIDWORKS saves STEP or ACIS (`.sat`), AutoCAD imports it | AutoCAD 2027's import modules for STEP, SAT, Parasolid, IGES and IFC are installed **[PC]**. This is the safe route. Result: 3D solids, one per body, no features or history |
| SOLIDWORKS saves a part or assembly straight to DWG | Possible, but it gives flat views or needs care. Not explored |

Notes: the 19 Aug try for Revit produced a 63 MB SAT file from one screen-plus-structure assembly (`docs\research\revit-hook.md`), so size needs managing (leave out fasteners and LED cabinets, or export per subframe). If the recipient is really working in Revit or Navisworks, IFC or a Revit link may suit them better than DWG solids; that is covered in `revit-hook.md`.

## 4. Recommendation

**Route 2: SOLIDWORKS export plus an AutoCAD clean-up pass, added to the release step.**

| Stage | What happens | Who |
| --- | --- | --- |
| Release (today) | Drawing checked, PDF exported by SwCmd, filed as `<number>_<rev>.pdf` | App |
| New: export | The same SwCmd run also saves `<number>_<rev>.dwg` with the agreed settings and map file | App |
| New: clean-up | AutoCAD pass tidies it, plots a check PDF, compares it with the release PDF | App, on a PC with AutoCAD |
| File | DWG filed next to the PDF, only for the drawings or sets that need it | App |

Why this one: it reuses James's finished drawing as is, costs nothing new, and both halves use tools already working here. The clean-up rules can start small (layers, fonts, purge) and grow only as far as the agencies really need. If they turn out to want only variant A or D, the clean-up may be nearly nothing.

What it won't be: a drawing an AutoCAD drafter would have built from scratch (model at true size with scaled viewports and live tables). Getting that means redrawing (route 5), and I don't think it is worth it unless an agency insists.

## 5. A tiny first test

Aim: see what SOLIDWORKS really produces and how far it is from acceptable, before building anything. About an hour, one SOLIDWORKS session.

| Step | What | Who |
| --- | --- | --- |
| 1 | Copy two finished drawings and their models to `C:\scratch\dwg-test\` (not `P:\`, not `C:\PDM-work`): one plate (for example GNRIC-PLP-06, which has ordinate dimensions) and one assembly with a BOM and balloons | Claude |
| 2 | Export each three ways, as both DWG and DXF: (a) model space, 1:1 off; (b) model space, 1:1 on; (c) paper space. R2013, TrueType fonts, no map file yet | Claude, SOLIDWORKS 2026 |
| 3 | Read the DXF twins with ezdxf and report: layer names, counts of each kind of object, whether dimensions are real and what value they measure against what they show, whether tables and title block are blocks or loose lines, fonts used | Claude |
| 4 | Open the six DWGs in AutoCAD and look (table below) | Grant or James |
| 5 | Write up what needs fixing and size the clean-up pass | Claude |

What to check by eye in AutoCAD:

| Check | How | Good result |
| --- | --- | --- |
| True size | Type `DIST` and pick two ends of a known member | Reads the real length in mm |
| Dimensions | Click a dimension, drag one grip, then undo | It is one object, and the value follows the geometry sensibly |
| Text | Double-click a note | Opens as editable text in a normal font, not exploded into lines |
| Layers | Open the Layer panel | A short, sensible list, not dozens of odd names |
| Title block | Click it | Ideally one block with fields (expected to fail on the raw export) |
| BOM / cut list | Click it | Ideally a table (expected to fail on the raw export) |
| Layout | Click the layout tab, then Plot preview | A3 sheet that looks like the PDF |
| Overall | Would you be happy sending this? | Tells us how much clean-up is worth doing |

## 6. Open questions for Grant

1. **Which agencies** have asked, and for which drawings: shop drawings, general arrangements, or setting-out plans?
2. **What do they do with the DWG:** lay it under their plans, edit and re-issue it, or just file it?
3. **Do any have a CAD standard** (layer list, fonts, title block, plot style)? A sample DWG from each that they consider good would settle most of this.
4. **2D only, or 3D solids too?**
5. **Which DWG version** do they want? Mainland offices often ask for older ones.
6. **Our title block or theirs** on the DWG, and do they need Chinese text?
7. **Who else has AutoCAD?** If releases will only ever run on your PC, nothing more is needed. If James or Joey release, they need a seat or the clean-up runs on yours.
8. **Every release or on request?** I'd suggest per set, on request, to begin with.

## Could not verify

- Nothing was run, so every statement about what the exported DWG contains is from the help, old forum posts or my understanding. The first test settles them.
- AutoCAD 2027's own help was unreachable today, so the SOLIDWORKS versions its IMPORT command reads are unconfirmed.
- ODA File Converter licence terms and current DraftSight prices were not checked at the vendors' own sites.
- Whether AutoCAD on this PC is licensed for unattended use beyond what Jerry's job did (it ran in the normal window there).

Sources: SOLIDWORKS 2026 API help on this PC (`api\swconst.chm`, `api\sldworksapi.chm`; same topic online at [help.solidworks.com](https://help.solidworks.com/2026/English/api/swconst/FileSaveAsDXFOptions.htm)); [SOLIDWORKS DXF/DWG export options](https://help.solidworks.com/2024/english/SolidWorks/sldworks/r_dxf_dwg_export_options.htm); [Eng-Tips thread on dimension problems after export](https://www.eng-tips.com/threads/dimension-problems-when-save-as-to-dwg-or-dxf.229439); [AutoCAD IMPORT, SOLIDWORKS versions (2020 help)](https://help.autodesk.com/cloudhelp/2020/ENG/AutoCAD-Core/files/GUID-69622D22-3E9E-4341-9E6C-5EF59B6FF15C.htm); [Autodesk MCP Server help](https://help.autodesk.com/view/ADSKMCP/ENU/) and [support article](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/AutoCAD-Civil-3D-MCP.html); [ezdxf ODA File Converter add-on](https://ezdxf.readthedocs.io/en/stable/addons/odafc.html); [DraftSight pricing (G2)](https://www.g2.com/products/draftsight/pricing); `C:\code\jerry\HANDOVER.md`; `docs\research\revit-hook.md`.
