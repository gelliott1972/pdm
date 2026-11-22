"""Grout 10 mm rehearsal (22 Nov 2026): compare two position files from SwGrout xforms, grouped by part file and move.
Usage: python sw/grout/compare.py <before.txt> <after.txt>
"""
import collections, sys


def load(p):
    d = {}
    for l in open(p, encoding='utf-8'):
        f = l.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            nums = [float(x) for x in f[4].replace('|', ' ').split()]
            d[f[1]] = (f[2], f[3], nums)
    return d


b, a = load(sys.argv[1]), load(sys.argv[2])
groups = collections.Counter()
rot = []
for n in b:
    if n not in a:
        groups[('MISSING AFTER', b[n][0], '')] += 1
        continue
    x, y = b[n][2], a[n][2]
    if any(abs(p - q) > 1e-4 for p, q in zip(x[:9], y[:9])):
        rot.append(n)
    dv = tuple(round(q - p, 2) for p, q in zip(x[9:12], y[9:12]))
    if any(abs(v) > 0.01 for v in dv):
        groups[('moved %+.2f %+.2f %+.2f' % dv, b[n][0], b[n][1])] += 1
for n in a:
    if n not in b:
        groups[('NEW AFTER', a[n][0], '')] += 1
print('components before %d, after %d; rotated: %d %s' % (len(b), len(a), len(rot), rot[:5]))
if not groups:
    print('nothing moved')
for (what, f, cfg), c in sorted(groups.items()):
    print('%-28s %4d x %s %s' % (what, c, f, cfg))
