# Hooking into Revit: research note

13 Nov 2026 · for Grant · research only, nothing built

## In short

Yes, we can hook into Revit, and the useful version is the opposite of what we did for Jerry's AutoCAD drawings. The most valuable thing is to **bring our steel and shop drawings into the building's Revit models so we can check that they fit**: our 3D steel as a linked IFC file, and our released drawing PDFs on sheets beside it. Marking up our shop drawings *inside* Revit is possible but poor: Revit turns PDFs into pictures, and we already decided that comments come back on the PDF (usually Bluebeam, decision R6). Revit can also add tags, dimensions, notes and revision clouds automatically. That is worth doing only if somebody needs Revit sheets from us.

The smallest first step needs no code: one hand trial on one screen (LS-08A-06) in a copy of the 311 structure model. Only if that proves useful should we build a small Revit button that brings in the latest released set from `P:`.

## What I found on this PC (read-only)

| Item | Found |
| --- | --- |
| Revit | **Revit 2024.3.6** only (`C:\Program Files\Autodesk\Revit 2024`). No 2025 or later. |
| AutoCAD | AutoCAD 2027.1 (used for Jerry's job). |
| Revit add-ins | Autodesk's own, plus **Bluebeam's Revit add-in** (Revu 21). None of ours. |
| Models you've opened recently | Copies of the 311 building models in `C:\scratch\RevitLocal`: `311_STR_SHAFTA/B/C`, `311_ST_ARUP`, `311_AR_RLP`, `311_DM_HXT`, `311_DD_WDI` (2.6 GB), `311_DD_UNI_Led`. |
| An earlier SOLIDWORKS-to-Revit try (19 Aug) | `LS-08A-06 SCREEN+STRUCTURE` exported as DWG (1.7 MB), **IFC (3.1 MB)** and SAT (63 MB), plus `SW_IMPORT TEST.rvt`. The IFC import log from that day shows an error ("revitLinkTypeId is not a loaded RevitLinkType") and **0 elements created**. I don't know whether a later try worked. |

The building models opened in Revit 2024, so they are Revit 2024 files (or older ones that were upgraded). An add-in for them must be built for Revit 2024.

## What Jerry's AutoCAD job was, and what carries over

`C:\code\jerry` (one commit, finished 28 Sep) marked up 29 of Jerry's wiring drawings as revision X1.

| Part | How it worked |
| --- | --- |
| Finding changes | Python and PyMuPDF compared the old and new PDFs and planned where the clouds and delta triangles go. No CAD needed. |
| Changing the DWGs | Python drove the **running AutoCAD from outside** through COM (`GetActiveObject("AutoCAD.Application")`, `SendCommand`). AutoLISP routines (`revmark.lsp`) drew the clouds and filled in the title block and revision row. Results came back through AutoCAD's log file. |
| Checking | AutoCAD plotted the PDFs, then Python compared them with Jerry's PDFs (text, linework, red markup only). |
| What went wrong | Sheet-to-model position mapping through viewports (34 sheets once got markup on the wrong sheet), fonts, busy-COM retries, PDF plotter pop-ups. |

**What carries over to Revit:**

| Carries over | Doesn't carry over |
| --- | --- |
| The PDF comparison and cloud placement (`revcloud.py`, `pdfcheck.py`). It's already earmarked for the PDM's AutoCloud (R5) and doesn't care which CAD made the PDF. | Driving the program from outside. **Revit has no COM or command line to drive.** Its API only works inside Revit, as an add-in (Autodesk's Building Coder blog says calling it from outside is not allowed). |
| Lessons on mapping sheet positions through viewports. Revit sheets and viewports have the same problem. | AutoLISP. A Revit add-in is written in C#. |
| Keeping all settings in one config file, and checking each result automatically. | Python. Revit has pyRevit and Dynamo for scripts, but a plain C# add-in is easier to ship to others. |

## What "hook into Revit" could mean

