"""Compare two versions of an assembly per configuration from DmRefPaths output (Grant 21 Nov 2026, roof sleeve kit):
components by name, their referenced configuration, file, suppression and stored position. Read-only.
Usage: python sw/white/kit-compare.py <old DmRefPaths output> <new DmRefPaths output>
"""
import collections, ntpath, sys


def load(p):
    d = collections.defaultdict(dict)
    for l in open(p, encoding='utf-8'):
        c = l.rstrip('\n').split('\t')
        if c[0] == 'COMP': d[c[2]][c[3]] = (c[4], ntpath.basename(c[5]).upper(), c[6], c[7])
    return d


o, n = load(sys.argv[1]), load(sys.argv[2])
print('configurations old:', {k: len(v) for k, v in o.items()})
print('configurations new:', {k: len(v) for k, v in n.items()})
for cfg in o:
    if cfg not in n: print(cfg, ': gone in new'); continue
    a, b = o[cfg], n[cfg]
    print('\n' + cfg)
    print('  only in old:', sorted(set(a) - set(b)) or 'none')
    print('  only in new:', sorted(set(b) - set(a)) or 'none')
    diff = sorted(k for k in set(a) & set(b) if a[k] != b[k])
    print('  same component, something different (config/file/suppressed/position):', len(diff))
    for k in diff: print('    ', k, a[k], '->', b[k])
    print('  active: old %d, new %d' % (sum(1 for v in a.values() if v[2] == ''), sum(1 for v in b.values() if v[2] == '')))
