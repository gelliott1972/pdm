"""Check the 4 drawings Grant wanted printed as before (20 Nov 2026): export each vault drawing to PDF (for this check only,
not for Grant) and compare its words with the original PDF from the 18 Nov rehearsal (the27/redim2/pdf/orig).
Usage: python sw/profiles/the27-decimals.py <out folder>
"""
import collections, os, subprocess, sys
import pymupdf

BS = chr(92)
SW = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
ORIG = 'C:/code/pdm/scratch/profiles/the27/redim2/pdf/orig/'
out = sys.argv[1].replace('/', BS)
os.makedirs(out, exist_ok=True)
D = {'08XXX-HBA-01': ['P:', 'APEX', 'SC08', '_Shared'], '08X04-DBP-01': ['P:', 'APEX', 'SC08', 'LS-08X-04', '_Shared'],
     '08X06-DBP-01': ['P:', 'APEX', 'SC08', 'LS-08X-06', '_Shared'], '08X06-DBP-02': ['P:', 'APEX', 'SC08', 'LS-08X-06', '_Shared']}
for n, parts in D.items():
    pdf = out + BS + n + '.pdf'
    if os.path.exists(pdf): os.remove(pdf)
    r = subprocess.run([SW, 'export-pdf', BS.join(parts + [n + '.SLDDRW']), pdf, '--as', 'sw'], capture_output=True, text=True, errors='replace')
    if not os.path.exists(pdf): print(n, 'EXPORT FAILED', r.stdout[-300:], r.stderr[-300:]); continue
    origs = [f for f in os.listdir(ORIG) if f.upper().startswith(n.upper())]
    now = collections.Counter(w[4] for w in pymupdf.open(pdf)[0].get_text('words'))
    for f in origs:
        o = collections.Counter(w[4] for w in pymupdf.open(ORIG + f)[0].get_text('words'))
        print(n, 'vs', f, '| only before:', dict(o - now), '| only now:', dict(now - o))
    if not origs: print(n, 'no original PDF found')
