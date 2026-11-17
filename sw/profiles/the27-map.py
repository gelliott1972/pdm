"""Map the 59 failing drawings of the 17 Nov night run to the 27 restored parts and their repointed rehearsal copies
(Admin, 17-18 Nov: prep for Grant's session on the 27). Read-only: reads compare.txt, restore.txt, an inventory.
Usage: python sw/profiles/the27-map.py <inventory folder> <out.tsv>
Out columns: part, rehearsal copy, drawing.
"""
import csv, ntpath, os, sys

B = 'C:/code/pdm/scratch/profiles/'
inv, out = sys.argv[1], sys.argv[2]
parts = [l.strip() for l in open(B + 'restore.txt', encoding='utf-8') if l.strip()]
fails = [l.split('\t')[1].strip() for l in open(B + 'compare.txt', encoding='utf-8') if l.startswith('FAIL\t')]
refs = list(csv.DictReader(open(inv + '/references.csv', encoding='utf-8-sig')))
down = {}
for r in refs:
    down.setdefault(r['file'].upper(), set()).add(ntpath.basename(r['reference']).upper())

def closure(name):
    seen, todo = set(), [name.upper()]
    while todo:
        n = todo.pop()
        for m in down.get(n, ()):
            if m not in seen:
                seen.add(m); todo.append(m)
    return seen

rows = []
for d in fails:
    below = closure(ntpath.basename(d))
    hit = [p for p in parts if ntpath.basename(p).upper() in below]
    for p in hit:
        rel = p[len('P:\\'):]
        copy = ntpath.join(B.replace('/', '\\') + 'rehearsal\\parts', rel)
        rows.append((p, copy if os.path.exists(copy) else 'MISSING ' + copy, d))
    if not hit:
        rows.append(('NONE', '', d))
with open(out, 'w', encoding='utf-8', newline='') as f:
    for r in sorted(rows):
        f.write('\t'.join(r) + '\n')
print(len(fails), 'failing drawings;', len({r[0] for r in rows}), 'parts;', len(rows), 'rows;',
      sum(1 for r in rows if r[1].startswith('MISSING')), 'missing copies;', sum(1 for r in rows if r[0] == 'NONE'), 'drawings with no part')
