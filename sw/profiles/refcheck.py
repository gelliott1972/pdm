"""Run SwRefCheck over a list of drawings/assemblies in batches, restarting SOLIDWORKS when memory runs low (17 Nov night).
Usage: python sw/profiles/refcheck.py <list.txt> <out.txt>
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

files = [l.strip() for l in open(sys.argv[1]) if l.strip()]
out = open(sys.argv[2], 'a', encoding='utf-8')
for i in range(0, len(files), 15):
    ok, m = memory_ok()
    if not ok:
        print(time.strftime('%H:%M:%S'), 'memory', m, 'restarting SOLIDWORKS', flush=True); restart_sw()
    batch = files[i:i + 15]
    try:
        r = subprocess.run([X] + batch, capture_output=True, text=True, errors='replace', timeout=1800)
        out.write(r.stdout); out.flush()
        if r.stderr.strip(): print(r.stderr.strip()[:500], flush=True)
    except subprocess.TimeoutExpired:
        subprocess.run(['taskkill', '/F', '/IM', 'SwRefCheck.exe'], capture_output=True)
        print(time.strftime('%H:%M:%S'), 'TIMEOUT on batch', batch, flush=True); sys.exit(3)
    print(time.strftime('%H:%M:%S'), 'done', min(i + 15, len(files)), 'of', len(files), flush=True)
