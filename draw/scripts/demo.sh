#!/bin/bash
# demo.sh : a show run for Grant's colleagues (16 Nov). Five approved plates drafted from scratch into a throw-away
# folder (scratch\draw-test\demo-16nov), one after another, each left on screen for a few seconds before closing.
# Nothing in the vault, the load folders or the overnight drafts is touched.
D=/c/code/pdm/scratch/draw-test/demo-16nov
mkdir -p "$D"
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX${B}SC08${B}LS-08X-04${B}_Shared"
OUT="C:${B}code${B}pdm${B}scratch${B}draw-test${B}demo-16nov"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
cd /c/code/pdm/draw/src
n=0
while IFS=$'\t' read -r pn file cfg; do
  n=$((n+1))
  rm -f "${D:?}/${pn:?}.SLDDRW" "${D:?}/${pn:?}.pdf"
  echo "[$n/5] $pn: opening the model, laying out the sheet, dimensioning, checking every value against the model"
  bin/Draw.exe make-plate "${OW}${B}${file}" "$cfg" "${OUT}${B}${pn}.SLDDRW" "$T" > "$D/$pn.log" 2>&1
  grep -E "^Check|dangling|^Saved|Time:" "$D/$pn.log" | sed 's/^/      /'
  sleep 8
  bin/Draw.exe close-mine > /dev/null 2>&1
done <<'EOF'
08X04-PLP-01	08X04-PLP-01.SLDPRT	Default
08X04-PLP-02	08X04-PLP-02.SLDPRT	Default
08X04-PLP-03	08X04-PLP-03.SLDPRT	PLATE ONLY
08X04-PLP-32	08X04-PLP.SLDPRT	180x140x10_SLOTS_SHORT_AXIS
08X04-PLP-33	08X04-PLP.SLDPRT	180x140x10_SLOTS_LONG_AXIS
EOF
echo "Demo done: 5 drafts in $D (temporary)"
