"""Re-import test prep (apex-admin / Grant 22 Nov 2026), read-only, no SOLIDWORKS.
1. asms: write the vault paths of the assemblies SOLIDWORKS loaded for LS-08A-11 (USES lines of a SwStep run).
2. where: from DmRefPaths output, list every active instance of the named parts per parent assembly and configuration.
Usage: python sw/reimport/prep.py asms <index.json> <SwStep output> <out.txt>
       python sw/reimport/prep.py where <DmRefPaths output> <name,name,...>
"""
import json, ntpath, sys

BS = chr(92)
if sys.argv[1] == 'asms':
    items = [i for i in json.load(open(sys.argv[2], encoding='utf-8'))['Items'] if 'removedAt' not in i]
    byname = {ntpath.basename(i['path']['S']).upper(): i for i in items}
    used = [l.split('\t')[1].upper() for l in open(sys.argv[3], encoding='utf-8') if l.startswith('USES')] + ['LS-08A-11+STRUCTURE.SLDASM']
    with open(sys.argv[4], 'w') as f:
        for n in used:
            if n.endswith('.SLDASM'): f.write('P:' + BS + byname[n]['path']['S'].replace('/', BS) + '\n')
elif sys.argv[1] == 'paths':
    # python sw/reimport/prep.py paths <DmRefPaths output> <top file name> <top config> <name,name,...>
    # every active instance path of the named parts, walking from the top through each component's configuration
    comps = {}
    for l in open(sys.argv[2], encoding='utf-8'):
        r = l.rstrip('\n').split('\t')
        if r[0] == 'COMP' and r[6] == '':
            comps.setdefault((ntpath.basename(r[1]).upper(), r[2]), []).append((r[3], r[4], ntpath.basename(r[5]).upper()))
    want = [w.upper() for w in sys.argv[5].split(',')]
    missing = set()

    def walk(fname, cfg, path):
        key = (fname, cfg)
        if key not in comps:
            if fname.endswith('.SLDASM'): missing.add(key)
            return
        for name, rcfg, child in comps[key]:
            p = path + [name]
            if any(w in child for w in want): print('%s\t%s\t<%s>' % (child, ' / '.join(p), rcfg))
            if child.endswith('.SLDASM'): walk(child, rcfg, p)

    walk(sys.argv[3].upper(), sys.argv[4], [sys.argv[3]])
    if missing: print('assemblies/configurations not in the DmRefPaths output:', sorted(missing))
else:
    want = [w.upper() for w in sys.argv[3].split(',')]
    for l in open(sys.argv[2], encoding='utf-8'):
        r = l.rstrip('\n').split('\t')
        if r[0] == 'COMP' and r[6] == '' and any(w in ntpath.basename(r[5]).upper() for w in want):
            print('%s [%s]  %s  <%s>' % (ntpath.basename(r[1]), r[2], r[3], r[4]))
