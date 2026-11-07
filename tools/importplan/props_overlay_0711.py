r"""New J2/J3 sheet (7 Nov): today's base sheet (props_sheet --since vault-P-2810) with James's 29 Oct suggested
Descriptions laid over it, and Grant's two slips fixed. Where the vault's Description on that file changed since
28 Oct (someone checked in a new one), the vault's value is kept and James's text is shown for Grant.
Usage: python -I sheet0711.py <james.xlsx> <sheet2810.xlsx> <base.xlsx> <out.xlsx> [--james-wins]"""
import sys, collections, openpyxl
from openpyxl.styles import PatternFill
from copy import copy
args = [a for a in sys.argv[1:] if not a.startswith("--")]
james_wins = "--james-wins" in sys.argv
jp, op, bp, out = args

def rows(p):
    ws = openpyxl.load_workbook(p, read_only=True)["Properties"]
    r = list(ws.iter_rows(values_only=True))
    return r[0], {(x[2], x[3]): x for x in r[1:]}

hj, J = rows(jp)
_, O = rows(op)
D, NT = hj.index("Proposed Description"), hj.index("Notes")
SLIPS = {  # Grant 7 Nov: "SUB-ASSY 1" should be 2; 08C11-HBA should match A and B
    ("08X11-LMW.SLDASM", "LS-08X-11-LCMM40X80-01B"): ("LS-08X-11 LED MOUNT MEMBER SUB-ASSY 1", "LS-08X-11 LED MOUNT MEMBER SUB-ASSY 2"),
    ("08C11-HBA-01.SLDASM", "Default"): ("LS-08C-11 HEADER BEAM 1 WELDED ASSY", "LS-08C-11 HEADER BEAM 1 ASSY"),
    ("08C11-HBA-02.SLDASM", "Default"): ("LS-08C-11 HEADER BEAM 2 WELDED ASSY", "LS-08C-11 HEADER BEAM 2 ASSY"),
    ("08C11-HBA-03.SLDASM", "Default"): ("LS-08C-11 HEADER BEAM 3 WELDED ASSY", "LS-08C-11 HEADER BEAM 3 ASSY"),
}
wb = openpyxl.load_workbook(bp)
ws = wb["Properties"]
head = [c.value for c in ws[1]]
cd, cn, cs = head.index("Proposed Description"), head.index("Notes"), head.index("Changed since")
cj = len(head) + 1
ws.cell(1, cj, "Description source (7 Nov)")
ws.cell(1, cj).font, ws.cell(1, cj).fill = copy(ws.cell(1, 1).font), copy(ws.cell(1, 1).fill)
take, keep = PatternFill("solid", fgColor="E2EFDA"), PatternFill("solid", fgColor="FCE4D6")
n = collections.Counter()
seen = set()
for row in ws.iter_rows(min_row=2):
    k = (row[2].value, row[3].value)
    if k not in J:
        continue
    seen.add(k)
    j, o = J[k], O[k]
    jd, od = j[D], o[D]
    src = None
    vault_changed = "description" in str(row[cs].value or "").lower()
    if k in SLIPS:
        was, now = SLIPS[k]
        base = jd if (jd or None) != (od or None) else row[cd].value
        if base != was:
            sys.exit("slip row %s: expected %r, found %r" % (k, was, base))
        jd, src = now, "slip fixed (Grant 7 Nov), was %r" % was
        n["slip fixed"] += 1
    elif (jd or None) != (od or None):
        src = "James 29 Oct"
    if src:
        if not jd:
            ws.cell(row[0].row, cj).value = "James left it blank; sheet value kept"
            n["James blank, kept"] += 1
        elif vault_changed and not james_wins and row[cd].value != jd:
            ws.cell(row[0].row, cj).value = "KEPT the vault's (changed since 28 Oct). %s suggests: %s" % (src, jd)
            row[cd].fill = keep
            n["vault changed since 28 Oct, kept, for Grant"] += 1
        else:
            if row[cd].value == jd:
                n["already the same"] += 1
            else:
                n["taken"] += 1
            row[cd].value = jd
            ws.cell(row[0].row, cj).value = src
            row[cd].fill = take
    if (j[NT] or None) != (o[NT] or None):
        row[cn].value = ((row[cn].value + " | ") if row[cn].value else "") + "James: " + j[NT]
        n["James note"] += 1
changed = {k for k in J if (J[k][D] or None) != (O[k][D] or None)} | set(SLIPS)
lost = sorted(changed - seen)
if lost:
    print("James rows not in the base sheet:", lost)
ws.column_dimensions[openpyxl.utils.get_column_letter(cj)].width = 70
wb.save(out)
print(dict(n))
