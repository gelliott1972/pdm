"""Item 4 (Admin overnight 17-18 Nov): on scratch copies, try putting back the 3 dimensions each of the 59 drawings loses
when its part is repointed, and compare with the original drawing. Nothing is written to P:\\.
  1. original: SwRefCheck --detail on the vault drawings (original parts, read-only)
  2. per (part, drawing) with 3 dangling: copy the drawing to scratch, SwRedim --hold <repointed copy> --save,
     then SwRefCheck --detail --hold <repointed copy> on the drawing copy
Usage: python sw/profiles/the27-redim.py <map.tsv> <detail.txt> <work folder>
Writes <work>/orig.txt, <work>/redim.log, <work>/after.txt.
"""
import ntpath, os, shutil, subprocess, sys, time
X = 'C:/code/pdm/tools/sw-macros/bin/SwRefCheck.exe'
R = 'C:/code/pdm/tools/sw-macros/bin/SwRedim.exe'
SWCMD = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'


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


mp, detail, work = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(work, exist_ok=True)
rows = [l.rstrip('\r\n').split('\t') for l in open(mp, encoding='utf-8') if l.strip()]
# which (part, drawing) pairs dangle 3: detail.txt has PART lines followed by that part's DRAWING lines
cur, bad = None, set()
for l in open(detail, encoding='utf-8'):
    c = l.rstrip('\r\n').split('\t')
    if c[0] == 'PART': cur = c[1]
    elif c[0] == 'DRAWING' and c[3] != 'dangling 0': bad.add((cur, c[1].upper()))
pairs = [(p, cp, d) for p, cp, d in rows if (p, d.upper()) in bad]
drawings = sorted({d for _, _, d in pairs})
print(len(pairs), 'pairs,', len(drawings), 'drawings', flush=True)

with open(work + '/orig.txt', 'a', encoding='utf-8') as out:
    for i in range(0, len(drawings) if out.tell() == 0 else 0, 10):   # an orig.txt already there is reused
        guard()
        out.write(subprocess.run([X, '--detail'] + drawings[i:i + 10], capture_output=True, text=True, errors='replace', timeout=1800).stdout); out.flush()
print(time.strftime('%H:%M:%S'), 'originals read', flush=True)

log = open(work + '/redim.log', 'w', encoding='utf-8')
after = open(work + '/after.txt', 'w', encoding='utf-8')
for n, (part, copy, d) in enumerate(pairs, 1):
    folder = os.path.join(work, 'drw', '%02d' % n)
    os.makedirs(folder, exist_ok=True)
    dcopy = os.path.join(folder, ntpath.basename(d))
    shutil.copyfile(d, dcopy)
    os.chmod(dcopy, 0o666)
    guard()
    r = subprocess.run([R, dcopy.replace('/', '\\'), '--hold', copy, '--save'], capture_output=True, text=True, errors='replace', timeout=1800)
    log.write('PAIR\t%d\t%s\t%s\t%s\texit %d\n' % (n, part, d, dcopy, r.returncode) + r.stdout + r.stderr); log.flush()
    guard()
    a = subprocess.run([X, '--detail', '--hold', copy, dcopy.replace('/', '\\')], capture_output=True, text=True, errors='replace', timeout=1800)
    after.write('PAIR\t%d\t%s\t%s\n' % (n, part, d) + a.stdout); after.flush()
    print(time.strftime('%H:%M:%S'), n, 'of', len(pairs), ntpath.basename(d), 'exit', r.returncode, flush=True)
subprocess.run([SWCMD, 'close-all', '--as', 'sw'], capture_output=True, text=True, timeout=300)
