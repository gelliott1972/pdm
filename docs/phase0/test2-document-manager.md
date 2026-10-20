# Phase 0, Test 2: Document Manager (results so far)

Date: 16 Oct 2026 · Machine: Grant's NZ PC, SOLIDWORKS 2026 SP4 · Test set: Pack and Go of LS-08A-06 from Grant's laptop

## Set-up

| Item | Result |
| --- | --- |
| Library | Already installed and registered with SOLIDWORKS (`swdocumentmgr.dll` 34.4). No SDK install needed. |
| Key | `SW_DM_KEY` user environment variable. Accepted. Never printed or written to files. |
| Probe | `tools\DmProbe`, C# compiled with the .NET Framework compiler built into Windows. Opens files read-only, never saves. |
| Output | Five CSV tables (files, configurations, properties, components, references) in `scratch\inventory\<screen>` (not in git). |

## What worked

| Check | Result |
| --- | --- |
| Open every file read-only | 59 of 59 SOLIDWORKS files opened, none failed |
| List configurations | 449 configurations, including weldment `<As Machined>` / `<As Welded>` configurations |
| Read custom properties, file-level and per configuration | 481 properties read; `PartNo` found on 97 configurations |
| List assembly components per configuration, with referenced configuration, suppressed, virtual | 1,549 component rows |
| List external references and resolve them by search path | 106 of 107 resolved inside the inbox; the other is a virtual component |
| Detect Toolbox flag | 3 parts flagged as Toolbox standard parts (the hex screw, hex nut and plain washer files) |

## What we learned

1. **Stored reference paths are old paths.** Assemblies still record where each part was last saved: `C:\Users\DELL\Dropbox\...` (laptop), `C:\Users\grant\Dropbox\Unilumin-Private\...` (another PC) and `M:\` (an earlier mapped drive). SOLIDWORKS finds them by file name through its search rules, which is exactly the fragility the `P:` drive removes. After loading, every file should be opened and saved once from `P:` so the stored paths become `P:\...`.
2. **Searching by file name works for the inventory**, so the Document Manager can tell what each reference will resolve to without opening SOLIDWORKS.
3. **The Document Manager can clear the Toolbox flag** (the interface has a settable Toolbox property). To be tested on a copy before relying on it; it would remove the need for the separate SOLIDWORKS utility.
4. **Fasteners are Toolbox family files**: one file per fastener type, sizes as configurations named like `ISO 4017 - M12 x 50-N`. The Hilti anchor is a single downloaded part with no configurations.
5. **Some `PartNo` values are already in the files.** 55 match the register, 6 differ (two LCMM configurations swapped; the four tie-beam interface plates are numbered 30–33 in the file but 03–06 in the register), and 36 are in files the register doesn't list per configuration.
6. **Pack and Go brings more than the screen uses.** It copies every file referenced by any configuration, suppressed or not. The K-beam interface plate (08X04) and the 08X11 brace arrived only because they sit in other screens' configurations of the LCMM family; LS-08A-06 doesn't use them. The SF04 floor plate is likewise only in a suppressed configuration. "Used by this screen" must be worked out by walking the tree through the configurations each parent actually references, skipping suppressed components. The import plan now does this and marks the rest "leave out".
7. **Families that mix place or type.** `LS-08X-11_BRACE` holds 08X11 and 08B11, BRP and DBP. `LS-08X-X_TIE_BEAM_POST` holds 08XXX and 08BXX. `MEMBER_SHS_40x80x8` holds STOCK-LMP and STOCK-LMW. `LCMM_40x80x8_SC08` holds four places (a known open item). Each breaks the one-place-per-family rule.

8. **Clearing the Toolbox flag works.** `tools\DmToolbox` cleared the flag on the screw, nut and washer copies in `E:\PDM-work\LS-08A-06` (Toolbox standard part to ordinary part), read back correctly, and SOLIDWORKS then loaded them as ordinary parts from the work copy. No separate SOLIDWORKS utility is needed. The tool refuses any path inside Dropbox.
9. **SOLIDWORKS settings can override the folder.** On the NZ PC, File Locations > Referenced Documents listed the live Dropbox `_Fasteners` folder, so SOLIDWORKS loaded the live fasteners instead of the copies next to the assembly. Removing it fixed this. Every PC needs that list checked before using `P:` (proposed for SPEC.md).

10. **Parts built from other files need extra search flags.** With the first set of search flags, the Document Manager listed only assembly components. Adding the in-context and part-to-assembly flags also returns the files a part is built from: the header-beam skeleton (used by the three main-member parts), the cantilever brace (used by the cantilever return part) and the Hilti STEP file. The probe now records these as "part link" and "linked file".
11. **Linked equation files are invisible to the Document Manager.** The header-beam skeleton uses an equations `.txt` file that Pack and Go didn't bring. No Document Manager call lists it. The import will need SOLIDWORKS itself to find it (or a manual check in Tools > Equations).

## Still to test

- Write a custom property and confirm SOLIDWORKS sees it (on a copy).
- Repoint a reference with `ReplaceReference` (on a copy).
- Revision tables on a real drawing. This Pack and Go included no drawings.
