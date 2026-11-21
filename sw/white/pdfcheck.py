"""Check SOLIDWORKS-exported drawing PDFs for what prints white or in colour (Grant, 21 Nov 2026). Read-only.
Per PDF: text in white, text in any colour other than black (with examples), and vector lines drawn in white or in a
colour (not black or grey). Prints OK or the findings.
Usage: python sw/white/pdfcheck.py <pdf or folder> [...]
"""
import collections, os, sys
import pymupdf


def grey(c):
    r, g, b = c
    return abs(r - g) < 0.02 and abs(g - b) < 0.02 and r < 0.9


def hexc(c):
    return '#%02x%02x%02x' % tuple(int(round(x * 255)) for x in c)


def check(path):
    doc = pymupdf.open(path)
    text = collections.defaultdict(list)
    lines = collections.Counter()
    for pno, page in enumerate(doc, 1):
        for b in page.get_text('dict')['blocks']:
            for l in b.get('lines', []):
                for s in l['spans']:
                    t = s['text'].strip()
                    if t and s['color'] != 0:
                        text['#%06x' % s['color']].append('p%d "%s"' % (pno, t))
        for d in page.get_drawings():
            for key in ('color', 'fill'):
                c = d.get(key)
                if key == 'fill' and c and hexc(c) == '#ffffff': continue   # white fills (table cells, masks) are normal
                if c and len(c) == 3 and not grey(c) and hexc(c) != '#000000':
                    lines[hexc(c) + (' line' if key == 'color' else ' fill')] += 1
    found = []
    for col, ex in sorted(text.items()):
        found.append('text %s x%d (%s)' % ('WHITE' if col == '#ffffff' else col, len(ex), ', '.join(ex[:4])))
    for col, n in sorted(lines.items()):
        found.append('%s x%d' % (col.replace('#ffffff', 'WHITE'), n))
    print(('OK' if not found else 'CHECK') + '\t' + os.path.basename(path) + ('\t' + ' | '.join(found) if found else ''), flush=True)


for a in sys.argv[1:]:
    if os.path.isdir(a):
        for f in sorted(os.listdir(a)):
            if f.lower().endswith('.pdf'): check(os.path.join(a, f))
    else:
        check(a)
