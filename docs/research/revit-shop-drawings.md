# Revit shop drawings: phase 2 design note

13 Nov 2026 · for Grant · research and design only, nothing built. Follows `revit-hook.md`.

## In short

The four things you want are all possible in Revit 2024 with one add-in we write (C#, .NET Framework 4.8). The order matters, though.

1. **Start with the independent check.** It only reads the Revit model, so it can't damage the shared model, and it pays off straight away: the August/September issues list ("packer and bolts missing", "bar too low", "grout in floor and ceiling") is exactly what it would catch.
2. **Then automate the steel swap.** IFC links swap and reload cleanly, but there are two catches. Linked IFCs **don't show in the ACC web viewer**, and every other user needs the IFC file too. So a one-screen trial has to settle IFC link versus a Revit family rebuilt automatically from SOLIDWORKS (closest to today's STEP + reference planes method).
3. **Keep dimensions off the steel geometry.** Hang them on named reference planes that the add-in moves to SOLIDWORKS positions. Then swapping the steel never deletes a dimension. This is today's reference-plane habit, made automatic.
4. **Versioning:** the shop-drawing models are **Revit cloud models in Autodesk Docs (project `DL_APEX`)**, which already handles locking and versions. Don't put the `.rvt` files in our PDM. Put the inputs (SOLIDWORKS exports) and the issued PDFs and check reports in the PDM, and record which model version each issue came from.

"IEC" is read as **IFC** throughout. Everything on this PC agrees: there's an `IFC files` folder, an IFC4 already inside the shaft A model, and an IFC trial in August. Nothing suggests IEC.

## 1. How the shop drawings are built today

Everything below comes from files, file headers and Revit journals. I didn't open Revit.

### The files

