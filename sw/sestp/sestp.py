"""Square-edge STEP (SESTP) export, sandbox only (Grant, SW window, 22 Nov 2026).

A screen's structure goes to <LS>_STR.STEP with every tube and angle corner square (Revit shape only, not for mass), and
its DL_APEX tiles go to <LS>_LED.STEP as they are; both come from the same top assembly, so they overlay exactly.
Nothing in P:\\ or the vault changes: the model is a Pack and Go copy under C:\\PDM-work\\sestp\\runs\\.

Speed: a squared part is kept in C:\\PDM-work\\sestp\\cache\\, keyed to the exact vault files it was made from (the part and
every file it references, by md5). Next time, if those files are unchanged, the kept copy is dropped in; only new or
changed tube parts are squared again.

Time limits (Admin, 25 Nov, option A): squaring a part is one SOLIDWORKS call with a 30-min limit, or 75 min for a part
with more than 4 configurations. Reason, from 08X07-SFP (10 configurations, 15 tube bodies): the first configuration
switch and rebuild after opening takes ~10 min, each further configuration ~1 min; the whole part squared in one call took
39 min (2327 s). One call per configuration was tried: each call pays the ~10-min opening again (reopening the squared copy
and reading its 10 configurations took 16 min), ~21 calls, 3.5-4 h. The kept copy makes the long call a once-per-version
cost. (SwSestp still has "square <part> --before" and "--config <c>" for that per-configuration way; sestp.py does not use them.)

Usage (SOLIDWORKS lock held by sw: tools\\SwCmd\\bin\\SwCmd.exe lock --as sw --purpose ...):
  python sw/sestp/sestp.py run  --top <P:\\...\\top.SLDASM> --config <name> --ls <LS-08A-11> [--count] [--plain] [--label <word>]
  python sw/sestp/sestp.py seed --from <old sandbox model folder> --index <vault index json at that pin> --parts A,B,C
  python sw/sestp/sestp.py tubes <folder>        (no SOLIDWORKS: the tube parts in a folder)
  python sw/sestp/sestp.py tidy                  (remove the sandbox model copies of every run; STEPs, notes, cache stay)
A good run removes its own sandbox model copy unless --keep-model; a bad run keeps it for a look.
The run checks every file it used against the vault's latest (SSO login, read-only) and writes <LS>_SESTP notes.txt.
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
    # --plain (Admin, 25 Nov, the 0807 stairs for wireframe): the same export with the squaring step skipped; the run
    # folder is marked "plain"
    # --label <word> (Admin, 25 Nov): a word in the run folder's name and the notes' first line, e.g. "test"
    rd = os.path.join(RUNS, a.ls + (' plain' if a.plain else '') + (' ' + a.label if a.label else '') + ' ' + stamp)
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
    vault = vault_check(model, rd)
    say('VAULT', vault)
    times['Vault check'] = time.time() - t

    t = time.time()
    tubes = tubes_in(model)
    plan = []
    for p in tubes:
        rel = os.path.relpath(p, model)
        k, parts = key_of('P:\\' + rel)
        name = os.path.splitext(os.path.basename(p))[0]
        hit = os.path.exists(os.path.join(cache_dir(name, k), 'meta.json'))
        plan.append((name, p, k, parts, hit))
    # --plain-shape: parts that are tubes or angles drawn as plain extrudes (no structural member), squared with every body
    # counted (28 Nov, APEX-A124: 08X11-BRA-01 rail, 08X11-MSP-01 seat)
    PLAIN_SHAPE.clear()
    PLAIN_SHAPE.update(x.strip().upper() for x in a.plain_shape.split(',') if x.strip())
    for dp, _, fs in os.walk(model):
        for f in fs:
            n0 = os.path.splitext(f)[0]
            if f.upper().endswith('.SLDPRT') and n0.upper() in PLAIN_SHAPE and not any(x[1] == os.path.join(dp, f) for x in plan):
                p = os.path.join(dp, f)
                k, parts = key_of('P:\\' + os.path.relpath(p, model))
                k = hashlib.md5((k + '|plain-shape').encode()).hexdigest()
                plan.append((n0, p, k, parts, os.path.exists(os.path.join(cache_dir(n0, k), 'meta.json'))))
    missing = PLAIN_SHAPE - set(x[0].upper() for x in plan)
    if missing:
        say('STOPPED: --plain-shape parts not in the model:', ', '.join(sorted(missing))); return 1
    times['Find tube parts'] = time.time() - t
    if a.plain:
        say('PLAIN: squaring skipped for', len(plan), 'tube and angle parts:', ', '.join(x[0] for x in plan))
        plan = []
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
            if not square_part(p, name):
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
    good = export_ok and not moved
    if good and not a.keep_model:
        t = time.time()
        tidy_run(rd)
        times['Tidy up'] = time.time() - t
    times['Total'] = time.time() - T0
    say('TIMES', ' | '.join('%s %d s' % kv for kv in times.items()))
    say('RESULT', 'OK' if good else 'PROBLEMS (see log.txt; the sandbox model is kept for a look)')
    write_notes(rd, a, vault, plan, before, moved, good, times)
    return 0 if good else 1


def configs_of(path):
    """Configuration names of a part, from Document Manager (no SOLIDWORKS)."""
    tmp = os.path.join(os.path.dirname(path), '_dmconfigs')
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(os.path.join(tmp, 'in')); os.makedirs(os.path.join(tmp, 'out'))
    shutil.copy2(path, os.path.join(tmp, 'in'))
    run([os.path.join(PDM, 'tools', 'DmProbe', 'bin', 'DmProbe.exe'), os.path.join(tmp, 'in'), os.path.join(tmp, 'out')], quiet=True)
    try:
        rows = open(os.path.join(tmp, 'out', 'configs.csv'), encoding='utf-8-sig').read().splitlines()[1:]
    except OSError:
        rows = []
    shutil.rmtree(tmp, ignore_errors=True)
    return [r.split(',')[1] for r in rows if r]


# Time limits for squaring one part (one SOLIDWORKS call for the whole part). Admin, 25 Nov, option A, from the 08X07-SFP
# timings: 10 configurations, 15 tube bodies; the first configuration switch and rebuild after opening took about 10 min,
# each further configuration about 1 min, and the whole part squared in one call took 39 min (2327 s). Splitting it into
# one call per configuration was written and timed: each call pays the ~10-min opening again (reopening the squared copy
# and reading its 10 configurations took 16 min), so about 21 calls, 3.5-4 h. So: parts with more than 4 configurations get
# 75 min in one call; every other part keeps 30 min. The squared part is kept in the cache (keyed to the exact vault files),
# so the long call is paid once per version of such a part.
SQUARE_LIMIT, SQUARE_LIMIT_MANY = 1800, 4500


PLAIN_SHAPE = set()


def square_part(p, name):
    """Square one sandbox part in one SOLIDWORKS call, with the longer limit for parts with more than 4 configurations."""
    n = len(configs_of(p))
    limit = SQUARE_LIMIT_MANY if n > 4 else SQUARE_LIMIT
    if limit != SQUARE_LIMIT:
        say('   ', name, 'has', n, 'configurations: time limit', limit // 60, 'min')
    try:
        rc, _ = run([os.path.join(MAC, 'SwSestp.exe'), 'square', p] + (['--plain-shape'] if name.upper() in PLAIN_SHAPE else []), timeout=limit)
        return rc == 0
    except subprocess.TimeoutExpired:
        say('    TIME LIMIT', name, 'not squared in', limit // 60, 'min')
        return False


def vault_check(model, rd):
    """Every file in the sandbox came from P:\\; compare those P:\\ files with the vault's latest (SSO login, read-only)."""
    idx = os.path.join(rd, 'vault-index.json')
    with open(idx, 'w') as f:
        p = subprocess.run(['aws', 'dynamodb', 'scan', '--table-name', 'pdm-files', '--profile', 'pdm-admin', '--region', 'ap-east-1',
                            '--output', 'json'], stdout=f, stderr=subprocess.PIPE, text=True, timeout=300)
    if p.returncode != 0:
        return 'NOT CHECKED (vault sign-in: %s)' % (p.stderr.strip().splitlines() or ['?'])[-1]
    items = [i for i in json.load(open(idx, encoding='utf-8'))['Items'] if 'removedAt' not in i]
    etag = {i['path']['S'].upper().replace('/', '\\'): i['etag']['S'] for i in items}
    files = [os.path.relpath(os.path.join(dp, f), model) for dp, _, fs in os.walk(model) for f in fs]
    # the pin: the latest check-in among the files this model uses (not the whole vault's latest)
    used = set(r.upper() for r in files)
    latest = max((i.get('updatedAt', {}).get('S', '') for i in items if i['path']['S'].upper().replace('/', '\\') in used), default='?')
    diff = [r for r in files if not os.path.exists('P:\\' + r) or etag.get(r.upper()) != md5('P:\\' + r)]
    os.remove(idx)
    return ('P:\\ matches the vault for all %d files used (pin: latest check-in among them %s)' % (len(files), latest)) if not diff else \
        ('P:\\ DIFFERS from the vault for %d of %d files: %s' % (len(diff), len(files), ', '.join(diff[:10])))


