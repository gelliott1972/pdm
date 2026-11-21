#!/bin/bash
# james_three.sh MODELROOT : the three trial drafts for the learned-from-James lessons (04d9b26), new behaviour only.
# Grant 21 Nov: as few drawings as possible. Needs the Draw round lock. MODELROOT is a Windows path to a copy of
# APEX (after Get), e.g. C:\code\pdm\scratch\draw-test\get-21nov\APEX
B='\'
R=/c/code/pdm/scratch/draw-test
OW="$1"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
cd /c/code/pdm/draw/src
OUT="$R/james-21nov"; mkdir -p "$OUT"; OWO=$(cygpath -w "$OUT")
while IFS=$'\t' read -r cmd pn file cfg folder; do
  bin/Draw.exe $cmd "${OW}${B}${folder}${B}${file}" "$cfg" "${OWO}${B}${pn}.SLDDRW" "$T" > "$OUT/$pn.log" 2>&1
  echo "$pn rc $?"; bin/Draw.exe close-mine > /dev/null 2>&1
done <<'EOF'
make-section	08C04-HBP-01	08C04-HBP-01.SLDPRT	Default<As Machined>	SC08\LS-08X-04\ShaftC
make-assembly	08A04-SFA-5B	08A04-SFA-5B.SLDASM	Default	SC08\LS-08X-04\ShaftA
make-assembly	08C04-SFA-01	08C04-SFA-01.SLDASM	Default	SC08\LS-08X-04\ShaftC
EOF
echo done