| File | Where | Size, date | What it is |
| --- | --- | --- | --- |
| `311_STR_SHAFTA.rvt`, `_SHAFTB`, `_SHAFTC` | **Cloud models in Autodesk Docs `DL_APEX`** (the file headers say "Cloud Central"). Local copies in `Downloads` (29 Sep) and `C:\scratch\RevitLocal` | A: 755 MB, B: 548 MB, C: 782 MB (29 Sep downloads). Shaft A has **1,334 saves** to the cloud. | The SC08/SC09/SC10 screen-structure shop drawings. Sheets `LED-08A-01` … `LED-08C-17`, titled e.g. "LED SCREEN STRUCTURE DETAIL SC08 SHAFT B LS-08B-04 - VM0804B". |
| `311_STR_SC07.rvt`, `311_STR_SC00_SC12.rvt`, `312_STR.rvt` | Downloads, 29 Sep | 45, 32, 45 MB | Same kind of model for the other scenes and building 312 (assumed from names). |
| `311_DD_UNI_Led.rvt` | Docs `DL_APEX`; copies in Dropbox | 300–960 MB | The LED design model (Unilumin). |
| `311_ST_ARUP`, `311_AR_RLP`, `311_DD_WDI`, `311_DM_HXT` | Docs `DL_APEX` and file servers | 100 MB – 2.6 GB | Other consultants' models (structure, architecture, WDI, HXT), linked for context. |
| `Unilumin Shared\311 SW STRUCTURES\*.STEP` | Dropbox | ~130 STEP files, 0.7–27 MB, 18 Aug – 26 Sep | SOLIDWORKS exports per screen and variant ("FRAME ONLY", "FRAME + SCREEN", "KBEAM (GC SCOPE)" …). Placed ones move to `Old - Been placed in model\`. |
| `Unilumin Shared\311 SW STRUCTURES\*.rfa` | Dropbox | 1–30 MB, 24–27 Sep | Revit families made from STEP (LED carts, frames, sheaths). Category **Generic Models**, **face-based**, one imported STEP shape inside, no part-number parameters. Some families exist in a "GROUT REFERENCED" version. |
| `Adjustments - STEP models into Revit files.rtf` | Dropbox | 18 Sep | Issues found once the STEP steel was in Revit, e.g. "08A-04 packer and bolts missing", "08C-04 steel bar too low", "0806 grout in floor and ceiling, unnecessary 180x180 plates". |
| `PDF for GC\LS-08A-04 FOR GC.pdf` etc. | Dropbox | 11–13 MB, 2 Oct | Printed sheet sets, A1 (841 × 594), **WDI title block** ("HDL-311-00-00", issue/revision table R1–R7, e.g. "90% CONTRACT DOCUMENT (ISSUE FOR TENDER)", "ISSUE FOR CONSTRUCTION"). |

### How the steel gets in

| Finding | Evidence |
| --- | --- |
| STEP is **imported, not linked**. | Shaft A's list of external files (links) has no STEP or IFC, only one CAD link and keynote tables. Inside the model are import names such as `LS-08A-06 STRUCTURE.STEP`, `LS-08A-11 STRUCTURE.STEP`, plus full source paths from a colleague's Dropbox (`...\311 SW STRUCTURES\LS-08A-04 STRUCTURE.STEP`). |
| SOLIDWORKS body names survive the import. | Strings like `EA40X40X5_460-LOWER-STEP<As Machined>`, `LS-08X-07_STEP_FRAME_Cantilever-490<As Machined>`. |
| Some steel came in as families. | The face-based Generic Model `.rfa` files above. |
| An IFC has already been used once. | Shaft A contains 181 references to `LS-08ABC-07-STR (IFC4)(1).IFC` from another user's desktop, apparently brought in as elements rather than linked. |
| Revit 2024 can't *link* STEP. It can only import it, from 2024.2. Linking STEP arrives in Revit 2025. | Autodesk support article, Revit 2025 What's New. |

### Annotation

| Item | What I can tell |
| --- | --- |
| Views | Per screen: a plan ("LS-08A-06 VM 0806A - PLAN VIEW"), numbered sections ("Section 101", "Section 53" …), elevations, location views, a K-beam view from under. |
| Dimensions | Aligned dimensions, mostly type **"4mm-1"**, plus "Diagonal_ProjectUnit_Transparent" and radial dimensions. In mm (e.g. 500, 2670, 3336 on LED-08A-04). |
| Tags and notes | Tag families in shaft A include Generic Model Tag, Structural Framing Tag, Callout Tags and WDI's "Super Tag" (tag lookup). Bolt sizes "M14", "M6" appear on the sheets; I can't tell which tag family places them. Text notes ("TYP1 - RHS 80X40 SPLICE JOINT. REFER LED-TYP-01"), section and detail callouts (LED-08A-05A …). Keynotes in use. |
| Schedules | Shaft A has a **"STEEL MEMBER SCHEDULE"** and a revision schedule per sheet. |
| Extra views | Per screen: "REFERENCE 3D", "ISOMETRIC", "ISOMETRIC WITH LED", "DIMENSIONED PLAN VIEW", "REFERENCE VIEW", plan callouts. Steel names inside include SOLIDWORKS sub-assemblies (`LS-0807-SUB-ASSEMBLIES_PART1-LR` …). |
| Revisions | WDI's revision table on the sheet (issue numbers 1–7, R1–R7), with "ISSUED FOR COMMENT" and "ISSUE FOR CONSTRUCTION" among the descriptions. Revision clouds are used. **Not** our 0A/0B codes. |
| Reference planes | You said steel is placed with reference planes. Journals confirm reference-plane use but not how many or what they're named. |

**I can't tell without opening the models:** which dimensions are attached to the imported STEP geometry versus reference planes or grids; which tag family places which tag; what the steel member schedule lists and how it counts; who owns which worksets; and the exact sheet list per model. A read-only "inventory" command in the add-in answers all of this in one run, and it's the first thing to build.

## 2. Design for the four needs (Revit 2024)

The add-in is one ribbon tab with a few buttons, built for Revit 2024 (.NET Framework 4.8). It reads its inputs from `P:` (our PDM) and makes no AWS calls. Every run that changes the model is one Undo-able step and writes a log.

### Need 1: replace STEP with IFC that updates itself

| Step | How (Revit 2024 API) |
| --- | --- |
| Export | SOLIDWORKS saves each screen as IFC (2x3 or 4) using a **named output coordinate system** per screen (SOLIDWORKS supports this for IFC), with `PartNo` mapped into the IFC properties. Driven by SwCmd so names and folders are fixed: `P:\APEX\SC08\_Revit\LS-08A-06\LS-08A-06_STEEL.ifc`. |
| First link | Open the IFC for linking (`Application.OpenIFCDocument` with link options), then `RevitLinkType.CreateFromIFC` plus a link instance. These are documented calls, but the exact sequence needs the trial: the 19 Aug try failed with "revitLinkTypeId is not a loaded RevitLinkType". |
| Placement | Once per screen: the add-in places the link so the SOLIDWORKS coordinate-system origin lands on a known point in the building (e.g. the screen's grid/level point). It stores that transform in a small settings file per screen. A reload keeps the position, and the add-in re-applies the transform and checks it. Shared coordinates are the alternative if the SOLIDWORKS models are ever built in building coordinates. |
| Swap | "Update steel" reloads every screen whose IFC is newer (`RevitLinkType.UpdateFromIFC` / reload), then runs the annotation check below. |

**Two catches with IFC links (verified):**

| Catch | Effect |
| --- | --- |
| Autodesk: linked IFC models **are not shown in the ACC/BIM 360 web viewer** of the host model. | WDI reviewing in the browser won't see our steel. |
| Other users of the cloud model need the IFC at a path they can reach. Our `P:` isn't theirs; links through Desktop Connector are possible but fragile. | WDI or other teams opening the model may see "link not found". |

**The fallback, closest to today:** the add-in builds or updates one **family per screen**. It imports the SOLIDWORKS steel (SAT; the API supports SAT import into a family) and adds **named reference planes** at SOLIDWORKS positions, then loads the family into the model, overwriting the old version. The family lives inside the model, so everyone sees it, ACC included. The cost is model size (7–30 MB per family, as now) and no per-part tags. **The trial tests both, and you pick.**

### Need 2: keep dimensions and tags right when the steel changes

**What happens on a swap (verified where possible):**

| Annotation hosted on… | After the steel is swapped |
| --- | --- |
| Imported STEP geometry (today) | Deleted with the old import. Revit doesn't list which ones. |
| Geometry in an IFC link | **Fragile.** Users report dimensions to IFC links are lost when the IFC is re-exported, because element IDs (GUIDs) change. Whether SOLIDWORKS keeps the same GUIDs between exports is **unknown**, and the trial tests it. |
| Tags on elements in a link | From Revit 2022, a tag remembers the linked element's ID and re-attaches after reload *if the ID still exists*. Otherwise it becomes "orphaned" (the API can detect this with `IndependentTag.IsOrphaned`). |
| Named reference planes in the host model or in the family | **Survive.** The plane is the same element, so the add-in just moves it and the dimension value updates. |

**Design:** dimensions go on named reference planes (`SW:08A06:FOOT-L`, `SW:08A06:FRAME-TOP` …). The add-in creates them and moves them to positions exported from SOLIDWORKS. Tags go on the steel itself (IFC elements carry `PartNo`), with a re-attach step by GUID or `PartNo` if they come back orphaned.

**Detect and repair, every swap:**

1. Before the swap, record every dimension and tag in that screen's views: ID, view, what it's attached to, value and text position.
2. After the swap, compare and list what was deleted, what changed value, which tags are orphaned, and which dimensions have overridden text. Revit's own warnings get captured as well.
3. Auto-fix only the safe things: re-attach orphaned tags by `PartNo`, re-create deleted dimensions between named planes, nudge tags or text that now overlap, and widen view crops to fit new steel. Anything else is listed for a person.

### Need 3: check the sheets against SOLIDWORKS

| SOLIDWORKS side (exported to JSON per screen) | Source |
| --- | --- |
| Part numbers, quantities, cut-list lengths | Document Manager (no SOLIDWORKS needed), as the PDM already does |
| Key dimensions (dimensions named with a prefix, e.g. `RV_`) and key points (named coordinate systems or points) | SwCmd (a new `revit-export` verb; SOLIDWORKS running) |
| Bolt counts by size (M14, M6 …), fastener components | SwCmd tree / Document Manager references |
| Placement origin, overall size, mass | SwCmd |

| Revit side (read-only) | API |
| --- | --- |
| Sheets for the screen, views on each sheet | `ViewSheet`, `GetAllPlacedViews` |
| Each dimension's value, overridden text, attached references | `Dimension.Value`, segments, `ValueOverride` |
| Tag text, text notes, schedules, title block and current revision | `IndependentTag.TagText`, `TextNote`, `ViewSchedule` table data, sheet revision API |

**Checks in the report (Excel and on screen, click to zoom to the item):**

| Check | Example |
| --- | --- |
| Mapped dimension = SOLIDWORKS value (±1 mm) | "LED-08A-09, Section 53: 2670, SW says 2690" |
| Bolt tags per size = SOLIDWORKS fastener count | "M14: 46 on sheets, 48 in model" |
| Part numbers on sheets exist in the model, and vice versa | "08A06-HBW-03 not shown on any sheet" |
| Steel member schedule = SOLIDWORKS cut list (member, length, quantity) | "RHS 80x40, 2670: schedule 4, cut list 6" |
| Steel position = SOLIDWORKS placement | "LS-08A-06 link moved 12 mm since last check" |
| Sheet hygiene | Overridden dimension text, orphaned tags, dimensions deleted since last check, revision row ≠ latest issue |

"Mapped" means each important dimension is linked once to a SOLIDWORKS name. An engineer picks the dimension and the name in a small dialog, and the add-in stores the link on the dimension itself (Revit's per-element data storage). From then on it's checked every run. Unmapped dimensions are listed as "not checked", never silently passed.

**Bonus from the Jerry job:** the PDF comparison that found Jerry's changes can compare two Revit PDF issues, and the add-in can then draw Revit revision clouds on the sheet (`RevisionCloud.Create` works directly on sheets, in sheet millimetres, so no viewport mapping is needed).

### Need 4: version control for the Revit files

| Option | Verdict |
| --- | --- |
| Put `.rvt` in our PDM with check-out | **No.** The models are cloud-workshared in Docs `DL_APEX`: Revit already handles who-edits-what (element borrowing) and keeps versions. A second lock system would fight it, and 0.5–0.8 GB files on every PC's `P:` and in S3 per version is too much. |
| Revit worksharing in ACC (as now) | **Yes, keep it.** It's the master for the `.rvt`. |
| File-based Revit central files in Dropbox | **No.** Old copies exist (`Unilumin-Private\...\Models\Revit\311_STR_SHAFTA.rvt`, Aug). Dropbox and Revit central files corrupt each other. |

**What the PDM adds:**

| Item | Where |
| --- | --- |
| SOLIDWORKS → Revit inputs: IFC/SAT per screen, the JSON check data, placement settings | `P:\APEX\<Scene>\_Revit\<Screen>\`, versioned in S3 like everything else |
| Each Revit issue: the PDF set, the check report, and which cloud model version it came from (version GUID / save count, as in the file header) | The release set folders we already use (R8) |
| Optional: a detached copy of the model at formal issues only | An archive area that isn't synced to every PC |

**Revision codes:** the Revit sheets carry WDI's revision table (issues 1–7, R1–R7), not our 0A/0B codes. The add-in can check that each sheet's current revision matches the issue being made. Which scheme applies is a question for you.

## 3. Can it run unattended?

| Way | Works? | Limits |
| --- | --- | --- |
| Add-in button, run by a person in Revit | Yes | The baseline. One click per screen or per model. |
| Add-in reacting when the model opens or Revit is idle | Yes | Only while someone has Revit open. Changes to a cloud model still need a sync. |
| Scheduled job launching desktop Revit with a journal file (e.g. the open-source Revit Batch Processor, reported working on 2024) | Possible | Needs a PC logged in with Revit and the user signed in to Autodesk. Breaks on unexpected dialogs. The SOLIDWORKS side also needs SOLIDWORKS running (SwCmd). |
| APS Design Automation (Revit in Autodesk's cloud) | Possible | Supports Revit 2022–2024 cloud models but needs a signed-in user token. Paid in Flex tokens, with prices raised from late 2025. Can't see `P:`, so files must be uploaded. A reported IFC-link problem there. Too heavy for now. |

**Honest summary:** desktop Revit has no headless mode. Realistically it's "one click, then walk away" on Grant's PC, possibly overnight on a dedicated PC later. Fully unattended against WDI's cloud model is not recommended: a bot syncing to the client's model is a risk.

## 4. Phased plan

| Phase | What | Effort (dev days, rough) | Value |
| --- | --- | --- | --- |
| 0. Trial on one screen | On a **detached local copy** of shaft A (never syncing to Docs): read-only inventory of LS-08A-06's sheets, dimensions, tags and hosts. Export LS-08A-06 as IFC (with coordinate system and `PartNo`) and as SAT. Try both IFC link and family swap. Change one plate in SOLIDWORKS, swap again, and record what survives. | 2–3, plus 2 h of your time | Settles IFC versus family, GUID stability and placement. Everything else depends on it. |
| 1. Check report (read-only) | SwCmd `revit-export` + Document Manager → JSON; add-in reads sheets → report; dimension mapping dialog. | 5–7 | Catches the "bolts missing / bar too low" class of error before issue. Safe on the live model. |
| 2. Steel swap | "Update steel" for IFC or family, placement settings, named reference planes driven by SOLIDWORKS. | 5–8 | Removes the manual STEP re-import. |
| 3. Annotation repair | Before/after snapshot, orphan and deletion report, safe auto-fixes. | 5–10, done in rounds | Fewer broken dimensions after each change. |
| 4. PDM link | Inputs in `_Revit` folders, issue record with model version, PDFs and check report in the release set. | 2–3 | Traceable SOLIDWORKS version → Revit issue. |
| 5. Optional | Revit revision clouds from PDF comparison (Jerry's method). | 3–4 | Faster revision marking. |

## Risks

| Risk | Mitigation |
| --- | --- |
| The model is WDI's cloud model; an add-in mistake reaches everyone on the next sync. | Trials on detached copies; the check is read-only; changes are one Undo-able step with a log; never auto-sync. |
| IFC links invisible in the ACC viewer and missing for other users | Settle in phase 0; family fallback. |
| SOLIDWORKS IFC GUIDs change every export | Dimensions on named planes, tags re-attached by `PartNo`. |
| Placement drift between SOLIDWORKS and the building | One stored transform per screen, re-checked every run. |
| Revit version: must stay 2024 for submission | Build for 2024 (.NET 4.8); a 2025+ build later is a recompile. |
| Other Revit users (the China team edits these models) | Add-in installed per PC by a small installer; the check can run anywhere, and swaps by one person only. |
| Mapping dimensions is manual work | Do the key ones only (overall, foot positions, heights); the rest are reported "not checked". |

## Sources (checked 13 Nov 2026)

- STEP in Revit: [How to import STEP into Revit](https://autodesk.com/support/technical/article/How-to-import-STP-file-directly-into-Revit), [Revit 2025 Import/Export STEP](https://help.autodesk.com/cloudhelp/2025/ENU/Revit-WhatsNew/files/GUID-B33822FF-03BC-46E3-915A-E36D906262FC.htm)
- IFC link API: [RevitLinkType.CreateFromIFC](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/5b944368-4ce5-d523-5fd5-29d0363861ae.htm), [UpdateFromIFC](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/a3196dab-5524-da34-df34-d01034e98df1.htm), [Application.OpenIFCDocument](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/84e92ca4-6c6a-af82-454e-1c0b7b145398.htm), [IFCImportOptions](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/f98f40e2-dbab-4b4c-7fcb-36df9b35cad5.htm)
- SAT import: [Document.Import (SAT)](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/c71d8882-24ad-1621-13f3-c83a8be8ef85.htm)
- Links and annotation: [Linked IFC not visible in ACC viewer](https://autodesk.com/support/technical/article/Linked-IFC-file-is-not-visible-in-the-Revit-model-in-the-BIM-360-viewer), [IFC link via Desktop Connector](https://autodesk.com/support/technical/article/Unable-to-add-new-or-reload-an-IFC-link-via-Desktop-Connector-in-opened-Cloud-model-in-Revit-2018-3-3), [Dimensions lost on linked IFC update](https://forums.autodesk.com/t5/revit-structure-forum/linked-ifc-files/m-p/14139578), [Tags re-host to links (Revit 2022)](https://help.autodesk.com/cloudhelp/2022/ENU/Revit-WhatsNew/files/GUID-6826CEB0-C896-49FF-90BB-5BCE11134DAB.htm), [IndependentTag.IsOrphaned](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/4906fa9c-e424-92e7-0b7e-15389726ca78.htm), [Tagging linked elements](https://jeremytammik.github.io/tbc/a/1750_tag_linked_elem.html), [Dimensions to linked elements via API](https://forums.autodesk.com/t5/revit-api-forum/proplem-with-creating-dimensions-based-on-objects-from-linked/td-p/7349926), [RevisionCloud.Create](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/090c0da6-db59-d1e6-5dcf-4335c531ee9f.htm)
- SOLIDWORKS IFC: [IFC files (2025)](https://help.solidworks.com/2025/English/SolidWorks/Sldworks/c_IFC_Files.htm), [FileSaveAsIFCOptions](https://help.solidworks.com/2025/English/api/swconst/FileSaveAsIFCOptions.htm), [Output coordinate system for IFC](https://3dswym.3dexperience.3ds.com/post/catia-buildings-infrastructure/solidworks-ifc-export-model-simplification-new-coordinate-system_VTBdp8i5SxaCZyxQZhvI6g)
- Unattended: [External Access to the Revit API](https://blog.autodesk.io/external-access-to-the-revit-api/), [Revit Batch Processor](https://github.com/bvn-architecture/RevitBatchProcessor), [Design Automation: Revit cloud models](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/revit_specific/revit-cloud-model-integration), [APS pricing change, Sept 2025](https://aps.autodesk.com/node/3776)

## Questions

1. **Which models are "ours"?** Is it `311_STR_SHAFTA/B/C`, `311_STR_SC07`, `311_STR_SC00_SC12` and `312_STR`? Who edits them: you, the China team, anyone at WDI?
2. **Does WDI need to see our steel in their own Revit and in the ACC browser viewer,** or only on our printed sheets? This decides IFC link versus family.
3. **Will WDI accept an add-in being used on their cloud model,** and are there rules for that project?
4. **Which revision scheme do the Revit sheets follow:** WDI's issue table (1–7, R1–R7) only, or also our 0A/0B codes?
5. **How do the SOLIDWORKS screen models relate to the building?** Is there a point (grid/level) per screen we can use as the placement origin, or do they already sit in building coordinates?
6. **The "STEEL MEMBER SCHEDULE" in shaft A:** is it on the issued sheets, and should the check compare it line by line with the SOLIDWORKS cut list?
7. **OK to run the phase 0 trial** on a detached copy of shaft A, screen LS-08A-06, with nothing synced to Docs?

## Your to-do

1. Answer questions 1 to 7 above (in Admin).
2. If yes to 7: say which LS-08A-06 SOLIDWORKS assembly is current. I'll then give the SOLIDWORKS and Revit steps one at a time.
