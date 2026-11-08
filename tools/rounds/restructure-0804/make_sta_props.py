# DmEdit batch actions for the three new 08x04-STA-01 assemblies (Admin 7 Nov: as 0811 D22/D25).
# At Default: PartNo 08x04-STA-01, PartNoStatus Provisional, Description "LS-08x-04 Structure"; every other property
# (file level or Default, e.g. from the assembly template) is deleted.
# Usage: python -I make_sta_props.py <DmProbe properties.csv> <folder of the STA-01s, relative to the DmEdit root> <out.tsv>
import csv
import sys

BS = chr(92)
props, folder, out = sys.argv[1:4]
want = {}
for x in "ABC":
    f = "08" + x + "04-STA-01.SLDASM"
    want[f] = {"PartNo": "08" + x + "04-STA-01", "PartNoStatus": "Provisional", "Description": "LS-08" + x + "-04 Structure"}
have = {}
for r in csv.DictReader(open(props, encoding="utf-8-sig")):
    if r["file"].upper() in (k.upper() for k in want):
        key = next(k for k in want if k.upper() == r["file"].upper())
        have.setdefault(key, {})[(r["config"], r["name"])] = r["expression"]
rows = []
for f, w in want.items():
    if f not in have:
        print("no properties found for", f, "(not probed yet, or none at all)")
    cur = have.get(f, {})
    path = folder + BS + f
    for (cfg, name), expr in sorted(cur.items()):
        level = "@file" if cfg == "(file)" else cfg
        if level == "Default" and name in w:
            continue
        rows.append([path, level, name, "del", "", expr])
    for name, val in w.items():
        old = cur.get(("Default", name))
        if old != val:
            rows.append([path, "Default", name, "set", val, "(none)" if old is None else old])
with open(out, "w", newline="", encoding="utf-8") as fh:
    fh.write("file\tlevel\tname\top\tvalue\told\r\n")
    for r in rows:
        fh.write("\t".join(r) + "\r\n")
print(len(rows), "actions ->", out)
