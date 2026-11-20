"""Printed-words check of every redone drawing (20 Nov 2026), for SW's own check (no PDFs for Grant): export each vault
drawing in <list> to PDF and compare its words with the 18 Nov original PDF (the27/redim2/pdf/orig/<n>.pdf, numbered as
in redim2/pdf-compare.tsv). Differences that are only the intended cut-list labels are marked LABELS.
Usage: python sw/profiles/the27-words.py <redim-list.tsv> <out folder>
"""
import collections, ntpath, os, re, subprocess, sys
import pymupdf

BS = chr(92)
SW = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
R = 'C:/code/pdm/scratch/profiles/the27/redim2/'
out = sys.argv[2].replace('/', BS)
os.makedirs(out, exist_ok=True)
num = {}
for l in open(R + 'pdf-compare.tsv', encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    num.setdefault(c[1].upper(), c[0])
LABEL = re.compile(r'^(RHS|SHS|RSA|EA)\d|^PLATE,$|^END$|^CAP$|^x$|^\d+x\d+x\d+$|^\d+$')
for l in open(sys.argv[1], encoding='utf-8'):
    d = l.split('\t')[0].strip()
    n = ntpath.basename(d).upper()
    pdf = out + BS + n.replace('.SLDDRW', '.pdf')
    if os.path.exists(pdf): os.remove(pdf)
    subprocess.run([SW, 'export-pdf', d, pdf, '--as', 'sw'], capture_output=True, text=True, errors='replace')
    if n not in num: print('NO ORIGINAL\t' + n); continue
    if not os.path.exists(pdf): print('EXPORT FAILED\t' + n); continue
    o = collections.Counter(w[4] for p in pymupdf.open(R + 'pdf/orig/' + num[n] + '.pdf') for w in p.get_text('words'))
    a = collections.Counter(w[4] for p in pymupdf.open(pdf) for w in p.get_text('words'))
    lost, new = o - a, a - o
    if not lost and not new: kind = 'SAME'
    elif all(LABEL.match(w) for w in list(lost) + list(new)) and not any('.' in w for w in list(lost) + list(new)): kind = 'LABELS'
    else: kind = 'CHECK'
    print('%s\t%s\tonly before: %s\tonly now: %s' % (kind, n, dict(lost), dict(new)), flush=True)
