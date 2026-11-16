#!/bin/bash
# run_day.sh LIST TAG : like night/run_night.sh, one draft per row, but stops between drawings when the file
# $S/TAG/STOP exists (so SOLIDWORKS can be handed to SW at once). Welded brackets with a plate (BKP-04/-05/-07)
# go straight to make-plate (the welded route); everything else as run_night (make-section, then make-plate).
S=${S:-/c/code/pdm/scratch/draw-test/night-logs}
LIST=$1; TAG=$2
mkdir -p "$S/$TAG"
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
tr -d '\r' < /c/code/pdm/draw/scripts/night/run_night.sh > "$S/$TAG/run_night.sh"
sed 's/\r$//' "$LIST" | while IFS=$'\t' read -r pn file cfg folder; do
  if [ -f "$S/$TAG/STOP" ]; then echo "STOPPED before $pn" >> "$S/$TAG/log.tsv"; break; fi
  case "$pn" in
    08X04-BKP-0[457])
      dir="${OW}${B}${folder}"; out="${dir}${B}${pn}.SLDDRW"; start=$(date +%s)
      (cd /c/code/pdm/draw/src && bin/Draw.exe make-plate "${dir}${B}${file}" "$cfg" "$out" "$T" > "$S/$TAG/$pn.txt" 2>&1; echo $? > "$S/$TAG/$pn.rc"; bin/Draw.exe close-mine > /dev/null 2>&1)
      printf '%s\tplate\trc=%s\t%ss\t%s\n' "$pn" "$(cat "$S/$TAG/$pn.rc")" "$(( $(date +%s) - start ))" "$( (grep -A8 '^PROBLEMS' "$S/$TAG/$pn.txt"; grep '^REFUSED' "$S/$TAG/$pn.txt") | grep -v '^PROBLEMS' | tr '\n' ';' | cut -c1-300)" >> "$S/$TAG/log.tsv" ;;
    *)
      printf '%s\t%s\t%s\t%s\n' "$pn" "$file" "$cfg" "$folder" > "$S/$TAG/one.tsv"
      S="$S" bash "$S/$TAG/run_night.sh" "$S/$TAG/one.tsv" "$TAG"
      sed -i '/^DONE$/d' "$S/$TAG/log.tsv" ;;
  esac
done
echo DONE >> "$S/$TAG/log.tsv"
