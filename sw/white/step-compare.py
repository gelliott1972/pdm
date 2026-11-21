"""Rough comparison of two STEP AP203 files (Admin 21 Nov 2026): product names and how many times each is placed
(NEXT_ASSEMBLY_USAGE_OCCURRENCE), grouped by product name. Read-only.
Usage: python sw/white/step-compare.py <old.step> <new.step>
"""
import collections, re, sys

ENT = re.compile(r"^#(\d+)\s*=\s*([A-Z_0-9]+)\s*\((.*)\)\s*;\s*$", re.S)


def load(path):
    text = open(path, encoding='latin-1').read()
    body = text.split('DATA;', 1)[1]
    ents = {}
    for chunk in body.split(';\n'):
        chunk = chunk.strip()
        m = ENT.match(chunk + ';')
        if m: ents[int(m.group(1))] = (m.group(2), m.group(3))
    prod = {k: re.match(r"\s*'([^']*)'", v[1]).group(1) for k, v in ents.items() if v[0] == 'PRODUCT'}
    # PRODUCT <- PRODUCT_DEFINITION_FORMATION(_WITH_SPECIFIED_SOURCE) <- PRODUCT_DEFINITION
    pdf = {k: int(re.findall(r'#(\d+)', v[1])[0]) for k, v in ents.items() if v[0].startswith('PRODUCT_DEFINITION_FORMATION')}
    pd = {}
    for k, v in ents.items():
        if v[0] == 'PRODUCT_DEFINITION':
            refs = [int(x) for x in re.findall(r'#(\d+)', v[1])]
            f = next((r for r in refs if r in pdf), None)
            if f is not None: pd[k] = prod.get(pdf[f], '?')
    uses = collections.Counter()
    for k, v in ents.items():
        if v[0] == 'NEXT_ASSEMBLY_USAGE_OCCURRENCE':
            refs = [int(x) for x in re.findall(r'#(\d+)', v[1])]
            if len(refs) >= 2: uses[pd.get(refs[1], '?')] += 1
    names = collections.Counter(prod.values())
    return names, uses


a, b = sys.argv[1], sys.argv[2]
na, ua = load(a)
nb, ub = load(b)
print('products: old %d (%d names), new %d (%d names)' % (sum(na.values()), len(na), sum(nb.values()), len(nb)))
print('placements: old %d, new %d' % (sum(ua.values()), sum(ub.values())))


def group(n):
    s = n.upper()
    for key in ('DL_APEX', 'SUBFRAME', 'TIE', 'GROUT', 'STA', 'SFA', 'HBA', 'HBW', 'HBP', 'BRA', 'BRP', 'LM', 'PANEL', 'FAST', 'MDL'):
        if key in s: return key
    return 'other'


ga, gb = collections.Counter(), collections.Counter()
for n, c in ua.items(): ga[group(n)] += c
for n, c in ub.items(): gb[group(n)] += c
print('\nplacements by kind (old -> new):')
for g in sorted(set(ga) | set(gb)): print('  %-10s %5d -> %5d' % (g, ga[g], gb[g]))
print('\nonly in old:', ', '.join('%s x%d' % (n, ua[n] or na[n]) for n in sorted(set(na) - set(nb))) or 'none')
print('only in new:', ', '.join('%s x%d' % (n, ub[n] or nb[n]) for n in sorted(set(nb) - set(na))) or 'none')
print('\nsame name, different count:')
for n in sorted(set(ua) & set(ub)):
    if ua[n] != ub[n]: print('  %s: %d -> %d' % (n, ua[n], ub[n]))
