"""Run SwFix over a list of parts (brief sw-profiles). Primary job first; if it refuses only because the shape changed and an
.alt job exists, the .alt job. Nothing is saved unless SwFix's checks pass.
Usage: python sw/profiles/run.py <jobs folder> <log folder> <base>|vault [--checked-out] [--only name,name] [--look]
  <base>: the rehearsal copies' root (vault path P:\\X -> <base>\\X); "vault": the P:\\ files themselves.
"""
import os, subprocess, sys, time

B = chr(92)
jobs, logs, base = sys.argv[1], sys.argv[2], sys.argv[3]
co = '--checked-out' in sys.argv
look = '--look' in sys.argv
only = None
if '--only' in sys.argv: only = set(sys.argv[sys.argv.index('--only') + 1].upper().split(','))
os.makedirs(logs, exist_ok=True)
X = 'C:/code/pdm/tools/sw-macros/bin/SwFix.exe'
summary = open(os.path.join(logs, 'summary.tsv'), 'a', encoding='utf-8')
for line in open(os.path.join(jobs, 'list.txt')):
    vp = line.strip()
    if not vp: continue
    name = vp.split(B)[-1]
    if only and name.upper() not in only: continue
    target = vp if base == 'vault' else base + B + vp[3:]
    tries = [os.path.join(jobs, name + '.tsv')]
    if os.path.exists(os.path.join(jobs, name + '.alt.tsv')): tries.append(os.path.join(jobs, name + '.alt.tsv'))
    result, used, text = 'NOT RUN', '', ''
    for k, jf in enumerate(tries):
        args = [X, target, jf] + ([] if look else ['--save']) + (['--checked-out'] if co else [])
        t0 = time.time()
        r = subprocess.run(args, capture_output=True, text=True, errors='replace')
        text = r.stdout + r.stderr
        open(os.path.join(logs, name + ('.alt' if k else '') + '.log'), 'w', encoding='utf-8').write(text + '\nexit %d, %.0f s\n' % (r.returncode, time.time() - t0))
        last = [l for l in text.splitlines() if l.strip()][-1] if text.strip() else '(no output)'
        result, used = last, os.path.basename(jf)
        shape_only = r.returncode == 1 and all(('shape changed' in l or 'without being asked' in l) for l in text.splitlines() if l.startswith('PROBLEM'))
        if r.returncode == 0 or not shape_only: break
    probs = [l[8:] for l in text.splitlines() if l.startswith('PROBLEM')]
    skips = [l[5:] for l in text.splitlines() if l.startswith('SKIP')]
    summary.write('\t'.join([time.strftime('%H:%M:%S'), name, result, used, ' | '.join(probs)[:2000], ' | '.join(skips)[:2000]]) + '\n'); summary.flush()
    print(time.strftime('%H:%M:%S'), name, '->', result, '(' + used + ')', ('PROBLEMS: ' + ' | '.join(probs)[:300]) if probs else '', flush=True)
