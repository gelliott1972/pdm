"""All vault drawings to Default colours (Grant, 21 Nov 2026: "all are to default UNLESS there is another color - list these
at the end but don't stop"). One SOLIDWORKS open per drawing (SwAnnFix --save --checked-out, which checks count, values,
positions and dangling in that same open and saves only if unchanged).
Steps (each can be run on its own):
  list      <work>: every live .SLDDRW in the vault index that exists on P:\\ (not _Review/_Released) -> drawings.txt
  checkout  <work>: pdm checkout of drawings.txt (dry first; stops if any is held by someone else), then backups
  fix       <work>: SwAnnFix over drawings.txt, 10 a run, guarded (sw lock, memory) -> fix.txt; a FAIL is put back from backup
  finish    <work>: dry load must list exactly the CHANGED SAVED drawings; pdm checkin those with a note; pdm unlock the rest
Usage: python sw/white/fixall.py <step> <work folder>
"""
import json, os, shutil, subprocess, sys, time

BS = chr(92)
PDM = 'C:/code/pdm/src/Pdm/bin/Debug/net8.0-windows/pdm.exe'
FIX = 'C:/code/pdm/tools/sw-macros/bin/SwAnnFix.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
NOTE = 'Annotation colours back to Default (white/black overrides removed); nothing else changed (Grant 21 Nov)'
step, W = sys.argv[1], sys.argv[2]
os.makedirs(W, exist_ok=True)


def run(args, timeout=3600):
    return subprocess.run(args, capture_output=True, text=True, errors='replace', timeout=timeout)


def drawings():
    return [l.strip() for l in open(W + '/drawings.txt', encoding='utf-8') if l.strip()]


def guard():
    st = run([SWCMD, 'status']).stdout
    if '\nLock: sw ' not in st: print('STOP: lock not held by sw', flush=True); sys.exit(3)
    ps = ('$o=Get-CimInstance Win32_OperatingSystem; $s=(Get-Process SLDWORKS -ErrorAction SilentlyContinue | Measure-Object PagedMemorySize64 -Sum).Sum;'
          ' "{0} {1}" -f [int]($o.FreeVirtualMemory/1024), [int]($s/1MB)')
    m = run(['powershell', '-NoProfile', '-Command', ps]).stdout.split()
    if int(m[0]) < 12000 or int(m[1]) > 8000:
        print(time.strftime('%H:%M:%S'), 'memory', m, 'restarting SOLIDWORKS (Close All, Exit, start)', flush=True)
        run([SWCMD, 'close-all', '--exit', '--as', 'sw'], 300)
        time.sleep(10)
        subprocess.Popen([r'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe'])
        time.sleep(60)
    run([SWCMD, 'close-all', '--as', 'sw'], 300)


if step == 'list':
    r = run(['aws', 'dynamodb', 'scan', '--table-name', 'pdm-files', '--profile', 'pdm-admin', '--region', 'ap-east-1', '--output', 'json'])
    if r.returncode != 0: print('index scan failed:', r.stderr[-300:]); sys.exit(4)
    items = json.loads(r.stdout)['Items']
    out, missing, held = [], [], []
    for i in items:
        p = i['path']['S']
        if not p.upper().endswith('.SLDDRW') or 'removedAt' in i: continue
        if '/_REVIEW/' in p.upper() or '/_RELEASED/' in p.upper(): continue
        f = 'P:' + BS + p.replace('/', BS)
        (out if os.path.exists(f) else missing).append(f)
    out.sort()
    open(W + '/drawings.txt', 'w', encoding='utf-8', newline='').write('\n'.join(out) + '\n')
    print(len(out), 'drawings;', len(missing), 'in the index but not on P:', missing[:5])

