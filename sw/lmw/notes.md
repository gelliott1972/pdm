# GNRIC-LMW.SLDPRT: SHS fix, ready (15 Nov 2026, Admin's job; not started, waiting for Admin's go)

Read-only, on P:\ by Document Manager (`scratch\inventory\lmw-before`) and on a renamed copy in SOLIDWORKS (`C:\PDM-work\lmw-read\GNRIC-LMW-swread.SLDPRT`, same hash as P:\).

| Item | Found |
| --- | --- |
| Description | 21 values: file level "RHS 40x40x4mm MEMBER" + 20 configurations "RHS 40x40x4mm MEMBER x<len>mm". Change list ready: `props-draft.tsv` (RHS -> SHS, length kept) |
| Cut-list label | Cut-list folder "RHS40x40x4<1>", its Description "RHS40x40x4" is plain text in the part (no link). The body is named "Shs SHS40X40X4(1)". DmEdit can't change cut-list properties: needs SOLIDWORKS or a new tool |
| Profile (Draw) | 40x40 outside, 4 walls, 32 inside, R8/R4; profile sketch 40/40/4/R8. End caps 36x36x5, inset 2 mm, tube trimmed 5 mm |
| Configurations | Upside-down weldment tree: the 10 "<As Machined>" configurations are top level, each with one "<As Welded>" child under it. Both carry the same PartNo |
| Lengths (`lengths.txt`) | all 10 As Machined lengths match their descriptions; none of the 10 As Welded children do (e.g. LMW-01 541 vs child 1041, LMW-11 1841 vs child 587) |
| Uses in the vault (`scratch\inventory\apex-all-1115`) | 13, all in 08X04-LMW-40X40.SLDASM, all As Machined configurations. No As Welded child is used |

## Admin's answers (15 Nov)

- File-level Description: include (in `props-draft.tsv`).
- Cut-list label: by a Dev command (set-cutlist-prop, Admin asking Dev).
- Profile fine. Hold the check-out until Grant's D9 (end cap 36x36x5 keep or change).
- As Welded children: leave as they are.

## Later clean-up list

- GNRIC-LMW.SLDPRT: the 10 "<As Welded>" child configurations (unused; lengths don't match their PartNo/Description). The upside-down tree (As Machined on top).
- Draw's survey (`draw\cutlist-survey.md`): 51 more RHS/SHS cut-list labels.
