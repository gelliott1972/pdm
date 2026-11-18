"""Before/after checks for the 08X04-BKP plate label and the 08X04-BKP-07 PLATE FRONT view (Admin, 18 Nov 2026).
Read-only on P:\\. "before" reads the vault files as they are. "after <dir>" first opens the changed copies in <dir>
(left open, so every user opened after them loads them by name), then runs the same checks; the two parts are
measured from <dir>. Run under SwCmd's lock (--as sw). Output: one text file per check in <out>.
Usage: python sw/profiles/bkp-1118.py before <out>
       python sw/profiles/bkp-1118.py after <out> <dir with 08X04-BKP.SLDPRT and 08X04-BKP-07.SLDPRT>
"""
import os, subprocess, sys

BS = chr(92)
SW = 'C:/code/pdm/tools/SwCmd/bin/SwCmd.exe'
REF = 'C:/code/pdm/tools/sw-macros/bin/SwRefCheck.exe'
SH = BS.join(['P:', 'APEX', 'SC08', 'LS-08X-04'])
BKP, BKP7 = SH + BS + '_Shared' + BS + '08X04-BKP.SLDPRT', SH + BS + '_Shared' + BS + '08X04-BKP-07.SLDPRT'
DRAWINGS = [SH + BS + '_Shared' + BS + '08X04-BKP-0%d.SLDDRW' % i for i in (2, 3, 4, 5)]
ASMS = [SH + BS + '_Shared' + BS + '08X04-BKA.SLDASM', SH + BS + 'ShaftA' + BS + '08A04-KBA-06.SLDASM',
        SH + BS + 'ShaftB' + BS + '08B04-KBA-02.SLDASM', SH + BS + 'ShaftC' + BS + '08C04-KBA-02.SLDASM']
KBP = {  # derived from 08X04-BKP (part link); As Machined configurations
    'ShaftA' + BS + '08A04-KBP-02': ['ORIGINAL<As Machined>', 'ASSY_MODEL<As Machined>'],
    'ShaftA' + BS + '08A04-KBP-03': ['PART<As Machined>', 'ASSY<As Machined>'],
    'ShaftA' + BS + '08A04-KBP-04': ['PART<As Machined>', 'ASSY<As Machined>'],
    'ShaftA' + BS + '08A04-KBP-05': ['PART<As Machined>', 'ASSY<As Machined>'],
    'ShaftA' + BS + '08A04-KBP-06': ['PART<As Machined>', 'ASSY<As Machined>'],
    'ShaftB' + BS + '08B04-KBP-01': ['ASSY<As Machined>', 'PART<As Machined>'],
    'ShaftB' + BS + '08B04-KBP-02': ['ASSY<As Machined>', 'P:ART<As Machined>'],
    'ShaftB' + BS + '08B04-KBP-03': ['ASSY<As Machined>', 'PART<As Machined>'],
    'ShaftB' + BS + '08B04-KBP-04': ['PART<As Machined>', 'ASSY<As Machined>'],
    'ShaftB' + BS + '08B04-KBP-05': ['Default<As Machined>'],
    'ShaftB' + BS + '08B04-KBP-06': ['Default<As Machined>'],
    'ShaftC' + BS + '08C04-KBP-01': ['PART<As Machined>', 'ASSY<As Machined>'],
    'ShaftC' + BS + '08C04-KBP-05': ['ASSY<As Machined>', 'PART<As Machined>'],
}
BKP_CONFIGS = ['68MM_SPACE_80X40RHS_150X140PLATE_SLOTS_SHOT_AXIS<As Machined>', '68MM_SPACE_80X40RHS_140X120PLATE<As Welded>',
               '68MM_SPACE_80X40RHS_180X140PLATE_SLOTS_SHORT_AXIS<As Machined>', 'Default<As Welded>',
               '68MM_SPACE_80X40RHS_150x140PLATE_SLOTS_LONG_AXIS<As Machined>', '68MM_SPACE_80X40RHS_140X120PLATE_SLOTS_LONG_AXIS<As Welded>',
               '68MM_SPACE_80X40RHS_180X140PLATE_SLOTS_LONG_AXIS<As Machined>', '68MM_SPACE_80X40RHS_180X120PLATE_SLOTS_LONG_AXIS<As Welded>']
BKP7_CONFIGS = ['90DEG<As Machined>', 'Default<As Welded>']


def run(args, out):
    st = subprocess.run([SW, 'status'], capture_output=True, text=True).stdout
    if '\nLock: sw ' not in st:
        sys.exit('lock not held by sw; stopped')
    r = subprocess.run(args, capture_output=True, text=True, errors='replace')
    with open(out, 'a', encoding='utf-8', newline='') as f:
        f.write('### ' + ' '.join(args[1:]) + '\n' + r.stdout + r.stderr + '\n')
    return r.returncode


def main():
    mode, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    bkp, bkp7 = BKP, BKP7
    if mode == 'after':
        d = sys.argv[3].replace('/', BS)
        bkp, bkp7 = d + BS + '08X04-BKP.SLDPRT', d + BS + '08X04-BKP-07.SLDPRT'
    run([SW, 'close-all', '--as', 'sw'], out + '/log.txt')
    if mode == 'after':
        for p in (bkp, bkp7):
            run([SW, 'open', p, '--as', 'sw'], out + '/log.txt')
    for c in BKP_CONFIGS:
        run([SW, 'mass', bkp, '--config', c, '--as', 'sw'], out + '/bkp-mass.txt')
        run([SW, 'cut-list', bkp, '--config', c, '--as', 'sw'], out + '/bkp-cutlist.txt')
    for c in BKP7_CONFIGS:
        run([SW, 'mass', bkp7, '--config', c, '--as', 'sw'], out + '/bkp7-mass.txt')
        run([SW, 'bodies', bkp7, '--config', c, '--as', 'sw'], out + '/bkp7-bodies.txt')
    for k, cs in KBP.items():
        for c in cs:
            run([SW, 'cut-list', SH + BS + k + '.SLDPRT', '--config', c, '--as', 'sw'], out + '/kbp-cutlist.txt')
            run([SW, 'mass', SH + BS + k + '.SLDPRT', '--config', c, '--as', 'sw'], out + '/kbp-mass.txt')
    for dw in DRAWINGS:
        run([REF, '--detail', dw], out + '/drawings.txt')
        run([SW, 'export-pdf', dw, out.replace('/', BS) + BS + os.path.basename(dw).replace('.SLDDRW', '.pdf'), '--as', 'sw'], out + '/log.txt')
    for a in ASMS:
        run([REF, a], out + '/assemblies.txt')
    run([SW, 'close-all', '--as', 'sw'], out + '/log.txt')
    print('done', out)


main()
