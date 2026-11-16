"""Analyse SwProfiles raw.tsv (brief sw-profiles step 0, plus steps 9 and 10) into markdown tables.
Usage: python sw/profiles/analyse.py scratch/profiles/survey/raw.tsv > scratch/profiles/survey/analysis.md
"""
import re, sys, collections

B = chr(92)
rows = [l.rstrip('\n').split('\t') for l in open(sys.argv[1], encoding='utf-8', errors='replace')]

def rel(p):  # vault-relative part path
    return p[3:] if p.upper().startswith('P:' + B) else p

def prof_key(path):  # library-relative profile path, lower case
    m = re.search(r'DLAPEX_Members' + re.escape(B) + r'(.*)$', path, re.I)
    if m: return m.group(1).lower()
    m = re.search(r'm-snapshot-1116' + re.escape(B) + r'(.*)$', path, re.I)
    return m.group(1).lower() if m else None

num = r'(\d+(?:\.\d+)?)'
def parse_label(s):
    m = re.match(r'^\s*(RHS|SHS|EA|RSA|PFC|UB|UC|FB|FL)\s*' + num + r'\s*[xX]\s*' + num + r'\s*[xX]\s*' + num, s or '', re.I)
    if not m: return None
    a, b, t = float(m.group(2)), float(m.group(3)), float(m.group(4))
    return m.group(1).upper(), tuple(sorted((a, b), reverse=True)), t

def parse_section(s):  # "axis len 1900; 80(wall 8, planes 4) x 40(wall 8, planes 4)"
    dirs = re.findall(num + r'\(wall ([\d.\-]+), planes (\d+)\)', s or '')
    if len(dirs) < 2: return None
    (a, wa, pa), (b, wb, pb) = dirs[0], dirs[1]
    walls = [float(w) for w in (wa, wb) if w != '-']
    t = min(walls) if walls else None
    pa, pb = int(pa), int(pb)
    if pa >= 4 and pb >= 4: kind = 'SHS' if abs(float(a) - float(b)) < 0.01 else 'RHS'
    elif pa == 3 and pb == 3: kind = 'EA' if abs(float(a) - float(b)) < 0.01 else 'UA'
    else: kind = '?'
    return kind, tuple(sorted((float(a), float(b)), reverse=True)), t

def fmt(v): return ('%g' % v)
def sect_text(p): return '?' if p is None else '%s %sx%sx%s' % (p[0], fmt(p[1][0]), fmt(p[1][1]), fmt(p[2]) if p[2] is not None else '?')

profiles = {}   # (file key, size) -> (description, sketch)
for r in rows:
    if r[0] == 'PROFILE':
        profiles[(prof_key(r[1]), r[2])] = (re.sub(r' \((cfg|file)\)$', '', r[3]), r[5] if len(r) > 5 else '')
prof_sizes = collections.defaultdict(set)
for (k, s) in profiles: prof_sizes[k].add(s)

def sketch_section(sk):
    d = dict(re.findall(r'(\w+)=([\d.]+)', sk))
    try:
        v, h = float(d['V_leg']), float(d['H_leg'])
        t = float(d.get('Thickness', d.get('thickness')))
        return tuple(sorted((v, h), reverse=True)), t
    except Exception: return None

members = {}     # (file, feature) -> (path, size)
for r in rows:
    if r[0] == 'MEMBER': members[(r[1], r[2])] = (r[3], r[4])
cuts = [r for r in rows if r[0] == 'CUT']
mats = [r for r in rows if r[0] == 'MAT']
errors = [r for r in rows if r[0] == 'ERROR']
parts_read = {r[1] for r in rows if r[0] in ('MEMBER', 'NOMEMBERS', 'MAT', 'CUT')}

out = []
w = out.append
w('Parts read: %d; with members: %d; members: %d; errors: %d' % (len(parts_read), len({f for f, _ in members}), len(members), len(errors)))
w('')

# measured section per member (first CUT row naming it)
meas = {}
for r in cuts:
    for m in (r[7].split(',') if r[7] else []):
        meas.setdefault((r[1], m), parse_section(r[8]))

