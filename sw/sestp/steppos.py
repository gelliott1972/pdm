"""Instance placements from a STEP assembly (26 Nov 2026, WDI1-02/-03): walks NEXT_ASSEMBLY_USAGE_OCCURRENCE from the root
and composes each occurrence's ITEM_DEFINED_TRANSFORMATION, then writes every leaf instance in the SwGrout xforms layout
(COMP<TAB>path<TAB>file<TAB><cfg><TAB>9 rotation numbers | origin), so sleevebolts.py can read it.
Usage: python steppos.py <STR.STEP> <out positions.txt>
"""
import re
import sys

text = open(sys.argv[1], encoding='latin-1').read()
data = text[text.index('DATA;'):]
ents = {}
for stmt in data.split(';\n'):
    stmt = stmt.strip()
    m = re.match(r"#(\d+)\s*=\s*(.*)$", stmt, re.S)
    if not m:
        continue
    body = m.group(2).strip()
    if body.startswith('('):
        kind = 'COMPLEX'
    else:
        kind = re.match(r'([A-Z_0-9]+)', body).group(1)
    ents[int(m.group(1))] = (kind, body)
refs = lambda i: [int(x) for x in re.findall(r'#(\d+)', ents[i][1])]
nums = lambda s: [float(x) for x in re.findall(r"[-+]?\d*\.?\d+(?:[Ee][-+]?\d+)?", s)]


def placement(i):
    loc, ax, rd = refs(i)[:3]
    o = nums(ents[loc][1].split(',', 1)[1])[:3]
    z = nums(ents[ax][1].split(',', 1)[1])[:3]
    x = nums(ents[rd][1].split(',', 1)[1])[:3]
    y = [z[1] * x[2] - z[2] * x[1], z[2] * x[0] - z[0] * x[2], z[0] * x[1] - z[1] * x[0]]
    return [[x[0], y[0], z[0], o[0]], [x[1], y[1], z[1], o[1]], [x[2], y[2], z[2], o[2]], [0, 0, 0, 1]]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def inv(m):   # rigid: transpose the rotation
    r = [[m[j][i] for j in range(3)] for i in range(3)]
    t = [-sum(r[i][k] * m[k][3] for k in range(3)) for i in range(3)]
    return [r[0] + [t[0]], r[1] + [t[1]], r[2] + [t[2]], [0, 0, 0, 1]]


pname = {}
for i, (k, v) in ents.items():
    if k == 'PRODUCT':
        pname[i] = re.match(r"PRODUCT\s*\(\s*'([^']*)'", v).group(1)
pdf_prod = {i: refs(i)[0] for i, (k, v) in ents.items() if k == 'PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE' or k == 'PRODUCT_DEFINITION_FORMATION'}
pd_name = {}
for i, (k, v) in ents.items():
    if k == 'PRODUCT_DEFINITION':
        f = refs(i)[0]
        pd_name[i] = pname.get(pdf_prod.get(f), '?')
nauo = {i: refs(i)[:2] for i, (k, v) in ents.items() if k == 'NEXT_ASSEMBLY_USAGE_OCCURRENCE'}
pds_of = {refs(i)[0]: i for i, (k, v) in ents.items() if k == 'PRODUCT_DEFINITION_SHAPE'}
cdsr = {refs(i)[1]: refs(i)[0] for i, (k, v) in ents.items() if k == 'CONTEXT_DEPENDENT_SHAPE_REPRESENTATION'}
xf = {}
for i, (k, v) in ents.items():
    if k == 'COMPLEX' and 'REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION' in v:
        idt = int(re.search(r'REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION\s*\(\s*#(\d+)', v).group(1))
        a1, a2 = refs(idt)[:2]
        xf[i] = mul(placement(a1), inv(placement(a2)))   # item 1 placed by item 2 (checked against the SOLIDWORKS positions)
children = {}
for n, (parent, child) in nauo.items():
    T = xf[cdsr[pds_of[n]]]
    children.setdefault(parent, []).append((child, T))
roots = set(children) - set(c for v in children.values() for c, _ in v)
lines = []


def walk(pd, M, path):
    kids = children.get(pd, [])
    if not kids:
        nm = pd_name[pd]
        lines.append('COMP\t%s\t%s\t<>\t%s | %s' % ('/'.join(path), nm,
                     ' '.join('%.6g' % M[r][c] for c in range(3) for r in range(3)), ' '.join('%.4f' % M[r][3] for r in range(3))))
        return
    count = {}
    for child, T in kids:
        nm = pd_name[child]
        count[nm] = count.get(nm, 0) + 1
        walk(child, mul(M, T), path + ['%s-%d' % (nm, count[nm])])


for r in roots:
    walk(r, [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]], [pd_name[r]])
open(sys.argv[2], 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
print('roots', [pd_name[r] for r in roots], 'leaf instances', len(lines))