elif step == 'checkout':
    fs = drawings()
    dry = run([PDM, 'checkout'] + fs + ['--dry'])
    open(W + '/checkout-dry.txt', 'w', encoding='utf-8').write(dry.stdout + dry.stderr)
    n = dry.stdout.count('would check out')
    print('dry:', n, 'of', len(fs), 'would be checked out;', dry.stdout.strip().splitlines()[-1])
    if n != len(fs): print('STOP: not all free; see checkout-dry.txt'); sys.exit(5)
    real = run([PDM, 'checkout'] + fs)
    open(W + '/checkout.txt', 'w', encoding='utf-8').write(real.stdout + real.stderr)
    print('checked out:', real.stdout.count('Checked out'))
    for f in fs:
        dst = W + '/backup/' + f[3:].replace(BS, '/')
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(f, dst)
    print(len(fs), 'backed up;', sum(not os.access(f, os.W_OK) for f in fs), 'not writable')

elif step == 'fix':
    fs = drawings()
    done = set()
    if os.path.exists(W + '/fix.txt'):
        done = {l.split('\t')[1] for l in open(W + '/fix.txt', encoding='utf-8', errors='replace') if l.startswith('RESULT\t')}
    todo = [f for f in fs if f not in done]
    out = open(W + '/fix.txt', 'a', encoding='utf-8')
    print(len(done), 'already done;', len(todo), 'to do', flush=True)
    for i in range(0, len(todo), 10):
        guard()
        batch = todo[i:i + 10]
        r = run([FIX] + batch + ['--save', '--checked-out'])
        out.write(r.stdout + r.stderr); out.flush()
        for l in r.stdout.splitlines():
            c = l.split('\t')
            if c[0] == 'RESULT' and c[2].startswith('FAIL'):
                shutil.copy2(W + '/backup/' + c[1][3:].replace(BS, '/'), c[1])   # put back exactly as it was
                out.write('RESTORED\t' + c[1] + '\n'); out.flush()
        print(time.strftime('%H:%M:%S'), min(i + 10, len(todo)), 'of', len(todo), flush=True)
    run([SWCMD, 'close-all', '--as', 'sw'], 300)

elif step == 'finish':
    res = {}
    for l in open(W + '/fix.txt', encoding='utf-8', errors='replace'):
        c = l.rstrip('\r\n').split('\t')
        if c[0] == 'RESULT': res[c[1]] = c[2]
    changed = sorted(f for f, r in res.items() if r.startswith('CHANGED') and r.endswith('SAVED'))
    rest = sorted(f for f in drawings() if f not in changed)
    dry = run([PDM, 'load', '--dry'])
    open(W + '/load-dry-before.txt', 'w', encoding='utf-8').write(dry.stdout + dry.stderr)
    listed = {l.split(None, 1)[1].strip().upper() for l in dry.stdout.splitlines() if l.startswith('  changed') or l.startswith('  new')}   # names may have spaces
    want = {f[3:].replace(BS, '/').upper() for f in changed}
    if listed != want:
        print('STOP: dry load lists', len(listed), 'but', len(want), 'were saved; extra', sorted(listed - want)[:5], 'missing', sorted(want - listed)[:5]); sys.exit(6)
    print('dry load: exactly the', len(want), 'saved drawings')
    for i in range(0, len(changed), 40):
        r = run([PDM, 'checkin'] + changed[i:i + 40] + ['--note', NOTE])
        open(W + '/checkin.txt', 'a', encoding='utf-8').write(r.stdout + r.stderr)
        print('checked in', r.stdout.count('Checked in'), 'of', len(changed[i:i + 40]), r.stderr.strip()[-200:])
        if r.returncode != 0: print('STOP: checkin failed (AWS login?)'); sys.exit(7)
    for i in range(0, len(rest), 40):
        r = run([PDM, 'unlock'] + rest[i:i + 40])
        open(W + '/unlock.txt', 'a', encoding='utf-8').write(r.stdout + r.stderr)
        print('released', r.stdout.count('Released'), 'of', len(rest[i:i + 40]))
        if r.returncode != 0: print('STOP: unlock failed (AWS login?)'); sys.exit(7)
    dry = run([PDM, 'load', '--dry'])
    open(W + '/load-dry-after.txt', 'w', encoding='utf-8').write(dry.stdout + dry.stderr)
    print([l for l in dry.stdout.splitlines() if 'files to load' in l])
