"""Sleeve slots vs bolt axes from a positions file (SwGrout xforms / SESTP positions.txt), 26 Nov 2026 (WDI1-02/-03).
Each roof sleeve (08XXX-HBA-01, or the post 08X11-/08A11-HBW-04): its 4 slot pairs in part axes (z-wall pairs at y 50 and
250/350, x-wall pairs at y 100 and 300); each M20 bolt (FAST-HEX in a MDL-ROOF-SLEEVE-BOLT kit) by its own x axis (the
bolt's axis). A pair is "bolted" when a bolt's axis is parallel to the pair's axis (within 1 degree) and passes within
1 mm of the pair's centre line point.  Usage: python sleevebolts.py <positions.txt>
"""
import math, sys
SLOTS = {'08XXX-HBA-01.SLDPRT': [('Z', 50), ('Z', 250), ('X', 100), ('X', 300)],
         'HBW-04': [('Z', 50), ('Z', 350), ('X', 100), ('X', 300)]}
def load(p):
    d = {}
    for l in open(p, encoding='utf-8-sig'):
        f = l.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            d[f[1]] = (f[2], f[3], [float(x) for x in f[4].replace('|', ' ').split()])
    return d
def pt(T, p): return [p[0]*T[0]+p[1]*T[3]+p[2]*T[6]+T[9], p[0]*T[1]+p[1]*T[4]+p[2]*T[7]+T[10], p[0]*T[2]+p[1]*T[5]+p[2]*T[8]+T[11]]
def vec(T, v): return [v[0]*T[0]+v[1]*T[3]+v[2]*T[6], v[0]*T[1]+v[1]*T[4]+v[2]*T[7], v[0]*T[2]+v[1]*T[5]+v[2]*T[8]]
d = load(sys.argv[1])
bolts = [(n, v[2]) for n, v in d.items() if v[0].startswith('FAST-HEX') and 'ROOF-SLEEVE-BOLT' in n]
fmt = lambda r: ' '.join('%g' % round(x, 3) for x in r)
for n, v in sorted(d.items()):
    key = '08XXX-HBA-01.SLDPRT' if v[0] == '08XXX-HBA-01.SLDPRT' else ('HBW-04' if v[0].endswith('HBW-04.SLDPRT') else None)
    if not key:
        continue
    T = v[2]
    print('%s  %s  rotation [%s]  origin (%s)' % (n.split('/', 1)[1], v[0], fmt(T[:9]), fmt(T[9:12])))
    used = set()
    for ax, y in SLOTS[key]:
        a = vec(T, [0, 0, 1] if ax == 'Z' else [1, 0, 0]); c = pt(T, [0, y, 0])
        hit = None
        for bn, B in bolts:
            ba = B[0:3]
            if abs(abs(sum(a[i]*ba[i] for i in range(3))) - 1) > 1.5e-4:
                continue
            o = B[9:12]; dd = [c[i]-o[i] for i in range(3)]; t = sum(dd[i]*ba[i] for i in range(3))
            r = math.sqrt(max(0, sum(x*x for x in dd) - t*t))
            if r < 1 and abs(t) < 200:
                hit = (bn.split('/')[-2] + '/' + bn.split('/')[-1], round(r, 3)); used.add(bn)
        print('    %s-wall slot pair at y %3d: centre (%s) axis (%s)  %s' % (ax.lower(), y, fmt(c), fmt(a), ('bolt ' + hit[0] + ' %.3f mm off' % hit[1]) if hit else 'NO BOLT'))
    # the kit's bolts near this sleeve that pass through none of its slot pairs
    for bn, B in bolts:
        if bn in used: continue
        if abs(B[9]-T[9]) < 100 and abs(B[11]-T[11]) < 200 and T[10]-10 < B[10] < T[10]+600:
            print('    bolt %s axis (%s) at (%s): through NO slot pair (solid wall)' % (bn.split('/')[-2] + '/' + bn.split('/')[-1], fmt(B[0:3]), fmt(B[9:12])))
