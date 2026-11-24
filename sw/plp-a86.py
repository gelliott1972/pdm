"""Checks for APEX-A86: GNRIC-PLP FP-150X200 anchor holes 15 -> 14 mm (Admin brief, 24 Nov 2026).
Read-only. Reads the part and drawing given (vault files or rehearsal copies). For copies, the part copy is opened
before the drawing checks so the drawing loads it by name. Run under SwCmd's lock (--as sw).
Usage: python sw/plp-a86.py <out dir> <part> <drawing>
Output in <out dir>: dims.txt (Thru Hole Dia. of M12 Clearance Hole2 per configuration), mass.txt, holes.txt,
drawing.txt (SwRefCheck --detail, SwDimKind), GNRIC-PLP-01.pdf
"""
import os, subprocess, sys

BS = chr(92)
SW = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
REF = 'C:/code/pdm/tools/sw-macros/bin/SwRefCheck.exe'
KIND = 'C:/code/pdm/tools/sw-macros/bin/SwDimKind.exe'
GROUT = 'C:/code/pdm/tools/sw-macros/bin/SwGrout.exe'
CONFIGS = ['FP-400x300', 'FP-180x180', 'FP-200x200', 'FP-574x200', 'FP-150X200', 'FP-220x220', 'FP-350x200',
           'FP-150X200-CHAMFERED_CORNERS', 'FP-400x220']


def run(args):
    st = subprocess.run([SW, 'status'], capture_output=True, text=True).stdout
    if '\nLock: sw ' not in st:
        sys.exit('lock not held by sw; stopped')
    r = subprocess.run(args, capture_output=True, text=True, errors='replace')
    return r.stdout + r.stderr


def main():
    out, part, drw = sys.argv[1], sys.argv[2], sys.argv[3]
    os.makedirs(out, exist_ok=True)
    copy = not part.upper().startswith('P:')
    run([SW, 'close-all', '--as', 'sw'])
    with open(out + '/dims.txt', 'w', encoding='utf-8') as f, open(out + '/mass.txt', 'w', encoding='utf-8') as g:
        for c in CONFIGS:
            d = run([SW, 'dims', part, '--config', c, '--as', 'sw'])
            hole = [l for l in d.splitlines() if l.startswith('DIM') and 'Clearance Hole' in l and 'Thru Hole Dia.' in l]
            f.write(c + '\n' + '\n'.join('  ' + l for l in hole) + '\n')
            g.write('### ' + c + '\n' + run([SW, 'mass', part, '--config', c, '--as', 'sw']) + '\n')
    with open(out + '/holes.txt', 'w', encoding='utf-8') as f:
        for c in ('FP-150X200', 'FP-150X200-CHAMFERED_CORNERS', 'FP-200x200'):
            for dia in ('14', '15'):
                f.write('### ' + c + ' ' + dia + '\n' + run([GROUT, 'holes', part, c, dia]) + '\n')
    if copy:  # opened only now: holes read stale faces when the part was already open in another configuration
        run([SW, 'open', part, '--as', 'sw'])
    with open(out + '/drawing.txt', 'w', encoding='utf-8') as f:
        f.write(run([REF, '--detail', drw]) + '\n' + run([KIND, drw]) + '\n')
    run([SW, 'export-pdf', drw, out.replace('/', BS) + BS + 'GNRIC-PLP-01.pdf', '--as', 'sw'])
    run([SW, 'close-all', '--as', 'sw'])
    print('done', out)


main()
