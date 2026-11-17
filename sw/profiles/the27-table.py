"""Per-part table for sw\\profiles\\the-27.md from the item-4 test results (18 Nov). Read-only.
Usage: python sw/profiles/the27-table.py <work folder> <released.txt>
"""
import collections, ntpath, sys

work, rel = sys.argv[1], {l.strip().upper() for l in open(sys.argv[2], encoding='utf-8') if l.strip()}
res = {}
for l in open(work + '/result.tsv', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    res[c[2].upper()] = (c[0], c[1])
pdf = {}
for l in open(work + '/pdf-compare.tsv', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    pdf[c[1].upper()] = c
LABEL = ('RHS80x40x4', 'RHS100x100x5', 'RSA50x50x5', 'RHS40x40x4', 'PLATE,', 'x', '32', '5', '72', '36', '95', '6.3')
parts = collections.OrderedDict()
for d, (r, part) in sorted(res.items(), key=lambda kv: kv[1][1]):
    parts.setdefault(part, []).append(d)
print('| Part | Drawings (released) | Values back, 0 dangling | Printed text | Lines/colour | Auto-fix |')
print('| --- | --- | --- | --- | --- | --- |')
for part, ds in parts.items():
    nrel = sum(1 for d in ds if d.replace('.SLDDRW', '') in rel)
    same = sum(1 for d in ds if res[d][0] == 'SAME')
    texts, px = [], []
    for d in ds:
        c = pdf.get(d)
        if not c: texts.append('no PDF'); continue
        if c[2] == 'TEXT DIFF':
            o = c[3].split(' | ')[0].replace('only original: ', '').split()
            if all(any(w.startswith(x) or w == x for x in LABEL) for w in o) and '6.3' not in o and '8' not in o and '40' not in o and '80' not in o:
                texts.append('labels')
            else:
                texts.append('DIMS ' + c[3])
        else:
            texts.append('same')
        px.append(int(c[4].split()[-1]))
    dimtxt = [t for t in texts if t.startswith('DIMS')]
    t = 'only cut-list labels' if all(t in ('same', 'labels') for t in texts) else '; '.join(sorted(set(x.replace('DIMS ', '') for x in dimtxt)))
    lab = 'same' if all(t == 'same' for t in texts) else t
    look = 'identical' if px and max(px) <= 2 else 'moved/colour (max %d px)' % max(px) if px else '?'
    auto = 'yes' if same == len(ds) and not dimtxt else 'yes, then set decimals' if same == len(ds) else 'no'
    print('| %s | %d (%d) | %d of %d | %s | %s | %s |' % (part.replace('.SLDPRT', ''), len(ds), nrel, same, len(ds), lab, look, auto))
