"""Step through drawings.txt one SwAnnFix open at a time (Grant, 21 Nov 2026), from the first drawing not yet in fix.txt,
until the first drawing that gets CHANGED (saved), or <max> drawings. A FAIL is put back from backup and the run goes on.
Usage: python sw/white/step.py <work folder> [max] [all]
"""
import os, shutil, subprocess, sys, time

BS = chr(92)
W = sys.argv[1]
run_all = 'all' in sys.argv[2:]   # 'all': don't stop at a change
mx = next((int(x) for x in sys.argv[2:] if x.isdigit()), 400)
FIX = 'C:/code/pdm/tools/sw-macros/bin/SwAnnFix.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
fs = [l.strip() for l in open(W + '/drawings.txt', encoding='utf-8') if l.strip()]
done = {l.split('\t')[1] for l in open(W + '/fix.txt', encoding='utf-8', errors='replace') if l.startswith('RESULT\t')} if os.path.exists(W + '/fix.txt') else set()
out = open(W + '/fix.txt', 'a', encoding='utf-8')
log = open(W + '/memory.log', 'a', encoding='utf-8')
PS = ('$o=Get-CimInstance Win32_OperatingSystem; $s=(Get-Process SLDWORKS -ErrorAction SilentlyContinue | Measure-Object PagedMemorySize64 -Sum).Sum;'
      ' "{0} {1}" -f [int]($o.FreeVirtualMemory/1024), [int]($s/1MB)')


def mem():
    m = subprocess.run(['powershell', '-NoProfile', '-Command', PS], capture_output=True, text=True).stdout.split()
    return int(m[0]), int(m[1])


def restart_sw():
    # Close All and Exit; if SOLIDWORKS does not answer within 2 minutes (21 Nov: stuck at 78 GB), end it by force.
    try:
        subprocess.run([SWCMD, 'close-all', '--exit', '--as', 'sw'], capture_output=True, text=True, timeout=120)
    except subprocess.TimeoutExpired:
        pass
    time.sleep(5)
    subprocess.run(['powershell', '-NoProfile', '-Command', 'Get-Process SLDWORKS -ErrorAction SilentlyContinue | Stop-Process -Force'], capture_output=True)
    subprocess.run(['taskkill', '/F', '/IM', 'SwAnnFix.exe'], capture_output=True)
    time.sleep(5)
    subprocess.Popen([r'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe'])
    time.sleep(60)
    log.write('%s\trestarted SOLIDWORKS\n' % time.strftime('%H:%M:%S')); log.flush()


n = 0
for f in fs:
    if f in done: continue
    if n >= mx: break
    n += 1
    st = subprocess.run([SWCMD, 'status'], capture_output=True, text=True).stdout
    if '\nLock: sw ' not in st: print('STOP: lock not held by sw'); sys.exit(3)
    # Memory guard before EVERY drawing (21 Nov: checking every 10 missed SOLIDWORKS running away inside one drawing).
    free, swmb = mem()
    log.write('%s\t%d\tbefore\tfree %d MB\tSOLIDWORKS %d MB\t%s\n' % (time.strftime('%H:%M:%S'), fs.index(f) + 1, free, swmb, f)); log.flush()
    if free < 12000 or swmb > 8000:
        print(time.strftime('%H:%M:%S'), 'memory: free %d MB, SOLIDWORKS %d MB: restarting SOLIDWORKS' % (free, swmb), flush=True)
        restart_sw()
    subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)
    # No PDFs (Grant 21 Nov: "the PDF idea was creating more problems than it was worth").
    # Watch SOLIDWORKS while this one drawing is done: over 12 GB or 5 minutes, stop it, put the drawing back, restart.
    p = subprocess.Popen([FIX, f, '--save', '--checked-out'], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors='replace')
    t0, why = time.time(), ''
    while p.poll() is None:
        time.sleep(5)
        free, swmb = mem()
        if swmb > 12000 or free < 8000: why = 'SOLIDWORKS %d MB, free %d MB' % (swmb, free)
        elif time.time() - t0 > 300: why = 'over 5 minutes'
        if why:
            p.kill()
            log.write('%s\t%d\tSTOPPED\t%s\t%s\n' % (time.strftime('%H:%M:%S'), fs.index(f) + 1, why, f)); log.flush()
            restart_sw()
            break
    text = p.stdout.read() if not why else ''
    if why: text = 'RESULT\t' + f + '\tFAIL watchdog: ' + why + '\n'
    free, swmb = mem()
    log.write('%s\t%d\tafter\tfree %d MB\tSOLIDWORKS %d MB\t%.0f s\n' % (time.strftime('%H:%M:%S'), fs.index(f) + 1, free, swmb, time.time() - t0)); log.flush()

    class R: pass
    r = R(); r.stdout = text
    out.write(text); out.flush()
    res = [l.split('\t') for l in r.stdout.splitlines() if l.startswith('RESULT\t')]
    kind = res[0][2] if res else 'NO RESULT'
    if kind.startswith('FAIL') or kind == 'NO RESULT':
        shutil.copy2(W + '/backup/' + f[3:].replace(BS, '/'), f)
        out.write('RESTORED\t' + f + '\n'); out.flush()
    print(time.strftime('%H:%M:%S'), fs.index(f) + 1, 'of', len(fs), f.split(BS)[-1], '|', kind, flush=True)
    for l in r.stdout.splitlines():
        if l.startswith('FLAG'): print('   ', l, flush=True)
    if kind.startswith('CHANGED') and not run_all:
        break
