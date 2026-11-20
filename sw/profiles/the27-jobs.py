"""Job folder for the real run of the 27 parts + 08X04-SFP-03 (Grant's go, 20 Nov 2026).
Copies the 16-17 Nov real-run job files of the 27 (restore.txt) and makes 08X04-SFP-03's from the rehearsed
historical-copy job with the vault root. list.txt = the 27 (run.py); SFP-03 runs on its own with --accept-vol.
Usage: python sw/profiles/the27-jobs.py <jobs folder>
"""
import os, shutil, sys

BS = chr(92)
S = 'C:/code/pdm/scratch/profiles/'
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
parts = [l.strip() for l in open(S + 'restore.txt', encoding='utf-8') if l.strip()]
with open(out + '/list.txt', 'w', encoding='utf-8', newline='') as f:
    f.write('\n'.join(parts) + '\n')
n = 0
for p in parts:
    name = p.split(BS)[-1]
    for x in ('.tsv', '.alt.tsv'):
        src = S + 'jobs-real/' + name + x
        if os.path.exists(src):
            shutil.copyfile(src, out + '/' + name + x); n += 1
        elif x == '.tsv':
            print('MISSING', src)
for name in os.listdir(out):
    if name.endswith('.tsv'):
        root = [l for l in open(out + '/' + name, encoding='utf-8') if l.startswith('ROOT')]
        want = 'ROOT\tP:' + BS + '_Shared' + BS + '_Profiles'
        if not root or root[0].strip() != want: print('ROOT not the vault in', name, root)
lines = open(S + 'the27/sfp03/job-hist.tsv', encoding='utf-8').read().splitlines()
lines = ['ROOT\tP:' + BS + '_Shared' + BS + '_Profiles' if l.startswith('ROOT') else l for l in lines]
with open(out + '/08X04-SFP-03.SLDPRT.tsv', 'w', encoding='utf-8', newline='') as f:
    f.write('\n'.join(lines) + '\n')
print(len(parts), 'parts in list.txt;', n, 'job files copied; 08X04-SFP-03.SLDPRT.tsv written')
