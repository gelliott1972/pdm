"""LS-08B-11 C16 check (25 Nov 2026): two SwGrout xforms files of LS-08B-11+STRUCTURE (before, after) and, optionally,
the 08B11-STA-01 mate lists before and after (SwGrout mates).
- removed must be exactly: 08B11-HBA-01/08XXX-HBW-01-1, sleeves 08XXX-HBA-01-23 and -16, bolt kit
  MDL-ROOF-SLEEVE-BOLT-08XXX-17 and the components inside that kit; nothing added
- every other component at the same place, same file and configuration
- bolt kits 9 -> 8; each remaining kit has the same number of mates as before (kit 12's two moved to sleeve 19, kit 18's one to spigot 2)
Usage: python sw/grout/c16_check.py <before.txt> <after.txt> [<mates before> <mates after>]
"""
import collections
import re
import sys

STA = '08B11-STA-01-1/'
GONE = [STA + '08B11-HBA-01-1/08XXX-HBW-01-1', STA + '08XXX-HBA-01-23', STA + '08XXX-HBA-01-16', STA + 'MDL-ROOF-SLEEVE-BOLT-08XXX-17']


def load(p):
    d = {}
    for line in open(p, encoding='utf-8-sig'):
        f = line.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            d[f[1]] = (f[2], f[3], [float(x) for x in f[4].replace('|', ' ').split()])
    return d


b, a = load(sys.argv[1]), load(sys.argv[2])
ok = True
removed = sorted(set(b) - set(a))
added = sorted(set(a) - set(b))
expected = sorted(n for n in b if any(n == g or n.startswith(g + '/') for g in GONE))
print('components before %d, after %d, removed %d (expected %d), added %d' % (len(b), len(a), len(removed), len(expected), len(added)))
ok &= removed == expected and not added
for g in GONE:
    print('  removed %-60s %s' % (g, 'yes' if g in removed else 'NO'))
print('  removed inside kit 17:', dict(collections.Counter(b[n][0] for n in removed if n.startswith(GONE[3] + '/'))))
moved = [n for n in a if n in b and (a[n][0] != b[n][0] or a[n][1] != b[n][1] or any(abs(x - y) > 1e-3 for x, y in zip(a[n][2], b[n][2])))]
print('other components moved or changed:', len(moved), moved[:5])
ok &= not moved
kits = lambda d: sorted(n for n in d if re.fullmatch(re.escape(STA) + r'MDL-ROOF-SLEEVE-BOLT-08XXX-\d+', n))
print('bolt kits before %d after %d' % (len(kits(b)), len(kits(a))))
ok &= len(kits(b)) == 9 and len(kits(a)) == 8
fast = lambda d: collections.Counter(v[0] for n, v in d.items() if v[0].startswith('FAST-'))
print('fasteners before', sum(fast(b).values()), 'after', sum(fast(a).values()), 'difference', dict(fast(b) - fast(a)))
if len(sys.argv) > 4:
    def mates(p):
        c = collections.Counter()
        for line in open(p, encoding='utf-8-sig', errors='replace'):
            if line.startswith('MATE\t'):
                if '\tERROR' in line:
                    c['ERROR'] += 1
                for k in set(re.findall(r'MDL-ROOF-SLEEVE-BOLT-08XXX-\d+', line)):
                    c[k] += 1
        return c
    mb, ma = mates(sys.argv[3]), mates(sys.argv[4])
    for k in sorted(set(mb) | set(ma)):
        print('  mates %-34s before %2d after %2d' % (k, mb[k], ma[k]))
    ok &= ma['ERROR'] == 0 and all(ma[k] == mb[k] for k in mb if k not in ('MDL-ROOF-SLEEVE-BOLT-08XXX-17', 'ERROR'))
print('RESULT', 'OK' if ok else 'PROBLEM')
sys.exit(0 if ok else 1)
