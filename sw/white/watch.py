"""Run one SOLIDWORKS tool with a memory/time watchdog (21 Nov 2026). Stops the tool, Closes All and, if SOLIDWORKS no longer
answers, ends it, when SOLIDWORKS passes <max GB> or free memory drops below 8 GB or <minutes> pass. Logs memory each 15 s.
Usage: python sw/white/watch.py <max GB> <minutes> <log> -- <tool> [args...]
"""
import subprocess, sys, time

mx, mins, logf = float(sys.argv[1]), float(sys.argv[2]), sys.argv[3]
cmd = sys.argv[sys.argv.index('--') + 1:]
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
PS = ('$o=Get-CimInstance Win32_OperatingSystem; $s=(Get-Process SLDWORKS -ErrorAction SilentlyContinue | Measure-Object PagedMemorySize64 -Sum).Sum;'
      ' "{0} {1}" -f [int]($o.FreeVirtualMemory/1024), [int]($s/1MB)')
log = open(logf, 'a', encoding='utf-8')
p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors='replace')
t0, why = time.time(), ''
while p.poll() is None:
    time.sleep(15)
    m = subprocess.run(['powershell', '-NoProfile', '-Command', PS], capture_output=True, text=True).stdout.split()
    free, swmb = int(m[0]), int(m[1])
    log.write('%s\tfree %d MB\tSOLIDWORKS %d MB\t%.0f s\n' % (time.strftime('%H:%M:%S'), free, swmb, time.time() - t0)); log.flush()
    if swmb > mx * 1024 or free < 8000: why = 'memory: SOLIDWORKS %d MB, free %d MB' % (swmb, free)
    elif time.time() - t0 > mins * 60: why = 'over %d minutes' % mins
    if why:
        p.kill()
        try: subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=120)
        except subprocess.TimeoutExpired:
            subprocess.run(['powershell', '-NoProfile', '-Command', 'Get-Process SLDWORKS -ErrorAction SilentlyContinue | Stop-Process -Force'], capture_output=True)
        break
print(p.stdout.read() if not why else '')
print('WATCHDOG STOPPED IT: ' + why if why else 'finished, exit %d, %.0f s' % (p.returncode, time.time() - t0))
