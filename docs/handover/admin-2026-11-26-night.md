# Admin handover, 26 Nov 2026 (night before the Friday Disney DRAFT)

## Running now
- **sw [dd36e0]**: every-bolt-through-a-hole check on the STEP exports, read-only. LS-08A-11 run 2026-11-22T1036Z and LS-08A-06 first, then B/C 06 and 11. Results go to `C:\PDM-work\wdi-slots\<LS> bolt-hole check.txt`. Tools: `sw\sestp\steppos.py`, `sleevebolts.py` (being generalised). When it reports: push its commit and give apex-admin the result as the reference for revit-fiona's Revit check.
- **sw [4195ea]**: standing by. No lock and nothing checked out. Part-made sleeve section pictures are in `C:\PDM-work\wdi-slots\try`.
- A second Admin window (a copy of this conversation) was also giving SW jobs. Grant is closing one of the two.

## Friday 27 Nov: DRAFT to Disney, LS-08A-06 + LS-08A-11 (Revit shop drawings + draft calcs)
- APEX-A109: only Disney's round-1 fixes go in; the weekend changes (A94, A95, A97, A99, A100, A101) are listed in a cover note as "next issue". **No SOLIDWORKS vault change.**
- apex-admin runs the package and has the plan (my message of this evening). The file list goes to Grant on the Desk.
- Revit: 2 sleeves turned 90 degrees on LS-08A-11 (apex-admin-43), and the same on LS-08A-06 (apex-admin-45). SOLIDWORKS and the STEP are right (sw checked: all 40 bolts in slots). revit-fiona is proving the fix on a copy; Fiona applies it in the morning. Likely cause passed on: body ~n swap in 08XXX-HBA-01 (research doc line 97).
- Waiting on apex-admin: its settled list for Friday, and anything to run overnight for Kevin.

## Grant's rules from today
- Reviewer comments are checked read-only and reported; nothing in the model changes without talking to Grant (memory: review-comments-check-first).

## Later
- Grant may want the standing Revit-against-STEP check (every part present, position, and turn judged by its holes) built next week by revit-fiona and SW.
- A20 (floor plates before the Revit stairs export); the AWS budget alert email still unconfirmed (check `aws notificationscontacts list-email-contacts`).
- Kevin portal: built and checked; the budget is US$20/month.
