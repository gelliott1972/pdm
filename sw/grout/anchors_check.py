"""Anchors step (22 Nov 2026): compare two SwGrout xforms files of LS-08A-06+STRUCTURE: the 24 kits must not move, the
floor kits must hold 4 x M12x185 and the roof kits 4 x M12x125, anchor origins unchanged, nothing else moved.
Usage: python sw/grout/anchors_check.py <before.txt> <after.txt>
"""
import collections, sys


def load(p):
    d = {}
    for l in open(p, encoding='utf-8'):
        f = l.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            d[f[1]] = (f[2], f[3], [float(x) for x in f[4].replace('|', ' ').split()])
    return d


b, a = load(sys.argv[1]), load(sys.argv[2])
kits = lambda d: {n: v for n, v in d.items() if v[0].startswith('MDL-ANCHOR') and n.count('/') == 1}
kb, ka = kits(b), kits(a)
pos = lambda k: sorted(tuple(round(x, 3) for x in v[2]) for v in k.values())
ok = True
print('kits before %d after %d; kit positions identical: %s' % (len(kb), len(ka), pos(kb) == pos(ka)))
ok &= len(kb) == len(ka) == 24 and pos(kb) == pos(ka)
print('kit files/configs after:', dict(collections.Counter((v[0], v[1]) for v in ka.values())))
per = collections.Counter()
for n, v in a.items():
    if 'FAST-HST3' in v[0]:
        kit = '/'.join(n.split('/')[:2])
        per[(ka.get(kit, ('?', '?'))[1], v[0])] += 1
print('anchors after, by kit configuration:', dict(per))
anc = lambda d: sorted(tuple(round(x, 2) for x in v[2][9:12]) for v in d.values() if 'FAST-HST3' in v[0])
print('anchor origins identical:', anc(b) == anc(a), '(%d before, %d after)' % (len(anc(b)), len(anc(a))))
ok &= anc(b) == anc(a)
kitname = lambda n: n.split('/')[1].rsplit('-', 1)[1] if n.count('/') >= 1 else ''
others = [n for n in b if 'ANCHOR' not in n and 'FAST-HST3' not in n and (n not in a or any(abs(x - y) > 0.01 for x, y in zip(b[n][2], a[n][2])))]
print('other components moved or missing:', len(others), others[:5])
ok &= not others
print('RESULT', 'OK' if ok else 'PROBLEMS')
