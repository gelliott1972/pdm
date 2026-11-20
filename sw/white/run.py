"""White annotations (Grant via Admin, 20 Nov 2026): run SwAnnColor (read-only) over every vault drawing, 10 at a time,
guarded (sw lock, memory). Usage: python sw/white/run.py <out.txt>
"""
import os, subprocess, sys, time

BS = chr(92)
X = 'C:/code/pdm/tools/sw-macros/bin/SwAnnColor.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
files = []
for root, dirs, fs in os.walk('P:' + BS + 'APEX'):
    if '_Review' in root or '_Released' in root: continue
    files += [os.path.join(root, f) for f in fs if f.upper().endswith('.SLDDRW')]
files.sort()
out = open(sys.argv[1], 'w', encoding='utf-8')


def guard():
    st = subprocess.run([SWCMD, 'status'], capture_output=True, text=True, errors='replace').stdout
    if '\nLock: sw ' not in st: print('STOP: lock not held by sw'); sys.exit(3)
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


for i in range(0, len(files), 10):
    guard()
    r = subprocess.run([X] + files[i:i + 10], capture_output=True, text=True, errors='replace', timeout=1800)
    out.write(r.stdout + r.stderr); out.flush()
    print(time.strftime('%H:%M:%S'), min(i + 10, len(files)), 'of', len(files), flush=True)
