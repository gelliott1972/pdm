"""Job files for SwFix, one per part, from the survey (brief sw-profiles steps 6, 9, 10, 12; Admin's rules 16 Nov night).
Usage: python sw/profiles/plan.py <parts.tsv> <root> <out folder>
Writes <out>/<part file name>.tsv and <out>/list.txt (vault path per line), and prints what was left out and why.
"""
import collections, os, re, sys

B = chr(92)
rows = [l.rstrip('\n').split('\t') for l in open(sys.argv[1], encoding='utf-8', errors='replace')]
root, out = sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)

# Size names that the library no longer has, mapped to the size drawing the same section (Admin: "repoint each member to
# the size matching its measured section, not its old name"); the measured section is checked against the new size below.
MAP = [
    ('DLAPEX' + B + 'ea.sldlfp', 'RSA100x100x8', 'DLAPEX' + B + 'ea.sldlfp', 'EA100x100x8'),
    ('DLAPEX' + B + 'ea.sldlfp', 'RSA50x50x5', 'DLAPEX' + B + 'ea.sldlfp', 'EA50x50x5'),
]
H = 'DLAPEX' + B + 'historical_files_accidental_rename' + B
# Two candidate copies of some sizes (16 Nov night: the main rhs.sldlfp's RHS80x40x8 has a 16 mm corner radius, the
# members were built with 10 mm, as in the historical file). The run tries the first job file, and the .alt one only when
# the first changes the shape; the shape check decides, nothing is assumed.
FIRST = {('DLAPEX' + B + 'rhs.sldlfp', 'RHS80x40x8'): (H + 'RHS 80x40x8.SLDLFP', 'RHS80x40x8')}
ALT = {('DLAPEX' + B + 'rhs.sldlfp', 'RHS80x40x8'): ('DLAPEX' + B + 'rhs.sldlfp', 'RHS80x40x8'),
       ('DLAPEX' + B + 'shs.sldlfp', '*'): (H + 'SHS 100x100x5.sldlfp', '*'),
       ('DLAPEX' + B + 'ea.sldlfp', 'RSA100x100x8'): (H + 'EA 100x100x8.sldlfp', 'RSA100x100x8'),
       ('DLAPEX' + B + 'ea.sldlfp', 'RSA50x50x5'): (H + 'EA 100x100x8.sldlfp', 'RSA50x50x5')}
STRAGGLER = {  # full path, size -> new file, size (measured RHS 80x40x8)
    ('C:' + B + '007jimdesign' + B + 'SolidWorks Templates 2013' + B + 'Weldment Profiles' + B + 'From Grant' + B + 'RHS' + B + 'RHS 80x40x8.sldlfp', ''): ('DLAPEX' + B + 'rhs.sldlfp', 'RHS80x40x8'),
}
num = r'(\d+(?:\.\d+)?)'
def size_of(name):
    m = re.match(r'^(RHS|SHS|EA)' + num + 'x' + num + 'x' + num + '$', name)
    return (m.group(1), tuple(sorted((float(m.group(2)), float(m.group(3))), reverse=True)), float(m.group(4))) if m else None
def measured(s):
    d = re.findall(num + r'\(wall ([\d.\-]+), planes (\d+)\)', s or '')
    if len(d) < 2: return None
    w = [float(x[1]) for x in d[:2] if x[1] != '-']
    return tuple(sorted((float(d[0][0]), float(d[1][0])), reverse=True)), (min(w) if w else None)

BRACKET_PLATES = {'08X04-BKP.SLDPRT', '08X04-BKP-06.SLDPRT', '08X04-BKP-07.SLDPRT', '08B04-BKP-03.SLDPRT'}   # step 10 (08B04-BKP-01: step 11)
MATERIAL_OK_CODES = None  # family = type code; material only where the family's parts all use one material

members = collections.defaultdict(list)
cuts = collections.defaultdict(list)
mats = collections.defaultdict(list)
allparts = []
for r in rows:
    if r[0] in ('MEMBER', 'NOMEMBERS', 'MAT', 'CUT') and r[1] not in allparts: allparts.append(r[1])
    if r[0] == 'MEMBER': members[r[1]].append(r)
    elif r[0] == 'CUT': cuts[r[1]].append(r)
    elif r[0] == 'MAT': mats[r[1]].append(r)

def code(f):
    m = re.match(r'^[0-9A-Z]+-([A-Z]+)', os.path.basename(f.replace(B, '/')), re.I)
    return m.group(1).upper() if m else None
fam = collections.defaultdict(collections.Counter)
for f, rs in mats.items():
    for r in rs:
        if r[3]: fam[code(f)][(r[3], r[4])] += 1

