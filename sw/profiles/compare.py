"""Compare SwRefCheck output before and after (brief sw-profiles step 7/8).
Usage: python sw/profiles/compare.py <before.txt> <after.txt>
Prints FAIL lines (dims, dangling or errors differ / more) and BOM lines (table text changed), per file.
"""
import collections, sys

def load(p):
    head, tables = {}, collections.defaultdict(list)
    for l in open(p, encoding='utf-8', errors='replace'):
        c = l.rstrip('\n').split('\t')
        if c[0] in ('DRAWING', 'MODEL', 'ERROR'): head[c[1].upper()] = c[2:]
        elif c[0] == 'TABLE': tables[c[1].upper()].append('\t'.join(c[2:]))
    return head, tables

bh, bt = load(sys.argv[1]); ah, at = load(sys.argv[2])
for f in sorted(ah):
    if f not in bh: print('NOBEFORE\t' + f); continue
    b, a = bh[f], ah[f]
    if b[0].startswith('could') or a[0].startswith('could'): print('FAIL\t%s\t%s -> %s' % (f, b, a)); continue
    if f.endswith('.SLDDRW'):
        bd = dict(x.split(' ', 1) for x in b); ad = dict(x.split(' ', 1) for x in a)
        bad = []
        if bd['dims'] != ad['dims']: bad.append('dims %s -> %s' % (bd['dims'], ad['dims']))
        if int(ad['dangling']) > int(bd['dangling']): bad.append('dangling %s -> %s' % (bd['dangling'], ad['dangling']))
        if int(ad['errors']) > int(bd['errors']): bad.append('errors %s -> %s' % (bd['errors'], ad['errors']))
        print(('FAIL\t%s\t%s' % (f, '; '.join(bad))) if bad else ('OK\t%s\tdims %s, dangling %s' % (f, ad['dims'], ad['dangling'])))
        if bt[f] != at[f]:
            gone = [x for x in bt[f] if x not in at[f]]; new = [x for x in at[f] if x not in bt[f]]
            print('BOM\t%s\t%d rows changed: %s => %s' % (f, len(new), ' // '.join(gone)[:600], ' // '.join(new)[:600]))
    else:
        be = dict(x.split('=', 1) for x in b[0][len('errors '):].split('; ') if '=' in x)
        ae = dict(x.split('=', 1) for x in a[0][len('errors '):].split('; ') if '=' in x)
        worse = ['%s %s -> %s' % (k, be.get(k), v) for k, v in ae.items() if int(v) > int(be.get(k, '0'))]
        print(('FAIL\t%s\tmore rebuild errors: %s' % (f, '; '.join(worse))) if worse else ('OK\t%s\terrors as before' % f))
