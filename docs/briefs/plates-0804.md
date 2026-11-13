# Brief: take the plates out of six 0804 weldments

From Admin, 13 Nov 2026, Grant's decisions in Admin (James agreed the rule on a call). Owners: **Dev** (the missing tool step), **SW** (dry run, real run, checks, check-in). Grant: "go"; aim to finish today, not overnight. James has checked everything in; nothing in the vault is checked out.

## Rule (James, 13 Nov)

A weldment part holds RHS/SHS members, end caps and occasionally a bracket. **Plates always go in an assembly** (LED plates and any top or bottom plates). K-beam parts (`KBP`, `BKP`, `PLP-05`) are fine as they are: leave them alone.

## Worked example: already done by James and Admin, 13 Nov

`08X04-HBW-05`: the old weldment part (plates inside) became
- `08X04-HBW-05.SLDASM`: PartNo `08X04-HBW-05`; holds the weldment plus the plates
- `08X04-HBW-06.SLDPRT`: PartNo `08X04-HBW-06`; the weldment only
- the old `08X04-HBW-05.SLDPRT` was removed from the vault (reason: replaced by the assembly)

Do the same for these six. The assembly keeps today's number; the weldment-only part takes the new one.

## The six (Admin's cut-list scan, 13 Nov; numbers approved by Grant)

| Today (`.SLDPRT`) | Folder | Used by (vault) | Plates inside today | Weldment only: new number | Plates in the new assembly |
| --- | --- | --- | --- | --- | --- |
| `08A04-HBW-02` | ShaftA | `08A04-SFA-2T` | 2 × 10 × 140 × 180, M12 slots (sketch) | `08A04-HBW-04` | 2 × `08X04-PLP-32` |
| `08A04-HBW-06` | ShaftA | `08A04-SFA-3T` | same | `08A04-HBW-05` | 2 × `08X04-PLP-32` |
| `08B04-SFP-01` | ShaftB | `08B04-SFA-3T` | same | `08B04-SFP-04` | 2 × `08X04-PLP-32` |
| `08B04-SFP-02` | ShaftB | `08B04-SFA-2T` | inserted `GNRIC-LPP` DOUBLE_BOTTOM | `08B04-SFP-05` | 1 × `GNRIC-LPP` config DOUBLE_BOTTOM |
| `08C04-SFP-02` | ShaftC | `08C04-SFA-2T` | 2 × 10 × 140 × 180, M12 slots (sketch) | `08C04-SFP-05` | 2 × `08X04-PLP-32` |
| `08C04-SFP-03` | ShaftC | `08C04-SFA-3T` | same | `08C04-SFP-06` | 2 × `08X04-PLP-32` |

None of the six has a drawing. End caps (5 × 32 × 72, 5 × 36 × 36) stay in the weldments. The scan read the active configuration only: check the other configurations (`08A04-HBW-02` and `-06` have As Machined and As Welded).

## Decisions (Grant, 13 Nov)

| # | Decision |
| --- | --- |
| P1 | New numbers as in the table above |
| P2 | Standard plate: a new configuration of `_Shared\08X04-PLP.SLDPRT`, 180 × 140 × 10 with the M12 slots, PartNo `08X04-PLP-32` (beside PLP-30 and -31, both 150 × 140 × 10). If the five parts' plates aren't all the same (slot size, spacing or direction), stop and ask Admin; don't invent more numbers |
| P3 | Plates go in **fixed** at exactly the position the plate bodies have today; no mates |
| P4 | Each new assembly: Default configuration, PartNo = today's number, `Provisional`, Description copied from today's part; no file-level PartNo. Weldment-only part: PartNo = the new number on every configuration, `Provisional`, Description as today's part; no file-level PartNo |

## How (Dev and SW agree the details)

1. **Dev:** add the missing step to `tools\SwCmd`: put a component (part + configuration) into an assembly, fixed, at the position and orientation of a named body in another part. Test it on a copy of one of the six first.
2. **SW:** add the `08X04-PLP-32` configuration (P2).
3. **Dry run on copies** outside `P:` (a scratch folder), all six:
   - Save As the part to the new number, then delete the plate features (bodies, slots, the inserted `GNRIC-LPP`) so only members and end caps are left
   - new assembly at today's number in the same folder: the weldment-only part fixed at the origin, plus the plates (P3)
   - in the SFA assembly, Replace Components: today's part → the new assembly, all instances
4. **Gate, each of the six:** the new assembly's mass equals today's part's mass (small differences from slot shapes only, listed); SwCheck on the SFA: opens with no errors, nothing missing, every reference inside the vault; PartNos as P4, no clash. **Mates in the SFA:** if Replace Components breaks mates to the old part's faces, re-attach them to the weldment-only part inside the new assembly. If that can't be done cleanly, fix the new assembly at the old part's exact position and list every mate dropped; Grant looks at those in the morning check.
5. **Real run** in the vault with the admin login: check out, change, check in (the new files, the six SFAs, `08X04-PLP`), then remove the six old `.SLDPRT` from the vault (reason "plates moved to assembly 0xx04-xxx-0n.SLDASM"). Do the removals only after the SFAs are checked in, as the vault refuses otherwise. Dev proposes the ledger rows; Admin adds them to `data\rename-ledger.csv`.
6. **Report:** SW sends Admin one message when done, with a table of the six (gate results, dropped mates, if any) and the list of the six SFAs for Grant's check in SOLIDWORKS.

## Firm lines

- SOLIDWORKS **2026** only. This PC also has 2024; the generic COM name `SldWorks.Application` starts 2024. Use SwCmd, or `SldWorks.Application.34`.
- Close All between sets, not Exit (few restarts).
- If anything in the vault is checked out by James when the real run starts, stop and tell Admin.