| # | Reading | Realistic? | Useful? |
| --- | --- | --- | --- |
| a1 | **Our 3D steel into the building model:** link an IFC of each screen's steel into the Revit model and check it fits (columns, slabs, shafts, clashes). | Yes. Revit links IFC as standard, and the API can do it too (`RevitLinkType.CreateFromIFC`). SOLIDWORKS 2025 exports IFC 2x3 or IFC 4 and can map custom properties such as `PartNo` into the IFC. | **High.** This is the real check that our steel fits the building. |
| a2 | **Our shop drawings onto Revit sheets:** place the released PDFs (or DWGs) on sheets or drafting views next to the building plans. | Yes. The API can place a PDF page as an image on a sheet (`ImageType` / `ImageInstance`) or link a DWG (`Document.Link`). | **Medium.** It's handy to have them in one place, but a placed PDF is a **picture** (rasterised), not lines you can measure. A DWG keeps the lines. |
| b | **Mark up our shop drawings in Revit** (text, revision clouds on the sheet holding our PDF) and get the comments back to us. | Technically yes (`TextNote`, `RevisionCloud` on a sheet). But comments would live in a Revit file, not on our PDF, and we'd have to dig them out again. | **Low.** Bluebeam (R6) or ACC does this better. Bluebeam's "Connected Sessions" shows markups in Revit only for PDFs that Revit itself printed, and it needs Bluebeam's top (Max) plan, so it doesn't fit SOLIDWORKS PDFs. |
| c | **Automatic annotation of our steel in Revit:** tags with part numbers, dimensions, notes, revision clouds. | Yes. The API covers all of these (`IndependentTag`, `NewDimension`, `TextNote`, `RevisionCloud.Create`, with some limits, e.g. no clouds in 3D views and no tags in unlocked 3D views). Tagging elements *inside a linked IFC* is still to be checked. | **Only if someone needs Revit sheets from us.** Otherwise our SOLIDWORKS drawings already carry this. |
| d | Use the PDM to check out Revit files (the building models). | Possible, but the models are 100 MB to 2.6 GB, belong to others, and Revit has its own sharing system. | Low. Mentioned only so it's ruled out. |

**Recommendation: a1 first, then a2. Leave b to Bluebeam/ACC. Do c only if a client or architect asks for it.**

## The no-code route: ACC Model Coordination

We already have ACC (Autodesk Construction Cloud). Its Model Coordination combines several models in a web viewer and can find clashes, so there's no add-in to install.

| Point | Detail |
| --- | --- |
| Automatic clash finding | Works on RVT, DWG, and "tracked" IFC or NWC files. |
| SOLIDWORKS IFC | **Not on Autodesk's list of trusted IFC sources**, so a SOLIDWORKS IFC can be viewed alongside the building but **doesn't get automatic clash finding**. |
| Workaround | Link our IFC into a Revit file and publish that as RVT. Whether clash finding then reaches the linked steel is **unchecked**. |
| Markups and issues | ACC has an Issues API, but no public markups API. Issues pinned to sheets can't be read through the API yet. |

That makes ACC a good place to *look* at our steel against the building with others, but it's weak for automatic checks unless the steel goes through Revit first.

## Revit add-in facts (checked against Autodesk docs)

| Topic | What's true |
| --- | --- |
| Add-in type | A .NET DLL plus a small `.addin` file in `C:\ProgramData\Autodesk\Revit\Addins\2024\`. It runs as a button on the ribbon. |
| .NET version | **Revit 2024 and older: .NET Framework 4.8. Revit 2025 and newer: .NET 8.** One source can build both. Our PDM app is .NET 8, so a Revit 2024 add-in can't share its code directly, but it can share files and the local page. |
| Talking to the PDM | Simplest: read the release folders on `P:` (e.g. `P:\APEX\SC08\_Review\LS-08X-06_0A\`). The PDM's local page (`http://localhost:5170/api/...`) also works. Revit only allows model changes from its own thread, so a web call has to be passed back to Revit through an "external event". That's standard and workable. |
| Licensing | No extra licence for the API, but it needs **full Revit**. **Revit LT doesn't run add-ins at all.** |
| Cloud Revit (APS Design Automation) | Revit can run in Autodesk's cloud with no desktop, paid in Flex tokens per processing hour. Autodesk raised these prices from late 2025. Too heavy for us now. |

## Proposed steps

| Step | What | Who / effort | Proves |
| --- | --- | --- | --- |
| 1. Hand trial, no code | Export LS-08A-06 steel from SOLIDWORKS as IFC (with `PartNo` mapped). In a **copy** of `311_STR_SHAFTA.rvt`: Insert > Link IFC, line it up with the shaft, look for clashes, then place one released PDF on a new sheet. | You, about half a day. I can write the steps one at a time. | Whether IFC from SOLIDWORKS comes in cleanly (it failed on 19 Aug), whether positions line up, whether it's worth more work. |
| 2. Revit button "APEX steel" | One ribbon button: pick a screen, then the add-in links or reloads that screen's IFC and (optionally) places the latest released PDFs on sheets. It reads from `P:` only, with no AWS. Built for Revit 2024, ready for 2025+. | Dev, about 3 to 5 days including tests. | Repeatable check of each release against the building. |
| 3. IFC at release | The release step also exports an IFC per screen into the set folder, next to the PDFs. | Dev/SW, about 1 day once step 1 settles the export settings. | Every release has matching 3D for checking. |
| 4. Only if asked | Automatic tags/dimensions on our steel in Revit (reading c). | 2 to 3 days. | Revit deliverables, if a client wants them. |

