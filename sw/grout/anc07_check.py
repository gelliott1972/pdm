"""0807 floor anchors (APEX-A84, 24 Nov 2026): compare two SwGrout xforms files of an 0807 +STRUCTURE top.
Every component in "before" must be in "after" at the same place; the new components must be exactly 28 kits
(22 FLOOR_200X200_M12x185 + 6 FLOOR_150X200_M12x185) with 4 FAST-HST3-R-M12x185-A4 each, kit Y axis up (anchors
below the plate), and nothing else new.
Usage: python sw/grout/anc07_check.py <before.txt> <after.txt>
"""
import collections
import sys


def load(p):
    d = {}
    for line in open(p, encoding='utf-8-sig'):
        f = line.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            d[f[1]] = (f[2], f[3], [float(x) for x in f[4].replace('|', ' ').split()])
    return d


b, a = load(sys.argv[1]), load(sys.argv[2])
ok = True
moved = [n for n in b if n not in a or a[n][0] != b[n][0] or a[n][1] != b[n][1] or any(abs(x - y) > 1e-3 for x, y in zip(a[n][2], b[n][2]))]
print('components before %d, after %d; earlier components moved, changed or missing: %d %s' % (len(b), len(a), len(moved), moved[:5]))
ok &= not moved
new = {n: v for n, v in a.items() if n not in b}
kits = {n: v for n, v in new.items() if v[0] == 'MDL-ANCHOR-GNRIC.SLDASM'}
cfg = collections.Counter(v[1] for v in kits.values())
print('new kits %d: %s' % (len(kits), dict(cfg)))
ok &= len(kits) == 28 and cfg == {'<FLOOR_200X200_M12x185>': 22, '<FLOOR_150X200_M12x185>': 6}
anchors = collections.Counter()
other = []
for n, v in new.items():
    if n in kits:
        continue
    parent = n.rsplit('/', 1)[0]
    if parent in kits and v[0] == 'FAST-HST3-R-M12x185-A4.SLDPRT':
        anchors[parent] += 1
    else:
        other.append((n, v[0]))
print('anchors per kit:', dict(collections.Counter(anchors[k] for k in kits)), '; other new components:', other[:5])
ok &= all(anchors[k] == 4 for k in kits) and not other
up = collections.Counter(tuple(round(x, 6) for x in v[2][3:6]) for v in kits.values())
print('kit Y axis in the top (anchor axis, up = nut side):', dict(up))
ok &= set(up) == {(0.0, 1.0, 0.0)}
print('kit origin heights (plate top, mm):', sorted(set(round(v[2][10], 3) for v in kits.values())))
print('RESULT', 'OK' if ok else 'PROBLEM')
sys.exit(0 if ok else 1)
