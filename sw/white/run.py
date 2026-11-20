"""White balloons/dimensions sweep (Grant via Admin, 20 Nov 2026).
detect: SwAnnColor (read-only) over the drawings in <list>, 10 at a time, guarded (sw lock, memory).
fix:    for each drawing in <list>: check out, back up, SwRefCheck --detail before, SwAnnColor --fix --save --checked-out,
        SwRefCheck --detail after, compare (values, count, dangling, errors) and SwAnnColor again (0 white, no overrides).
        Pass: pdm checkin with a note. Fail: back from the backup and released (pdm unlock). One drawing at a time.
Usage: python sw/white/run.py detect <list.txt> <out.txt>
       python sw/white/run.py fix <list.txt> <work folder>
"""
import os, shutil, subprocess, sys, time

BS = chr(92)
X = 'C:/code/pdm/tools/sw-macros/bin/SwAnnColor.exe'
REF = 'C:/code/pdm/tools/sw-macros/bin/SwRefCheck.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
PDM = 'C:/code/pdm/src/Pdm/bin/Debug/net8.0-windows/pdm.exe'
CMP = 'C:/code/pdm/sw/profiles/the27-real-compare.py'
NOTE = 'Balloon and dimension colours back to default (were set to white); nothing else changed (Grant 20 Nov)'


def guard():
    st = subprocess.run([SWCMD, 'status'], capture_output=True, text=True, errors='replace').stdout
    if '\nLock: sw ' not in st: print('STOP: lock not held by sw', flush=True); sys.exit(3)
    ps = ('$o=Get-CimInstance Win32_OperatingSystem; $s=(Get-Process SLDWORKS -ErrorAction SilentlyContinue | Measure-Object PagedMemorySize64 -Sum).Sum;'
          ' "{0} {1}" -f [int]($o.FreeVirtualMemory/1024), [int]($s/1MB)')
    m = subprocess.run(['powershell', '-NoProfile', '-Command', ps], capture_output=True, text=True).stdout.split()
    if int(m[0]) < 12000 or int(m[1]) > 8000:
        print(time.strftime('%H:%M:%S'), 'memory', m, 'restarting SOLIDWORKS', flush=True)
        subprocess.run([SWCMD, 'close-all', '--exit', '--as', 'sw'], capture_output=True, text=True, timeout=300)
        time.sleep(10)
        subprocess.Popen([r'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe'])
        time.sleep(60)
    subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)


def run(args, timeout=1800):
    return subprocess.run(args, capture_output=True, text=True, errors='replace', timeout=timeout)


mode, lst = sys.argv[1], sys.argv[2]
files = [l.strip() for l in open(lst, encoding='utf-8') if l.strip()]
if mode == 'detect':
    out = open(sys.argv[3], 'a', encoding='utf-8')
    for i in range(0, len(files), 10):
        guard()
        r = run([X] + files[i:i + 10])
        out.write(r.stdout + r.stderr); out.flush()
        print(time.strftime('%H:%M:%S'), min(i + 10, len(files)), 'of', len(files), flush=True)
    sys.exit(0)

work = sys.argv[3]
os.makedirs(work + '/backup', exist_ok=True)
res = open(work + '/result.tsv', 'a', encoding='utf-8')
for n, d in enumerate(files, 1):
    name = d.split(BS)[-1]
    k = '%02d-%s' % (n, name)
    co = run([PDM, 'checkout', d])
    if 'Checked out' not in co.stdout:
        res.write('NOT CHECKED OUT\t%s\t%s\n' % (d, co.stdout.strip()[-200:])); res.flush(); continue
    bak = work + '/backup/' + k
    shutil.copy2(d, bak)
    guard()
    open(work + '/' + k + '.before.txt', 'w', encoding='utf-8').write(run([REF, '--detail', d]).stdout)
    guard()
    fx = run([X, d, '--fix', '--save', '--checked-out'])
    open(work + '/' + k + '.fix.txt', 'w', encoding='utf-8').write(fx.stdout + fx.stderr)
    guard()
    open(work + '/' + k + '.after.txt', 'w', encoding='utf-8').write(run([REF, '--detail', d]).stdout)
    cmp = run(['python', CMP, work + '/' + k + '.before.txt', work + '/' + k + '.after.txt', work + '/' + k + '.compare.tsv'])
    guard()
    chk = run([X, d]).stdout
    open(work + '/' + k + '.check.txt', 'w', encoding='utf-8').write(chk)
    left = [l for l in chk.splitlines() if l.startswith('ANN') and ('swNote(balloon)' in l or 'swDisplayDimension' in l) and '255,255,255\tcolour override' in l]
    why = []
    if fx.returncode != 0 or 'SAVED\t' not in fx.stdout: why.append('fix: ' + ' | '.join(l for l in fx.stdout.splitlines() if l.startswith(('POSITIONS', 'NOT SAVED', 'SAVE FAILED', 'REFUSED', 'SUMMARY')) or 'STILL ON' in l)[:300])
    if '0 fail' not in cmp.stdout: why.append('compare: ' + cmp.stdout.strip())
    if left: why.append('%d white overrides left' % len(left))
    if why:
        guard()
        shutil.copy2(bak, d)
        un = run([PDM, 'unlock', d])
        res.write('FAIL\t%s\t%s\trestored and released: %s\n' % (d, ' ; '.join(why), un.stdout.strip()[-120:]))
    else:
        ci = run([PDM, 'checkin', d, '--note', NOTE])
        fixed = [l for l in fx.stdout.splitlines() if l.startswith('SUMMARY')]
        res.write('PASS\t%s\t%s\t%s\n' % (d, fixed[0].split('\t', 2)[-1] if fixed else '', ci.stdout.strip()[-120:]))
    res.flush()
    print(time.strftime('%H:%M:%S'), n, 'of', len(files), name, 'FAIL' if why else 'PASS', flush=True)
subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)
