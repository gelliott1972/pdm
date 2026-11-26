"""Every-bolt check on a STR.STEP (26 Nov 2026, for the Disney package: the STEP Revit was built from is the reference that
revit-fiona's Revit check is compared against). Read-only; needs only the STEP file.

For every bolt (a FAST-* part that is not a nut or washer: hex bolts and HST3 anchors), its axis (the shared axis of its
cylinder faces) and length are found, then each plate or tube body (every non-FAST body) near it is tested:
  - "solid": the bolt axis, within the bolt's length, passes through the inside of a flat face of the body (not through a
    hole in that face). That is a bolt through solid wall.
  - "hole": the body has a hole face (a cylinder facing inwards) parallel to the bolt (within 0.5 degrees) whose axis is
    within 1 mm of the bolt's axis, over the bolt's length.
  - "slot": two parallel hole-end faces of the same radius, and the bolt's axis lies within 1 mm of the line between them.
A body counts as crossed when it gives "solid", "hole" or "slot". Problems: any "solid", and any bolt that crosses nothing.
Holes crossed by no bolt are listed at the end (information only: drain holes, spare holes, holes for parts not exported).
Flat faces are tested exactly for straight and circular edges; other edges (splines, ellipses) are taken by their end
points only, so a face bounded mainly by splines can be misjudged: such a case is flagged "(approx)".

Usage: python boltholes.py <STR.STEP> <out.txt> <label>
"""
import collections
import math
import re
import sys

STEP, OUT, LABEL = sys.argv[1], sys.argv[2], sys.argv[3]
PAR = math.cos(math.radians(0.5))
OFF = 1.0

text = open(STEP, encoding='latin-1').read()
ents = {}
for stmt in text[text.index('DATA;'):].split(';\n'):
    m = re.match(r"\s*#(\d+)\s*=\s*(.*)$", stmt, re.S)
    if not m:
        continue
    body = m.group(2).strip()
    kind = 'COMPLEX' if body.startswith('(') else re.match(r'([A-Z_0-9]+)', body).group(1)
    ents[int(m.group(1))] = (kind, body)
refs = lambda i: [int(x) for x in re.findall(r'#(\d+)', ents[i][1])]
NUM = r"[-+]?\d*\.?\d+(?:[Ee][-+]?\d+)?"
kind = lambda i: ents[i][0]


def tup(i):   # the numbers in the last (...) group: CARTESIAN_POINT / DIRECTION
    b = ents[i][1]
    return [float(x) for x in re.findall(NUM, b[b.rindex('('):])][:3]


def tailnum(i):   # the number after the last #ref: radius of CIRCLE / CYLINDRICAL_SURFACE
    b = ents[i][1]
    return float(re.findall(NUM, b[b.rindex('#'):].split(',', 1)[1])[0])


def flag(i):
    return ents[i][1].rstrip().rstrip(')').rstrip().endswith('.T.')


