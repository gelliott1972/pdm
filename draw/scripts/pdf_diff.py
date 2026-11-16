# pdf_diff.py DIR_A DIR_B [names...] : compare same-named PDFs in two folders (e.g. yesterday's regression run and
# today's, or Grant's saved drawing and a remade one). Per drawing: words only in A, words only in B, words that moved
# more than 3 mm, and the share of the page whose ink differs. A regression check that sees layout, not only values.
import sys, os, glob, collections, re
import pymupdf

def words(p):
    return [(w[4], (w[0] + w[2]) / 2 * 25.4 / 72, (w[1] + w[3]) / 2 * 25.4 / 72) for w in pymupdf.open(p)[0].get_text('words') if not re.match(r'^[0-9]{2}/[0-9]{2}/[0-9]{4}$', w[4])]   # not the revision date

def ink(p):
    pm = pymupdf.open(p)[0].get_pixmap(dpi=36, colorspace=pymupdf.csGRAY)
    return pm.width, pm.height, pm.samples

a, b = sys.argv[1], sys.argv[2]
names = sys.argv[3:] or sorted(os.path.basename(f)[:-4] for f in glob.glob(os.path.join(a, '*.pdf')))
for n in names:
    fa, fb = os.path.join(a, n + '.pdf'), os.path.join(b, n + '.pdf')
    if not os.path.exists(fb): print(n, ': not in', b); continue
    wa, wb = words(fa), words(fb)
    ca, cb = collections.Counter(w[0] for w in wa), collections.Counter(w[0] for w in wb)
    onlyA, onlyB = ca - cb, cb - ca
    moved = []
    for t in set(ca) & set(cb):
        pa = sorted((x, y) for w, x, y in wa if w == t); pb = sorted((x, y) for w, x, y in wb if w == t)
        if len(pa) != len(pb): continue
        for (x1, y1), (x2, y2) in zip(pa, pb):
            d = ((x1 - x2) ** 2 + (y1 - y2) ** 2) ** 0.5
            if d > 3: moved.append('%s %.0f mm' % (t, d))
    w1, h1, s1 = ink(fa); w2, h2, s2 = ink(fb)
    diff = sum(1 for i in range(min(len(s1), len(s2))) if (s1[i] < 200) != (s2[i] < 200)) * 100.0 / max(1, min(len(s1), len(s2)))
    same = not onlyA and not onlyB and not moved and diff < 0.3
    print('%-16s %s' % (n, 'SAME' if same else 'DIFFERENT'), '| only in A:', dict(onlyA), '| only in B:', dict(onlyB), '| moved:', moved[:12], '| ink %.2f%%' % diff)
