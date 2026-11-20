"""Compare SwRefCheck --detail output before and after the real run (20 Nov 2026). Read-only.
Drawings: same dimension count, every (sheet, view, dimension name, value) the same (the sketch name in a dimension's
full name may differ), dangling 0 after (or no more than before), no new rebuild errors. Assemblies: no new errors.
Table (BOM / cut-list) text changes are listed as TABLE lines, not failures (the new labels are intended).
Usage: python sw/profiles/the27-real-compare.py <before.txt> <after.txt> <out.tsv>
Out: PASS/FAIL, file, why.
"""
import collections, sys


def load(p):
    head, dims, tables = {}, collections.defaultdict(collections.Counter), collections.defaultdict(list)
    for l in open(p, encoding='utf-8', errors='replace'):
        c = l.rstrip('\r\n').split('\t')
        if len(c) < 2: continue
        f = c[1].upper()
        if c[0] in ('DRAWING', 'MODEL', 'ERROR'): head[f] = c[2:]
        elif c[0] == 'DIM' and len(c) >= 6: dims[f][(c[2], c[3], c[4].split('@')[0], c[5])] += 1
        elif c[0] == 'TABLE': tables[f].append('\t'.join(c[2:]))
    return head, dims, tables


bh, bd, bt = load(sys.argv[1])
ah, ad, at = load(sys.argv[2])
out = open(sys.argv[3], 'w', encoding='utf-8', newline='')
npass = nfail = 0
for f in sorted(set(bh) | set(ah)):
    why = []
    if f not in bh: why.append('no before')
    elif f not in ah: why.append('no after')
    else:
        b, a = bh[f], ah[f]
        if b[0].startswith('could') or a[0].startswith('could'): why.append('%s -> %s' % (b, a))
        elif f.endswith('.SLDDRW'):
            bx = dict(x.split(' ', 1) for x in b if ' ' in x); ax = dict(x.split(' ', 1) for x in a if ' ' in x)
            if bx.get('dims') != ax.get('dims'): why.append('dims %s -> %s' % (bx.get('dims'), ax.get('dims')))
            if int(ax.get('dangling', 99)) > int(bx.get('dangling', 0)): why.append('dangling %s -> %s' % (bx.get('dangling'), ax.get('dangling')))
            if int(ax.get('errors', 99)) > int(bx.get('errors', 0)): why.append('errors %s -> %s' % (bx.get('errors'), ax.get('errors')))
            lost, new = bd[f] - ad[f], ad[f] - bd[f]
            if lost: why.append('lost ' + '; '.join('%s %s=%s' % (k[1], k[2], k[3]) for k in sorted(lost)))
            if new: why.append('new ' + '; '.join('%s %s=%s' % (k[1], k[2], k[3]) for k in sorted(new)))
        else:
            be = sum(int(x.split('=')[1]) for x in b[0].replace('errors ', '').split('; ') if '=' in x)
            ae = sum(int(x.split('=')[1]) for x in a[0].replace('errors ', '').split('; ') if '=' in x)
            if ae > be: why.append('errors %s -> %s' % (b[0], a[0]))
            elif ae == be and a[0] != b[0] and set(x.split('=')[0] for x in a[0].split('; ')) != set(x.split('=')[0] for x in b[0].split('; ')):
                why.append('configurations %s -> %s' % (b[0], a[0]))
    res = 'FAIL' if why else 'PASS'
    npass += res == 'PASS'; nfail += res == 'FAIL'
    out.write('\t'.join([res, f, ' | '.join(why)]) + '\n')
    if bt.get(f) != at.get(f):
        old, new = collections.Counter(bt.get(f, [])), collections.Counter(at.get(f, []))
        out.write('\t'.join(['TABLE', f, '%d rows changed: %s => %s' % (sum((old - new).values()), ' // '.join(sorted(old - new)), ' // '.join(sorted(new - old)))]) + '\n')
print(npass, 'pass,', nfail, 'fail')
