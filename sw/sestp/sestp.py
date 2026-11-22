"""Square-edge STEP (SESTP) export, sandbox only (Grant, SW window, 22 Nov 2026).

A screen's structure goes to <LS>_STR.STEP with every tube and angle corner square (Revit shape only, not for mass), and
its DL_APEX tiles go to <LS>_LED.STEP as they are; both come from the same top assembly, so they overlay exactly.
Nothing in P:\\ or the vault changes: the model is a Pack and Go copy under C:\\PDM-work\\sestp\\runs\\.

Speed: a squared part is kept in C:\\PDM-work\\sestp\\cache\\, keyed to the exact vault files it was made from (the part and
every file it references, by md5). Next time, if those files are unchanged, the kept copy is dropped in; only new or
changed tube parts are squared again.

Usage (SOLIDWORKS lock held by sw: tools\\SwCmd\\bin\\SwCmd.exe lock --as sw --purpose ...):
  python sw/sestp/sestp.py run  --top <P:\\...\\top.SLDASM> --config <name> --ls <LS-08A-11> [--count]
  python sw/sestp/sestp.py seed --from <old sandbox model folder> --index <vault index json at that pin> --parts A,B,C
  python sw/sestp/sestp.py tubes <folder>        (no SOLIDWORKS: the tube parts in a folder)
--count: before squaring, check on the unsquared copies that the new corner rule finds exactly the faces the 21 Nov rule
did (needed once for parts squared by the 21 Nov rule; slower).
"""
import argparse, datetime, hashlib, json, os, re, shutil, subprocess, sys, time

PDM = r'C:\code\pdm'
SESTP = r'C:\PDM-work\sestp'
CACHE = os.path.join(SESTP, 'cache')
RUNS = os.path.join(SESTP, 'runs')
MAC = os.path.join(PDM, r'tools\sw-macros\bin')
SWCMD = os.path.join(PDM, r'tools\SwCmd\bin\SwCmd.exe')
LOG = None


def say(*a):
    line = ' '.join(str(x) for x in a)
    print(line, flush=True)
    if LOG:
        with open(LOG, 'a', encoding='utf-8') as f:
            f.write(line + '\n')


def run(cmd, timeout=1800, quiet=False):
    t0 = time.time()
    p = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=timeout)
    out = (p.stdout or '') + (p.stderr or '')
    if LOG:
        with open(LOG, 'a', encoding='utf-8') as f:
            f.write('$ ' + ' '.join(cmd) + '\n' + out + '(exit %d, %d s)\n' % (p.returncode, time.time() - t0))
    if not quiet:
        for l in out.splitlines():
            if re.match(r'(PROBLEM|FAILED|REFUSED|DIFFERENT|SAVED|NOT SAVED|DONE|COUNT|MOVED|STR |LED |ENVELOPE|TILE|HIDDEN|WHATSWRONG|Written|Pack and Go|REPOINTED)', l.strip()):
                say('   ', l.strip())
    return p.returncode, out


def md5(path):
    h = hashlib.md5()
    with open(path, 'rb') as f:
        for b in iter(lambda: f.read(1 << 20), b''):
            h.update(b)
    return h.hexdigest()


def refs(path):
    """Files a part references, as Document Manager resolves them (no search folders)."""
    _, out = run([os.path.join(MAC, 'DmRefPaths.exe'), path], quiet=True)
    return [l.split('\t')[2] for l in out.splitlines() if l.startswith('REF\t')]


def is_tube(path):
    """A part with a structural member (a cut-list item measured by LENGTH)."""
    _, out = run([os.path.join(MAC, 'DmCutList.exe'), path, '*'], quiet=True)
    return 'UNIT_OF_MEASURE = LENGTH' in out


def key_of(vault_part):
    """md5 of the vault part and of every file it references: the cache key."""
    parts = [os.path.basename(vault_part).upper() + '=' + md5(vault_part)]
    for r in sorted(refs(vault_part)):
        parts.append(os.path.basename(r).upper() + '=' + (md5(r) if os.path.exists(r) else 'MISSING'))
    return hashlib.md5('\n'.join(parts).encode()).hexdigest(), parts


def cache_dir(name, key):
    return os.path.join(CACHE, name.upper(), key[:16])


