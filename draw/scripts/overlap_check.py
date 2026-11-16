# overlap_check.py PDF... : layout faults a number check can't see, read from the PDF itself:
#   - text on text (two words whose boxes overlap),
#   - a drawn line running through a word,
#   - text outside the sheet.
# Prints one line per drawing: OK, or the words hit (with where on the sheet, mm from the bottom left).
import sys, os, glob
import pymupdf

MM = 25.4 / 72

def segs(page):
    out = []
    for d in page.get_drawings():
        for it in d['items']:
            if it[0] == 'l': out.append((it[1].x, it[1].y, it[2].x, it[2].y))
            elif it[0] == 're':
                r = it[1]
                out += [(r.x0, r.y0, r.x1, r.y0), (r.x1, r.y0, r.x1, r.y1), (r.x1, r.y1, r.x0, r.y1), (r.x0, r.y1, r.x0, r.y0)]
            elif it[0] == 'qu':
                q = it[1]; p = [q.ul, q.ur, q.lr, q.ll]
                out += [(p[i].x, p[i].y, p[(i + 1) % 4].x, p[(i + 1) % 4].y) for i in range(4)]
            elif it[0] == 'c':   # a curve: its chord is near enough for short arcs
                out.append((it[1].x, it[1].y, it[4].x, it[4].y))
    return out

def cuts(s, r):
    # does segment s pass through rectangle r (x0, y0, x1, y1)? Liang-Barsky
    x0, y0, x1, y1 = s; dx, dy = x1 - x0, y1 - y0; t0, t1 = 0.0, 1.0
    for p, q in ((-dx, x0 - r[0]), (dx, r[2] - x0), (-dy, y0 - r[1]), (dy, r[3] - y0)):
        if abs(p) < 1e-12:
            if q < 0: return False
        else:
            t = q / p
            if p < 0: t0 = max(t0, t)
            else: t1 = min(t1, t)
            if t0 > t1: return False
    return True

def check(pdf):
    page = pymupdf.open(pdf)[0]; H = page.rect.height; W = page.rect.width
    ws = [w for w in page.get_text('words') if w[4].strip()]
    ss = segs(page); faults = []
    def where(w): return '(%.0f, %.0f)' % ((w[0] + w[2]) / 2 * MM, (H - (w[1] + w[3]) / 2) * MM)
    def core(w, f):   # the middle of the word's box (text boxes carry air round the glyphs)
        hx, hy = (w[2] - w[0]) * f, (w[3] - w[1]) * f
        return (w[0] + hx, w[1] + hy, w[2] - hx, w[3] - hy)
    # CHECK notes sit outside the border, bottom left, across the frame's zone strip (where Grant put them): they are
    # only reported when they run into other text inside the border. The title block's own words are the template's.
    chk = set(w[5] for w in ws if w[4] == 'CHECK:')
    def mm(w): return ((w[0] + w[2]) / 2 * MM, (H - (w[1] + w[3]) / 2) * MM)
    def strip(w): x, y = mm(w); return x < 12.5 or y < 12.5 or x > W * MM - 12.5 or y > H * MM - 12.5
    def title(w): x, y = mm(w); return x > 228 and y < 58
    for i, a in enumerate(ws):
        ca = core(a, 0.18)
        if (a[0] < 0 or a[1] < 0 or a[2] > W or a[3] > H) and a[5] not in chk: faults.append('off the sheet: %s %s' % (a[4], where(a)))
        for b in ws[i + 1:]:
            if (a[5] in chk and (b[5] in chk or strip(b))) or (b[5] in chk and strip(a)) or (title(a) and title(b)): continue
            cb = core(b, 0.18)
            if ca[0] < cb[2] and cb[0] < ca[2] and ca[1] < cb[3] and cb[1] < ca[3]: faults.append('text on text: %s / %s %s' % (a[4], b[4], where(a)))
        if a[5] in chk or title(a) or strip(a): continue
        cl = core(a, 0.25)
        if any(cuts(sg, cl) for sg in ss): faults.append('line through text: %s %s' % (a[4], where(a)))
    return faults

if __name__ == '__main__':
    files = []
    for a in sys.argv[1:]: files += sorted(glob.glob(a)) if any(c in a for c in '*?') else [a]
    for f in files:
        r = check(f)
        print('%-22s %s' % (os.path.basename(f)[:-4], 'OK' if not r else '%d: ' % len(r) + '; '.join(r[:10])))
