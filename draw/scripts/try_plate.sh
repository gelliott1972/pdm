#!/bin/bash
# try_plate.sh LIST OUTDIR : make-plate only, per row (PartNo, file, config, folder under APEX), into OUTDIR (a test
# folder; nothing in the overnight or load folders is written, and no approved drawing is opened).
LIST=$1; OUT=$2
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
mkdir -p "$OUT"; OW_OUT=$(cygpath -w "$OUT")
cd /c/code/pdm/draw/src
sed 's/\r$//' "$LIST" | while IFS=$'\t' read -r pn file cfg folder rest; do
  rm -f "${OUT:?}/${pn:?}.SLDDRW" "${OUT:?}/${pn:?}.pdf"
  echo "=================== $pn"
  bin/Draw.exe make-plate "${OW}${B}${folder}${B}${file}" "$cfg" "${OW_OUT}${B}${pn}.SLDDRW" "$T" > "$OUT/$pn.make-plate.log" 2>&1
  bin/Draw.exe close-mine > /dev/null 2>&1
  grep -E "^Check|^Flag|dangling|^Saved|views need|isometric hits|Views moved|Isometric at 1:.*,|balloon moved|note block|REFUSED|NOT saved" "$OUT/$pn.make-plate.log" | cut -c1-200
done
