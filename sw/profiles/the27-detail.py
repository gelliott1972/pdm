"""For each of the 27 restored parts: open its repointed rehearsal copy (read-only, --hold), then check its failing drawings
with --detail, so the dangling dimensions are named (Admin overnight 17-18 Nov, item 4). Read-only on P:\\.
Usage: python sw/profiles/the27-detail.py <map.tsv> <out.txt>
"""
import subprocess, sys, time
sys.path.insert(0, 'C:/code/pdm/sw/profiles')
X = 'C:/code/pdm/tools/sw-macros/bin/SwRefCheck.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'


def memory_ok():
    ps = ('$o=Get-CimInstance Win32_OperatingSystem; $s=(Get-Process SLDWORKS -ErrorAction SilentlyContinue | Measure-Object PagedMemorySize64 -Sum).Sum;'
          ' "{0} {1}" -f [int]($o.FreeVirtualMemory/1024), [int]($s/1MB)')
    out = subprocess.run(['powershell', '-NoProfile', '-Command', ps], capture_output=True, text=True).stdout.split()
    return int(out[0]) >= 12000 and int(out[1]) <= 8000, out


def restart_sw():
    subprocess.run([SWCMD, 'close-all', '--exit', '--as', 'sw'], capture_output=True, text=True, timeout=300)
    time.sleep(10)
    subprocess.Popen([r'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe'])
    time.sleep(60)


rows = [l.rstrip('\r\n').split('\t') for l in open(sys.argv[1], encoding='utf-8') if l.strip()]
parts = {}
for part, copy, drw in rows:
    parts.setdefault((part, copy), []).append(drw)
out = open(sys.argv[2], 'a', encoding='utf-8')
for n, ((part, copy), drws) in enumerate(sorted(parts.items()), 1):
    st = subprocess.run([SWCMD, 'status'], capture_output=True, text=True, errors='replace').stdout
    if '\nLock: sw ' not in st:   # stop at once if SOLIDWORKS isn't ours (17 Nov: a run once overlapped Draw's lock)
        print('STOP: the SOLIDWORKS lock is not held by sw', flush=True); sys.exit(3)
    ok, m = memory_ok()
    if not ok:
        print(time.strftime('%H:%M:%S'), 'memory', m, 'restarting SOLIDWORKS', flush=True); restart_sw()
    subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)
    out.write('PART\t' + part + '\t' + copy + '\n')
    try:
        r = subprocess.run([X, '--detail', '--hold', copy] + drws, capture_output=True, text=True, errors='replace', timeout=1800)
        out.write(r.stdout); out.flush()
    except subprocess.TimeoutExpired:
        out.write('TIMEOUT\t' + part + '\n'); out.flush()
    print(time.strftime('%H:%M:%S'), n, 'of', len(parts), part, flush=True)
subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)
