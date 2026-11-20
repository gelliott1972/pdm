"""Users of the 28 parts (the 27 + 08X04-SFP-03) for the real run, 20 Nov 2026. Read-only (a DmProbe inventory).
Every drawing and assembly whose reference tree reaches one of the parts, with which parts.
Usage: python sw/profiles/the27-users.py <inventory folder> <jobs folder> <out folder>
Writes <out>/parts.txt (28 vault paths), <out>/users.tsv (user path, kind, parts), <out>/drawings.txt, <out>/assemblies.txt.
"""
import csv, ntpath, os, sys

BS = chr(92)
inv, jobs, out = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
parts = [l.strip() for l in open(jobs + '/list.txt', encoding='utf-8') if l.strip()]
parts.append(BS.join(['P:', 'APEX', 'SC08', 'LS-08X-04', '_Shared', '08X04-SFP-03.SLDPRT']))
files = {r['file'].upper(): r for r in csv.DictReader(open(inv + '/files.csv', encoding='utf-8-sig'))}
down = {}
for r in csv.DictReader(open(inv + '/references.csv', encoding='utf-8-sig')):
    down.setdefault(r['file'].upper(), set()).add(ntpath.basename(r['reference']).upper())
pn = {ntpath.basename(p).upper(): p for p in parts}
missing = [p for p in parts if ntpath.basename(p).upper() not in files]


def path(name):
    r = files[name]
    return BS.join(['P:', 'APEX', r['folder'], r['file']])


def reach(name):
    seen, todo = set(), [name]
    while todo:
        n = todo.pop()
        for m in down.get(n, ()):
            if m not in seen:
                seen.add(m); todo.append(m)
    return seen


rows = []
for name, r in files.items():
    if r['type'] not in ('drawing', 'assembly'): continue
    hit = sorted(reach(name) & set(pn))
    if hit: rows.append((path(name), r['type'], ','.join(hit)))
rows.sort()
with open(out + '/parts.txt', 'w', encoding='utf-8', newline='') as f: f.write('\n'.join(parts) + '\n')
with open(out + '/users.tsv', 'w', encoding='utf-8', newline='') as f:
    for r in rows: f.write('\t'.join(r) + '\n')
for kind, fn in (('drawing', 'drawings.txt'), ('assembly', 'assemblies.txt')):
    with open(out + '/' + fn, 'w', encoding='utf-8', newline='') as f:
        f.write('\n'.join(r[0] for r in rows if r[1] == kind) + '\n')
print(len(parts), 'parts;', sum(r[1] == 'drawing' for r in rows), 'drawings;', sum(r[1] == 'assembly' for r in rows), 'assemblies;',
      'parts not in the inventory:', missing or 'none')
