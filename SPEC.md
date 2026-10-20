# Lightweight PDM – Build Spec

Version: 16 Oct 2026 · Owner: Grant Elliott

## Purpose and context

We are building a lightweight PDM (product data management) tool so a small, spread-out SOLIDWORKS team can work on one project without overwriting each other. It replaces sharing files through Dropbox, which produces conflicted copies, and avoids SOLIDWORKS PDM, which is too heavy for a team this size.

**Scope:** PDM only: files, locks and versions. The drawing submittal workflow (checking, approval, customer review) is a separate track, likely in Autodesk Construction Cloud (ACC), which we already have. The PDM's only link to it is issuing a drawing (see Issuing drawings).

**Who uses it**

| Person | Location | Role in the tool |
| --- | --- | --- |
| Grant | NZ, often HK and mainland China | Admin, engineer |
| James | NZ | Engineer |
| Daniel | Hong Kong | Engineer |
| Joey | Mainland China | Engineer |

**The project:** about 10GB of SOLIDWORKS parts, assemblies and drawings, organised by scene and screen, with job-wide steel, shared parts and fasteners in read-only folders. Steel files follow the APEX part numbering standard (see Part numbers and file names).

**What good looks like**

- Two people never edit the same file at the same time, and nobody loses work.
- Everyone can see who has what, and which revision of each drawing has been issued.
- It works reliably from NZ, Hong Kong and mainland China, including short spells offline.
- It is simple enough that engineers use it without thinking, and Grant can maintain it.

## Guiding principles

These describe what matters and why. Use judgement on the details; where something here doesn't fit what you find, say so and suggest a better way rather than forcing it.

1. **Never lose work, never silently overwrite.** This is the one firm commitment. Every check-in keeps the previous version, and a clash produces a saved side copy, not a winner and a loser.
2. **Prevent conflicts rather than resolve them.** SOLIDWORKS files can't be merged. Locking is the main safeguard, so make taking and releasing locks quick and obvious.
3. **Simple beats clever.** Four engineers, one of them admin. Prefer fewer moving parts, plain AWS services, and nothing that needs a server to be kept alive.
4. **Fit how SOLIDWORKS behaves.** It finds parts by file name and location, opens read-only files as read-only, and holds open files locked. Work with that rather than around it.
5. **Capture data now, report later.** Record who did what and when at every step, even where no screen uses it yet. Reports can be added later without losing history.
6. **Enforce permissions at the AWS end.** The app guides people, but read-only folders and permissions should hold even if someone bypasses the app.
7. **Keep project details as settings.** Folder names, read-only areas, region and users belong in configuration, so the tool could later serve other projects.
8. **Plain language in the interface.** Some users work in English as a second language. Short labels, clear status words, no jargon.

## Users and permissions

Each person has their own login (Amazon Cognito), and each permission is a switch set per user rather than a fixed role. Behind the scenes a switch can be a Cognito group mapped to AWS permissions, so it is enforced by Amazon, not only by the app.

| Permission | Typical holders |
| --- | --- |
| Check out and check in files | All engineers |
| Issue drawings | All engineers, or Grant only (a setting) |
| Break another person's lock | Grant, optionally James |
| Edit read-only folders | Grant |
| Restore old versions | Grant, optionally James |
| Rename, move or delete files | Grant |
| Create projects and subprojects | Grant |
| Manage users and permissions | Grant only |

## Architecture overview

There's no server to run. The vault and logins sit in one AWS account in the Hong Kong region, and each engineer runs a small local app that does the file work on their PC.

```
Engineer's PC (Windows)                     AWS child account (Hong Kong)
+---------------------------+               +---------------------------+
| PDM app (local web page)  | <-- login --> | Cognito login             |
|  - reads SW references    |               |  - per-user permissions   |
|    (Document Manager)     | <-- sync ---> | S3 vault (master copy)    |
|  - sets read-only flags   |               |  - real folder names      |
+------------+--------------+               |  - every version kept     |
             |                              |  - lock records           |
      P: drive (local mirror)               +---------------------------+
             |
        SOLIDWORKS
```

