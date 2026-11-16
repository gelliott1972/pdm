#!/bin/bash
# chain_17nov.sh : after SW's profiles run: regression (plates, brackets, members), the 11 redone drafts, 08B04-BKP-01.
D=/c/code/pdm/scratch/draw-test; SC=/c/code/pdm/draw/scripts
bash $SC/regress.sh $D/regress-17nov.tsv $D/regress-17nov > $D/regress-17nov.out 2>&1
bash $SC/try_routes.sh $SC/regress-members.tsv $D/regress-17nov-members > $D/regress-17nov-members.out 2>&1
bash $SC/try_routes.sh $D/redo-16nov.tsv $D/redo-17nov > $D/redo-17nov.out 2>&1
bash $SC/try_routes.sh $D/try-bkp01.tsv $D/redo-17nov > $D/try-bkp01.out 2>&1
python - <<'PY'
import pymupdf, glob, os
D = r'C:\code\pdm\scratch\draw-test'
for d in ('regress-17nov', 'regress-17nov-members', 'redo-17nov'):
    os.makedirs(os.path.join(D, d, 'png'), exist_ok=True)
    for f in glob.glob(os.path.join(D, d, '*.pdf')):
        pymupdf.open(f)[0].get_pixmap(dpi=100).save(os.path.join(D, d, 'png', os.path.basename(f)[:-4] + '.png'))
PY
echo CHAIN DONE
