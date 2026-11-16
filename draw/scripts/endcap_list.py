# endcap_list.py : from scratch\draw-test\endcaps\survey.txt (endcap_survey.sh) write endcaps.tsv: one row per end-cap
# body (a body whose first feature is an End cap feature): part, configuration, body, cut-list item, the item's
# Description now, size, proposed Description "END CAP <a>x<b>x<t>" (Grant 16 Nov).
import re, io
E = r'C:\code\pdm\scratch\draw-test\endcaps'
rows = []; part = cfg = item = None; items = {}; desc = {}
def num(x):
    f = float(x); return ('%g' % f)
for line in io.open(E + r'\survey.txt', encoding='utf-8', errors='replace'):
    line = line.rstrip('\r\n')
    if line.startswith('PART\t'):
        part = line.split('\t')[1]; cfg = item = None; items = {}; desc = {}
    elif line.startswith('Configuration: '):
        cfg = line[len('Configuration: '):]
    elif re.match(r'^  \S', line):
        item = re.sub(r': \d+ bod(y|ies)$', '', line.strip())
    elif line.startswith('      body '):
        items[line[len('      body '):]] = item
    elif line.strip().startswith('Description = '):
        desc[item] = line.strip()[len('Description = '):]
    elif line.startswith('BODY\t'):
        f = line.split('\t')
        feats = f[-1].replace('features ', '', 1).split(', ')
        if not re.match(r'(?i)^end ?cap', feats[0]): continue
        m = re.match(r'([\d.]+) x ([\d.]+) x ([\d.]+) mm', f[2])
        d = sorted(float(m.group(i)) for i in (1, 2, 3))
        it = items.get(f[1], '?')
        rows.append([part.replace('./', '').replace('/', '\\'), cfg or '?', f[1], it, desc.get(it, ''), f[2], 'END CAP %sx%sx%s' % (num(d[2]), num(d[1]), num(d[0]))])
with io.open(E + r'\endcaps.tsv', 'w', encoding='utf-8', newline='\r\n') as o:
    o.write('Part (under APEX)\tConfiguration surveyed (active)\tBody\tCut-list item\tDescription now\tSize\tProposed Description\n')
    for r in rows: o.write('\t'.join(r) + '\n')
parts = sorted(set(r[0] for r in rows))
print(len(rows), 'end-cap bodies in', len(parts), 'parts;', sum(1 for r in rows if r[4] == ''), 'without a Description')
for p in parts: print(' ', p, sorted(set(r[6] for r in rows if r[0] == p)))
