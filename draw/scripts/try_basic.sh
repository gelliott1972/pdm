#!/bin/bash
# try_basic.sh LIST OUTDIR : make-basic only, per row (PartNo, file, config, folder under APEX), into OUTDIR (a test
# folder), then a PNG of each PDF in OUTDIR/png for looking at.
LIST=$1; OUT=$2
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
mkdir -p "$OUT/png"; OW_OUT=$(cygpath -w "$OUT")
cd /c/code/pdm/draw/src
sed 's/\r$//' "$LIST" | while IFS=$'\t' read -r pn file cfg folder rest; do
  rm -f "${OUT:?}/${pn:?}.SLDDRW" "${OUT:?}/${pn:?}.pdf"
  echo "=================== $pn"
  bin/Draw.exe make-basic "${OW}${B}${folder}${B}${file}" "$cfg" "${OW_OUT}${B}${pn}.SLDDRW" "$T" > "$OUT/$pn.make-basic.log" 2>&1
  bin/Draw.exe close-mine > /dev/null 2>&1
  grep -E "^General|^Check sizes|Dangling|Isometric at|Views moved|REFUSED|NOT saved|^Saved|removed a|not on the drawing" "$OUT/$pn.make-basic.log" | cut -c1-200
done
python - "$OUT" <<'PY'
import pymupdf, glob, os, sys
out = sys.argv[1]
for f in glob.glob(out + '/*.pdf'):
    pymupdf.open(f)[0].get_pixmap(dpi=100).save(out + '/png/' + os.path.basename(f)[:-4] + '.png')
PY
