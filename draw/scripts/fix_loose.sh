#!/bin/bash
# fix_loose.sh SRC OUT PARTS NAME... : fix-loose on scratch copies. For each NAME: copy SRC/NAME.SLDDRW to OUT (a
# fresh folder under scratch\draw-test), make it writable, run Draw.exe fix-loose with a PDF, log to OUT/NAME.log,
# close all. The copies point at their models on P:\ (opened read-only). Needs the Draw round lock.
B='\'
SRC="$1"; OUT="$2"; PARTS="$3"; shift 3
mkdir -p "$OUT"; W=$(cygpath -w "$OUT")
cd /c/code/pdm/draw/src
for n in "$@"; do
  if [ -f "$OUT/$n.SLDDRW" ]; then echo "$n: already in $OUT, skipped"; continue; fi
  cp "$SRC/$n.SLDDRW" "$OUT/$n.SLDDRW"; attrib -R "${W}${B}${n}.SLDDRW"
  bin/Draw.exe fix-loose "${W}${B}${n}.SLDDRW" "${W}${B}${n}.pdf" "--parts=$PARTS" > "$OUT/$n.log" 2>&1
  rc=$?; bin/Draw.exe close-all > /dev/null 2>&1
  echo "$n: rc $rc | $(grep '^Done:' "$OUT/$n.log")"
done