## Risks

| Risk | Why it matters |
| --- | --- |
| Lining up positions | SOLIDWORKS and the building model use different origins. We need to place it once and keep that position when we reload (Revit keeps it), or export against a SOLIDWORKS coordinate system matching the building grid. |
| IFC quality from SOLIDWORKS | Weldments may come in as generic "proxy" objects, and the August import produced nothing. Step 1 settles this. |
| Revit versions | You have 2024 only. If the client moves to 2025 or newer, the add-in needs a .NET 8 build and you need that Revit too. |
| PDFs become pictures | Fine for viewing. Not good for measuring or snapping (snapping to PDF lines exists but has a known scale bug in 2024.2). Use DWG if lines matter. |
| Who has Revit | If only you have full Revit, the add-in is a tool for one person. Revit LT users can't run it. |
| Scope creep | SPEC keeps markups and approvals out of the PDM (in ACC). This work should stay a viewer and checker, not a second review system. |

## Sources

- Revit API .NET 8 move and migration: [Autodesk blog: Revit API is moving to .NET Core 8](https://blog.autodesk.io/revit-api-is-moving-to-net-core-70/), [Migrating from .NET 4.8 to .NET 8](https://jeremytammik.github.io/tbc/a/2035_net_core_8.html)
- PDF on sheets: [ImageTypeOptions](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/981135c3-777b-df9b-747f-60a35b74e00e.htm), [ImageInstance.Create](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/0f4cd250-7925-6714-3e08-2bd7c0266252.htm), [Import and Manage PDF Files](https://help.autodesk.com/cloudhelp/2023/ENU/Revit-Model/files/GUID-87BCF397-1F72-4C1D-991C-FB18656D4DF5.htm), [PDF snap scale issue in 2024.2](https://forums.autodesk.com/t5/revit-architecture-forum/vector-and-graphics-in-linked-pdf-don-t-match-in-revit-2024-2/m-p/12496961)
- DWG and IFC links: [Document.Link (DWG)](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/f3112a35-91c2-7783-f346-8f21d7cb99b5.htm), [RevitLinkType.CreateFromIFC](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/5b944368-4ce5-d523-5fd5-29d0363861ae.htm)
- Annotations: [RevisionCloud.Create](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/090c0da6-db59-d1e6-5dcf-4335c531ee9f.htm), [Revit API annotation elements (Text)](https://help.autodesk.com/cloudhelp/2018/ENU/Revit-API/Revit_API_Developers_Guide/Revit_Geometric_Elements/Annotation_Elements/Text.html)
- API only inside Revit: [External Access to the Revit API](https://blog.autodesk.io/external-access-to-the-revit-api/)
- Revit LT and add-ins: [About Add-Ins (2026)](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-Installation/files/GUID-B51E757E-CEC1-48D8-B188-BF20EE19FC99.htm)
- ACC: [ACC API overview](https://aps.autodesk.com/en/docs/acc/v1/overview/), [Issues API](https://aps.autodesk.com/en/docs/acc/v1/reference/http/issues-issues-GET), [Model Coordination clash file types](https://help.autodesk.com/cloudhelp/ENU/Coord-Clashes/files/Model_Coord_Supported_Files_Clash.html)
- APS pricing: [APS pricing](https://aps.autodesk.com/pricing), [Sept 2025 pricing change](https://aps.autodesk.com/node/3776)
- SOLIDWORKS export: [IFC files (SW 2025)](https://help.solidworks.com/2025/English/SolidWorks/Sldworks/c_IFC_Files.htm), [Export to AEC (RFA)](https://help.solidworks.com/2022/English/SolidWorks/sldworks/HIDD_DVE_AEC_3_SAVETOACIS.htm)
- Bluebeam in Revit: [Connected Sessions](https://support.bluebeam.com/revu/how-to/connect-sessions-revit.html)

## Questions

1. **What did you mean?** Mainly checking our steel and drawings against the building (a), marking up drawings in Revit (b), or Revit tags and dimensions on our steel (c)?
2. **Who uses Revit?** Only you, or James, Daniel or Joey too? Do any of them have full Revit or only LT? Does the client or architect expect anything from us in Revit?
3. **Which Revit version does the client work in?** Still 2024, or moving to 2025 or newer?
4. **The 19 Aug IFC try:** did the IFC ever come into Revit properly, or did you go with SAT or DWG instead?
5. **ACC:** are the 311 building models in an ACC project we can use for Model Coordination?

## Your to-do

1. Answer the five questions above (in Admin).
2. If the answer to 1 is (a): say yes or no to the step 1 hand trial on LS-08A-06, in a copy of `311_STR_SHAFTA.rvt`. I'd give the steps one at a time.