sub = lambda a, b: [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
add = lambda a, b: [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
mulv = lambda a, s: [a[0] * s, a[1] * s, a[2] * s]
dot = lambda a, b: a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
cross = lambda a, b: [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def unit(a):
    n = math.sqrt(dot(a, a))
    return [a[0] / n, a[1] / n, a[2] / n]


def axis2(i):
    r = refs(i)
    o = tup(r[0]); z = unit(tup(r[1]))
    if len(r) > 2:
        x = unit(tup(r[2]))
    else:
        x = unit(cross(z, [1, 0, 0] if abs(z[0]) < 0.9 else [0, 1, 0]))
    x = unit(sub(x, mulv(z, dot(x, z))))
    return o, x, cross(z, x), z


# ---------- assembly tree -> leaf instances with world transforms (same as steppos.py, checked against SOLIDWORKS)
def placement(i):
    o, x, y, z = axis2(i)
    return [[x[0], y[0], z[0], o[0]], [x[1], y[1], z[1], o[1]], [x[2], y[2], z[2], o[2]], [0, 0, 0, 1]]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def inv(m):
    r = [[m[j][i] for j in range(3)] for i in range(3)]
    t = [-sum(r[i][k] * m[k][3] for k in range(3)) for i in range(3)]
    return [r[0] + [t[0]], r[1] + [t[1]], r[2] + [t[2]], [0, 0, 0, 1]]


pname = {i: re.match(r"PRODUCT\s*\(\s*'([^']*)'", v).group(1) for i, (k, v) in ents.items() if k == 'PRODUCT'}
pdf_prod = {i: refs(i)[0] for i, (k, v) in ents.items() if k.startswith('PRODUCT_DEFINITION_FORMATION')}
pd_name = {i: pname.get(pdf_prod.get(refs(i)[0]), '?') for i, (k, v) in ents.items() if k == 'PRODUCT_DEFINITION'}
nauo = {i: refs(i)[:2] for i, (k, v) in ents.items() if k == 'NEXT_ASSEMBLY_USAGE_OCCURRENCE'}
pds_of = {refs(i)[0]: i for i, (k, v) in ents.items() if k == 'PRODUCT_DEFINITION_SHAPE'}
cdsr = {refs(i)[1]: refs(i)[0] for i, (k, v) in ents.items() if k == 'CONTEXT_DEPENDENT_SHAPE_REPRESENTATION'}
xf = {}
for i, (k, v) in ents.items():
    if k == 'COMPLEX' and 'REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION' in v:
        idt = int(re.search(r'REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION\s*\(\s*#(\d+)', v).group(1))
        a1, a2 = refs(idt)[:2]
        xf[i] = mul(placement(a1), inv(placement(a2)))
children = collections.defaultdict(list)
for n, (parent, child) in nauo.items():
    children[parent].append((child, xf[cdsr[pds_of[n]]]))
roots = set(children) - set(c for v in children.values() for c, _ in v)
leaves = []


def walk(pd, M, path):
    if not children.get(pd):
        leaves.append(('/'.join(path[1:]), pd, M))
        return
    count = collections.Counter()
    for child, T in children[pd]:
        count[pd_name[child]] += 1
        walk(child, mul(M, T), path + ['%s-%d' % (pd_name[child], count[pd_name[child]])])


for r in roots:
    walk(r, [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]], [pd_name[r]])

# ---------- product -> solid bodies
sdr = collections.defaultdict(list)
for i, (k, v) in ents.items():
    if k == 'SHAPE_DEFINITION_REPRESENTATION':
        sdr[refs(i)[0]].append(refs(i)[1])
srr = collections.defaultdict(list)
for i, (k, v) in ents.items():
    if k == 'SHAPE_REPRESENTATION_RELATIONSHIP':
        a1, a2 = refs(i)[:2]
        srr[a1].append(a2); srr[a2].append(a1)


def breps_of(pd):
    out, todo, seen = [], list(sdr.get(pds_of.get(pd), [])), set()
    while todo:
        r = todo.pop()
        if r in seen:
            continue
        seen.add(r)
        for it in refs(r):
            if it in ents and kind(it) == 'MANIFOLD_SOLID_BREP':
                out.append(it)
        todo.extend(srr.get(r, []))
    return out


# ---------- body geometry in part coordinates
def edge_points(oe):
    """Points along one oriented edge, in loop order; approx flag when the curve is not a line or circle."""
    r = refs(oe)
    ec = r[-1]
    v1, v2, crv = refs(ec)[:3]
    p1, p2 = tup(refs(v1)[0]), tup(refs(v2)[0])
    pts, approx = [p1, p2], False
    if kind(crv) == 'CIRCLE':
        o, x, y, z = axis2(refs(crv)[0]); rad = tailnum(crv)
        ang = lambda p: math.atan2(dot(sub(p, o), y), dot(sub(p, o), x))
        a1, a2 = ang(p1), ang(p2)
        sweep = (a2 - a1) % (2 * math.pi)
        if v1 == v2 or sweep < 1e-9:
            sweep = 2 * math.pi
        if not flag(ec):
            sweep = sweep - 2 * math.pi
        n = max(2, int(abs(sweep) / (math.pi / 24)) + 1)
        pts = [add(o, add(mulv(x, rad * math.cos(a1 + sweep * t / n)), mulv(y, rad * math.sin(a1 + sweep * t / n)))) for t in range(n + 1)]
    elif kind(crv) != 'LINE':
        approx = True
    if not flag(oe):
        pts = pts[::-1]
    return pts, approx


bodycache = {}


def body(b):
    if b in bodycache:
        return bodycache[b]
    shell = refs(b)[0]
    planes, cyls, verts = [], [], []
    for f in refs(shell):
        if f not in ents or kind(f) != 'ADVANCED_FACE':
            continue
        fr = refs(f); surf = fr[-1]; same = flag(f)
        loops, approx, fverts = [], False, []
        for bd in fr[:-1]:
            lp = refs(bd)[0]
            poly = []
            for oe in refs(lp):
                if oe in ents and kind(oe) == 'ORIENTED_EDGE':
                    p, ap = edge_points(oe); poly.extend(p); approx |= ap
            if kind(lp) == 'VERTEX_LOOP':
                poly = [tup(refs(refs(lp)[0])[0])]
            loops.append(poly); fverts.extend(poly)
        verts.extend(fverts)
        if kind(surf) == 'PLANE':
            o, x, y, z = axis2(refs(surf)[0])
            loops2 = [[(dot(sub(p, o), x), dot(sub(p, o), y)) for p in lp] for lp in loops if len(lp) > 2]
            planes.append((o, x, y, z, loops2, approx))
        elif kind(surf) == 'CYLINDRICAL_SURFACE' and not same:   # face normal points to the axis: a hole face
            o, x, y, z = axis2(refs(surf)[0])
            ts = [dot(sub(p, o), z) for p in fverts] or [0]
            cyls.append((o, z, tailnum(surf), min(ts), max(ts)))
    name = re.match(r"MANIFOLD_SOLID_BREP\s*\(\s*'([^']*)'", ents[b][1]).group(1)
    lo = [min(p[j] for p in verts) for j in range(3)]; hi = [max(p[j] for p in verts) for j in range(3)]
    bodycache[b] = (name, planes, cyls, lo, hi)
    return bodycache[b]


def bolt_axis(bs):
    """Axis of a bolt from its cylinder faces (any sense): point, unit direction, length range, shank radius."""
    cyl, pts = [], []
    for b in bs:
        for f in refs(refs(b)[0]):
            if f not in ents or kind(f) != 'ADVANCED_FACE':
                continue
            surf = refs(f)[-1]
            if kind(surf) == 'CYLINDRICAL_SURFACE':
                o, x, y, z = axis2(refs(surf)[0]); cyl.append((o, z, tailnum(surf)))
        name, planes, cyls, lo, hi = body(b)
        pts += [lo, hi]
        for pl in planes:
            for lp in pl[4]:
                pts += [add(pl[0], add(mulv(pl[1], u), mulv(pl[2], v))) for u, v in lp]
    # the most common axis direction among the cylinders, then their mean line
    best = max(cyl, key=lambda c: sum(1 for d in cyl if abs(dot(c[1], d[1])) > PAR))
    z = best[1]; o = best[0]
    ts = [dot(sub(p, o), z) for p in pts]
    rad = min(c[2] for c in cyl if abs(dot(c[1], z)) > PAR)
    return o, z, min(ts), max(ts), rad


def T(M, p):
    return [M[i][0] * p[0] + M[i][1] * p[1] + M[i][2] * p[2] + M[i][3] for i in range(3)]


def R(M, v):
    return [M[i][0] * v[0] + M[i][1] * v[1] + M[i][2] * v[2] for i in range(3)]


def inside(pt, loops):
    """Even-odd point in polygon set; also the distance to the nearest edge."""
    x, y = pt; c = False; dmin = 1e18
    for lp in loops:
        n = len(lp)
        for i in range(n):
            (x1, y1), (x2, y2) = lp[i], lp[(i + 1) % n]
            if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
                c = not c
            dx, dy = x2 - x1, y2 - y1; L = dx * dx + dy * dy
            t = 0 if L == 0 else max(0, min(1, ((x - x1) * dx + (y - y1) * dy) / L))
            dmin = min(dmin, math.hypot(x - x1 - t * dx, y - y1 - t * dy))
    return c, dmin


# ---------- instances
bolts, plates = [], []
for path, pd, M in leaves:
    nm = pd_name[pd]
    bs = breps_of(pd)
    if not bs:
        continue
    if nm.startswith('FAST-'):
        if any(w in nm for w in ('NUT', 'WSH', 'WASH')):
            continue
        o, z, t0, t1, rad = bolt_axis(bs)
        bolts.append((path, nm, T(M, o), unit(R(M, z)), t0, t1, rad))
    else:
        for b in bs:
            name, planes, cyls, lo, hi = body(b)
            corners = [T(M, [(lo, hi)[i >> 2 & 1][0], (lo, hi)[i >> 1 & 1][1], (lo, hi)[i & 1][2]]) for i in range(8)]
            wlo = [min(c[j] for c in corners) for j in range(3)]; whi = [max(c[j] for c in corners) for j in range(3)]
            plates.append((path, nm, name, b, M, wlo, whi))


def seg_box(p0, p1, lo, hi, pad=2.0):
    t0, t1 = 0.0, 1.0
    d = sub(p1, p0)
    for j in range(3):
        if abs(d[j]) < 1e-12:
            if p0[j] < lo[j] - pad or p0[j] > hi[j] + pad:
                return False
        else:
            a = (lo[j] - pad - p0[j]) / d[j]; b = (hi[j] + pad - p0[j]) / d[j]
            if a > b:
                a, b = b, a
            t0, t1 = max(t0, a), min(t1, b)
            if t0 > t1:
                return False
    return True


used_holes = set()   # (plate index, cylinder index)
rows, problems = [], []
for (path, nm, o, z, t0, t1, rad) in bolts:
    p0, p1 = add(o, mulv(z, t0)), add(o, mulv(z, t1))
    crossed = []
    for pi, (ppath, pnm, bname, b, M, wlo, whi) in enumerate(plates):
        if not seg_box(p0, p1, wlo, whi):
            continue
        name, planes, cyls, lo, hi = body(b)
        # bolt axis in the body's own coordinates
        Mi = inv(M)
        lo_ = T(Mi, o); lz = R(Mi, z)
        solid = []
        for (po, px, py, pz, loops, approx) in planes:
            dn = dot(lz, pz)
            if abs(dn) < 0.01 or not loops:
                continue
            t = dot(sub(po, lo_), pz) / dn
            if t < t0 + 0.2 or t > t1 - 0.2:
                continue
            q = add(lo_, mulv(lz, t))
            ins, dmin = inside((dot(sub(q, po), px), dot(sub(q, po), py)), loops)
            if ins and dmin > 0.5:
                solid.append((t, dmin, approx))
        # holes parallel to the bolt, within the bolt's length
        hits, ends = [], []
        for ci, (co, cz, cr, c0, c1) in enumerate(cyls):
            if abs(dot(cz, lz)) < PAR:
                continue
            d = sub(lo_, co); perp = sub(d, mulv(cz, dot(d, cz)))
            # the hole's length range seen along the bolt axis
            a0 = dot(sub(add(co, mulv(cz, c0)), lo_), lz); a1 = dot(sub(add(co, mulv(cz, c1)), lo_), lz)
            if max(a0, a1) < t0 - 1 or min(a0, a1) > t1 + 1:
                continue
            off = math.sqrt(dot(perp, perp))
            ends.append((ci, co, cz, cr, perp))
            if off <= OFF:
                hits.append(('hole', ci, None, cr, off))
        if not hits:   # slot: bolt axis on the line between two parallel hole ends of equal radius
            for i in range(len(ends)):
                for j in range(i + 1, len(ends)):
                    ci, _, _, r1, e1 = ends[i]; cj, _, _, r2, e2 = ends[j]
                    if abs(r1 - r2) > 0.01:
                        continue
                    # e = vector from hole-end axis to the bolt axis (perpendicular); segment between the two ends
                    a = mulv(e1, -1); bb = mulv(e2, -1); dd = sub(bb, a); L = dot(dd, dd)
                    if L < 0.01:
                        continue
                    t = max(0, min(1, dot(mulv(a, -1), dd) / L))
                    q = add(a, mulv(dd, t)); off = math.sqrt(dot(q, q))
                    if off <= OFF:
                        hits.append(('slot', ci, cj, r1, off))
        if solid:
            crossed.append((pi, 'SOLID', min(solid)[1], any(s[2] for s in solid)))
        elif hits:
            h = min(hits, key=lambda x: x[4])
            used_holes.add((pi, h[1]))
            if h[2] is not None:
                used_holes.add((pi, h[2]))
            for x in hits:
                used_holes.add((pi, x[1]))
                if x[2] is not None:
                    used_holes.add((pi, x[2]))
            crossed.append((pi, h[0], h[4], 2 * h[3]))
    rows.append((path, nm, o, z, t0, t1, rad, crossed))
    if not crossed or any(c[1] == 'SOLID' for c in crossed):
        problems.append(rows[-1])

# ---------- report
fmt = lambda v: '(%s)' % ', '.join('%.1f' % x for x in v)
short = lambda p: p.split('/', 1)[1] if '/' in p else p
lines = []
nprob = len(problems)
lines.append('%s: %d bolts, %d through holes or slots in every part they cross, %d problems' % (LABEL, len(rows), len(rows) - nprob, nprob))
lines.append('Source: %s' % STEP)
lines.append('Test: bolt axis parallel to the hole within 0.5 deg and within 1 mm of the hole axis (round hole) or of the line between the slot ends;')
lines.append('      "SOLID" = the bolt axis passes through the inside of a flat face of that part (through solid wall). Positions are in the STEP\'s')
lines.append('      own axes, mm (same as the SOLIDWORKS screen assembly: y up). Bolt position = the bolt\'s axis point at its head end.')
lines.append('')
lines.append('PROBLEMS (%d)' % nprob)
for (path, nm, o, z, t0, t1, rad, crossed) in problems:
    what = '; '.join('%s %s: %s' % (short(plates[c[0]][0]), plates[c[0]][2], 'SOLID wall' + (' (approx)' if c[3] else '')) for c in crossed if c[1] == 'SOLID') or 'crosses no part at all'
    lines.append('  %s  [%s]  at %s axis %s: %s' % (path, nm, fmt(add(o, mulv(z, t0))), fmt(z), what))
if not problems:
    lines.append('  none')
lines.append('')
lines.append('EVERY BOLT, by bolt type')
bytype = collections.defaultdict(list)
for r in rows:
    bytype[r[1]].append(r)
for nm in sorted(bytype):
    lines.append('== %s: %d' % (nm, len(bytype[nm])))
    for (path, _, o, z, t0, t1, rad, crossed) in sorted(bytype[nm], key=lambda r: r[0]):
        parts = []
        for c in crossed:
            pp = plates[c[0]]
            if c[1] == 'SOLID':
                parts.append('%s [%s] SOLID' % (short(pp[0]), pp[2]))
            else:
                parts.append('%s %s %.0f %.2f mm off' % (short(pp[0]), c[1], c[3], c[2]))
        status = 'OK' if crossed and not any(c[1] == 'SOLID' for c in crossed) else 'PROBLEM'
        lines.append('  %-7s %s  at %s axis %s  through: %s' % (status, path, fmt(add(o, mulv(z, t0))), fmt(z), ' | '.join(parts) or 'nothing'))
lines.append('')
# holes crossed by no bolt, grouped per part instance; coaxial faces counted once
lines.append('HOLES CROSSED BY NO BOLT (information only; slot ends and split hole faces grouped by axis)')
count = 0
for pi, (ppath, pnm, bname, b, M, wlo, whi) in enumerate(plates):
    name, planes, cyls, lo, hi = body(b)
    groups = []   # coaxial hole faces of one radius = one hole axis (both walls of a tube, split faces); used if any face is
    for ci, (co, cz, cr, c0, c1) in enumerate(cyls):
        w = T(M, add(co, mulv(cz, (c0 + c1) / 2))); wz = unit(R(M, cz))
        for g in groups:
            d = sub(w, g[0]); perp = sub(d, mulv(g[1], dot(d, g[1])))
            if abs(dot(wz, g[1])) > PAR and dot(perp, perp) < 0.01 and abs(cr - g[2]) < 0.01:
                g[3].append(ci)
                break
        else:
            groups.append((w, wz, cr, [ci]))
    groups = [g for g in groups if not any((pi, ci) in used_holes for ci in g[3])]
    if groups:
        sizes = collections.Counter('%.1f' % (2 * g[2]) for g in groups)
        lines.append('  %s [%s]: %s' % (short(ppath), bname, ', '.join('%d x dia %s' % (n, s) for s, n in sorted(sizes.items()))))
        count += len(groups)
lines.append('  total %d hole or slot-end axes with no bolt' % count)
open(OUT, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
print(lines[0])