w('### Members by profile file and size')
w('')
w('| Profile file | Size asked | In the file? | The size\'s sketch | Members | Measured (all members) | Parts |')
w('| --- | --- | --- | --- | --- | --- | --- |')
groups = collections.defaultdict(list)
for (f, feat), (path, size) in members.items(): groups[(path, size)].append((f, feat))
for (path, size), lst in sorted(groups.items(), key=lambda x: (x[0][0].lower(), x[0][1])):
    k = prof_key(path)
    shown = (k or path)
    insize = '' if k is None else ('yes' if size in prof_sizes.get(k, ()) else '**no**')
    sk = sketch_section(profiles.get((k, size), ('', ''))[1]) if k else None
    sks = '' if sk is None else '%sx%sx%s' % (fmt(sk[0][0]), fmt(sk[0][1]), fmt(sk[1]))
    ms = sorted({sect_text(meas.get(x)) for x in lst})
    ps = sorted({rel(f).split(B)[-1] for f, _ in lst})
    w('| %s%s | %s | %s | %s | %d | %s | %s |' % ('**straggler** ' if k is None else '', shown, size, insize, sks, len(lst), ', '.join(ms), ', '.join(ps)))
w('')

w('### Cut-list labels that disagree with the measured section, or with the profile\'s description')
w('')
w('| Part | Configurations | Folder | Label | Measured | Profile description (size asked) | Finding |')
w('| --- | --- | --- | --- | --- | --- | --- |')
lab = collections.OrderedDict()
for r in cuts:
    if not r[7]: continue
    f, cfg, folder, label, raw, mem = r[1], r[2], r[3], r[5], r[6], r[7].split(',')[0]
    path, size = members.get((f, mem), ('', ''))
    pdesc = profiles.get((prof_key(path), size), ('', ''))[0] if prof_key(path) else ''
    m = meas.get((f, mem)); L = parse_label(label)
    finds = []
    if m is None: finds.append('section not measured')
    elif L is None: finds.append('label not a section')
    else:
        typ = {'RSA': 'EA'}.get(L[0], L[0])
        if typ != m[0]: finds.append('type %s, measured %s' % (L[0], m[0]))
        if L[1] != m[1] or (m[2] is not None and abs(L[2] - m[2]) > 0.05): finds.append('size differs')
    if label != pdesc: finds.append('not the profile\'s description' + (' (hand-typed or stale)' if pdesc else ''))
    if raw != label: finds.append('expression ' + raw)
    if not finds: continue
    key = (f, folder, label)
    if key not in lab: lab[key] = [set(), sect_text(m), pdesc + (' (' + size + ')' if size else ''), '; '.join(finds)]
    lab[key][0].add(cfg)
for (f, folder, label), (cfgs, m, pdesc, fs) in lab.items():
    w('| %s | %d | %s | %s | %s | %s | %s |' % (rel(f), len(cfgs), folder, label, m, pdesc, fs))
w('')

w('### Cut-list folders with a blank Description (step 10)')
w('')
w('| Part | Configurations | Folder | Bodies | Measured | Proposed |')
w('| --- | --- | --- | --- | --- | --- |')
blank = collections.OrderedDict()
for r in cuts:
    if r[5].strip(): continue
    key = (r[1], r[3], r[8])
    blank.setdefault(key, [set(), r[4]])[0].add(r[2])
for (f, folder, m), (cfgs, n) in blank.items():
    pm = re.match(r'plate ([\d.]+) x ([\d.]+) x ([\d.]+)', m or '')
    prop = 'PLATE, %s x %s x %s' % pm.groups() if pm else ''
    w('| %s | %d | %s | %s | %s | %s |' % (rel(f), len(cfgs), folder, n, m, prop))
w('')

w('### Material (step 9): parts with a configuration missing the SOLIDWORKS material or the Material property')
w('')
w('| Part | Configurations | SOLIDWORKS material | Material property | Mass | Siblings (same type code) |')
w('| --- | --- | --- | --- | --- | --- |')
def code(f):
    n = rel(f).split(B)[-1]
    m = re.match(r'^[0-9A-Z]+-([A-Z]+)', n, re.I)
    return m.group(1).upper() if m else n
fam = collections.defaultdict(collections.Counter)
for r in mats:
    if r[3]: fam[code(r[1])][r[3]] += 1
bypart = collections.OrderedDict()
for r in mats:
    no_sw = not r[3]; no_prop = not r[7].strip() or r[7].strip() in ('.', '-')
    if not (no_sw or no_prop): continue
    bypart.setdefault(r[1], []).append(r)
for f, rs in bypart.items():
    sw = sorted({r[3] or '(none)' for r in rs}); pr = sorted({(r[7] or '(none)') for r in rs})
    ms = sorted({r[8] for r in rs})
    sib = ', '.join('%s (%d)' % kv for kv in fam[code(f)].most_common())
    w('| %s | %d | %s | %s | %s | %s |' % (rel(f), len(rs), '; '.join(sw), '; '.join(pr), '; '.join(ms)[:60], sib or '**none**'))
w('')
if errors:
    w('### Errors')
    w('')
    for r in errors: w('- %s: %s' % (rel(r[1]), r[2] if len(r) > 2 else ''))
print('\n'.join(out))
