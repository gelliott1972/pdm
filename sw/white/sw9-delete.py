"""Grant's SW9 answers (21 Nov 2026): delete the listed stub centre lines in 8 drawings. Check out, back up, SwAnnFix
--delete (checks count, values, positions, dangling in the same open; only the named items may go), recheck, check in,
dry load. A failure is put back from backup and released.
Usage: python sw/white/sw9-delete.py <work folder>
"""
import os, shutil, subprocess, sys

BS = chr(92)
W = sys.argv[1]
PDM = 'C:/code/pdm/src/Pdm/bin/Debug/net8.0-windows/pdm.exe'
FIX = 'C:/code/pdm/tools/sw-macros/bin/SwAnnFix.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
NOTE = 'Deleted stub centre lines that were hidden in white (Grant 21 Nov, Desk SW9); nothing else changed'
G = BS.join(['P:', 'APEX', 'GNRIC']) + BS
JOBS = [(G + n + '.SLDDRW', ids) for n, ids in (
    ('GNRIC-BRP-10', 'DetailItem2242,DetailItem2243'), ('GNRIC-BRP-12', 'DetailItem2242,DetailItem2243'),
    ('GNRIC-BRP-13', 'DetailItem2242,DetailItem2243'), ('GNRIC-BRP-14', 'DetailItem2242,DetailItem2243'),
    ('GNRIC-BRP-15', 'DetailItem2242,DetailItem2243'), ('GNRIC-BRW-16', 'DetailItem2242,DetailItem2243'),
    ('GNRIC-LWP-23', 'DetailItem1684,DetailItem1685'), ('GNRIC-LWP-24', 'DetailItem1684,DetailItem1685'))]
os.makedirs(W + '/sw9-backup', exist_ok=True)
log = open(W + '/sw9-delete.txt', 'a', encoding='utf-8')


def run(a, t=1800):
    return subprocess.run(a, capture_output=True, text=True, errors='replace', timeout=t)


if '\nLock: sw ' not in run([SWCMD, 'status']).stdout: sys.exit('STOP: lock not held by sw')
co = run([PDM, 'checkout'] + [f for f, _ in JOBS])
log.write(co.stdout + co.stderr)
print('checked out', co.stdout.count('Checked out'), 'of', len(JOBS))
if co.stdout.count('Checked out') != len(JOBS): sys.exit('STOP: not all checked out')
done = []
for f, ids in JOBS:
    bak = W + '/sw9-backup/' + f.split(BS)[-1]
    shutil.copy2(f, bak)
    run([SWCMD, 'close-all', '--as', 'sw'], 300)
    r = run([FIX, f, '--delete', ids, '--save', '--checked-out'])
    run([SWCMD, 'close-all', '--as', 'sw'], 300)
    chk = run([FIX, f])   # read-only recheck: nothing left to change
    log.write(r.stdout + chk.stdout)
    ok = r.stdout.count('DELETE\t') == 2 and 'CHANGED 2 SAVED' in r.stdout and '\tNOCHANGE' in chk.stdout
    if ok: done.append(f)
    else:
        shutil.copy2(bak, f)
        run([PDM, 'unlock', f])
    print(f.split(BS)[-1], 'OK' if ok else 'FAILED, put back and released', flush=True)
run([SWCMD, 'close-all', '--as', 'sw'], 300)
dry = run([PDM, 'load', '--dry'])
listed = sorted(l.split(None, 1)[1].strip() for l in dry.stdout.splitlines() if l.startswith('  changed') or l.startswith('  new'))
print('dry load lists', len(listed), 'changed;', len(done), 'expected')
if len(listed) != len(done): sys.exit('STOP: dry load does not match; nothing checked in')
ci = run([PDM, 'checkin'] + done + ['--note', NOTE])
log.write(ci.stdout + ci.stderr)
print('checked in', ci.stdout.count('Checked in'), 'of', len(done))
dry = run([PDM, 'load', '--dry'])
print([l for l in dry.stdout.splitlines() if 'files to load' in l])
