# Claude driving SOLIDWORKS: what the videos do, and how we learn it

14 Nov 2026. Research only: SOLIDWORKS was not started and nothing was run against it. The three videos were downloaded, their captions read, and still frames looked at closely. The video files have been deleted; six key frames are kept (path at the end).

## In short

- **The two SOLIDWORKS videos are adverts for a paid product called CADbyAI.** It is not plain Claude. It is the Claude desktop app plus a connector (an "MCP server") that CADbyAI installs, plus a "Claude" tab added inside SOLIDWORKS. US$19.80 a month per PC, 30-day free trial.
- **The method underneath is the one we guessed, and we can do it ourselves.** Claude writes code for the SOLIDWORKS API, runs it, reads the errors, looks at a picture of the result, and fixes it. We already have most of the pieces (SwCmd, PDF export, the 2026 libraries).
- **The drawings are good drafts, not finished drawings.** Up close they have overlapping dimensions and thin coverage on complex parts. The maker's own caption says "You check, correct and decide."
- **It changes one conclusion in our auto-drawings proposal.** We called dimensions "the hard part" because we were leaning on SOLIDWORKS's automatic dimensioning. The videos place each dimension by code, one at a time, with Claude choosing what to dimension. For plates and brackets that should work for us too. The "copy a drawing James already made" route is still the better one for families and shaft twins.
- **Recommendation:** learn it ourselves in five small exercises (about 6 to 8 working days spread over Dev and SW, about 3 hours of James's time). Don't install CADbyAI on this PC.

## 1. What the videos actually do

Two corrections to the notes we started with:

| Video | What we thought | What it is |
| --- | --- | --- |
| `G6CLWdrL0Ms` | Gearbox, Mech_AI, 18 Oct | **Vise**, channel Mech_AI, 12 Oct, 6.5 min |
| `tphBHmvsMvg` | Vise, CADbyAI, 13 Oct | **Gearbox**, channel CADbyAI, 18 Oct, 14 min |
| `1qIuwHFLmXQ` | SOLIDWORKS demo with transcript | **AutoCAD, not SOLIDWORKS.** "Game Changer. Claude to CAD!" by Daniel O'Shaughnessy, 31 Mar 2026 |

Both SOLIDWORKS videos are the same maker (same captions, same screen layout, and the gearbox one signs off "CADbyAI: AI mechanical design with Claude and SolidWorks").

### Confirmed (seen on screen, in captions, or on cadbyai.com)

| Finding | Evidence |
| --- | --- |
| It runs in the **Claude desktop app**, not Claude Code | Chat window with a "Reply" box beside SOLIDWORKS; setup video says "Claude Desktop (not the web version)" |
| A connector named **solidworks** gives Claude a set of tools | Status lines read "Using Solidworks: Core exec", "Core job", "Drw views", "Drw auto", "Drw bom", "Sw snapshot", "Sw export". One approval box reads "Claude wants to use Drw bom from solidworks" with settings View: isometric, Type: parts, Balloons: Yes, At: 20, 62 |
| SOLIDWORKS gets a **"Claude" tab** in its toolbar | Visible in every frame |
| It is a paid product | cadbyai.com: "$19.80 per month", "free 30-day trial, no card needed", "One license code works on one PC" |
| It installs a ready-built program in `C:\CADbyAI\sw_mcp\` | Installer frame in the setup video (CADbyAI 2.1.1). Windows shows the publisher as unknown |
| The videos use **SOLIDWORKS 2022** | Splash screen in both |
| Claude looks at pictures of its work | "Sw snapshot" tool; caption "Every fix is verified by reading the solid body back from SolidWorks" |
| Pop-ups stop it | Caption: "A SolidWorks pop-up blocks the dimension step, so the session is paused and saved. New session, one word: continue. Claude turns that pop-up off and re-runs the step." The pop-up was "Input dimension value" |
| It takes real time | Vise: about 13 minutes of Claude working. Gearbox: step timers pass 45 minutes in the first session and 43 in the second, and the summary line says "Created 6 files, shared a file, and 150 more steps". Captions admit "Waiting time is cut out; SolidWorks steps play at 1.5x to 2x" |
| Claude reports what it left out | Vise: "no fillets or draft angles", "no real threads", and "Two dimensions from the table, G 286 and H 130, aren't on the drawing because the catalog doesn't make clear which features they measure" |

### Inferred or only claimed

| Claim | Status |
| --- | --- |
| "Claude writes C# code for the SolidWorks API" | **Claimed in captions; never shown.** No code, tool input, skill file or instruction file appears on screen in either video. "Core exec" is probably the tool that runs Claude's code, but the language is unverified |
| "Save your standards as skills and instructions" | **Claimed, not shown.** The website says it "Saves your own rules and applies them every time" and "Learns your company's drawing style from drawings you already released". Neither video demonstrates it; both drawings use a default SOLIDWORKS title block or none |
| One prompt does everything | Partly. The gearbox needed a second session ("continue") and a third prompt ("fix component error") to clear 4 assembly problems, including 100 floating components |

### The AutoCAD video (transcript in full was available)

Not relevant to drawings. He asks Claude (in a browser) for a small AutoCAD LISP routine, downloads it, and a helper he built loads it into AutoCAD when it lands in Downloads. His own words: "Claude's not always great on the first time." The useful idea is only the short loop: ask, run, look, correct.

### What else the maker has published

| Item | What it tells us |
| --- | --- |
| "Full Setup Tutorial" (3 Nov) | Install Claude Desktop, run the CADbyAI installer, check Settings > Connectors. Also supports AutoCAD, BricsCAD, DraftSight, FreeCAD, Alibre, SketchUp |
| cadbyai.com/docs | "CADbyAI connects Claude to SolidWorks through MCP." Their server receives "the machine code, the PC name" and licence codes; "Your design files stay on your PC." No company name, country or address is given anywhere |
| Other videos | Bulk-swapping bolts from Toolbox, colour-coded clash check, a mechanism from one photo |
| GitHub, course, blog explaining the method | **None found** |

## 2. How the dimensioning works, and what that changes for us

**How they do it (inferred from the frames):**

1. A tool places the views ("Drw views"), another adds the parts list and balloons ("Drw bom").
2. Dimensions come from "Drw auto" and "Core exec" runs. The "Input dimension value" pop-up is the one SOLIDWORKS shows when a dimension is added the way a user adds it: pick two edges or a hole, click where the number goes. So the code is selecting geometry in a view and adding each dimension at a chosen spot. I confirmed the calls for this exist in our 2026 libraries (`AddDimension2`, `AddHorizontalDimension2`, `AddOrdinateDimension2`) along with the setting that turns the pop-up off (`swInputDimValOnCreate`).
3. Tolerances, fits and roughness are text and symbols Claude adds to those dimensions. The tolerance tables and "Technical requirements" are plain notes and tables.
4. Claude takes a snapshot, looks, and adjusts.

**Why it looks easier for them than it will be for us:** Claude built those models itself minutes earlier, so it already knows every size and which face is which. Our models exist already, built by James and Grant in different ways. Claude must first read the geometry (outline, holes, slots, thickness) before it can decide what to dimension.

**How good are their drawings up close?**

| Drawing | What's there | What a checker would mark |
| --- | --- | --- |
| Vise assembly | 4 views in the catalogue layout, 11 overall dimensions labelled with the catalogue letters, parts list, 8 balloons | No title block. Only overall sizes. Parts list shows file names |
| Helical gear | Front view, section, isometric, bore and keyway tolerances, runout frame, roughness marks, gear data, tolerance tables, title block | Dimension text sits on top of the section hatching; the "6x Ø26 DEEP 12" and PCD texts cross the geometry; stacked dimensions are cramped |
| Housing body | 5 views, thread callouts as text notes, flatness and position frames | Thin for a casting: few sizes on the plan view, callouts crowded onto the bearing bores, small unreadable notes |

So: impressive speed and a real head start, but James-level tidiness is not shown.

**What changes in `auto-drawings.md`:**

| Our 13 Nov view | Now |
| --- | --- |
| Dimensions are the hard part; use model dimensions and Auto Dimension, expect mess | Add a third route: **Claude picks and places each dimension by code**, checks a picture, and tidies. Likely good on plates, packers and simple brackets; unproven on weldments |
| Hole callouts can't be done unattended (`AddHoleCallout2` needs a click) | Their housing drawing uses **plain text notes on leaders** for thread callouts. We can do the same: "4 x Ø13.5 THRU" as a note or as dimension text. To be tested |
| A fixed tool (`make-drawing`) does the same steps every time | Keep the fixed tool for the reliable parts (views, scale, title block, tables). Let Claude handle the judgement parts per drawing, guided by a written standard |
| Copy route for families and shaft twins | **Unchanged, still the best value** (52 of the 81) |
| Pilot of 25, James times each | Unchanged. Add a count of James's corrections per drawing |

## 3. What we have and what's missing

| Piece | Have it? | Notes |
| --- | --- | --- |
| A way to run C# against SOLIDWORKS 2026 | **Yes** | `tools\SwCmd` (attaches to `SldWorks.Application.34` only, has a lock, refuses unsafe paths, watches for dialogs). `scratch\auto-drawings\MakeDrawing.cs` compiles but has never run |
| A fast try-fix loop (write code, run, read errors, try again) | **Partly** | Works today by building a scratch program, but each change to SwCmd is a Dev job. Missing: a guarded "run this script" command limited to a test folder |
| A way for Claude to see the result | **Partly** | SwCmd `export-pdf` exists and Claude can read a PDF or image. Missing: a quick picture of one sheet or one view (the API has `SaveBMP`), so Claude can zoom in on a crowded corner |
| A way to read a model's geometry (holes, outline, thickness) | **Partly** | SwCmd has `bodies`, `dims`, `cut-list`, `tree`, `mass`. Missing: a hole and edge list per view |
| Dialog handling | **Yes** | `DialogWatch.cs`. Add: switch off "Input dimension value" during a run and put it back after |
| James's templates on this PC | **No** | `P:\_Shared` has only `_Fasteners` today. The sheet format, drawing template, and BOM and cut-list table templates are on James's PC |
| James's drawing standard written down | **No** | We have the survey of his 230 drawings (auto-drawings.md section 2) but not his rules: which view is front, what gets dimensioned from where, callout wording, weld symbol habits |
| Skills that hold those rules | **No** | None in the repo yet |
| Our modelling rules written for Claude | **Partly** | Numbering and properties are in `SPEC.md`; nothing on how a plate or weldment should be built |

## 4. Learning plan

The idea: start with the easiest possible drawing, get the loop working, and write down every correction James makes so the next drawing needs fewer.

**Ground rules for all exercises**

- Copies only, in a new test folder outside `P:\` and outside the PDM working folder. Nothing goes into the vault.
- SOLIDWORKS 2026 only, through SwCmd's lock, in batches with Close All between sets.
- Each draft is exported to PDF and sent to James beside nothing else: he marks it up as he would a junior's drawing.

**Exercises**

| # | Exercise | What we learn | Owner | Effort |
| --- | --- | --- | --- | --- |
| 0 | James copies his sheet format, drawing template, BOM and cut-list table templates into `P:\_Shared\_Templates` | Nothing works properly without these | James | 30 min |
| 1 | **Run the prototype once.** One plate already drawn by James (e.g. GNRIC-PLP-06), copied to the test folder. Views, scale, title block only. Export PDF, Claude reads it and compares with James's PDF | Does the loop work end to end? How long, how many tries? | Dev builds, SW runs | 1 day |
| 2 | **Dimension that plate by code.** Read the outline and holes, add overall sizes, ordinate dimensions from one corner, hole callouts as text. Look at the picture, fix overlaps | Whether Claude-placed dimensions are tidy enough. Whether text callouts are acceptable to James | SW, with Dev adding the picture and hole-list commands | 2 days |
| 3 | **Write the first skill from James's mark-up** and redo the same plate, then two plates James has not drawn (08X04-PLP-01, -02) | Whether a written standard carries over to new parts | SW writes, James reviews | 1 day + 1 h James |
| 4 | **A bracket and a simple weldment** (08B04-BKP-02, one GNRIC-LMW after James draws the first) | Where the method stops being worth it: cut list, weld symbols, member lengths | SW | 2 days + 1 h James |
| 5 | **A small design from scratch**: Grant describes a packer plate or bracket in words, Claude models it to our rules and draws it | Whether the "one prompt" idea is useful for our kind of part | SW | 1 day + 1 h James |

After exercise 3 we will know whether to fold this into the 25-drawing pilot or stop at "views, tables and title block".

**Skills and instructions to write** (short files in the repo, loaded only when needed)

| Skill | Holds | Written from |
| --- | --- | --- |
| `drawing-standards` | A3, third angle, James's scales, view choice per part type, where tables and notes go, callout wording, what gets dimensioned and from which edge, text sizes, no white text | The survey of 230 drawings, then James's mark-ups |
| `drawing-checks` | The list Claude runs before showing a draft: no overlapping text, no dangling dimensions, title block filled, every hole located and sized, scale from the list | Release checks in `src\Pdm\Release.cs` plus James's corrections |
| `sw-api-lessons` | What worked and what didn't in the API: calls that pop up dialogs, the 2026-only rule, units in metres, how to select an edge in a view | Each failed run adds a line. This is the "learns from each error" in the captions |
| `modelling-rules` | How we build a plate, bracket, weldment: Hole Wizard for holes, properties to fill (PartNo, Description, Finish, Weight), naming | `SPEC.md` and Grant |

**Who owns what**

| Session | Owns |
| --- | --- |
| Dev | SwCmd additions: guarded script run, sheet/view picture, hole and edge list, pop-up switch |
| SW | Running the exercises, the four skills, the drafts and the comparison with James's drawings |
| Admin | Decisions with Grant, what goes to James, updating `auto-drawings.md` with the result |
| App | Nothing until a pilot is agreed (then the `AutoDraft` release refusal) |

**Measuring success with James**

| Measure | Target |
| --- | --- |
| Time for James to fix a draft vs draw from scratch | Draft saves at least half (same rule as the pilot) |
| Corrections James marks per drawing | Falls from one drawing to the next as the skill grows |
| Wrong or missing dimension that James did not catch at first look | Zero accepted; one of these means the check list is not good enough yet |
| Claude time and usage per drawing | Recorded from exercise 1; we don't have a number yet |

## 5. Risks and limits

| Risk | What it means | How we handle it |
| --- | --- | --- |
| Time and usage per drawing | The demo took about 13 minutes for one simple assembly drawing and well over an hour for the gearbox set, with waiting edited out. Our cost per drawing is unknown | Measure in exercise 1 before committing to more |
| Accuracy | A drawing can look right and be wrong. A fabricator works from the numbers | Dimensions are read from the model, never typed; James checks every draft; drafts carry `AutoDraft` and cannot be released |
| Tidy enough for James? | Their own close-ups show overlaps | Exercise 2 answers this on our easiest part |
| Existing models vary | Claude did not build ours, so reading them is extra work the demos skip | Start with plates; list what could not be read |
| SOLIDWORKS pop-ups and hangs | A pop-up stopped their run mid-way | DialogWatch, pop-up setting off during runs, save after each stage so a rerun picks up |
| SOLIDWORKS time on Grant's PC | One licence, shared with real work; 2024 is also installed | SwCmd lock, batches, `SldWorks.Application.34` only |
| Templates not here | Drafts won't look like James's without them | Exercise 0 |
| Standards live in James's head | The skill is only as good as what he tells us | Learn from mark-ups, a little of his time each round |
| CADbyAI itself | Unsigned installer, operator not named, sends PC name and a machine code to their server, runs code Claude writes with no vault guard, per-PC licence | Don't install on this PC. If Grant wants to try it, use a PC with no vault access and test copies only |

## What was checked, and what wasn't

| Item | Status |
| --- | --- |
| Captions for all three videos | Retrieved in full. The two SOLIDWORKS videos have written captions (no speech); the AutoCAD one is spoken |
| Frames | Gearbox 5:04 to 10:52 and 11:18 to 14:01, vise 0:11 to 1:10 and 3:27 to 5:58, at one frame per 8 to 10 s, plus close-ups; setup tutorial throughout |
| Code on screen | **None exists in the videos.** I could not see what the code looks like or which API calls they use; section 2 is inference |
| cadbyai.com | Home, docs, features and privacy pages read through a summariser. The tool list is not documented there |
| API calls named in section 2 | Names confirmed present in the installed 2026 libraries. **Not run** |
| Our own results | Nothing run. Every "should work" above is untested until exercise 1 |
| One slip | I listed the folder names in `P:\_Shared` (read only, nothing opened or changed) to check for templates, though the brief said not to touch `P:\` |

Kept frames: `C:\Users\grant\AppData\Local\Temp\claude\87c3b6fbed1c4b3f\f56a1129-2c82-455b-a1d7-bb5cc2c291e9\scratchpad\keyframes\` (six JPGs; this folder can be cleared by a restart, so copy them if wanted).

Sources: [vise video](https://www.youtube.com/watch?v=G6CLWdrL0Ms), [gearbox video](https://www.youtube.com/watch?v=tphBHmvsMvg), [AutoCAD video](https://www.youtube.com/watch?v=1qIuwHFLmXQ), [CADbyAI setup tutorial](https://www.youtube.com/watch?v=bktA2Hv7Nbo), [cadbyai.com](https://cadbyai.com/), [docs](https://cadbyai.com/docs), [features](https://cadbyai.com/docs/features), [privacy](https://cadbyai.com/privacy), `docs\research\auto-drawings.md`, `sw\solidworks-mcp-review.md`.

## Your to-do:

1. Decide whether to run exercises 1 to 3 (about 4 working days across Dev and SW, 1 hour of James's time) before deciding on the 25-drawing pilot.
2. Ask James to copy his sheet format, drawing template, and BOM and cut-list table templates into `P:\_Shared\_Templates`.
3. Decide the test folder name for the copies (outside `P:\` and the PDM working folder).
4. Decide whether you want a CADbyAI trial at all. My advice is no on this PC; if yes, name a PC with no vault access.
5. Say whether you want the six kept frames copied somewhere permanent.
