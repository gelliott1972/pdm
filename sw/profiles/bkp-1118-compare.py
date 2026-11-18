"""Compare bkp-1118.py before/after folders (18 Nov 2026). Read-only. Prints every differing line per check, and the PDF
word differences (PyMuPDF). Path differences between P:\\ and the copy folder, and open/close chatter, are ignored.
Usage: python sw/profiles/bkp-1118-compare.py <before> <after> [<copy dir>]
"""
import collections, difflib, os, re, sys
import fitz

before, after = sys.argv[1], sys.argv[2]
copy = sys.argv[3].replace('/', chr(92)) if len(sys.argv) > 3 else None
SKIP = re.compile(r'^(Attached to|Opened |  \(opened read-only\)|Closed |OK$|Already open|Using the open)')


def lines(p):
    out = []
    for l in open(p, encoding='utf-8', errors='replace'):
        l = l.rstrip('\r\n')
        if SKIP.match(l):
            continue
        if copy:
            l = l.replace(copy + chr(92), 'P:' + chr(92) + 'APEX' + chr(92) + 'SC08' + chr(92) + 'LS-08X-04' + chr(92) + '_Shared' + chr(92))
        out.append(l)
    return out


bad = 0
for name in sorted(os.listdir(before)):
    if name.endswith('.txt') and name != 'log.txt':
        a, b = lines(os.path.join(before, name)), lines(os.path.join(after, name))
        d = [l for l in difflib.unified_diff(a, b, lineterm='', n=0) if l[:1] in '+-' and l[:3] not in ('+++', '---')]
        print('%-20s %s' % (name, 'same' if not d else '%d lines differ' % len(d)))
        for l in d:
            print('    ' + l)
    elif name.endswith('.pdf'):
        wa = collections.Counter(w[4] for w in fitz.open(os.path.join(before, name))[0].get_text('words'))
        wb = collections.Counter(w[4] for w in fitz.open(os.path.join(after, name))[0].get_text('words'))
        print('%-20s %s' % (name, 'same words' if wa == wb else 'only before: %s | only after: %s' % (dict(wa - wb), dict(wb - wa))))
