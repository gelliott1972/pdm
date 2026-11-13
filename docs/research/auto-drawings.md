# Automatic first-draft drawings: proposal

13 Nov 2026. Research only: SOLIDWORKS was not started. Counts come from Document Manager reads of `P:\APEX`. API calls were checked against the SOLIDWORKS 2026 API help installed on this PC, and a prototype was compiled against the 2026 libraries but never run.

## In short

- **Yes, it's worth doing, but in two halves.** For simple parts (plates, brackets, single members, simple weldments), a tool can produce a draft that matches James's sheet: views, scale, title block, cut list or BOM, balloons, the Filename/Config/Weight/Finish note, and centre marks. The dimensions on those drafts will still need James's hand.
- **The bigger, more reliable win is copying a drawing James has already made.** 34 of the 81 undrawn items are configurations inside six family files where no configuration has a drawing yet. Another 18 are shaft B and C subframes whose shaft A twin is already drawn. Copying a finished drawing and pointing it at the sibling keeps all of James's dimensions and notes. The tool then reports any that broke.
- **Where we are:** 311 numbered items, 230 drawn, **81 undrawn**. Of the 81, 51 are simple (63%), 28 are subframe assemblies and 2 are structures.
- **Honest size of the payoff:** at James's pace of about 6.8 drawings a day, 81 is roughly 12 working days. Building this takes about 1.5 weeks elapsed. It pays off on SC08 only if the copying works well, and it pays off clearly if more scenes follow the same pattern.
- **Recommendation:** run a pilot of about 5 of each type (25 drafts). James times each draft against drawing from scratch. Keep going only if a draft saves him at least half the time.

## 1. What's undrawn (13 Nov snapshot)

These are the same rules as `pdm coverage`: one item per PartNo; FAST-, MDL- and K-beam skeletons are left out, as are PartNos that exist only as As Welded configurations. "Drawn" means a drawing is named by the PartNo or its title block shows it. My counts match the app's 9 Nov figures for 0806 (59/62), 0811 (44/44), GNRIC (59/69) and SC08 `_Shared` (10/11). 0804 has since grown to 58 of 125 drawn, because James's 0804 drawings came into the vault on 13 Nov.

| Type | Undrawn | Examples | Best route |
| --- | --- | --- | --- |
| Plates / packers (PLP) | 5 | 08X04-PLP-01, -02, -03; -05 and -06 are K-beam angle pieces | New draft |
| Brackets (BKP) | 10 | 08X04-BKP-01 to -07, 08B04-BKP-01 to -03 (K-beam brackets, 1 to 3 pieces); 4 of them in one family file | New draft; copy across the family |
| Single members (SFP / HBP / BRP) | 12 | 08B04-SFP-03, 08C04-HBP-01; several are really small multi-body weldments | New draft |
| Simple weldments (HBW / BRW / SFW / LMW / MSW) | 19 | GNRIC-LMW-01 to -12 (10 configurations of one file), 08A04-HBW-02, 08B04-BRW-01 | One drawn, then copy across the family |
| Small assemblies (BKA, MSA) | 5 | 08X04-BKA-01 to -04 (one family), 08X04-MSA-01 | New draft, then copy |
| Subframe assemblies (SFA) | 28 | 08B04-SFA-2T, 08C04-SFA-45 | 18 have a drawn shaft A twin: copy and repoint |
| Structure assemblies (STA) | 2 | 08B04-STA-01, 08C04-STA-01 | Views and BOM only; James does the rest |
| **Total** | **81** | 0804: 67, GNRIC: 10, 0806: 3, SC08 `_Shared`: 1 | |

Notes:
- **0804 is changing today.** The plates job affects six of these items (08A04-HBW-02/-06, 08B04-SFP-01/-02, 08C04-SFP-02/-03). Each becomes an assembly plus a new weldment-only part, so 0804's numbers will shift slightly.
- **Six family files have no drawn configuration yet:** GNRIC-LMW (10), 08B04-SFA (7), 08C04-SFA (7), 08X04-BKA (4), 08X04-BKP (4), 08X06-SFW (2). That's 34 items where one good drawing can become many.

## 2. What James's drawings look like (all 230 read)

