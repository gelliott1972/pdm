"""Item 4 (17-18 Nov): print each original drawing and its re-dimensioned scratch copy to PDF and compare what is printed:
the words on each page (as a multiset) and the page images (pixels that differ). Read-only on P:\\; PDFs go to scratch.
Usage: python sw/profiles/the27-pdf.py <work folder>   (the folder the27-redim.py used)
Writes <work>/pdf/orig/NN.pdf, <work>/pdf/redo/NN.pdf and <work>/pdf-compare.tsv.
"""
import collections, ntpath, os, subprocess, sys, time
import fitz  # PyMuPDF

SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
work = sys.argv[1]
pairs = []
for l in open(work + '/redim.log', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    if c[0] == 'PAIR': pairs.append((int(c[1]), c[3], c[4].replace('/', '\\')))
for sub in ('orig', 'redo'):
    os.makedirs(os.path.join(work, 'pdf', sub), exist_ok=True)


def guard():
    st = subprocess.run([SWCMD, 'status'], capture_output=True, text=True, errors='replace').stdout
    if '\nLock: sw ' not in st:
        print('STOP: the SOLIDWORKS lock is not held by sw', flush=True); sys.exit(3)
    subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)


def export(src, out):
    if os.path.exists(out): return 'kept'
    guard()
    r = subprocess.run([SWCMD, 'export-pdf', src, out.replace('/', '\\'), '--as', 'sw'], capture_output=True, text=True, errors='replace', timeout=900)
    return 'ok' if os.path.exists(out) else 'FAILED ' + (r.stdout + r.stderr).strip().splitlines()[-1:][0] if (r.stdout + r.stderr).strip() else 'FAILED'


def words(doc):
    return collections.Counter(w[4] for p in doc for w in p.get_text('words'))


def pixels(a, b):
    n = 0
    for pa, pb in zip(a, b):
        ia, ib = pa.get_pixmap(dpi=100, colorspace=fitz.csGRAY), pb.get_pixmap(dpi=100, colorspace=fitz.csGRAY)
        if (ia.width, ia.height) != (ib.width, ib.height): return -1
        sa, sb = ia.samples, ib.samples
        n += sum(1 for i in range(0, len(sa)) if abs(sa[i] - sb[i]) > 64)
    return n


out = open(work + '/pdf-compare.tsv', 'w', encoding='utf-8')
for n, orig, redo in pairs:
    po, pr = os.path.join(work, 'pdf', 'orig', '%02d.pdf' % n), os.path.join(work, 'pdf', 'redo', '%02d.pdf' % n)
    eo, er = export(orig, po), export(redo, pr)
    if not (os.path.exists(po) and os.path.exists(pr)):
        row = ['%02d' % n, ntpath.basename(orig), 'NO PDF', eo, er]
    else:
        a, b = fitz.open(po), fitz.open(pr)
        wa, wb = words(a), words(b)
        row = ['%02d' % n, ntpath.basename(orig), 'TEXT SAME' if wa == wb else 'TEXT DIFF',
               'only original: ' + ' '.join(sorted((wa - wb).elements())) + ' | only re-done: ' + ' '.join(sorted((wb - wa).elements())) if wa != wb else '',
               'pixels differing %d' % pixels(a, b)]
    out.write('\t'.join(row) + '\n'); out.flush()
    print(time.strftime('%H:%M:%S'), '\t'.join(row)[:200], flush=True)
subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)
