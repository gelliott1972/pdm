"""Cut a DmProbe inventory down to what one top assembly reaches (components, suppressed or not, and linked files).

For a work copy that holds more than the screen being planned (0807: the old B/C model sits beside A's top; Admin's
brief, 24 Nov: A is planned alone, B/C added later as a mirror of A). The plan then sees one top. Files cut are listed.

Usage: python -I cut_probe.py <inventoryDir> <outDir> <top file name> [<top file name> ...]
"""
import csv, os, sys

inv, out, tops = sys.argv[1], sys.argv[2], [t.lower() for t in sys.argv[3:]]

def load(n):
    p = os.path.join(inv, n)
    if not os.path.isfile(p):
        return None, None
    with open(p, encoding="utf-8-sig", newline="") as f:
        r = csv.DictReader(f)
        return r.fieldnames, list(r)

data = {n: load(n) for n in ("files.csv", "configs.csv", "properties.csv", "components.csv", "references.csv", "views.csv", "others.csv")}
kids = {}
for c in data["components.csv"][1]:
    kids.setdefault(c["assembly"].lower(), set()).add(c["component_file"].lower())
for r in data["references.csv"][1]:
    kids.setdefault(r["file"].lower(), set()).add(os.path.basename(r["reference"]).lower())
names = {f["file"].lower() for f in data["files.csv"][1]}
missing = [t for t in tops if t not in names]
if missing:
    sys.exit("not in the probe: " + ", ".join(missing))
keep, stack = set(tops), list(tops)
while stack:
    for k in kids.get(stack.pop(), ()):
        if k not in keep:
            keep.add(k)
            stack.append(k)
os.makedirs(out, exist_ok=True)
col = {"files.csv": "file", "configs.csv": "file", "properties.csv": "file", "components.csv": "assembly", "references.csv": "file", "views.csv": "file", "others.csv": None}
for n, (hdr, rows) in data.items():
    if hdr is None:
        continue
    c = col[n]
    rows = rows if c is None else [r for r in rows if r[c].lower() in keep]
    with open(os.path.join(out, n), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=hdr)
        w.writeheader()
        w.writerows(rows)
kept = sorted(f["file"] for f in data["files.csv"][1] if f["file"].lower() in keep)
cut = sorted(f["file"] for f in data["files.csv"][1] if f["file"].lower() not in keep)
print(f"Kept {len(kept)} files reached from {', '.join(sys.argv[3:])}; cut {len(cut)}:")
for f in cut:
    print("  cut: " + f)