def tidy_run(rd):
    """Remove the sandbox model and staging copies of a run (the STEPs, notes, log and position files stay; the kept
    squared parts in the cache stay)."""
    freed = 0
    for sub in ('model', 'staging'):
        d = os.path.join(rd, sub)
        if os.path.isdir(d):
            for dp, _, fs in os.walk(d):
                for f in fs:
                    p = os.path.join(dp, f)
                    freed += os.path.getsize(p)
                    os.chmod(p, 0o666)
            try:
                shutil.rmtree(d)
            except OSError:
                # SOLIDWORKS can keep the last folder it used open for a while; whatever is left goes at the next tidy
                time.sleep(5)
                shutil.rmtree(d, ignore_errors=True)
    left = [s for s in ('model', 'staging') if os.path.isdir(os.path.join(rd, s))]
    say('TIDY', rd, 'sandbox copy removed,' if not left else 'partly removed (in use, try "tidy" later),', freed // (1 << 20), 'MB')


def cmd_tidy(a):
    for r in sorted(os.listdir(RUNS)):
        tidy_run(os.path.join(RUNS, r))


def write_notes(rd, a, vault, plan, before, moved, good, times):
    led = os.path.join(rd, a.ls + '_LED.txt')
    # LED CORNER and ENVELOPE without their label (the Revit import reads "bottom-left of the LED face seen from the front ...");
    # APEX-A67's LED FACE (one per face on a wrap), AXES and END STEEL lines with theirs
    corner = [l.split('\t', 1)[1] if l.startswith(('LED CORNER', 'ENVELOPE')) else l for l in open(led, encoding='utf-8')
              if l.startswith(('LED CORNER', 'ENVELOPE', 'LED FACE', 'AXES', 'END STEEL'))] if os.path.exists(led) else []
    plain = getattr(a, 'plain', False)
    label = getattr(a, 'label', '')
    L = ['%s %s, %s, made by SW (SOLIDWORKS 2026)%s' % (a.ls, 'PLAIN STEP (corners as in the vault, NOT squared)' if plain else 'square-edge STEP (SESTP)', os.path.basename(rd)[len(a.ls) + 1:],
                                                       ('   ' + label.upper() + ' RUN: not for issue') if label else ''),
         '',
         'Result: ' + ('OK' if good else 'PROBLEMS, see log.txt'),
         'Two STEP files (AP203) from the same top assembly, %s (configuration %s), in the same axes, so they overlay:' % (a.top, a.config),
         ('  %s_STR.STEP   the structure as modelled (corners not squared: for analysis, not for the Revit import)' % a.ls) if plain else
         ('  %s_STR.STEP   the structure, every tube and angle corner square (Revit SHAPE ONLY: not for mass or weight)' % a.ls),
         '  %s_LED.STEP   the DL_APEX tiles, as in the vault (not squared)' % a.ls,
         'Source: ' + vault,
         'Sandbox only: nothing checked out, nothing written to P:\\ or the vault.',
         '',
         'Tube and angle parts: %d (%d kept squared copies used, %d squared in this run)' % (len(plan), sum(1 for x in plan if x[4]), sum(1 for x in plan if not x[4])),
         '  ' + ', '.join(x[0] for x in plan),
        ] + (['  Plain-extrude parts squared with every body counted: ' + ', '.join(sorted(PLAIN_SHAPE))] if PLAIN_SHAPE else []) + ["  " + n for n in a.note] + [
         'Positions: %d active components; %d moved while squaring or exporting.' % (len(before), len(moved)),
         '',
         'LED face (APEX-A49):'] + ['  ' + c.strip() for c in corner] + [
         '',
         'Times: ' + ' | '.join('%s %d s' % kv for kv in times.items())]
    open(os.path.join(rd, a.ls + '_SESTP notes.txt'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')


def main():
    ap = argparse.ArgumentParser()
    sp = ap.add_subparsers(dest='cmd', required=True)
    r = sp.add_parser('run'); r.add_argument('--top', required=True); r.add_argument('--config', required=True)
    r.add_argument('--ls', required=True); r.add_argument('--count', action='store_true')
    r.add_argument('--plain', action='store_true', help='skip the squaring step (plain STEP; run folder marked "plain")')
    r.add_argument('--label', default='', help='a word for the run folder name and the notes, e.g. test')
    r.add_argument('--plain-shape', default='', help='parts (names, comma-separated) that are tubes or angles drawn as plain extrudes: squared with every body counted')
    r.add_argument('--note', action='append', default=[], help='a line for the notes file (repeatable)')
    r.add_argument('--keep-model', action='store_true', help='keep the sandbox model after a good run (it is always kept after a bad one)')
    s = sp.add_parser('seed'); s.add_argument('--from', required=True); s.add_argument('--index', required=True); s.add_argument('--parts', required=True)
    t = sp.add_parser('tubes'); t.add_argument('folder')
    sp.add_parser('tidy', help='remove the sandbox model copies of every run (STEPs, notes and the cache stay)')
    a = ap.parse_args()
    if a.cmd == 'run' and not a.top.upper().startswith('P:\\'):
        sys.exit('--top must be a P:\\ file')
    sys.exit({'run': cmd_run, 'seed': cmd_seed, 'tubes': cmd_tubes, 'tidy': cmd_tidy}[a.cmd](a) or 0)


if __name__ == '__main__':
    main()
