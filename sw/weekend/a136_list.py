r"""A136 (28 Nov 2026): the joint 8.2 rail's hole centres go from 400 to 398 apart, so every M12x65 bolt set (bolt, 2 washers,
nut) added by change E moves 1 mm along the rail toward the rail's centre. From a STA's xforms (SwGrout, before), writes the
movelist for SwGrout movelist: "<component><TAB>dx dy dz" (mm, the STA's axes; the rails run along X).
Usage: python a136_list.py <xforms.txt> <out.tsv>
"""
import sys

xf, out = sys.argv[1], sys.argv[2]
comps = {}
for l in open(xf, encoding='utf-8-sig'):
    f = l.rstrip('\n').split('\t')
    if f[0] != 'COMP':
        continue
    rot, org = f[4].split('|')
    comps[f[1]] = (f[2], f[3], [float(x) for x in rot.split()], [float(x) for x in org.split()])
top = {k: v for k, v in comps.items() if '/' not in k}
rails = [v[3] for k, v in top.items() if v[0].upper() == '08X11-BRA-01.SLDPRT']
assert len(rails) == 12, len(rails)
want = {('FAST-HEX-8.8-HDG.SLDPRT', '<ISO 4014 - M12 x 65-N>'), ('FAST-NUT-8.8-HDG.SLDPRT', '<BS EN 24032 - M12 - W - N>'),
        ('FAST-WSH-8.8-HDG.SLDPRT', '<Washer ISO 7089 - 12>')}
lines, per = [], {}
for k, (fn, cfg, R, o) in sorted(top.items()):
    if (fn.upper(), cfg) not in {(a.upper(), b) for a, b in want}:
        continue
    hit = [r for r in rails if abs(r[2] - o[2]) < 0.5 and -60 < o[1] - r[1] < 60 and abs(abs(o[0] - r[0]) - 200) < 0.5]
    if len(hit) != 1:
        continue
    r = hit[0]
    dx = -1.0 if o[0] > r[0] else 1.0
    lines.append('%s\t%g 0 0' % (k, dx))
    per[fn] = per.get(fn, 0) + 1
open(out, 'w').write('\n'.join(lines) + '\n')
print(out, len(lines), per)
