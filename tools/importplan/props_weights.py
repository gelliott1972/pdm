r"""Check that every Weight the DmEdit batch set evaluates to a number in SOLIDWORKS, from SwKg's output.
SwKg prints, per saved file, "WEIGHT<tab>config<tab>raw<tab>evaluated[<tab>(not resolved)]" for @file and each
configuration. Files SwKg did not save (already kg, refused) have no WEIGHT lines and are listed as not checked.
Read-only.

Usage: python -I props_weights.py <swkg.txt> <actions.tsv>
"""
import csv, os, re, sys

if len(sys.argv) != 3:
    sys.exit("Usage: python -I props_weights.py <swkg.txt> <actions.tsv>")
swkg, tsv = sys.argv[1:3]

seen, cur = {}, None   # (file lower, level) -> (raw, evaluated, resolved)
for line in open(swkg, encoding="utf-8-sig", errors="replace"):
    line = line.rstrip("\r\n")
    m = re.match(r"^(\S.*?\.sld(?:prt|asm|drw)): ", line, re.I)
    if m:
        cur = os.path.basename(m.group(1)).lower()
        continue
    if cur and line.strip().startswith("WEIGHT\t"):
        f = line.strip().split("\t")
        if len(f) >= 4:
            seen[(cur, f[1])] = (f[2], f[3], len(f) < 5)

acts = [a for a in csv.DictReader(open(tsv, encoding="utf-8", newline=""), delimiter="\t", quoting=csv.QUOTE_NONE)
        if a["op"] == "set" and a["name"].lower() == "weight"]
ok, bad, unchecked = 0, [], []
for a in acts:
    k = (os.path.basename(a["file"]).lower(), a["level"])
    if k[0] not in {s[0] for s in seen}:
        unchecked.append("%s [%s]" % k)
    elif k not in seen:
        bad.append("%s [%s]: no WEIGHT line" % k)
    else:
        raw, val, resolved = seen[k]
        if raw != a["value"].strip('"') and raw != a["value"]:
            bad.append("%s [%s]: formula %r, expected %r" % (k + (raw, a["value"])))
        elif not resolved or not re.match(r"^\d+(\.\d+)?$", val.strip()):
            bad.append("%s [%s]: evaluates to %r%s" % (k + (val, "" if resolved else " (not resolved)")))
        else:
            ok += 1
print("Weight actions: %d | evaluate to a number: %d | wrong: %d | file not in SwKg output: %d"
      % (len(acts), ok, len(bad), len(unchecked)))
for b in bad[:50]:
    print("  WRONG " + b)
for u in unchecked[:50]:
    print("  NOT CHECKED " + u)
sys.exit(1 if bad else 0)
