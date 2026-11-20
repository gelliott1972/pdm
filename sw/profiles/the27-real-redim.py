"""Real run (Grant's go, 20 Nov 2026): SwRedim on each checked-out vault drawing that has dangling dimensions after the
parts were repointed. Decimals as Grant decided (print as before). Guarded: the sw lock, memory, Close All between drawings.
Usage: python sw/profiles/the27-real-redim.py <list.tsv: drawing path, held part path> <log>
"""
import ntpath, subprocess, sys, time

R = 'C:/code/pdm/tools/sw-macros/bin/SwRedim.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
DECIMALS = {  # Grant 20 Nov: 6.3 stays 6.3; 40, 8 and 80 stay without decimals
    '08XXX-HBA-01.SLDDRW': ['Thickness=1', 'H_leg=1'],   # the original printed 6.3 and 120.0 (H_leg); V_leg printed 120
    '08X04-DBP-01.SLDDRW': ['H_leg=0', 'V_leg=0', 'Thickness=0'],
    '08X06-DBP-01.SLDDRW': ['H_leg=0', 'V_leg=0', 'Thickness=0'],
    '08X06-DBP-02.SLDDRW': ['H_leg=0', 'V_leg=0', 'Thickness=0'],
}


def guard():
    st = subprocess.run([SWCMD, 'status'], capture_output=True, text=True, errors='replace').stdout
    if '\nLock: sw ' not in st:
        print('STOP: the SOLIDWORKS lock is not held by sw', flush=True); sys.exit(3)
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


rows = [l.rstrip('\r\n').split('\t') for l in open(sys.argv[1], encoding='utf-8') if l.strip()]
log = open(sys.argv[2], 'a', encoding='utf-8')
bad = 0
for n, (d, part) in enumerate(rows, 1):
    guard()
    dec = []
    for x in DECIMALS.get(ntpath.basename(d).upper(), []): dec += ['--decimals', x]
    r = subprocess.run([R, d, '--hold', part] + dec + ['--save', '--checked-out'], capture_output=True, text=True, errors='replace', timeout=1800)
    log.write('DRAWING\t%d\t%s\t%s\texit %d\n' % (n, d, part, r.returncode) + r.stdout + r.stderr); log.flush()
    bad += r.returncode != 0
    print(time.strftime('%H:%M:%S'), n, 'of', len(rows), ntpath.basename(d), 'exit', r.returncode, flush=True)
subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)
print('done;', bad, 'not clean')
