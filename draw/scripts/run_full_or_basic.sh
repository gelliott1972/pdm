#!/bin/bash
# run_full_or_basic.sh LIST TAG : per row (PartNo, file, config, folder under APEX): the full route first (as run_day:
# make-section / make-plate / make-assembly); if that saves no drawing, make-basic. Stops between drawings when
# $S/TAG/STOP exists. Writes $S/TAG/result.tsv: PartNo, folder, full|basic|none, last problem line.
S=${S:-/c/code/pdm/scratch/draw-test/night-logs}
LIST=$1; TAG=$2
mkdir -p "$S/$TAG"; : > "$S/$TAG/result.tsv"
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
sed 's/\r$//' "$LIST" | while IFS=$'\t' read -r pn file cfg folder; do
  if [ -f "$S/$TAG/STOP" ]; then echo "STOPPED before $pn" >> "$S/$TAG/result.tsv"; break; fi
  out="${OW}${B}${folder}${B}${pn}.SLDDRW"; outu=$(cygpath -u "$out")
  if [ -f "$outu" ]; then printf '%s\t%s\texists\t\n' "$pn" "$folder" >> "$S/$TAG/result.tsv"; continue; fi
  printf '%s\t%s\t%s\t%s\n' "$pn" "$file" "$cfg" "$folder" > "$S/$TAG/one.tsv"
  S="$S" bash /c/code/pdm/draw/scripts/run_day.sh "$S/$TAG/one.tsv" "$TAG" > /dev/null 2>&1
  rm -f "$S/$TAG/STOP.tmp"
  if [ -f "$outu" ]; then
    printf '%s\t%s\tfull\t%s\n' "$pn" "$folder" "$( (grep -A6 '^PROBLEMS' "$S/$TAG/$pn.txt" | grep -v '^PROBLEMS') | tr '\n' ';' | cut -c1-200)" >> "$S/$TAG/result.tsv"
    continue
  fi
  pdfu="${outu%.SLDDRW}.pdf"; [ -f "$pdfu" ] && mv "$pdfu" "$pdfu.failed"
  (cd /c/code/pdm/draw/src && bin/Draw.exe make-basic "${OW}${B}${folder}${B}${file}" "$cfg" "$out" "$T" > "$S/$TAG/$pn.basic.txt" 2>&1; bin/Draw.exe close-mine > /dev/null 2>&1)
  if [ -f "$outu" ]; then r=basic; else r=none; fi
  printf '%s\t%s\t%s\t%s\n' "$pn" "$folder" "$r" "$( (grep -h -E 'REFUSED|NOT saved|PROBLEMS' -A3 "$S/$TAG/$pn.basic.txt" "$S/$TAG/$pn.txt" 2>/dev/null) | tr '\n' ';' | cut -c1-200)" >> "$S/$TAG/result.tsv"
done
