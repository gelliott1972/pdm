r"""Turn the reviewed J2 / J3 property sheet into DmEdit batch actions (read-only: writes only the .tsv).

Reads the Proposed columns of the Properties sheet and the DrawingRef sheet (from props_sheet.py, after Grant and
James have filled it in), compares them with a fresh DmProbe of the vault, and writes one line per change:
  Description and Weight at the row's level (configuration, or @file for a file-level PartNo),
  Finish once per file at @file (all rows of a file must agree),
  PartNo where the vault has none yet (D17),
  delete every DrawingRef listed.
Each line carries the value the probe saw, so DmEdit batch refuses to write if a file has changed since.
Nothing is written where the vault already has the proposed value; blank proposals are skipped and listed.

With --since <sheetProbeDir> (the probe the sheet was built from), any Description, Finish, Weight, PartNo or
DrawingRef on a sheet file that changed in the vault since then is a problem: someone else has edited it, so the
sheet must be checked against the new value before anything is written.

Usage: python -I props_actions.py <sheet.xlsx> <probeDir> <out.tsv> [--since <sheetProbeDir>]
       (probeDir holds APEX\ and _Shared\ from DmProbe; paths in the .tsv are relative to the vault root, P:\)
"""
import collections, csv, os, sys
import openpyxl

args = sys.argv[1:]
since = None
if "--since" in args:
    i = args.index("--since")
    since = args[i + 1] if i + 1 < len(args) else None
    del args[i:i + 2]
if len(args) != 3 or ("--since" in sys.argv and not since):
    sys.exit("Usage: python -I props_actions.py <sheet.xlsx> <probeDir> <out.tsv> [--since <sheetProbeDir>]")
sheet, probe, out = args

def props(probe):
    e = {}
    for area in ("APEX", "_Shared"):
        for p in csv.DictReader(open(os.path.join(probe, area, "properties.csv"), encoding="utf-8-sig")):
            if p["name"]:
                e[(p["file"].lower(), "@file" if p["config"] == "(file)" else p["config"], p["name"].lower())] = p["expression"]
    return e

expr, cfgs, folder = props(probe), set(), {}
for area in ("APEX", "_Shared"):
    d = os.path.join(probe, area)
    for c in csv.DictReader(open(os.path.join(d, "configs.csv"), encoding="utf-8-sig")):
        cfgs.add((c["file"].lower(), c["config"]))
    for f in csv.DictReader(open(os.path.join(d, "files.csv"), encoding="utf-8-sig")):
        folder[f["file"].lower()] = area + "\\" + f["folder"]
cur = lambda f, lvl, n: expr.get((f.lower(), lvl, n.lower()))

wb = openpyxl.load_workbook(sheet, read_only=True)
rows = list(wb["Properties"].iter_rows(values_only=True))
head = rows[0]
col = {h: i for i, h in enumerate(head)}
need = ["PartNo", "File", "Configuration", "Vault folder", "Proposed Description", "Proposed Finish (file level)", "Proposed Weight"]
missing = [h for h in need if h not in col]
if missing:
    sys.exit("Sheet is missing columns: %s" % missing)
val = lambda r, h: ("" if r[col[h]] is None else str(r[col[h]])).strip()

acts, skipped, problems = [], [], []
def act(f, lvl, name, op, value=""):
    old = cur(f, lvl, name)
    if op == "set" and old == value:
        return
    if any(c in (value or "") for c in "\t\r\n"):
        problems.append("%s [%s] %s: value has a tab or line break" % (f, lvl, name))
    rel = folder[f.lower()] + "\\" + f
    acts.append([rel, lvl, name, op, value, "(none)" if old is None else old])

finish = collections.defaultdict(set)
for r in rows[1:]:
    f, cfg, pn = val(r, "File"), val(r, "Configuration"), val(r, "PartNo")
    if not f:
        continue
    if f.lower() not in folder:
        problems.append("%s: not in the probe" % f)
        continue
    lvl = "@file" if cfg == "(file)" else cfg
    if lvl != "@file" and (f.lower(), cfg) not in cfgs:
        problems.append("%s: no configuration %s in the probe" % (f, cfg))
        continue
    have = cur(f, lvl, "PartNo")
    if have is None:
        act(f, lvl, "PartNo", "set", pn)                               # D17
    elif have != pn:
        problems.append("%s [%s]: sheet PartNo %s, vault %s" % (f, lvl, pn, have))
    for name, h in (("Description", "Proposed Description"), ("Weight", "Proposed Weight")):
        v = val(r, h)
        if v:
            act(f, lvl, name, "set", v)
        else:
            skipped.append("%s [%s] %s" % (f, lvl, name))
    v = val(r, "Proposed Finish (file level)")
    if v:
        finish[f].add(v)
    else:
        skipped.append("%s [@file] Finish (row %s)" % (f, cfg))
for f, vs in sorted(finish.items()):
    if len(vs) > 1:
        problems.append("%s: rows propose different Finish values %s" % (f, sorted(vs)))
    else:
        act(f, "@file", "Finish", "set", next(iter(vs)))

for r in list(wb["DrawingRef"].iter_rows(values_only=True))[1:]:
    if not r[0]:
        continue
    f, lvl = str(r[0]), "@file" if r[1] == "(file)" else str(r[1])
    if cur(f, lvl, "DrawingRef") is None:
        problems.append("%s [%s]: no DrawingRef in the probe" % (f, lvl))
    else:
        acts.append([folder[f.lower()] + "\\" + f, lvl, "DrawingRef", "del", "", cur(f, lvl, "DrawingRef")])

if since:
    old = props(since)
    files = {val(r, "File").lower() for r in rows[1:] if val(r, "File")}
    for k in sorted(set(old) | set(expr)):
        if k[0] in files and k[2] in ("description", "finish", "weight", "partno", "drawingref") and old.get(k) != expr.get(k):
            problems.append("%s [%s] %s changed in the vault since the sheet: %r -> %r" % (k[0], k[1], k[2], old.get(k), expr.get(k)))

if problems:
    print("PROBLEMS (no .tsv written):")
    for p in problems:
        print("  " + p)
    sys.exit(1)
with open(out, "w", encoding="utf-8", newline="") as fh:
    fh.write("file\tlevel\tname\top\tvalue\told\r\n")
    for a in acts:
        fh.write("\t".join(a) + "\r\n")
n = collections.Counter((a[2], a[3]) for a in acts)
print("changes:", len(acts), "in", len(set(a[0] for a in acts)), "files:", dict(sorted(n.items())))
print("skipped (blank in the sheet):", len(skipped))
