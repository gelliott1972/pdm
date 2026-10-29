r"""Check a property run: compare two DmProbes (before, after) against the DmEdit batch actions.
Every property difference must be one of the actions, every action must show in the after probe, and nothing else
may differ in files, configurations, components or references. Read-only.

Usage: python -I props_compare.py <beforeProbeDir> <afterProbeDir> <actions.tsv>
       (each probe dir holds APEX\ and _Shared\ from DmProbe)
"""
import csv, os, re, sys

if len(sys.argv) != 4:
    sys.exit("Usage: python -I props_compare.py <beforeProbeDir> <afterProbeDir> <actions.tsv>")
before, after, tsv = sys.argv[1:4]

def load(d, name, keyf):
    out = {}
    for area in ("APEX", "_Shared"):
        for r in csv.DictReader(open(os.path.join(d, area, name), encoding="utf-8-sig")):
            for c in ("reference", "stored_path"):   # a scratch copy resolves (and SOLIDWORKS re-saves) paths
                if c in r:                            # inside itself: compare from APEX\ or _Shared\ on
                    r[c] = re.sub(r"^.*?\\(?=(APEX|_Shared)\\)", "<vault>\\\\", r[c], flags=re.I)
            out[keyf(area, r)] = r
    return out

pk = lambda a, r: (r["file"].lower(), "@file" if r["config"] == "(file)" else r["config"], r["name"].lower())
pb = {k: v["expression"] for k, v in load(before, "properties.csv", pk).items() if k[2]}
pa = {k: v["expression"] for k, v in load(after, "properties.csv", pk).items() if k[2]}

want = dict(pb)
acts = list(csv.DictReader(open(tsv, encoding="utf-8", newline=""), delimiter="\t", quoting=csv.QUOTE_NONE))
for a in acts:
    k = (os.path.basename(a["file"]).lower(), a["level"], a["name"].lower())
    if a["op"] == "set":
        want[k] = a["value"]
    else:
        want.pop(k, None)

bad = []
for k in sorted(set(want) | set(pa)):
    if want.get(k) != pa.get(k):
        bad.append("property %s [%s] %s: expected %r, after %r" % (k + (want.get(k), pa.get(k))))

# Everything except properties (and fields that follow from them) must be identical.
skip = {"file_PartNo", "config_PartNos", "file_prop_names", "PartNo", "prop_count"}
for name, keyf in (("files.csv", lambda a, r: (a, r["file"].lower())),
                   ("configs.csv", lambda a, r: (r["file"].lower(), r["config"])),
                   ("components.csv", lambda a, r: tuple(r.values())),
                   ("references.csv", lambda a, r: tuple(r.values()))):
    b, f = load(before, name, keyf), load(after, name, keyf)
    if set(b) != set(f):
        bad.append("%s: rows differ (%d only before, %d only after)" % (name, len(set(b) - set(f)), len(set(f) - set(b))))
    for k in set(b) & set(f):
        diff = [c for c in b[k] if c not in skip and b[k][c] != f[k].get(c)]
        if diff:
            bad.append("%s %s: %s changed" % (name, k, diff))

print("actions:", len(acts), "| property differences matching the actions:", sum(1 for k in set(pb) | set(pa) if pb.get(k) != pa.get(k)))
if bad:
    print("NOT CLEAN:", len(bad))
    for b in bad[:50]:
        print("  " + b)
    sys.exit(1)
print("CLEAN: only the listed changes; files, configurations, components and references unchanged")
