"""Check in the real run's changed files (20 Nov 2026) with pdm checkin (src build), and release the unchanged one.
Usage: python sw/profiles/the27-checkin.py <work folder> <unchanged part path>
"""
import subprocess, sys

PDM = 'C:/code/pdm/src/Pdm/bin/Debug/net8.0-windows/pdm.exe'
W, unchanged = sys.argv[1], sys.argv[2]
parts = [l.strip() for l in open(W + '/parts.txt', encoding='utf-8') if l.strip() and l.strip().upper() != unchanged.upper()]
sfp = [p for p in parts if p.upper().endswith('08X04-SFP-03.SLDPRT')]
parts = [p for p in parts if p not in sfp]
drawings = [l.strip() for l in open(W + '/redim-drawings.txt', encoding='utf-8') if l.strip()]
NOTE_P = 'Profiles moved to the vault library (P:\\_Shared\\_Profiles), cut-list labels; same shape (Grant 20 Nov)'
NOTE_S = ('Profile moved to the vault library (older RHS 80x40x8 copy), cut-list labels; one body 0.016 mm3 smaller, '
          'accepted by Grant for this part only (20 Nov)')
NOTE_D = 'Profile dimensions put back after the move to the vault profiles; same values, 0 dangling (Grant 20 Nov)'
log = open(W + '/checkin.txt', 'a', encoding='utf-8')
for files, note in ((parts, NOTE_P), (sfp, NOTE_S), (drawings, NOTE_D)):
    r = subprocess.run([PDM, 'checkin'] + files + ['--note', note], capture_output=True, text=True, errors='replace')
    log.write(r.stdout + r.stderr); log.flush()
    print(len(files), 'files:', r.stdout.count('Checked in'), 'checked in, exit', r.returncode, (r.stderr or '')[-300:])
r = subprocess.run([PDM, 'unlock', unchanged], capture_output=True, text=True, errors='replace')
log.write(r.stdout + r.stderr)
print('unlock', unchanged, '->', r.stdout.strip().splitlines()[-1:] , r.returncode)
