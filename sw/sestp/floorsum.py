"""Summarise SwGrout floor output (SW, 24 Nov 2026): the lowest plate level, its plates grouped by thickness, size,
gap to the steel above, what that steel is (file and configuration) and whether anything is under them; then any other
plate within 100 mm above the floor (a floating plate). Usage: python floorsum.py <floor.txt> [<floor.txt> ...]
"""
import re
import sys
from collections import Counter


def summary(path):
    plates = []
    for line in open(path, encoding='utf-8', errors='replace'):
        if not line.startswith('PLATE\t'):
            continue
        f = line.rstrip('\n').split('\t')
        und = float(f[1].split()[1])
        above, under = f[6], f[7]
        gap = above.split()[2] if above.startswith('above gap') else 'none'
        steel = re.sub(r'^.*\]\s+', '', above) if gap != 'none' else '-'
        plates.append((und, f[3], f[4], gap, steel, under, f[8]))
    lo = min(p[0] for p in plates)
    floor = [p for p in plates if abs(p[0] - lo) < 0.005]
    near = [p for p in plates if 0.005 <= p[0] - lo <= 100]
    print('==', path)
    print('floor underside %g: %d plates' % (lo, len(floor)))
    for k, n in sorted(Counter((p[1], p[2], 'gap above ' + p[3], p[4], p[5]) for p in floor).items()):
        print('  %2d x  %s' % (n, ' | '.join(k)))
    print('plates 0-100 mm above the floor level:', len(near))
    for p in near:
        print('  underside %g  %s  %s' % (p[0], p[1], p[6]))
    return lo, floor


if __name__ == '__main__':
    for a in sys.argv[1:]:
        summary(a)
