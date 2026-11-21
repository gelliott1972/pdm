"""Manifest for the LS-08A-11 STEP export (Admin/Grant 21 Nov 2026). Read-only.
Walks the vault index (pdm-files scan JSON) from the top assembly through every reference (all configurations: the index
does not record which configuration uses what), and lists each file with its S3 versionId and updatedAt. Files that
SOLIDWORKS actually loaded for the exported configuration (USES lines of SwStep) are marked "used".
Usage: python sw/white/step-manifest.py <index.json> <top vault path> <config> <SwStep output> <out.txt>
"""
import collections, json, ntpath, sys

idx, top, config, stepout, out = sys.argv[1:6]
items = [i for i in json.load(open(idx, encoding='utf-8'))['Items'] if 'removedAt' not in i]
byid = {i['fileId']['S']: i for i in items}
bypath = {i['path']['S'].upper(): i for i in items}
root = bypath[top.upper()]
seen, order, todo, missing = set(), [], [root['fileId']['S']], []
while todo:
    f = todo.pop()
    if f in seen: continue
    seen.add(f)
    rec = byid.get(f)
    if rec is None: missing.append(f); continue
    order.append(rec)
    for r in rec.get('refs', {}).get('L', []): todo.append(r['S'])
    for m in rec.get('missingRefs', {}).get('L', []): missing.append(rec['path']['S'] + ' -> ' + json.dumps(m))
used = collections.Counter()
text = open(stepout, encoding='utf-8', errors='replace').read()
for l in text.splitlines():
    c = l.split('\t')
    if c[0] == 'USES': used[c[1].upper()] = int(c[2])
head = [l for l in text.splitlines() if l.split('\t')[0] in ('OPENED', 'COMPONENTS', 'MISSING', 'WHATSWRONG', 'STEP', 'SAVED', 'FAILED') or l.startswith('  error') or l.startswith('  warning')]
with open(out, 'w', encoding='utf-8', newline='') as f:
    f.write('STEP export of %s, configuration %s, from the PDM vault (P:\\ = vault after pdm get, 21 Nov 2026).\n' % (top, config))
    f.write('Exported read-only with SOLIDWORKS 2026 (SwStep), STEP AP203, like the 7 Oct reference.\n\n')
    f.write('SOLIDWORKS report:\n' + '\n'.join('  ' + l for l in head) + '\n\n')
    f.write('Reference tree from the vault index: %d files (all configurations of each file; "used" = loaded for %s, with instance count).\n' % (len(order), config))
    f.write('References the index could not find: %d%s\n\n' % (len(missing), (' ' + '; '.join(missing[:10])) if missing else ''))
    f.write('path\tversionId\tupdatedAt\tused\n')
    for rec in sorted(order, key=lambda r: r['path']['S'].upper()):
        p = rec['path']['S']
        u = used.get(ntpath.basename(p).upper(), 0)
        f.write('%s\t%s\t%s\t%s\n' % (p, rec.get('versionId', {}).get('S', ''), rec.get('updatedAt', {}).get('S', ''), ('used x%d' % u) if u else ''))
    notin = sorted(k for k in used if k not in {ntpath.basename(r['path']['S']).upper() for r in order})
    f.write('\nLoaded by SOLIDWORKS but not in the vault tree: %s\n' % (', '.join(notin) or 'none'))
print(len(order), 'files;', len(missing), 'missing;', sum(1 for r in order if used.get(ntpath.basename(r['path']['S']).upper())), 'used;', 'not in tree:', notin)
