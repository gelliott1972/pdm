"""C18 check (25 Nov 2026): LS-08C-11 before/after (SwGrout xforms of LS-08C-11+STRUCTURE) and LS-08A-11 (xforms) as the
reference. x_C = -9881.174 - x_A.
- removed exactly: sleeves 08XXX-HBA-01-19 and -1, spigots 08C11-HBA-01-1/08XXX-HBW-01-6 and 08C11-HBA-03-1/08XXX-HBW-01-1
- added exactly: the two 08X11-HBW-05 spigots and two 08X11-HBA-04 (with their HBW-04 and DBW-01)
- changed only: bolt kits 5 and 1 (BRACED_ROOF_SLEEVE) and the parts inside them; nothing else moved
- each new part (HBW-05, HBW-04, DBW-01) and each changed kit at A's mirror: identity rotation (kits: A's rotation),
  x = -9881.174 - x_A, same y and z, to 0.1 mm; bolt kits still 10; the kits' insides as A's (same kit, same rotation)
Usage: python sw/grout/c18_check.py <C before> <C after> <A xforms>
"""
import collections
import sys

M = -9881.174


def load(p):
    d = {}
    for line in open(p, encoding='utf-8-sig'):
        f = line.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            d[f[1]] = (f[2], f[3], [float(x) for x in f[4].replace('|', ' ').split()])
    return d


b, a, ref = load(sys.argv[1]), load(sys.argv[2]), load(sys.argv[3])
CS, AS = '08C11-STA-01-1/', '08A11-STA-01-1/'
ok = True
inkit = lambda n: n.startswith(CS + 'MDL-ROOF-SLEEVE-BOLT-08XXX-5/') or n.startswith(CS + 'MDL-ROOF-SLEEVE-BOLT-08XXX-1/')
# the two kits' insides change with their configuration (other bolt instances): checked below against A's kits
removed = sorted(n for n in set(b) - set(a) if not inkit(n)); added = sorted(n for n in set(a) - set(b) if not inkit(n))
exp_removed = sorted(CS + n for n in ['08XXX-HBA-01-19', '08XXX-HBA-01-1', '08C11-HBA-01-1/08XXX-HBW-01-6', '08C11-HBA-03-1/08XXX-HBW-01-1'])
print('components before %d after %d; removed %d, added %d' % (len(b), len(a), len(removed), len(added)))
print('  removed:', [r.replace(CS, '') for r in removed])
print('  added:  ', [r.replace(CS, '') for r in added])
ok &= removed == exp_removed and len(added) == 8
changed = sorted(n for n in a if n in b and not inkit(n) and (a[n][0] != b[n][0] or a[n][1] != b[n][1] or any(abs(x - y) > 1e-3 for x, y in zip(a[n][2], b[n][2]))))
kits_changed = sorted(set(n.split('/')[1] for n in changed))
print('  changed (moved or configuration):', len(changed), [n.replace(CS, '') for n in changed])
ok &= set(n.replace(CS, '') for n in changed) <= {'MDL-ROOF-SLEEVE-BOLT-08XXX-5', 'MDL-ROOF-SLEEVE-BOLT-08XXX-1'}
kits = [n for n in a if n.count('/') == 1 and a[n][0] == 'MDL-ROOF-SLEEVE-BOLT-08XXX.SLDASM']
print('  bolt kits after:', len(kits), dict(collections.Counter(a[n][1] for n in kits)))
ok &= len(kits) == 10
# mirror pairs: C name -> A name
pairs = {
    '08C11-HBA-01-1/08X11-HBW-05-6': '08A11-HBA-01-2/08X11-HBW-05-6',
    '08C11-HBA-03-1/08X11-HBW-05-1': '08A11-HBA-03-1/08X11-HBW-05-2',
    '08X11-HBA-04-1/08X11-HBW-04-1': '08X11-HBA-04-25/08X11-HBW-04-1',
    '08X11-HBA-04-1/08X11-DBW-01-1': '08X11-HBA-04-25/08X11-DBW-01-1',
    '08X11-HBA-04-2/08X11-HBW-04-1': '08X11-HBA-04-26/08X11-HBW-04-1',
    '08X11-HBA-04-2/08X11-DBW-01-1': '08X11-HBA-04-26/08X11-DBW-01-1',
    'MDL-ROOF-SLEEVE-BOLT-08XXX-5': 'MDL-ROOF-SLEEVE-BOLT-08XXX-4',
    'MDL-ROOF-SLEEVE-BOLT-08XXX-1': 'MDL-ROOF-SLEEVE-BOLT-08XXX-8',
}
for c, r in pairs.items():
    cv, rv = a.get(CS + c), ref.get(AS + r)
    if cv is None or rv is None:
        print('  MISSING', c, cv is None, r, rv is None); ok = False; continue
    rot_ok = all(abs(x - y) < 1e-6 for x, y in zip(cv[2][:9], rv[2][:9]))
    dx = cv[2][9] - (M - rv[2][9]); dy = cv[2][10] - rv[2][10]; dz = cv[2][11] - rv[2][11]
    good = rot_ok and max(abs(dx), abs(dy), abs(dz)) <= 0.1 and cv[0] == rv[0] and cv[1] == rv[1]
    print('  %-34s at (%.3f, %.3f, %.3f)  A %-32s (%.3f) mirrored %.3f  off %.3f %.3f %.3f  rotation same %s  file/config same %s  %s'
          % (c, cv[2][9], cv[2][10], cv[2][11], r, rv[2][9], M - rv[2][9], dx, dy, dz, rot_ok, cv[0] == rv[0] and cv[1] == rv[1], 'OK' if good else 'PROBLEM'))
    ok &= good
# parts inside the two changed kits: the same kit as A's in the same rotation, so A's parts shifted by the kit's move
for c, r in (('MDL-ROOF-SLEEVE-BOLT-08XXX-5', 'MDL-ROOF-SLEEVE-BOLT-08XXX-4'), ('MDL-ROOF-SLEEVE-BOLT-08XXX-1', 'MDL-ROOF-SLEEVE-BOLT-08XXX-8')):
    sx = a[CS + c][2][9] - ref[AS + r][2][9]
    cs = sorted((v[0], round(v[2][9], 1), round(v[2][10], 1), round(v[2][11], 1)) for n, v in a.items() if n.startswith(CS + c + '/'))
    rs = sorted((v[0], round(v[2][9] + sx, 1), round(v[2][10], 1), round(v[2][11], 1)) for n, v in ref.items() if n.startswith(AS + r + '/'))
    print('  inside %s: %d parts, same as A %s shifted %.3f in x: %s' % (c, len(cs), r, sx, cs == rs))
    ok &= cs == rs
print('RESULT', 'OK' if ok else 'PROBLEM')
sys.exit(0 if ok else 1)