- **S3 vault:** the master copy, stored under real folder names with S3 Versioning on, so every overwritten file is kept. Locks use S3 conditional writes. Project settings sit in a small database alongside (DynamoDB fits).
- **Cognito:** user logins, which hand the app a short-lived AWS pass matching that person's permissions. Nothing permanent is stored on PCs, and Cognito is the single source of truth for permissions.
- **Local PDM app:** a small program serving a web page on the engineer's own PC. It has to be local, because only a local program can change files and read-only flags on disk.
- **Document Manager:** SOLIDWORKS' library for reading files without opening SOLIDWORKS. It lists references, reads and writes custom properties, and gives thumbnails. The key is already issued and must stay private (never commit it).

## Local working copy

Each engineer keeps a full mirror of the vault on their PC and works on that, never on the vault directly. Changes travel only at deliberate moments (Get latest, check-in), which is what keeps them from colliding.

- **Same path everywhere.** Each user picks their own root folder, and the app maps it to drive `P:` using Windows SUBST, set up permanently (so it survives restarts and is visible to all programs). SOLIDWORKS then sees identical paths on every PC. Check the letter is free at install and warn if not.
- **Read-only by default.** Every file is read-only on disk unless the user has it checked out.
- **Get latest** downloads only files that changed since the last sync. It never touches files the user has checked out, and skips files open in SOLIDWORKS, listing them so they're caught next time. It could also run quietly every so often, since it can't overwrite checked-out work.
- **First sync** is about 10GB. Allow seeding from a USB drive or zip, then verify against the vault and fetch only differences. This matters most for mainland China.
- **Keep it out of Dropbox.** Two sync tools on one folder will fight. Warn if the root sits inside a Dropbox, OneDrive or similar folder.
- **Reference check.** At check-in, use the Document Manager key to confirm every reference points inside `P:`. Flag any that point elsewhere (an old Dropbox copy is the likely culprit), because SOLIDWORKS matches by file name and can quietly pick up the wrong file.

## Check-out and check-in

Check-out works per file. Picking an assembly or drawing shows a tick-list of what it uses, pre-ticked, because in practice most files in a screen folder get checked out together.

**Check-out**

1. The user picks one or more files (an assembly brings its referenced parts and drawings into the list).
2. The list shows each file, whether it's free, and who holds it if not. Files in read-only folders appear greyed out.
3. On confirm, the app makes sure each file is the latest, takes the lock in S3, and makes the local copy editable in place. Files never move folders, because SOLIDWORKS references depend on location.
4. If a file is already open read-only in SOLIDWORKS, prompt the user to reload it there. The habit to encourage: check out, then open.

**Locks** are small lock records in S3, created with a conditional write ("only if none exists"), so two people can't take the same file at once. A lock records who, when, and an optional note such as "offline until Sat".

**Check-in**

1. The app lists checked-out files that changed, plus new files in the folder that aren't in the vault yet. New files go through the same checks as Bring in (see Part numbers and file names) before they are accepted.
2. Every file being checked in must be fully closed in SOLIDWORKS, including any open assembly or drawing that uses it. The app says what's still holding a file open. Read-only files can stay open.
3. An optional note describes the change.
4. The app uploads, keeps the previous version, releases the locks and sets the files read-only again.
5. **Undo check-out** discards local edits, restores the vault copy and releases the lock.

**Dashboard:** who has what, lock age, files changed since the user's last sync, conflicted versions, and requests to release.

## Offline working and conflicts

Offline works because a lock is the right to edit, and a lock taken before going offline still holds. Three rules keep it simple:

1. **Check out before you go offline.** Checked-out files can be edited anywhere, with or without a connection.
2. **Everything else stays read-only.** Nothing to clash with.
3. **Admin can break a lock if it's urgent.** That is the only way a conflict arises.

**When a conflict happens** (a broken lock, or a file edited without a lock while someone else checked in a newer version):

- The vault version stands; whoever checked in first is the main version.
- The returning copy is saved as a labelled side copy, e.g. "LV2 – Grant's offline edits, 18 Oct", and the conflict is recorded.
- If nobody else changed the file meanwhile, just offer to check it out and in for the user; there is no conflict.

