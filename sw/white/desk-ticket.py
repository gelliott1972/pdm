"""PDM Desk ticket SW9 (Grant, 21 Nov 2026): the centre lines and centre marks that were white and now show, for Grant to
review (some may be stubs James had hidden). Free-text answer: which to delete. Reads fix.txt; writes desk/tickets/SW9.json.
Usage: python sw/white/desk-ticket.py <work folder>
"""
import collections, datetime, json, ntpath, os, sys

W = sys.argv[1]
items = collections.defaultdict(list)
for l in open(W + '/fix.txt', encoding='utf-8', errors='replace'):
    c = l.rstrip('\r\n').split('\t')
    if c[0] == 'ITEM' and c[4] in ('CenterLine', 'CenterMarkSym'):
        items[c[1]].append((c[2], c[3], 'centre line' if c[4] == 'CenterLine' else 'centre mark', c[5], c[7] if len(c) > 7 else ''))
# GNRIC-BRP-10 was done before item logging started (its 2 centre lines on Sheet4, seen and approved by Grant)
items['P:\\APEX\\GNRIC\\GNRIC-BRP-10.SLDDRW'] += [('Sheet4', '', 'centre line', '(2, before item logging)', '')]
lines = []
for f in sorted(items, key=lambda x: ntpath.basename(x).upper()):
    by = collections.Counter((s, v, k) for s, v, k, n, at in items[f])
    where = '; '.join('%s %s: %d %s%s' % (s, v, n, k, 's' if n > 1 else '') for (s, v, k), n in sorted(by.items()))
    lines.append('- **%s** (%s): %s' % (ntpath.basename(f)[:-7], ntpath.dirname(f)[3:].replace('\\', '/'), where))
body = ('These centre lines and centre marks were set to white by James and now show (colour back to Default). '
        'Some may be stubs he hid on purpose, like the 2 on GNRIC-BRP-11, which are now deleted.\n'
        'Please look at each drawing and type which ones to delete (drawing and sheet is enough, e.g. '
        '"GNRIC-LPP-02 Sheet1: delete both"), or "all fine".\n\n' + '\n'.join(lines) +
        '\n\n%d drawings, %d centre lines/marks. All are checked in; SW checks out and changes only the ones you name.' %
        (len(items), sum(len(v) for v in items.values())))
t = {'id': 'SW9', 'type': 'decision', 'ref': 'SW9', 'title': 'Centre lines that were white now show: any to delete?',
     'body': body, 'options': [], 'priority': 'later', 'owner': 'sw',
     'createdAt': datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'), 'status': 'open'}
p = 'C:/code/pdm/desk/tickets/SW9.json'
if os.path.exists(p): sys.exit('SW9 already exists; not overwritten')
open(p + '.tmp', 'w', encoding='utf-8', newline='').write(json.dumps(t, indent=1, ensure_ascii=False) + '\n')
os.replace(p + '.tmp', p)
print(len(items), 'drawings,', sum(len(v) for v in items.values()), 'items; written', p)
