"""Summarise DmRefPaths output (calc-08a-11 / Grant 21 Nov 2026): every stored path not under P:\\, by file, leaving out
drawing-view names (Document Manager gives views a bare file name, not a path). Read-only.
Usage: python sw/white/refpaths-report.py <DmRefPaths output> [file filter, comma-separated substrings]
"""
import collections, sys

rows = [l.rstrip('\n').split('\t') for l in open(sys.argv[1], encoding='utf-8', errors='replace')]
filt = [s.upper() for s in sys.argv[2].split(',')] if len(sys.argv) > 2 else None
keep = lambda f: filt is None or any(s in f.upper() for s in filt)
notp = [r for r in rows if r[0] == 'NOTP' and ':' in r[3] and keep(r[1])]
supp = {(r[1], r[2], r[5]): r[6] for r in rows if r[0] == 'COMP'}
by = collections.defaultdict(list)
for r in notp: by[r[1]].append(r)
print('files with a stored path not under P:\\ :', len(by))
for f in sorted(by):
    print(f)
    c = collections.Counter()
    for r in by[f]:
        cfg = r[2].replace('component in ', '')
        c[(r[3], 'suppressed' if supp.get((f, cfg, r[3])) == 'suppressed' else 'active', cfg if r[2].startswith('component') else r[2])] += 1
    for (p, s, where), n in sorted(c.items()): print('   %s  [%s, %s] x%d' % (p, where, s, n))
broken = [r for r in rows if r[0] == 'REF' and r[3] != '2' and keep(r[1])]
print('\nREF lines with status other than 2:')
for r in broken: print('  ', r[1], '->', r[2], 'status', r[3])
