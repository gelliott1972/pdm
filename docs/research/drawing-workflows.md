# Drawing drafts: no dangling dimensions, two workflows, and the handover to James

14 Nov 2026. Research only: SOLIDWORKS was not started, nothing was written to `P:\`, nothing committed. API calls were checked in the SOLIDWORKS 2026 API help on this PC. Item counts come from the 13 Nov Document Manager survey (0804 has changed since with the plates job, so its numbers will have shifted a little).

## In short

- **You're right to push back, and the fix is a rule, not a hope:** a draft is never saved with a dangling dimension. The tool checks every annotation after any copy. Anything that came loose is either re-made fresh on the new model or removed and replaced by a numbered "add this" flag. James gets "missing, here", never "broken, maybe wrong".
- **Dangling only comes from copying.** Anything the machine draws fresh cannot dangle. So fresh drawing is the main route for simple parts, and copying is used only where it clearly pays (families and shaft twins), with the rule above.
- **New finding: James already copies.** 112 of his 122 family drawings and 64 of his 69 shaft-twin drawings carry the same internal view names as a sibling, which means he makes them by copying a drawing and switching it over. The machine would be doing his own method, plus a check he doesn't have (the 0A HBW-03 mistake came from that hand method).
- **Two workflows, as you said.** Simple parts: the machine does the whole drawing and James checks it. Complex assemblies: the machine does the "nuts and bolts" (sheet, views, BOM, balloons, note block, links) and James does the dimensions.
- **Handover:** James learns nothing new. Drafts arrive in the vault like any other file. Each draft has a few magenta numbered flags on the sheet where his eye is needed; he deletes them as he goes and ticks "Draft checked" when he checks in. Release refuses anything still flagged.
- **Tabulated drawings (one drawing, a table of sizes)** would avoid dangling too, but they break our "one drawing per part number" rule and differ from James's 123 existing family drawings. Not recommended now.

## 1. Why dimensions dangle, and how to avoid it

**Why.** A drawing dimension is not attached to "the left edge". It is attached to a hidden tag that SOLIDWORKS gave one particular edge or corner in one particular model. When the drawing is pointed at another model or configuration, SOLIDWORKS looks for the same tag. Found: the dimension updates by itself. Not found: it "dangles" (keeps its old number, changes colour).

| Situation | Tags found? | Basis |
| --- | --- | --- |
| Another configuration of the same file, same features | Yes | Inferred; matches James's own practice |
| Same file, but the feature is suppressed or the count differs (slots on the other axis, an end cap missing) | No, for those dimensions only | Inferred |
| A different file that was made by Save As / Pack and Go from the first | Mostly yes | Help for `ReplaceReferencedDocument` says the internal ID must match and that this is only likely for "a renamed copy" |
| A different file modelled separately | No, nearly everything dangles | Same help page: "dimensions, and other references might fail" |
| Assembly where a component was swapped for a different file | No, for dimensions on that component | Inferred |

**Not known:** whether the shaft B and C files were copied from shaft A or modelled again. That decides how well the twin copies behave, and it's a half-day test (pilot step 1).

**Routes compared**

| Route | Can it dangle? | Verified in 2026 help | Fits which of the 81 | Verdict |
| --- | --- | --- | --- | --- |
| **Fresh by code.** Machine reads the outline and holes, picks what to dimension, adds each dimension (`IModelDocExtension.AddDimension`, edges from `IView.GetVisibleEntities2`), hole callouts as notes | No | Calls exist; not run | 30 "alone" items; any simple part | **Main route for simple parts** |
| **Copy, then clean** (route d). Copy a finished drawing, switch configuration (`IView.ReferencedConfiguration`) or model (`IDrawingDoc.ReplaceViewModel`), rebuild, test every annotation (`IAnnotation.IsDangling`), re-make or remove and flag each loose one | Not in what's saved | Yes. Note: there is no general "re-attach" call (only `ReattachOrdinate`), so repair means delete and re-create | 28 family items + 23 shaft twins = 51 | **Use for families and twins** |
| **Dimensions from the model** (route b). `InsertModelAnnotations4` brings in the model's own dimensions: "marked for drawing", Hole Wizard sizes and positions, hole callouts, weld symbols | No (they belong to the model's features, not to edges) | Yes, all those options exist | Parts whose sketches are dimensioned the way the shop reads them. Unknown for ours until looked at | **Use as a helper** on parts that suit it; good rule for new parts |
| **DimXpert** (route c). Annotate the model once; `IView.ImportAnnotations(... IncludeDimXpertAnnotations ...)` shows it on the drawing; `IDimXpertPart.AutoDimensionScheme` can do it automatically | No | Yes | Plates with holes and slots (parts only) | **Try once on one plate, expect no.** It edits the model files (check-outs), is set up per configuration, and its style is tolerance-heavy, not James's |
| **Tabulated drawing** (route a). One drawing, dimensions shown as letters, a table of sizes per configuration (`ISheet.InsertFamilyTable`, or the model's design table via `InsertFamilyTableNew`) | No | Yes | Pure-length families only: about 7 of the 10 GNRIC-LMW. Not BKA/BKP (slot direction changes), not SFA | **Not now** (below) |
| **One multi-sheet drawing per family** | Same as copy | n/a | Same as families | No: same clash as tabulated |

**Why not tabulated, for now.** SPEC says a drawing has the same name as its model's part number, release files one PDF per part number, and the release check expects the title block's PartNo to equal the drawing's number. One sheet covering ten part numbers breaks all three, and the shop would have to read sizes off a table. James has already drawn 123 family items the one-per-number way. Worth revisiting only if a later scene has long pure-length families.

**What modelling discipline the "from the model" route needs** (a rule for new parts, not a job on the old ones):

| Rule | Why |
| --- | --- |
| Holes made with Hole Wizard | Size and position dimensions and the callout come across automatically |
| Sketch dimensions go from the edge the shop measures from | They appear on the drawing exactly as sketched |
| Only the useful dimensions "marked for drawing" | Keeps construction dimensions off the sheet |
| Weldments: lengths come from the cut list, not dimensions | Layout-sketch dimensions make poor drawing dimensions (as 13 Nov) |

**How the 81 split by route** (13 Nov snapshot)

| Group | Count | Items | Route |
| --- | --- | --- | --- |
| Alone: no sibling or twin drawing exists | 30 | PLP 5, BKP 6, SFP 9, BRP 1, BRW 2, HBW 3, SFA 3 (08A04-SFA-5B, 08B04-SFA-5B, 08C04-SFA-5B), MSA 1 | Fresh |
| Family, same file | 28 | GNRIC-LMW 10, 08X04-BKA 4, 08X04-BKP 4, 08X06-SFW 2, 08BXX-MSW 1, plus 7 SFA configurations in 08B04-SFA / 08C04-SFA that have no shaft A number (-4A to -4D) | One seed drawing per family (fresh or James's), then copy and clean |
| Shaft A twin already drawn by James | 23 | SFA 18, STA 2, HBP 2, HBW 1 | Copy James's shaft A drawing, repoint, clean |

Families need few seeds: one each for BKA, BKP, SFW; two or three for LMW (end types differ); the SFA families are seeded by their shaft A twins.

## 2. The two workflows

### A. Simple parts (plates, packers, brackets, single members, simple weldments): 46 items

| Step | Machine does | API route | Reliability |
| --- | --- | --- | --- |
| Pre-check | Model has PartNo, Description, Material, Weight, Finish; if not, no draft, and it's listed with the reason | Document Manager (already in use) | High |
| Sheet | A3, James's Unilumin format and template | `NewDocument` with his template | High once his templates are here |
| Views | Front, edge or top, isometric, in his layout | Template with predefined views + `InsertModelInPredefinedView` | High (help: no dialogs when a file name is given) |
| Scale | Picked from James's list to fit | `ISheet.SetScale`, `IView.GetOutline` | High; layout may need nudging |
| Title block, note block | Filled by property links | `ISheet.CustomPropertyView` | High |
| Cut list / BOM | His table template | `IView.InsertWeldmentTable` / `InsertBomTable6` | High |
| Balloons | On the isometric | `AutoBalloon5` | Medium; cut-list balloons untested |
| Centre marks | All holes | `AutoInsertCenterMarks2` | High |
| Dimensions | Overall sizes, hole positions from one corner, thickness, member lengths; model dimensions where the part suits | `AddDimension` by code; `InsertModelAnnotations4` | **Medium on plates, unproven on weldments** |
| Hole callouts | Text note on a leader, wording from James's standard | Note (the proper callout call needs a click) | Medium; James to accept the look |
| Self-check | Nothing dangling, nothing overlapping, title block filled; Claude looks at a picture of the sheet | `IsDangling`, PDF export | High for what it tests |
| Family siblings | Copy the checked seed, switch configuration, clean | Section 1 | To be measured |

**James checks:** are the right things dimensioned, from the right edge; weld symbols; drainage and special notes; anything flagged. Target: minutes per drawing, not a redraw.

### B. Complex assemblies (SFA 28, STA 2, BKA 4, MSA 1): 35 items

| Item | Machine does | API route | Reliability |
| --- | --- | --- | --- |
| Sheet + title block | James's format, links live | As above | High |
| View layout | Front, plan, side, one or two isometrics, placed and scaled to fit. Where a shaft A twin exists, his layout comes with the copy | Predefined views, `IView.Position`, `SetScale` | High for placement; "which view is front" needs a rule from James |
| Right model / configuration | Every view on the PartNo's configuration, checked | `ReferencedConfiguration`, `ReplaceViewModel` | High; this is the check that prevents the HBW-03 kind of error |
| BOM | Top level, PartNo column, top left, from model properties | `InsertBomTable6` with his template | High |
| First-pass balloons | Every BOM item once, on the isometric, on their own layer | `CreateAutoBalloonOptions` (layout, layer, ignore multiples) + `AutoBalloon5` | Medium: complete but James will move some |
| Material / finish note block | From model properties | Template links | High if properties are filled (pre-check) |
| Revision table | Present, empty | Template or `InsertRevisionTable2` | High |
| Dimensions | **None by default.** On a twin copy, James's dimensions that are still properly attached stay; loose ones are removed and flagged | `IsDangling`, `GetAttachedEntities3` | To be measured in pilot step 1 |
| Sections, details, hidden parts, weld symbols, setting-out dimensions | Not attempted | | James |

**James does:** dimensions, sections and details, balloon tidy, notes. The machine has done the setup grind.

## 3. The handover loop

```
Pick models  ->  machine drafts a batch  ->  self-check  ->  into the vault as drafts
     ^                                                              |
     |                                                              v
 skills updated  <-  machine compares his version  <-  James fixes flags, checks in
