r"""Weekend B redo (Desk A30): compare two whole-top xforms files (SwGrout xforms), every component, with its movement even
when its configuration changed. Prints a summary grouped by file, configuration change and movement.
Usage: python bdiff.py <before.txt> <after.txt>
"""
import collections
import sys


def load(p):
    d = {}
    for l in open(p, encoding='utf-8-sig'):
        f = l.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            rot, org = f[4].split('|')
            d[f[1]] = (f[2], f[3], [float(x) for x in rot.split()], [float(x) for x in org.split()])
    return d


a, b = load(sys.argv[1]), load(sys.argv[2])
groups = collections.Counter()
rows = []
for k in sorted(set(a) | set(b)):
    if k not in a or k not in b:
        groups[('ADDED' if k not in a else 'REMOVED', (b.get(k) or a.get(k))[0], '', '')] += 1
        continue
    fa, ca, ra, oa = a[k]
    fb, cb, rb, ob = b[k]
    d = tuple(round(ob[i] - oa[i], 3) for i in range(3))
    rot = max(abs(ra[i] - rb[i]) for i in range(9)) > 1e-5
    if d == (0.0, 0.0, 0.0) and not rot and ca == cb:
        continue
    groups[('CHANGED', fa, (ca + ' -> ' + cb) if ca != cb else '', str(d) + (' ROTATED' if rot else ''))] += 1
    rows.append((k, fa, ca, cb, d, rot))
print('components', len(a), '->', len(b), '; changed', len(rows))
for (kind, f, c, d), n in sorted(groups.items(), key=lambda x: (x[0][1], x[0][3])):
    print('%4d  %-8s %-38s %-12s %s' % (n, kind, f, d, c))
if len(sys.argv) > 3:
    for r in rows:
        print('ROW', r[0], r[1], r[2], '->', r[3], r[4], 'ROTATED' if r[5] else '')
