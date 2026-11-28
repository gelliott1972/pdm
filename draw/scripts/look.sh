#!/bin/bash
# look.sh FOLDER NAME... : read-only look at scratch copies of drawings. For each NAME (no extension) in FOLDER
# (a folder under scratch\draw-test holding NAME.SLDDRW): Draw.exe inspect to NAME.txt, PDF to NAME.pdf, close all.
# The copies point at their models on P:\, which SOLIDWORKS opens read-only. Needs the Draw round lock.
B='\'
F="$1"; shift
W=$(cygpath -w "$F")
cd /c/code/pdm/draw/src
for n in "$@"; do
  bin/Draw.exe inspect "${W}${B}${n}.SLDDRW" > "$F/$n.txt" 2>&1
  [ -f "$F/$n.pdf" ] || bin/Draw.exe pdf "${W}${B}${n}.SLDDRW" "${W}${B}${n}.pdf" >> "$F/$n.txt" 2>&1
  bin/Draw.exe close-all > /dev/null 2>&1
  echo "$n: $(grep -c DANGL "$F/$n.txt") dangling line(s), $(grep -c '^VIEW' "$F/$n.txt") views"
done
