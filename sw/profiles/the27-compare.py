"""Compare the re-dimensioned scratch drawing copies with the original vault drawings (item 4, 17-18 Nov).
Per pair: same dimension count, 0 dangling, and every dimension's (sheet, view, name, value) the same as the original;
the sketch name inside the dimension's full name may differ (repointing rebuilds the profile sketch).
Usage: python sw/profiles/the27-compare.py <work folder>   -> prints one line per pair and writes <work>/result.tsv
"""
import collections, ntpath, sys

work = sys.argv[1]


def dims(lines):
    """DRAWING summary and a multiset of (sheet, view, dim name, value) from SwRefCheck --detail lines."""
    head, ms = None, collections.Counter()
    for c in lines:
        if c[0] == 'DRAWING': head = c
        elif c[0] == 'DIM':   # DIM file sheet view fullname value
            ms[(c[2], c[3], c[4].split('@')[0], c[5])] += 1
    return head, ms


orig = collections.defaultdict(list)
for l in open(work + '/orig.txt', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    if len(c) > 1 and c[0] in ('DRAWING', 'DIM'): orig[c[1].upper()].append(c)
pairs, cur = [], None
for l in open(work + '/after.txt', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    if c[0] == 'PAIR': cur = [c[2], c[3], []]; pairs.append(cur)
    elif cur and c[0] in ('DRAWING', 'DIM', 'DANGLING'): cur[2].append(c)
logs = {}
for l in open(work + '/redim.log', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    if c[0] == 'PAIR': key = (c[2], c[3]); logs[key] = collections.Counter(); logs[key]['exit ' + c[5].split()[-1]] += 1
    elif c[0] in ('KEPT', 'MISSING', 'DELETED'): logs[key][c[0]] += 1
    elif c[0] == 'EXTRA': logs[key]['EXTRA'] += int(c[2].split()[0])
out = open(work + '/result.tsv', 'w', encoding='utf-8')
ok = 0
for part, d, lines in pairs:
    oh, om = dims(orig[d.upper()])
    ah, am = dims(lines)
    lg = logs.get((part, d), {})
    why = []
    if ah is None: why.append('no check output')
    else:
        if ah[3] != 'dangling 0': why.append(ah[3])
        if oh and ah[2] != oh[2]: why.append(oh[2] + ' -> ' + ah[2])
        lost, new = om - am, am - om
        if lost: why.append('lost ' + '; '.join('%s %s=%s' % (k[1], k[2], k[3]) for k in lost))
        if new: why.append('new ' + '; '.join('%s %s=%s' % (k[1], k[2], k[3]) for k in new))
    res = 'SAME' if not why else 'DIFF'
    ok += res == 'SAME'
    row = [res, ntpath.basename(part), ntpath.basename(d), 'deleted %d kept %d missing %d extra %d' % (lg.get('DELETED', 0), lg.get('KEPT', 0), lg.get('MISSING', 0), lg.get('EXTRA', 0)), ' | '.join(why)]
    out.write('\t'.join(row) + '\n'); print('\t'.join(row))
print(ok, 'of', len(pairs), 'pairs the same as the original')