left = []   # (part, why)
jobs = {}
alts = {}
for f in allparts:
    name = os.path.basename(f.replace(B, '/'))
    j = ['ROOT\t' + root]
    # step 6: repoint and labels
    if members[f]:
        j.append('REPOINT')
        for key in MAP: j.append('MAP\t%s\t%s\t%s\t%s' % key)
        for r in members[f]:
            path, size, feat = r[3], r[4], r[2]
            if (path, size) in STRAGGLER:
                nf, ns = STRAGGLER[(path, size)]; j.append('MAP\t%s\t%s\t%s\t%s' % (path, size, nf, ns)); newsize = ns
            else:
                newsize = next((m[3] for m in MAP if path.lower().endswith(m[0].lower()) and m[1] == size), size)
            if 'DLAPEX_Members' not in path and (path, size) not in STRAGGLER: left.append((name, 'member %s: straggler %s with no mapping' % (feat, path))); continue
            # the new size must be the measured section
            ms = next((measured(c[8]) for c in cuts[f] if feat in c[7].split(',') and measured(c[8])), None)
            sz = size_of(newsize)
            if sz and ms and (sz[1] != ms[0] or (ms[1] is not None and abs(sz[2] - ms[1]) > 0.05)):
                left.append((name, 'member %s: size %s but measured %s: label not set' % (feat, newsize, ms))); continue
            if sz: j.append('MEMBERDESC\t%s\t%s' % (feat, newsize))
    # step 12
    if any(len(c) > 10 and c[10] for c in cuts[f]): j.append('CAPDESC')
    # step 10
    if name.upper() in BRACKET_PLATES:
        for c in cuts[f]:
            m = re.match(r'plate ([\d.]+) x ([\d.]+) x ([\d.]+) \(volume ([\d.]+) cm3\)', c[8] or '')
            if c[5].strip() or not m or (len(c) > 10 and c[10]) or c[7]: continue
            t, w, l, v = map(float, m.groups())
            if v < 0.85 * t * w * l / 1000: left.append((name, 'plate %s: volume %.0f cm3 is not a plate of %s' % (c[3], v, m.group(0)))); continue
            j.append('PLATEDESC\t%s x %s x %s\tPLATE, %s x %s x %s' % (m.group(1), m.group(2), m.group(3), m.group(1), m.group(2), m.group(3)))
    # step 9
    ms = mats[f]
    nomat = [r for r in ms if not r[3]]
    vol0 = all(r[9].startswith('0 ') for r in ms if len(r) > 9)
    if nomat and not vol0:
        sib = [k for k in fam[code(f)]] if code(f) else []
        if len(sib) == 1: j.append('MATERIAL\t%s\t%s' % sib[0])
        else: left.append((name, 'no SOLIDWORKS material; siblings use ' + (', '.join('%s (%s)' % k for k in sib) or 'none') + ': not set'))
    if any(not r[5] for r in ms) and not vol0:
        if all(r[3] for r in ms) or any(l.startswith('MATERIAL') for l in j): j.append('MATPROP')
    if vol0 and nomat: left.append((name, 'no bodies (skeleton): no material'))
    j = list(dict.fromkeys(j))
    if len(j) > 1: jobs[f] = j
    if members[f]:
        first, alt = [], []
        for r in members[f]:
            p = r[3]; i = p.lower().find('dlapex_members' + B)
            rel = p[i + len('dlapex_members' + B):] if i >= 0 else None
            if rel is None:
                if (p, r[4]) in STRAGGLER:   # straggler: the alternative is the historical 10 mm copy
                    alt.append('MAP\t%s\t%s\t%s\t%s' % (p, r[4], H + 'RHS 80x40x8.SLDLFP', 'RHS80x40x8'))
                continue
            k = (rel, r[4])
            if k in FIRST: first.append('MAP\t%s\t%s\t%s\t%s' % ((rel, r[4]) + FIRST[k]))
            a = ALT.get(k) or (ALT.get((rel, '*')) and (ALT[(rel, '*')][0], r[4]))
            if a: alt.append('MAP\t%s\t%s\t%s\t%s' % ((rel, r[4]) + tuple(a)))
        # a later MAP line for the same key wins (SwFix reads them in order)
        jobs[f] = jobs[f] + list(dict.fromkeys(first))
        if alt: alts[f] = jobs[f] + list(dict.fromkeys(alt))

with open(os.path.join(out, 'list.txt'), 'w') as fl:
    for f, j in jobs.items():
        fn = os.path.basename(f.replace(B, '/'))
        open(os.path.join(out, fn + '.tsv'), 'w', encoding='utf-8').write('\n'.join(j) + '\n')
        if f in alts: open(os.path.join(out, fn + '.alt.tsv'), 'w', encoding='utf-8').write('\n'.join(alts[f]) + '\n')
        fl.write(f + '\n')
print('parts with jobs:', len(jobs))
for k, v in collections.Counter(l.split('\t')[0] for j in jobs.values() for l in j).items(): print(' ', k, v)
print('left out:')
for n, why in left: print('  %s: %s' % (n, why))
