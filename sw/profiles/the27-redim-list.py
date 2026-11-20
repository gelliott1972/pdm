"""Drawings that need SwRedim after the real repoint (20 Nov 2026): more dangling than before. Read-only.
Held part: the first of the drawing's parts (users.tsv) that was changed (not in the skip list).
Usage: python sw/profiles/the27-redim-list.py <work folder> <before.txt> <mid.txt> [skip part name ...]
Writes <work>/redim-list.tsv (drawing, held part) and prints counts.
"""
import ntpath, sys

work, before, mid, skip = sys.argv[1], sys.argv[2], sys.argv[3], {s.upper() for s in sys.argv[4:]}
BS = chr(92)


def dangling(p):
    d = {}
    for l in open(p, encoding='utf-8', errors='replace'):
        c = l.rstrip('\r\n').split('\t')
        if c[0] == 'DRAWING' and len(c) >= 5: d[c[1].upper()] = int(c[3].split()[-1])
    return d


b, m = dangling(before), dangling(mid)
parts = {ntpath.basename(p.strip()).upper(): p.strip() for p in open(work + '/parts.txt', encoding='utf-8') if p.strip()}
users = {}
for l in open(work + '/users.tsv', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    users[c[0].upper()] = [x for x in c[2].split(',') if x.upper() not in skip]
rows, nohold = [], []
for d in sorted(m):
    if m[d] > b.get(d, 0):
        ps = users.get(d, [])
        if not ps: nohold.append(d); continue
        rows.append((d, parts[ps[0].upper()], m[d] - b.get(d, 0)))
with open(work + '/redim-list.tsv', 'w', encoding='utf-8', newline='') as f:
    for d, p, n in rows: f.write(d + '\t' + p + '\n')
print(len(m), 'drawings read;', len(rows), 'with new dangling;', sum(r[2] for r in rows), 'new dangling in all;',
      'missing from mid:', len(set(b) - set(m) - {k for k in b if k.endswith('.SLDASM')}), '; no held part:', nohold)
