#!/bin/bash
# try_routes.sh LIST OUTDIR : per row (PartNo, file, config, folder under APEX): make-section, then make-plate, then
# make-basic, into OUTDIR (a test folder; nothing in the overnight or load folders is written). Prints the checks.
LIST=$1; OUT=$2
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
mkdir -p "$OUT"; OW_OUT=$(cygpath -w "$OUT")
cd /c/code/pdm/draw/src
sed 's/\r$//' "$LIST" | while IFS=$'\t' read -r pn file cfg folder rest; do
  rm -f "${OUT:?}/${pn:?}.SLDDRW" "${OUT:?}/${pn:?}.pdf"
  out="${OW_OUT}${B}${pn}.SLDDRW"; model="${OW}${B}${folder}${B}${file}"
  echo "=================== $pn"
  for route in make-section make-plate make-basic; do
    bin/Draw.exe $route "$model" "$cfg" "$out" "$T" > "$OUT/$pn.$route.log" 2>&1
    bin/Draw.exe close-mine > /dev/null 2>&1
    if [ -f "$OUT/$pn.SLDDRW" ]; then
      echo "$route: saved"; grep -E "^Check|^Flag|dangling|removed|not on the drawing|^Dimension|width on|height on" "$OUT/$pn.$route.log"; break
    fi
    echo "$route: $( (grep -E 'REFUSED|NOT saved' "$OUT/$pn.$route.log"; grep -A4 '^PROBLEMS' "$OUT/$pn.$route.log") | tr '\n' ';' | cut -c1-220)"
    rm -f "${OUT:?}/${pn:?}.pdf"
  done
done
