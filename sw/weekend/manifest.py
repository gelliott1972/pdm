r"""Weekend run (APEX-A125/A126, 27-28 Nov 2026): record, for every file in a work copy, the vault's updatedAt, etag and
versionId at copy time, and check that the P:\ file the copy was packed from is byte-identical to that vault version (Pack and Go re-saves the
copies with repointed references, so the copies themselves differ in bytes). Read-only (vault index scan).
Usage: python manifest.py <work copy folder> [<work copy folder> ...]   -> writes <folder>\vault-manifest.tsv
       python manifest.py --recheck <work copy folder> <file rel path> ...  -> today's index vs the manifest, per file
"""
import hashlib
import json
import os
import subprocess
import sys

AWS = ['aws', 'dynamodb', 'scan', '--table-name', 'pdm-files', '--profile', 'pdm-admin', '--region', 'ap-east-1', '--output', 'json']


def index():
    items = [i for i in json.loads(subprocess.run(AWS, capture_output=True, text=True, check=True).stdout)['Items'] if 'removedAt' not in i]
    return {i['path']['S'].replace('/', '\\').upper(): i for i in items}


def md5(p):
    return hashlib.md5(open(p, 'rb').read()).hexdigest()


def write(folder, idx):
    rows, bad = [], []
    for dp, _, fs in os.walk(folder):
        for f in fs:
            p = os.path.join(dp, f)
            rel = os.path.relpath(p, folder)
            if rel.lower() == 'vault-manifest.tsv' or not rel.upper().endswith(('.SLDPRT', '.SLDASM')):
                continue
            it = idx.get(rel.upper())
            if it is None:
                bad.append('not in the vault index: ' + rel); continue
            e = it['etag']['S'].strip('"'); src = 'P:/' + rel.replace(chr(92), '/'); same = os.path.exists(src) and md5(src) == e
            if not same:
                bad.append('P:\ source differs from the vault latest: ' + rel)
            rows.append('\t'.join([rel, it.get('updatedAt', {}).get('S', ''), e, it.get('versionId', {}).get('S', ''),
                                   'same' if same else 'DIFFERENT', it.get('lockedByName', {}).get('S', '')]))
    with open(os.path.join(folder, 'vault-manifest.tsv'), 'w', encoding='utf-8') as fh:
        fh.write('path\tupdatedAt\tetag\tversionId\tcopy\tlockedBy\n' + '\n'.join(sorted(rows)) + '\n')
    print(folder, len(rows), 'files;', len(bad), 'problems'); [print('  ' + b) for b in bad[:20]]


if __name__ == '__main__':
    idx = index()
    if sys.argv[1] == '--recheck':
        folder = sys.argv[2]
        man = {l.split('\t')[0].upper(): l.rstrip('\n').split('\t') for l in open(os.path.join(folder, 'vault-manifest.tsv'), encoding='utf-8')}
        for rel in sys.argv[3:]:
            m = man.get(rel.upper()); it = idx.get(rel.upper())
            ok = m is not None and it is not None and it['etag']['S'].strip('"') == m[2] and it.get('updatedAt', {}).get('S', '') == m[1]
            print('%s\t%s\trecorded %s\tnow %s\tlocked by %s' % ('SAME' if ok else 'CHANGED', rel, m[1] if m else '-',
                  it.get('updatedAt', {}).get('S', '') if it else 'not in index', it.get('lockedByName', {}).get('S', '') if it else ''))
    else:
        for folder in sys.argv[1:]:
            write(folder, idx)
