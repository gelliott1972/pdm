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
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
def memory_ok():
    # 17 Nov 00:40: SOLIDWORKS grew to 17.7 GB over the rehearsal and the PC ran out of commit (SOLIDWORKS stopped on
    # low-memory dialogs, then exited). Restart it cleanly before that can happen again.
    ps = ('$o=Get-CimInstance Win32_OperatingSystem; $s=(Get-Process SLDWORKS -ErrorAction SilentlyContinue | Measure-Object PagedMemorySize64 -Sum).Sum;'
          ' "{0} {1}" -f [int]($o.FreeVirtualMemory/1024), [int]($s/1MB)')
    out = subprocess.run(['powershell', '-NoProfile', '-Command', ps], capture_output=True, text=True).stdout.split()
    free, swmb = int(out[0]), int(out[1])
    return free >= 12000 and swmb <= 8000, free, swmb
def restart_sw():
    subprocess.run([SWCMD, 'close-all', '--exit', '--as', 'sw'], capture_output=True, text=True, timeout=300)
    time.sleep(10)
    subprocess.Popen([r'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe'])   # SOLIDWORKS 2026 (34.3), visible
    time.sleep(60)
    r = subprocess.run([SWCMD, 'status', '--as', 'sw'], capture_output=True, text=True, timeout=600)
    print(time.strftime('%H:%M:%S'), 'SOLIDWORKS restarted:', ' '.join(r.stdout.split())[:200], flush=True)
for line in open(os.path.join(jobs, 'list.txt')):
    vp = line.strip()
    if not vp: continue
    name = vp.split(B)[-1]
    if only and name.upper() not in only: continue
    ok, free, swmb = memory_ok()
    if not ok:
        print(time.strftime('%H:%M:%S'), 'memory: commit free %d MB, SOLIDWORKS %d MB: restarting SOLIDWORKS' % (free, swmb), flush=True)
        restart_sw()
    target = vp if base == 'vault' else base + B + vp[3:]
    tries = [os.path.join(jobs, name + '.tsv')]
    if os.path.exists(os.path.join(jobs, name + '.alt.tsv')): tries.append(os.path.join(jobs, name + '.alt.tsv'))
    result, used, text = 'NOT RUN', '', ''
    for k, jf in enumerate(tries):
        args = [X, target, jf] + ([] if look else ['--save']) + (['--checked-out'] if co else [])
        t0 = time.time()
        try:
            r = subprocess.run(args, capture_output=True, text=True, errors='replace', timeout=900)
        except subprocess.TimeoutExpired as e:
            subprocess.run(['taskkill', '/F', '/IM', 'SwFix.exe'], capture_output=True)
            text = (e.stdout or b'').decode('utf-8', 'replace') if isinstance(e.stdout, bytes) else (e.stdout or '')
            open(os.path.join(logs, name + ('.alt' if k else '') + '.log'), 'w', encoding='utf-8').write(text + '\nTIMEOUT after 900 s: SwFix stopped, nothing saved by it\n')
            result, used = 'TIMEOUT (not saved)', os.path.basename(jf)
            print(time.strftime('%H:%M:%S'), name, '-> TIMEOUT: stopping here; SOLIDWORKS may be waiting on a dialog', flush=True)
            summary.write('	'.join([time.strftime('%H:%M:%S'), name, result, used, 'timeout', '']) + '\n'); summary.flush()
            sys.exit(3)
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