def store(squared_file, name, key, parts, made_root, note):
    d = cache_dir(name, key)
    os.makedirs(d, exist_ok=True)
    shutil.copy2(squared_file, os.path.join(d, os.path.basename(squared_file)))
    json.dump({'key': key, 'sources': parts, 'made_root': made_root, 'made': note,
               'stored': datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')},
              open(os.path.join(d, 'meta.json'), 'w'), indent=1)


def drop_in(name, key, target, new_root, stage):
    """Copy the kept squared part over the sandbox copy; its references are pointed into the new sandbox first (done in a
    staging folder with nothing beside it, so Document Manager sees the stored paths)."""
    d = cache_dir(name, key)
    meta = json.load(open(os.path.join(d, 'meta.json')))
    src = os.path.join(d, os.path.basename(target))
    os.makedirs(stage, exist_ok=True)
    tmp = os.path.join(stage, os.path.basename(target))
    shutil.copy2(src, tmp)
    os.chmod(tmp, 0o666)
    rc, out = run([os.path.join(MAC, 'DmRepoint.exe'), tmp, meta['made_root'], new_root], quiet=True)
    if rc != 0:
        say('    PROBLEM repointing', name, out.strip().splitlines()[-1:] )
        return False
    if os.path.exists(target):
        os.chmod(target, 0o666)
    shutil.copy2(tmp, target)
    return True


def positions(path):
    d = {}
    for l in open(path, encoding='utf-8'):
        f = l.rstrip('\n').split('\t')
        if f[0] == 'COMP':
            d[f[1]] = (f[2], f[3], [float(x) for x in f[4].replace('|', ' ').split()])
    return d


def tubes_in(folder):
    out = []
    for dp, _, fs in os.walk(folder):
        for f in fs:
            if f.upper().endswith('.SLDPRT') and is_tube(os.path.join(dp, f)):
                out.append(os.path.join(dp, f))
    return sorted(out)


def lock_held():
    _, out = run([SWCMD, 'status'], quiet=True)
    m = re.search(r'^Lock: (\S+)', out, re.M)
    return m and m.group(1).rstrip(';') == 'sw', out


def cmd_tubes(a):
    t0 = time.time()
    ts = tubes_in(a.folder)
    for t in ts:
        print(t)
    print(len(ts), 'tube parts,', int(time.time() - t0), 's')


def cmd_seed(a):
    """Keep parts squared in an earlier sandbox, after checking they were made from the vault files now in P:\\."""
    items = [i for i in json.load(open(a.index, encoding='utf-8'))['Items'] if 'removedAt' not in i]
    etag = {i['path']['S'].upper().replace('/', '\\'): i['etag']['S'] for i in items}
    root = os.path.abspath(a.__dict__['from'])
    for name in a.parts.split(','):
        hits = [os.path.join(dp, f) for dp, _, fs in os.walk(root) for f in fs if f.upper() == name.upper() + '.SLDPRT']
        if len(hits) != 1:
            print('SKIP', name, 'found', len(hits), 'times in', root); continue
        sq = hits[0]
        rel = os.path.relpath(sq, root)
        vault = 'P:\\' + rel
        k, parts = key_of(vault)
        # the vault files now in P:\ must be the ones of that pin
        bad = [p for p in [vault] + refs(vault) if etag.get(os.path.relpath(p, 'P:\\').upper()) != md5(p)]
        if bad:
            print('SKIP', name, 'P:\\ differs from that pin for', bad); continue
        store(sq, name, k, parts, root, 'seeded from ' + root + ' (squared 21/22 Nov with the 21 Nov rule; pin in ' + os.path.basename(a.index) + ')')
        print('KEPT', name, k[:16])


def cmd_run(a):
    global LOG
    T0 = time.time()
    ok, st = lock_held()
    if not ok:
        print('STOPPED: the SOLIDWORKS lock is not held by sw. Status:\n' + st); return 2
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H%MZ')
    rd = os.path.join(RUNS, a.ls + ' ' + stamp)
    os.makedirs(rd)
    LOG = os.path.join(rd, 'log.txt')
    model = os.path.join(rd, 'model')
    top_rel = os.path.relpath(a.top, 'P:\\')
    top = os.path.join(model, top_rel)
    times = {}
    say('SESTP', a.ls, 'from', a.top, 'configuration', a.config, 'into', rd)

    t = time.time()
    rc, out = run([SWCMD, 'pack-and-go', a.top, model, '--as', 'sw'], timeout=1800)
    run([SWCMD, 'close', a.top, '--as', 'sw'], quiet=True)
    times['Pack and Go'] = time.time() - t
    if rc != 0 or not os.path.exists(top):
        say('STOPPED: Pack and Go failed'); return 1

    t = time.time()
    tubes = tubes_in(model)
    plan = []
    for p in tubes:
        rel = os.path.relpath(p, model)
        k, parts = key_of('P:\\' + rel)
        name = os.path.splitext(os.path.basename(p))[0]
        hit = os.path.exists(os.path.join(cache_dir(name, k), 'meta.json'))
        plan.append((name, p, k, parts, hit))
    times['Find tube parts'] = time.time() - t
    say('TUBE PARTS', len(plan), '| kept squared copies to use:', sum(1 for x in plan if x[4]), '| to square now:', sum(1 for x in plan if not x[4]))
    for name, p, k, _, hit in plan:
        say('   ', 'kept' if hit else 'SQUARE', name, k[:16])

    t = time.time()
    if a.count:
        for name, p, k, _, hit in plan:
            rc, _ = run([os.path.join(MAC, 'SwSestp.exe'), 'count', p], timeout=900)
            if rc != 0:
                say('STOPPED: the corner rules disagree on', name); return 1
        times['Rule check (unsquared copies)'] = time.time() - t
        t = time.time()
    rc, _ = run([os.path.join(MAC, 'SwSestp.exe'), 'xforms', top, a.config, os.path.join(rd, 'positions-before.txt')], timeout=1200)
    times['Positions before'] = time.time() - t
    if rc != 0:
        say('STOPPED: could not read positions'); return 1

    t = time.time()
    stage = os.path.join(rd, 'staging')
    for name, p, k, parts, hit in plan:
        if hit:
            if not drop_in(name, k, p, model, stage):
                say('STOPPED: could not drop in', name); return 1
        else:
            rc, _ = run([os.path.join(MAC, 'SwSestp.exe'), 'square', p], timeout=1800)
            if rc != 0:
                say('STOPPED: squaring failed for', name, '(see log.txt)'); return 1
            store(p, name, k, parts, model, 'squared in ' + rd)
    times['Square / drop in'] = time.time() - t

    t = time.time()
    rc, _ = run([os.path.join(MAC, 'SwSestp.exe'), 'export', top, a.config, rd, a.ls], timeout=1800)
    times['Export (open, 2 STEPs, checks)'] = time.time() - t
    export_ok = rc == 0

    before, after = positions(os.path.join(rd, 'positions-before.txt')), positions(os.path.join(rd, 'positions.txt'))
    moved = [n for n in before if n not in after or any(abs(x - y) > 0.01 for x, y in zip(before[n][2], after[n][2]))]
    say('POSITIONS', len(before), 'before,', len(after), 'after;', len(moved), 'moved or missing', moved[:10])

    for f in ('_STR', '_LED'):
        s = os.path.join(rd, a.ls + f + '.STEP')
        if os.path.exists(s):
            say('FILE', s, os.path.getsize(s) // 1024, 'KB')
    times['Total'] = time.time() - T0
    say('TIMES', ' | '.join('%s %d s' % kv for kv in times.items()))
    good = export_ok and not moved
    say('RESULT', 'OK' if good else 'PROBLEMS (see log.txt)')
    return 0 if good else 1


def main():
    ap = argparse.ArgumentParser()
    sp = ap.add_subparsers(dest='cmd', required=True)
    r = sp.add_parser('run'); r.add_argument('--top', required=True); r.add_argument('--config', required=True)
    r.add_argument('--ls', required=True); r.add_argument('--count', action='store_true')
    s = sp.add_parser('seed'); s.add_argument('--from', required=True); s.add_argument('--index', required=True); s.add_argument('--parts', required=True)
    t = sp.add_parser('tubes'); t.add_argument('folder')
    a = ap.parse_args()
    if a.cmd == 'run' and not a.top.upper().startswith('P:\\'):
        sys.exit('--top must be a P:\\ file')
    sys.exit({'run': cmd_run, 'seed': cmd_seed, 'tubes': cmd_tubes}[a.cmd](a) or 0)


if __name__ == '__main__':
    main()
