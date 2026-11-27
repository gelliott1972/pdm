r"""Weekend run, change E (APEX-A124, joint 8.2), 28 Nov 2026: from the rebuilt positions of one 11 screen's STA (SwGrout
xforms, before any change) write the addfixed lists:
  sta.tsv           12 rails 08X11-BRA-01 (at each GNRIC-BRA's place: same centreline, level and line) and per rail end one
                    M12x65 (ISO 4014) bolt, 2 washers, 1 nut, vertical, head on top of the rail, nut under the seat
  <SFA file>.tsv    per subframe file, its new seats 08X11-MSP-01, one per old GNRIC-MSW-01 (unsuppressed), in that file's
                    axes: heel on the post face at the rail's underside, centred on the rail (front to back), 75 leg to the rail
Joint geometry from calc-08a-11's brief v4: post faces 460 apart (rail origin -/+ 230), hole centres 400 apart (rail origin
-/+ 200, 30 from the post face), rail 40x40 on the old rail's centreline, seat top = rail underside.
Usage: python e_lists.py <xforms.txt> <copy root> <out folder>
"""
import collections
import os
import sys

xf, root, out = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
comps = {}
for l in open(xf, encoding='utf-8-sig'):
    f = l.rstrip('\n').split('\t')
    if f[0] != 'COMP':
        continue
    rot, org = f[4].split('|')
    comps[f[1]] = (f[2], f[3], [float(x) for x in rot.split()], [float(x) for x in org.split()])
rails = {k: v for k, v in comps.items() if v[0].upper() == 'GNRIC-BRA.SLDPRT'}
seats = {k: v for k, v in comps.items() if v[0].upper() == 'GNRIC-MSW-01.SLDPRT'}
assert len(rails) == 12 and len(seats) == 24, (len(rails), len(seats))
for k, v in rails.items():
    assert v[2] == [1, 0, 0, 0, 1, 0, 0, 0, 1], (k, v[2])
F = os.path.join(root, r'_Shared\_Fasteners')
N = os.path.join(root, r'APEX\SC08\LS-08X-11\_Shared')
fmt = lambda xs: ' '.join(('%.6f' % x).rstrip('0').rstrip('.') for x in xs)
r3 = lambda x: round(x, 4)
lines = []
for k, (fn, cfg, R, o) in sorted(rails.items()):
    x, y, z = o
    lines.append('\t'.join([os.path.join(N, '08X11-BRA-01.SLDPRT'), 'Default', fmt([1, 0, 0, 0, 1, 0, 0, 0, 1]), fmt([x, y, z])]))
    top, under = y + 20, y - 25          # rail top; seat leg underside (rail underside - 5)
    for bx in (x - 200, x + 200):
        lines.append('\t'.join([os.path.join(F, 'FAST-HEX-8.8-HDG.SLDPRT'), 'ISO 4014 - M12 x 65-N', '0 -1 0 0 0 -1 1 0 0', fmt([bx, top + 2.5 + 7.5, z])]))
        lines.append('\t'.join([os.path.join(F, 'FAST-WSH-8.8-HDG.SLDPRT'), 'Washer ISO 7089 - 12', '0 1 0 0 0 1 1 0 0', fmt([bx, top + 2.5, z])]))
        lines.append('\t'.join([os.path.join(F, 'FAST-WSH-8.8-HDG.SLDPRT'), 'Washer ISO 7089 - 12', '0 1 0 0 0 1 1 0 0', fmt([bx, under, z])]))
        lines.append('\t'.join([os.path.join(F, 'FAST-NUT-8.8-HDG.SLDPRT'), 'BS EN 24032 - M12 - W - N', '-0.36812 0 0.92978 -0.92978 0 -0.36812 0 -1 0', fmt([bx, under - 2.5 - 10.8, z])]))
open(os.path.join(out, 'sta.tsv'), 'w').write('\n'.join(lines) + '\n')
print('sta.tsv', len(lines), 'parts (12 rails, 96 fasteners)')


def matmul(A, B):   # 3x3 row-major lists of 9
    return [sum(A[3 * i + k] * B[3 * k + j] for k in range(3)) for i in range(3) for j in range(3)]


def transpose(A):
    return [A[3 * j + i] for i in range(3) for j in range(3)]


per = collections.defaultdict(list)
for k, (fn, cfg, Rm, om) in sorted(seats.items()):
    parent = k.rsplit('/', 1)[0]
    match = [(rk, rv) for rk, rv in rails.items() if abs(rv[3][1] - 20 - om[1]) < 15 and min(abs(rv[3][0] - 230 - om[0]), abs(rv[3][0] + 230 - om[0])) < 0.5]
    assert len(match) == 1, (k, om, match)
    rv = match[0][1][3]
    if abs(rv[1] - 20 - om[1]) > 0.5:
        print('NOTE', k, 'old seat top at y %.3f, the rail underside is %.3f: the new seat goes to the rail' % (om[1], rv[1] - 20))
    left = abs(rv[0] - 230 - om[0]) < 0.5
    Rw = [1, 0, 0, 0, 1, 0, 0, 0, 1] if left else [-1, 0, 0, 0, 1, 0, 0, 0, -1]
    ow = [om[0], rv[1] - 20, rv[2]]
    pfn, pcfg, Rs, ts = comps[parent]
    RsT = transpose(Rs)
    Rl = matmul(Rw, RsT)
    d = [ow[i] - ts[i] for i in range(3)]
    ol = [sum(d[k2] * RsT[3 * k2 + j] for k2 in range(3)) for j in range(3)]
    per[(pfn, pcfg)].append('\t'.join([os.path.join(N, '08X11-MSP-01.SLDPRT'), 'Default', fmt([r3(v) for v in Rl]), fmt([r3(v) for v in ol])]))
for (pfn, pcfg), ls in sorted(per.items()):
    head = []
    if pfn.upper().endswith('-SFA.SLDASM'):
        head = ['#show\t' + pcfg.strip('<>'), '#suppress-in\t' + ('PART 1' if pcfg.strip('<>') == 'PART 2' else 'PART 2')]
    open(os.path.join(out, pfn.replace('.SLDASM', '.tsv')), 'w').write('\n'.join(head + sorted(ls)) + '\n')
    print(pfn, pcfg, len(ls), 'seats')
