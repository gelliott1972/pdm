# run_night.sh LIST TAG : one draft per row (PartNo, file, config, folder under APEX) into the overnight copies.
# Assemblies -> make-assembly; parts -> make-section (members, angles, welded members), then make-plate if that refuses.
S=${S:-/c/code/pdm/scratch/draw-test/night-logs}
LIST=$1; TAG=$2
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
cd /c/code/pdm/draw/src; mkdir -p $S/$TAG
while IFS=$'\t' read -r pn file cfg folder; do
  dir="${OW}${B}${folder}"; out="${dir}${B}${pn}.SLDDRW"
  start=$(date +%s); route=""
  if [ -f "$(cygpath -u "$out")" ]; then printf '%s\tskipped (draft exists)\n' "$pn" >> $S/$TAG/log.tsv; continue; fi
  case "$file" in
    *.SLDASM) route=assembly; bin/Draw.exe make-assembly "${dir}${B}${file}" "$cfg" "$out" "$T" > "$S/$TAG/$pn.txt" 2>&1; rc=$? ;;
    *) route=section; bin/Draw.exe make-section "${dir}${B}${file}" "$cfg" "$out" "$T" > "$S/$TAG/$pn.txt" 2>&1; rc=$?
       if [ $rc -ne 0 ] && [ ! -f "$(cygpath -u "$out")" ] && grep -q "REFUSED" "$S/$TAG/$pn.txt"; then
         bin/Draw.exe close-mine > /dev/null 2>&1
         mv "$S/$TAG/$pn.txt" "$S/$TAG/$pn.section.txt"
         route=plate; bin/Draw.exe make-plate "${dir}${B}${file}" "$cfg" "$out" "$T" > "$S/$TAG/$pn.txt" 2>&1; rc=$?
       fi ;;
  esac
  bin/Draw.exe close-mine > /dev/null 2>&1
  printf '%s\t%s\trc=%s\t%ss\t%s\n' "$pn" "$route" "$rc" "$(( $(date +%s) - start ))" "$( (grep -A8 '^PROBLEMS' "$S/$TAG/$pn.txt"; grep '^REFUSED' "$S/$TAG/$pn.txt") | grep -v '^PROBLEMS' | tr '\n' ';' | cut -c1-300)" >> $S/$TAG/log.tsv
done < "$LIST"
echo DONE >> $S/$TAG/log.tsv