**Resolving**, from three buttons on the conflict (either party or an admin; the file is locked while it's resolved):

| Choice | Result |
| --- | --- |
| Keep main | The side copy is set aside |
| Use side copy | The side copy becomes the new main version |
| Combine by hand | Check out the main file, redo changes from the side copy in SOLIDWORKS (its Compare utility helps), check in, mark resolved |

Nothing is deleted either way; both stay in history. Unresolved conflicts never block work, but show a warning on the file list, when checking out, when issuing, and in the Get latest summary.

## Issuing drawings

Issuing is where the PDM hands a drawing over to the submittal workflow. It is deliberately small.

1. Someone with permission clicks **Issue** on a checked-in drawing.
2. The app files the PDF in the project's `_Released` folder, named `<part number>_<revision>.pdf`, e.g. `08A11-SFA-1T_B.pdf`, since a drawing carries its model's number. For now the engineer exports the PDF from SOLIDWORKS and attaches it.
3. The vault tags the exact file versions issued, the drawing and the models it shows, so the issued state can always be recovered.
4. Further changes become the next revision. Release revisions are letters, skipping I and O, and never appear in the part number. The app writes the new revision letter into the drawing's custom properties with the Document Manager key, and the title block already links to that property.
5. Sending the PDF on to ACC is manual to start with.

Comments, markups and approvals live in ACC, not in the PDM.

## Languages

The interface supports English and Simplified Chinese, so each engineer can work in their preferred language.

- **Interface:** every label and status in both languages; each user picks theirs.
- **Check-in and lock notes** can be translated automatically, with the original kept and a "show original" option. Options: Amazon Translate (same AWS account, custom terminology) or the Claude API (better with engineering language). Try both on real notes.
- **File and folder names stay ASCII (English)**, avoiding garbled Chinese names in zips and on Windows.

## Projects, shared areas and folders

Shared folders sit above projects, so any project can use them; because every PC sees the same `P:` drive, references to them work everywhere. Inside a project the rule is **folder = place**: a steel file lives in the folder named by its place tag (see Part numbers and file names). The layout is fixed at the start, and renames and moves are admin-only.

```
P:\
  _Shared\
    _Templates\    part, assembly, drawing templates and sheet formats
    _Fasteners\    bought-in fasteners (FAST- names), the same on every job
    _Profiles\     weldment profiles
  APEX\            a project
    STOCK\         job-wide steel used in more than one scene (place STOCK)
    SC08\          a scene
      _Shared\     steel used by more than one SC08 screen (08XXX, 08BXX)
      LS-08X-04\   a screen; its LS- screen assemblies sit at this level
        ShaftA\    08A04
        ShaftB\    08B04
        ShaftC\    08C04
        ShaftBC\   08Z04, shafts B and C
        _Shared\   08X04, all shafts
    _Released\     issued PDFs
```

| Folder | Access |
| --- | --- |
| `P:\_Shared` and everything in it | Read-only; admin edits |
| `<Project>\STOCK`, `<Scene>\_Shared`, `<Screen>\_Shared` | Read-only; one nominated owner edits |
| `<Screen>` root and its shaft folders | Check-out / check-in per file |
| LED panels, SKUs and LS- screen assemblies | Grant edits; read-only for everyone else |
| `<Project>\_Released` | Read-only; filled by Issue |

- **Underscore convention:** a folder created with a leading underscore is read-only to engineers by default. The per-folder setting, enforced through Cognito, is what counts: it's pre-ticked for underscore folders and can be changed. A rename never silently changes permissions; the app warns if a name and its setting disagree.
- **New project** and **new subproject** (a scene or a screen) are app actions. Permissions are set at project level and inherited downwards. Each engineer syncs only the projects they belong to, plus the global `_Shared`.
- **Keep the global `_Shared` small.** Only files identical across jobs belong there. A change ripples into every project, including issued drawings, so treat shared files as frozen once used: fix by adding a new file, and have the app show how many files use one before any edit.
- **References:** a file may use its own screen folder tree, its scene's `_Shared`, the project's `STOCK` or the global `_Shared`, never another screen or scene. Place tags make this checkable: a part numbered at one level may only be used by files at that level or below it, so an `08X04` part may sit in an `08A04` assembly but not the other way round. Moving a file between levels later breaks references, so it's an admin job using the key's repointing ability.
- **SOLIDWORKS File Locations** for templates, sheet formats and weldment profiles should point at `P:\_Shared\...` on every PC, so everyone gets the same title block and profiles. The app checks and sets this at install.

## Part numbers and file names

The APEX structural part numbering standard (`docs\part-numbering.md`; the master copy is the Rev 3 workbook) decides what steel files are called. The PDM enforces it rather than defining it. What the PDM relies on:

| Rule | Detail |
| --- | --- |
| Format | `PLACE-CCT-nn`: a five-character place tag, a two-letter category, a type letter (A assembly, W weldment, P part) and an item number. Examples: `08A11-SFA-1T`, `08XXX-PLP-03`, `STOCK-PKP-01`. Top-level structure assemblies are named by the place tag alone, e.g. `08A04`. |
| Source of truth | The `PartNo` custom property, configuration-specific. BOMs read it, and the file name must agree with it. Always treat it as text; scene 00 tags have leading zeros. |
| Single-configuration file | File name equals the part number: `08A11-SFA-1T.sldasm`. |
| Family file | File name is place and category, e.g. `STOCK-BRP`; each configuration is named by its item number and carries its own `PartNo`, e.g. `STOCK-BRP-07`. A family holds one place tag only. |
| Drawings | Same name as the model they show. A handed pair gets one drawing per hand. |
| Fasteners | Bought-in, named by specification: `FAST-HEX-M20x35-8.8-HDG`. Size and length are configurations (exact split to confirm, see open questions). One spelling per fastener. |
| Not numbered | LS- screen assemblies, LED panels and SKUs, carts and shrouds keep their own names. These are Grant's area, and tidying the SKU naming is a separate job. |
| Revisions | Never in the part number. A change to fit or form is a new number. |

The map from place tag to folder is project configuration, not code (principle 7).

**What the app checks.** These run when a folder is brought in and on any new file at check-in. A steel file that fails is held back with the reason; a non-steel file is listed in the report and allowed in.

| Check | Why |
| --- | --- |
| File name agrees with `PartNo` (every configuration, for a family) | One source of truth |
| Folder matches the place tag | Folder = place, and references stay inside allowed levels |
| No other file or configuration in the vault has the same number | SOLIDWORKS matches by name, and the vault becomes the register of used numbers |
| A family file holds one place tag | A split family can't be moved later without breaking references |
| Each drawing has a model of the same name | Keeps the pair obvious and makes Issue unambiguous |
| No virtual components (`^` in the name) | They can't be numbered or referenced on their own |
| No conflicted-copy names, nothing from `old`, `_old`, `PnG` or `Working` folders | Dead files don't enter the vault |
| Every reference points inside `P:` and to an allowed level | Already the check-in rule |

**Allocation.** Numbers are allocated in the Rev 3 workbook for now, and the vault is the record of what has been used. Offering the next free number is a later bolt-on.

**Renaming after loading** is rare by design and admin-only, done through the app with the Document Manager repointing every file that uses the old name.

### Bringing files in

Loading happens once per folder, in this order: global `_Shared` (templates, fasteners, profiles), then `STOCK`, then each scene's `_Shared`, then screen by screen. For each screen:

1. **Clean up first.** James clears the open items in the numbering standard for that screen: relink anything pointing at `_old` folders, save virtual parts to their own files, pick the live copy of each duplicate, split families that span screens. None of this can be automated.
2. **Pack and Go into place.** Grant runs Pack and Go from James's export into the screen's new folders on `P:`, with every file taking its new name and folder from the Rename Register. The plan is a small tool that drives SOLIDWORKS' own Pack and Go through its API, so names and destination folders come straight from the register instead of being typed; the fallback is the Pack and Go dialog by hand. Shared files already in `P:` must end up referenced there, not copied again (open question).
3. **Bring in check.** The app runs the checks above, writes `PartNo` into every file and configuration from the register, and lists anything that doesn't fit.
4. **Confirm in SOLIDWORKS.** Grant opens the structure and screen assemblies from `P:` and checks nothing is missing or pointing outside `P:`.
5. **Add to vault.** The folder becomes the real folder; nothing moves afterwards.

## Handover and future generic use

The system lives in a dedicated AWS child account under Candelic (via AWS Organizations), so it can be handed over whole or closed cleanly at project end. Keep handover simple:

- Nothing else lives in that account.
- It's registered to Grant's Unilumin email for now, which can change at handover.
- Files sit in S3 under their real folder names, so the vault is a normal folder tree anyone can download and open in SOLIDWORKS, even without the app.
- A one-page handover note: what's where, how to log in, how to add users.

**If it becomes more generic later:** project details stay in settings (see principles); one AWS account and vault per customer; each customer supplies their own Document Manager key, since SOLIDWORKS doesn't allow the key to be shared outside the company or shipped in software; avoid "SOLIDWORKS" in the product name (Dassault trademark).

## Later bolt-ons and the submittals track

Not for the first build, but leave room for them.

| Bolt-on | What it does |
| --- | --- |
| Push issued PDFs to ACC | Upload issued drawings into the ACC project automatically |
| Automatic PDF export | Generate the PDF from SOLIDWORKS at Issue instead of attaching it |
| AutoCloud | Draw revision clouds around changes between PDF revisions; may sit on the ACC side |
| Reports | Summaries of check-ins, locks and issues from the recorded history |
| Next free number | Offer the next unused item number for a place and category, using the vault as the register |

**Submittals track (separate):** checking, approval, customer review, markups, the drawing register and schedule tracking. It's likely handled in ACC, and will be decided once the PDM is in use.

## Build approach and models

Build in phases, each usable on its own, and test on a copy of the project, never the live files.

| Phase | Delivers |
| --- | --- |
| 0. Proving tests | S3 and Cognito upload and download from Joey's network; Document Manager reads references and configuration properties from a real APEX assembly, drawing and family file, and can repoint a reference; a Pack and Go trial driven from the Rename Register; permanent SUBST works with SOLIDWORKS |
| 1. Core | Vault, Get latest, per-file check-out and check-in, locks, dashboard, permissions, projects and subprojects |
| 2. Offline and conflicts | Side copies, conflict warnings, the three resolution choices |
| 3. Issuing | Issue action, `_Released`, tagged versions, revision properties |
| 4. Bolt-ons | As listed above, when wanted |

**Models:** Fable 5.1 as architect and reviewer, with Opus 5.5 agents doing most of the implementation. Use Fable for the parts where a wrong design is expensive: the overall structure, locking and conflict logic, and SOLIDWORKS reference handling. It should also review each phase before it's used. Opus handles well-defined pieces such as the dashboard, the AWS setup and translation.

**Technical choices are the builder's to make**, with one known constraint: the Document Manager is a Windows COM library. That points the local app towards a language with easy Windows COM access, such as C#/.NET or Python, rather than Go or Rust. Explain the choice before building.

## Open questions and things to verify early

- [ ] Upload and download speed and reliability to the Hong Kong region from Joey's actual network
- [ ] Whether the Document Manager key can edit revision tables (the API lists read and update calls for them; confirm in Test 2)
- [ ] Document Manager key renewal with each major SOLIDWORKS release
- [ ] Whether drawings shared across screens (e.g. the custom panels drawing) need their own folder
- [ ] Lock reminder timing (e.g. after 3 days)
- [ ] Hong Kong region is opt-in and needs enabling in the new account
- [ ] Who may issue drawings: all engineers or Grant only
- [ ] Fastener family rule: one file per type, grade and finish with size and length as configurations, so file `FAST-HEX-8.8-HDG`, configuration `M20x35`, `PartNo` the full spec. Confirm and add to the numbering standard
- [ ] Loading screen by screen: how shared files used by the second and later screens get repointed to the copies already in `P:` (include them in Pack and Go with the existing destination, or repoint with the Document Manager afterwards). Settle in the Pack and Go trial
- [ ] Where LED panels and SKUs live in the tree, decided when Grant rebuilds them
- [ ] Whether scene and screen `_Shared` folders keep that name or take their place tag (standard open item 14). Keeping `_Shared` keeps the read-only default; the tag-to-folder map lives in settings either way
