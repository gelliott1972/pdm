"""Slot faces on the sleeve bodies of a STEP file (WDI1-02/-03 check, 26 Nov 2026): every solid whose box is a 120 x 120 tube
(any length) is listed with its cylindrical faces of radius 11 mm (the M20 slot ends), grouped by axis direction and by
position along the tube. Usage: python stepslots.py <STR.STEP>
"""
import collections
import re
import sys

sys.path.insert(0, __import__('os').path.dirname(__file__))
import stepcmp  # noqa: E402

ents = stepcmp.read(sys.argv[1])
refs = lambda i: [int(x) for x in re.findall(r'#(\d+)', ents[i][1])]
nums = lambda s: [float(x) for x in re.findall(r"[-+]?\d*\.?\d+(?:[Ee][-+]?\d+)?", s)]


def point(i):
    return nums(ents[i][1].split(',', 1)[1])[:3]


def axis2(i):
    r = refs(i)   # location, axis, ref direction
    return point(r[0]), nums(ents[r[1]][1].split(',', 1)[1])[:3]


bodies = []
for i, (k, v) in sorted(ents.items()):
    if k != 'MANIFOLD_SOLID_BREP':
        continue
    name = re.match(r"\s*'([^']*)'", v).group(1)
    shell = refs(i)[0]
    pts, slots = [], []
    for f in refs(shell):
        if f not in ents:
            continue
        surf = refs(f)[-1]
        todo, seen = [f], set()
        while todo:
            n = todo.pop()
            if n in seen or n not in ents:
                continue
            seen.add(n)
            if ents[n][0] == 'VERTEX_POINT':
                pts.append(point(refs(n)[0]))
            elif ents[n][0] != 'CARTESIAN_POINT':
                todo.extend(refs(n))
        if ents.get(surf, ('',))[0] == 'CYLINDRICAL_SURFACE':
            vals = ents[surf][1]
            rad = float(re.findall(r"[-+]?\d*\.?\d+(?:[Ee][-+]?\d+)?", vals.split(',')[-1])[0])
            if abs(rad - 11) < 0.01:
                loc, ax = axis2(refs(surf)[0])
                slots.append((loc, ax))
    if not pts:
        continue
    lo = [min(p[j] for p in pts) for j in range(3)]; hi = [max(p[j] for p in pts) for j in range(3)]
    size = sorted(round(hi[j] - lo[j], 1) for j in range(3))
    if size[0] == 120.0 and size[1] == 120.0:
        bodies.append((name, lo, hi, slots))
print('sleeve-like bodies (120 x 120 tube):', len(bodies))
for name, lo, hi, slots in bodies:
    long_axis = max(range(3), key=lambda j: hi[j] - lo[j])
    # slot ends grouped by axis direction (which walls) and by the end's height along the tube from its top
    groups = collections.Counter()
    for loc, ax in slots:
        d = 'xyz'[max(range(3), key=lambda j: abs(ax[j]))]
        groups[(d, round(hi[long_axis] - loc[long_axis]))] += 1
    print('%-34s box %s..%s  slot-end faces (r 11): %d' % (name, [round(x, 1) for x in lo], [round(x, 1) for x in hi], len(slots)))
    for (d, h), n in sorted(groups.items()):
        print('    through the %s walls, %4d mm below the top: %d end faces' % (d, h, n))