```

| Stage | What happens | New for James? |
| --- | --- | --- |
| 1. Pick | Grant (in Admin) names a batch: a folder, a shaft, or a list of part numbers. Later this can be a "Draft drawings" button on the PDM page | No |
| 2. Draft | SW session runs the batch on Grant's PC through SwCmd, in a staging folder outside the vault. Drafts that fail the self-check never leave staging; they're listed as "not drafted" with the reason | No |
| 3. Deliver | Passing drafts are saved beside their models, named by part number, and checked in as new files with the note "Auto draft, batch N". They are not left checked out to anyone | No: he does Download latest and they're there |
| 4. See what needs him | The PDM page shows "Drafts waiting" with each drawing's short list. The same list is on the drawing as numbered magenta flags on a `PDM-CHECK` layer, each placed where the issue is | One short message from Grant, once |
| 5. Fix | James checks out a draft as normal, works in SOLIDWORKS, deletes each flag as he deals with it | No |
| 6. Approve | At check-in the PDM app shows one tick box, "Draft checked", only for draft drawings. Ticking it clears the drawing's `AutoDraft` property | One tick |
| 7. Guard | Release refuses a drawing that still has `AutoDraft`, and refuses a PDF with any magenta flag left (same kind of check as today's "no white text") | No |
| 8. Learn | The machine compares the draft it made with the version James checked in (both are in vault history): dimensions added, moved or deleted, views moved, scale changed, note wording. SW turns repeated corrections into lines in the `drawing-standards` skill. James can also type a remark in his normal check-in note | No |

**Flags are short and from a fixed list**, at most about six per drawing:

| Flag | Meaning |
| --- | --- |
| DIMS: none placed | Assembly draft; dimensions are James's |
| DIM REMOVED: was 1250 on 08A04-SFA-2T | A copied dimension came loose and was taken off; add it if wanted |
| CALLOUT: plain text | Hole callout is a note; check wording and size |
| BALLOON: item 7 not placed | Auto balloon could not find a clear spot |
| WELDS: not added | Weld symbols are James's |
| VIEW: tight at 1:20 | Layout is crowded; consider another scale |

**Fit with SPEC's rules**

| Rule | How drafts follow it |
| --- | --- |
| Never lose or overwrite work | The machine only ever creates a draft, or re-makes one that no person has checked in since. Once James has touched a drawing the machine will not write to it again |
| Check-out / check-in per file | Drafts go in by a normal check-in under Grant's login (the Desk's PDM step). James locks only what he works on |
| Drawing named as its model | Yes: one drawing per part number |
| Models | Not edited by drafting (one more reason against DimXpert, which would need model check-outs) |
| Coverage counts | Show "drafted, not checked" apart from "drawn", so the 81 doesn't fall until James has approved |

**Feeding corrections back.** After each batch SW writes a short "what James changed" table for Admin. A correction seen twice becomes a rule in the skill. Anything that would change James's standard, not just our copy of it, goes to him as one question through Grant. Three numbers per batch: flags per drawing, James's minutes per drawing, his changes per drawing. All three should fall.

## 4. Revised pilot plan

This replaces exercises 1 to 5 in `claude-drives-solidworks.md`. Scratch copies only until step 6.

| # | Step | What it settles | Owner | Effort |
| --- | --- | --- | --- | --- |
| 0 | James's sheet format, drawing template, BOM and cut-list table templates into `P:\_Shared\_Templates` | Nothing looks like his without them | James | 30 min |
| 1 | **Dangling facts.** SwCmd `drawing-info` also reports dangling annotations and what each dimension is attached to. Then (a) count dangling items in James's 230 today; (b) known-answer test: copy GNRIC-LM-01's drawing, switch to -02, compare with James's own -02; (c) dry run of the 23 shaft A to B/C copies; count what stays attached | Whether copy-and-clean is worth it for families and for twins, with real numbers | Dev builds (half a day), SW runs (half a day) | 1 day |
| 2 | **Simple part, fresh.** GNRIC-PLP-06 (James's exists to compare): sheet, views, title block, then dimensions and callouts by code; then 08X04-PLP-01 and -02 unseen | Whether fresh dimensions are tidy enough; first lines of the skill | Dev (picture and hole-list commands, 1 day), SW (2 days) | 3 days + 1 h James |
| 3 | **Assembly nuts and bolts.** 08A04-SFA-2T fresh (compare with James's), then 08B04-SFA-2T as a twin copy with the clean rule | View layout, BOM, balloon quality; fresh vs copy for SFA | SW | 1.5 days + 30 min James |
| 4 | **Handover plumbing.** `PDM-CHECK` layer and flags in SwCmd; `AutoDraft` property; "Drafts waiting" list; "Draft checked" tick at check-in; release refusal | The loop in section 3 | App (1 day), Dev (half a day) | 1.5 days |
| 5 | **First real batch of 10** into the vault: 3 plates, 2 members, 2 family siblings, 3 SFA twins. James works them and gives rough times; machine compares; skill v1 | Go or stop for the rest of the 81 | SW (1 day), Admin, James (about 2 h) | 1 day |
| | **Total** | | | **about 8 working days, about 4 h of James** |

Dropped for now: "design a part from scratch" (old exercise 5) and the DimXpert trial beyond one plate in step 2.

| Session | Owns |
| --- | --- |
| Dev | SwCmd: dangling report, sheet picture, hole and edge list, flag layer, guarded draft commands |
| SW | Running steps 1 to 3 and 5, the `drawing-standards` and `drawing-checks` skills, the "what James changed" table |
| App | `AutoDraft`, "Drafts waiting", the check-in tick, release refusal, coverage wording |
| Admin | Decisions with Grant, batch lists, what goes to James, SPEC wording if the loop is adopted |

**Stop rule (unchanged):** carry on only if a draft saves James at least half his time, and no wrong dimension gets past him.

## What's verified, inferred, untested

| Item | Status |
| --- | --- |
| API calls named above exist with the stated behaviour | **Verified** in the 2026 help on this PC. None run |
| No general "re-attach a dimension" call | Verified by search of the help (only `ReattachOrdinate` and a DXF-import one) |
| James copies drawings for families and twins | **Verified from data**: identical view names in 112 of 122 family drawings and 64 of 69 twin drawings |
| Split of the 81 into 30 / 28 / 23 | Verified against the 13 Nov survey; 0804 has moved since |
| When tags survive a configuration switch or model swap | **Inferred** from how SOLIDWORKS works and the one help remark; measured in step 1 |
| Whether shaft B/C models are copies of shaft A | **Unknown** |
| Whether our parts use Hole Wizard or have usable sketch dimensions | **Unknown** (needs SOLIDWORKS: SwCmd `dims`) |
| Whether the families have design tables | Unknown |
| DimXpert on multi-body weldments; its look on James's sheet | Untested; I expect poor |
| Auto balloons against a cut list; BOM anchor in James's sheet format | Untested |
| Magenta flag check in the PDF; the check-in tick | Design only; App to confirm it fits the app as built |
| Dimension quality by code | Untested until step 2 |

Scripts used for the counts: `C:\Users\grant\AppData\Local\Temp\claude\87c3b6fbed1c4b3f\f56a1129-2c82-455b-a1d7-bb5cc2c291e9\scratchpad\scripts\sib.py` and `C:\Users\grant\AppData\Local\Temp\claude\87c3b6fbed1c4b3f\f56a1129-2c82-455b-a1d7-bb5cc2c291e9\scratchpad\scripts\tw.py` (temporary folder; a restart can clear it).

## Your to-do:

1. Say whether the rule "never hand James a dangling dimension: re-make it or remove and flag it" answers your concern, or whether you want no copying at all (fresh only, which costs James's existing dimensions on the 23 twins).
2. Agree or change the two workflows in section 2, in particular "no dimensions by default on assemblies".
3. Agree or change the handover: magenta flags on the drawing, "Drafts waiting" on the PDM page, one "Draft checked" tick at check-in.
4. Confirm tabulated drawings are parked.
5. Decide whether to start pilot step 1 (one day, no James time) while James copies his templates into `P:\_Shared\_Templates` (step 0).