| Feature | What every drawing has | What it means for generated drafts |
| --- | --- | --- |
| Sheet | One A3 sheet (420 × 297) per drawing | Always A3, one sheet |
| Sheet format | James's Unilumin format: `unilumin revised.slddrt` (137) or `unilumin.slddrt` (93), stored on his PC (`c:\007jimdesign\...`, `c:\z_old hard drive\...`). None of his templates are on this PC or in `P:\_Shared`. | James's templates need to go into `P:\_Shared\_Templates` first (as SPEC already intends) |
| Projection | The sheet setting is **third angle** on all 230 | Use `Create3rdAngleViews2`, but see the projection-symbol question |
| Scale | 1:5 (53), 1:10 (50), 1:20 (40), 1:25 (35), 1:2 (18), 1:1 (12), then 1:15, 1:50, 1:12 | The tool picks from this list to fit the model |
| Views | Usually 4 to 6: front, a side or top, one or two isometrics, sometimes a detail | Front + top + right + iso |
| Title block | Description, DWG NO, PART NO, scale, size, sheet, REV. Links: `$PRPSHEET:"PartNo"`, `"Description"`, `"SW-File Name"` (SW D25). REV comes from the drawing property `Revision-Sheet1` | Correct automatically if the sheet's property view is the front view on the PartNo's configuration |
| Note block | "Filename / Config name / Material / Weight (kg) / Finish". Models carry `Weight` (linked to SW-Mass), `Finish` and `Description` | Comes with the template |
| Revision table | All 230: REV / DATE / DESCRIPTION / ZONE / DRAWN / CHECKED / APPROVED, top row 0A | Leave empty in a draft; James adds 0A when he releases |
| Parts lists | 124 assembly drawings: BOM with ITEM NO. / QTY. / DESCRIPTION / PartNo, top left. Weldments: cut list ITEM NO. / QTY. / DESCRIPTION / LENGTH | Insert using James's table templates |
| Dimensions | Plates: ordinate (GNRIC-PLP-06) or linear with hole callouts ("4 x 15.0 X 25 THRU", "6 x Ø13.5 THRU"). Weldments: overall and member lengths, drainage-hole notes, fillet weld symbols "5 TYP". Assemblies: ordinate reference dimensions in brackets, balloons on the isometrics | This is the hard part (see 3) |

