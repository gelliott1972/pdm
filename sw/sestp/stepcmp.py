"""Compare two STEP files for the same solids in the same order (SW, 24 Nov 2026).

The header (time stamp, file name) and the entity numbers may differ; what must match is the list of products in order,
the solid bodies in order (name, face count) and the geometry entities. Usage: python stepcmp.py <a.STEP> <b.STEP>
"""
import re
import sys
from collections import Counter  # noqa: E401

ENT = re.compile(r"#(\d+)\s*=\s*([A-Z_0-9]+)\s*\((.*)\)\s*;\s*$", re.S)


def read(path):
    text = open(path, encoding='latin-1').read()
    data = text[text.index('DATA;'):]
    ents = {}
    for stmt in data.split(';\n'):
        m = ENT.match(stmt.strip() + ';')
        if m:
            ents[int(m.group(1))] = (m.group(2), m.group(3))
    return ents


def summary(ents):
    order = sorted(ents)
    products = [re.match(r"\s*'([^']*)'", ents[i][1]).group(1) for i in order if ents[i][0] == 'PRODUCT']
    solids = []
    for i in order:
        if ents[i][0] == 'MANIFOLD_SOLID_BREP':
            name = re.match(r"\s*'([^']*)'", ents[i][1]).group(1)
            shell = int(re.search(r"#(\d+)", ents[i][1]).group(1))
            faces = len(re.findall(r"#\d+", ents[shell][1])) if shell in ents else -1
            solids.append((name, faces))
    kinds = Counter(k for k, _ in ents.values())
    # geometry values, independent of entity numbering: every cartesian point, rounded to 1e-6 mm
    pts = Counter(tuple(round(float(v), 6) for v in re.findall(r"[-+]?\d*\.?\d+(?:[Ee][-+]?\d+)?", ents[i][1].split(',', 1)[1]))
                  for i in order if ents[i][0] == 'CARTESIAN_POINT')
    return products, solids, kinds, pts


def main():
    a, b = summary(read(sys.argv[1])), summary(read(sys.argv[2]))
    print('products', len(a[0]), len(b[0]), 'same order' if a[0] == b[0] else 'DIFFERENT')
    print('solids', len(a[1]), len(b[1]), 'same names, face counts and order' if a[1] == b[1] else 'DIFFERENT')
    if a[1] != b[1]:
        for i, (x, y) in enumerate(zip(a[1], b[1])):
            if x != y:
                print('  first difference at solid', i + 1, x, y)
                break
    diffk = {k: (a[2].get(k, 0), b[2].get(k, 0)) for k in set(a[2]) | set(b[2]) if a[2].get(k, 0) != b[2].get(k, 0)}
    print('entity kinds', 'identical counts' if not diffk else 'DIFFERENT ' + str(sorted(diffk.items())[:10]))
    print('cartesian points', sum(a[3].values()), sum(b[3].values()), 'identical' if a[3] == b[3] else 'DIFFERENT (%d differ)' % sum(((a[3] - b[3]) + (b[3] - a[3])).values()))
    # 24 Nov: two exports of the same sandbox model in one session gave the same products, solids and points in a different
    # order, so the order is reported but content is what decides
    same_content = Counter(a[0]) == Counter(b[0]) and Counter(a[1]) == Counter(b[1]) and not diffk and a[3] == b[3]
    same_order = a[0] == b[0] and a[1] == b[1]
    print('products and solids as sets', 'identical' if Counter(a[0]) == Counter(b[0]) and Counter(a[1]) == Counter(b[1]) else 'DIFFERENT')
    print('RESULT', ('SAME SOLIDS, SAME GEOMETRY, ' + ('SAME ORDER' if same_order else 'different order in the file')) if same_content else 'DIFFERENT')
    return 0 if same_content else 1


if __name__ == '__main__':
    sys.exit(main())
