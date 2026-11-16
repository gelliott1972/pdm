#!/bin/bash
# regress.sh LIST OUTDIR : remake each approved drawing (PartNo, file, config, folder under APEX, load batch) from the
# overnight model copies into OUTDIR (never the load folders), then list the remade and the approved drawing's views
# side by side (scale, drawn box, note block) with the value checks. Read-only on the approved drawings.
LIST=$1; OUT=$2
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
LD="C:${B}code${B}pdm${B}scratch${B}draw-test${B}load"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
mkdir -p "$OUT"; OW_OUT=$(cygpath -w "$OUT")
cd /c/code/pdm/draw/src
sed 's/\r$//' "$LIST" | while IFS=$'\t' read -r pn file cfg folder batch; do
  out="${OW_OUT}${B}${pn}.SLDDRW"
  rm -f "$OUT/$pn.SLDDRW" "$OUT/$pn.pdf"
  echo "=================== $pn"
  bin/Draw.exe make-plate "${OW}${B}${folder}${B}${file}" "$cfg" "$out" "$T" > "$OUT/$pn.log" 2>&1
  grep -E "^Isometric at|^Check|dangling|^Saved|^  layout|hits" "$OUT/$pn.log"
  bin/Draw.exe close-mine > /dev/null 2>&1
  echo "--- remade"
  bin/Draw.exe view-boxes "$out" 2>&1 | grep -E "Drawing View|edges|note 'Filename"
  bin/Draw.exe close-mine > /dev/null 2>&1
  echo "--- approved"
  bin/Draw.exe view-boxes "${LD}${B}${batch}${B}APEX${B}${folder}${B}${pn}.SLDDRW" 2>&1 | grep -E "Drawing View|edges|note 'Filename"
  bin/Draw.exe close-mine > /dev/null 2>&1
done