Rendered examples (from the 0A PDFs) are in `C:\Users\grant\AppData\Local\Temp\claude\87c3b6fbed1c4b3f\02fef17b-2a81-44d1-9910-b91a4106fb7b\scratchpad\ad\img\`.

**Release checks a draft must pass** (`src\Pdm\Release.cs`, `docs\releases-decisions.md`):
- The title block's model carries the drawing's number as its PartNo, and is not from another shaft.
- No white text or lines.
- Title-block REV = top row of the revision table = file code.

A generated draft passes the first two by construction: it always uses the same-name model in the same folder, and annotations use the default colour. The third is James's step at release.

## 3. The SOLIDWORKS 2026 route

Each call was checked in the 2026 API help on this PC (`api\sldworksapi.chm`). Most were also compiled against the 2026 libraries in `scratch\auto-drawings\MakeDrawing.cs`.

| Step | API | Reliable? |
| --- | --- | --- |
| New drawing from James's template, A3 | `ISldWorks.NewDocument(template, swDwgPaperA3size, 0.42, 0.297)` | Yes |
| James's layout in one go | Template with **predefined views** + `IDrawingDoc.InsertModelInPredefinedView(model)` (no dialogs when a file name is given) | Yes. This is the best way to match his layout |
| Or: standard 3 views + iso | `Create3rdAngleViews2(model)` + `CreateDrawViewFromModelView3(model, "*Isometric", x, y, 0)` | Yes |
| Right configuration | `IView.ReferencedConfiguration` | Yes |
| Scale | `ISheet.SetScale` from the model's bounding box, picked from James's list | Yes (layout may need nudging) |
| Title block | `ISheet.CustomPropertyView` = front view; template links do the rest | Yes |
| Weldment cut list | `IView.InsertWeldmentTable(..., config, template)` | Yes |
| Assembly BOM | `IView.InsertBomTable6(...)` with James's BOM template (PartNo column) | Yes |
| Balloons | `IDrawingDoc.CreateAutoBalloonOptions` + `AutoBalloon5` | Yes for BOM; to confirm for cut lists |
| Centre marks | `IView.AutoInsertCenterMarks2` | Yes |
| Model dimensions | `IDrawingDoc.InsertModelAnnotations4` (marked-for-drawing dimensions, Hole Wizard callouts) | Partly: depends on how the model was built; weldment layout sketches give poor dimensions |
| Auto dimension | `IDrawingDoc.AutoDimension` (ordinate / baseline / chain) | Partly: decent on a plate's front view, messy on weldments |
| Hole callout on a picked hole | `AddHoleCallout2` | **No for unattended use**: the help says the user must click OK in a dialog |
| Tidy dimensions | `IModelDocExtension.AlignDimensions(AutoArrange)` | Partly |
| Copy to a sibling | `IView.ReferencedConfiguration` (same family) or `IDrawingDoc.ReplaceViewModel` (Tools > Replace Model) | Yes for families; good for shaft twins if B/C were copied from A |
| Broken annotations after a copy | `IAnnotation.IsDangling` | Yes: the tool lists them for James |
| Save next to the model | `IModelDocExtension.SaveAs3`, named by the PartNo (e.g. `GNRIC-LMW-01.SLDDRW` beside `GNRIC-LMW.SLDPRT`) | Yes (see 5 for how it enters the vault) |

**What a "good enough first draft" is, per type:**

| Type | Tool does | James does |
| --- | --- | --- |
| Plate / packer | Front + edge + iso, scale, title block, note block, centre marks, ordinate dimensions on the front, Hole Wizard callouts | Check the dimensions, add callouts for non-Hole-Wizard holes and drainage notes |
| Single member / bracket | Views, cut list, overall length, callouts where possible | Hole positions, end details, notes |
| Simple weldment | Views, cut list with lengths, balloons, overall dimensions | Weld symbols, hole and drainage details, a detail view if needed |
| Family sibling (copy) | Everything James drew on the first one, on the new configuration; broken dimensions listed | Check the listed broken items and fix them |

## 4. Complex assemblies (SFA, STA)

| Can automate | Needs James |
| --- | --- |
| Views (front, top, side, 1 or 2 isos) at a sensible scale | Which sections and details matter |
| BOM (top level, PartNo column), anchored top left | Setting-out (ordinate) dimensions and which edges they measure from |
| Balloons on the isometric | Weld symbols, installation notes |
| Title block, note block, revision table | Hiding components, display states |
| **For SFA: copy shaft A's drawing to B and C** (18 of 28) | Fixing what the copy broke (the tool lists it) |

The copy route also fixes a real error. 08B06/08C06-HBW-03 went out at 0A showing shaft A's part because a copied drawing was never repointed. A tool that copies **and** repoints, then checks, prevents that class of mistake.

## 5. Recommended plan

**How it fits the PDM**
- New SwCmd commands run on Grant's PC through the Desk/SwCmd lock, one job at a time, in batches.
  - `make-drawing` makes a new draft.
  - `clone-drawing` copies a drawing to a sibling configuration or model.
- SwCmd never creates files in the vault except from a list. The drafts are named in a round list (as `new-asm --round` does), saved beside their models, and checked in as new files with the note "auto draft".
- Each draft carries a drawing property `AutoDraft` (date, tool). The PDM page can list "drafts waiting for James". The release step refuses a drawing that still has `AutoDraft`, so an unchecked draft can never go out. James deletes the property when he's happy, which is his approval.
- A drawing keeps its model's number (title block from the model's PartNo); the release step makes the PDFs as today.

**Pilot (about 25 drafts)**

| Group | Candidates |
| --- | --- |
| Plates | 08X04-PLP-01, -02, -03 + 2 GNRIC plates already drawn (to compare with James's) |
| Brackets | 08X04-BKP-02 to -05 (family), 08B04-BKP-02 |
| Single members | 08B04-SFP-03, 08C04-HBP-01, -02, 08X04-SFP-04, 08Z04-SFP-01 |
| Weldments (family copy) | James draws GNRIC-LMW-01 by hand, the tool copies it to -02 to -06 |
| SFA (twin copy) | 08B04-SFA-2T, -3T, -5T, 08C04-SFA-2T, -3T from shaft A's drawings |

James times each one (fix the draft vs start fresh). Go on if the median saving is at least half.

**Effort (rough)**

| Step | Who | Effort |
| --- | --- | --- |
| James's sheet format, drawing template, BOM and cut-list table templates into `P:\_Shared\_Templates` | James | 30 min |
| Three seed templates with predefined views (plate, weldment, assembly) in James's layout | SW + James to look | 1 day |
| SwCmd `make-drawing` | Dev | 2 days |
| SwCmd `clone-drawing` (configuration switch, Replace Model, broken-annotation list) | Dev | 1 day |
| `AutoDraft` refusal in the release step; drafts list on the page | App | half a day |
| Pilot runs, James's review, tuning | SW + James (2 to 3 h of his time) | 2 to 3 days elapsed |
| **Total** | | **about 1.5 weeks elapsed** |

**Risks**

| Risk | How we handle it |
| --- | --- |
| Auto dimensions are messy, so James spends as long cleaning as drawing | Pilot timing and the stop rule; fall back to "views, tables and title block only", which still saves the setup grind |
| Models vary (sketch dimensions, Hole Wizard or not) | Pilot covers each type; the tool lists what it couldn't do |
| A copied drawing keeps the wrong model | The tool repoints and checks; the existing release checks catch the rest |
| SOLIDWORKS time on Grant's PC is shared | Batch runs under the SwCmd lock; Close All between sets |
| Templates only on James's PC today | Step 1 above; `ISheet.SaveFormat` can rebuild the sheet format from one of his drawings if needed |
| 0804 is still changing (plates job) | Pilot 0804 items only after that job is checked in |
| SOLIDWORKS 2024 on the same PC | Attach to `SldWorks.Application.34` only |

## What was checked, and what wasn't

| Item | Status |
| --- | --- |
| Undrawn counts | Done and checked: Document Manager reads of all 537 files; matches the app's figures where they overlap |
| James's sheet setup, scales, views, tables | Done and checked on all 230 drawings (sheet size, scale and projection from the Document Manager's sheet properties; index 5 = first-angle flag per the DM help) |
| Layout, notes, dimension styles | Checked by eye on 8 of the 0A PDFs |
| API calls | Checked in the installed 2026 help and compiled against the 2026 libraries. **Not run.** The online help pages are script-rendered and couldn't be fetched |
| Draft quality, dimension import, cut-list balloons | Unknown until the pilot |

Files: `scratch\auto-drawings\DrwProbe.cs` (read-only drawing survey, Document Manager) and `MakeDrawing.cs` (prototype, never run).

Sources: [DM GetSheetProperties](https://help.solidworks.com/2012/English/api/swdocmgrapi/SolidWorks.Interop.swdocumentmgr~SolidWorks.Interop.swdocumentmgr.ISwDMDocument13~GetSheetProperties.html), [DM sheet properties example](https://help.solidworks.com/2019/English/api/swdocmgrapi/Get_Drawing_Sheets_Properties_Example_CSharp.htm), [projection symbols](https://en.wikipedia.org/wiki/Multiview_orthographic_projection), and the SOLIDWORKS 2026 API help on this PC.

## Your to-do

1. Decide whether to run the pilot (about 1.5 weeks elapsed, 2 to 3 hours of James's time).
2. **Projection question for James.** Every sheet is set to third angle, but the symbol printed in his title block looks like the **first-angle** symbol (short side of the cone pointing away from the circles). View placement also varies, e.g. 08A06-SFA-1T has its plan view below the front. Which does he intend? The fabricators need the symbol and the views to agree.
3. Ask James to copy his sheet format, drawing template, and BOM and cut-list table templates into `P:\_Shared\_Templates`.
4. Decide what a draft's revision table shows before James checks it: empty (recommended), or 0A with DRAWN = J MCKILLOP.
5. Decide whether James's approval is simply deleting the `AutoDraft` property, or a button on the PDM page.
