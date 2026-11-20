#!/bin/bash
# james_20nov.sh : after the "learned from James" change (Grant's yes 20 Nov). Needs the Draw round lock.
# 1. Regression: the approved plates and brackets (regress-17nov-c.tsv) and members (regress-members.tsv) remade
#    into regress-20nov and regress-20nov-members, compared with the 17 Nov runs by pdf_diff.py.
# 2. Trials of the new behaviour into james-20nov: two subframes (make-assembly) and two short members
#    (make-section), each also with --classic for a side-by-side.
B='\'
R=/c/code/pdm/scratch/draw-test
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX"
T="C:${B}code${B}pdm${B}scratch${B}draw-test${B}_Templates${B}Unilumin-A3.DRWDOT"
cd /c/code/pdm/draw/src
step=${1:-all}

if [ "$step" = all ] || [ "$step" = plates ]; then
  bash /c/code/pdm/draw/scripts/regress.sh "$R/regress-17nov-c.tsv" "$R/regress-20nov" > "$R/regress-20nov.out" 2>&1
  python /c/code/pdm/draw/scripts/pdf_diff.py "$R/regress-17nov-c" "$R/regress-20nov" > "$R/regress-20nov.diff" 2>&1
fi

if [ "$step" = all ] || [ "$step" = members ]; then
  OUT="$R/regress-20nov-members"; mkdir -p "$OUT"; OWO=$(cygpath -w "$OUT")
  sed 's/\r$//' /c/code/pdm/draw/scripts/regress-members.tsv | while IFS=$'\t' read -r pn file cfg folder batch; do
    rm -f "$OUT/$pn.SLDDRW" "$OUT/$pn.pdf"
    bin/Draw.exe make-section "${OW}${B}${folder}${B}${file}" "$cfg" "${OWO}${B}${pn}.SLDDRW" "$T" > "$OUT/$pn.log" 2>&1
    echo "$pn rc $?"; bin/Draw.exe close-mine > /dev/null 2>&1
  done > "$R/regress-20nov-members.out" 2>&1
  python /c/code/pdm/draw/scripts/pdf_diff.py "$R/regress-17nov-members2" "$R/regress-20nov-members" > "$R/regress-20nov-members.diff" 2>&1
fi

if [ "$step" = all ] || [ "$step" = new ]; then
  for mode in new classic; do
    OUT="$R/james-20nov/$mode"; mkdir -p "$OUT"; OWO=$(cygpath -w "$OUT")
    flag=""; [ "$mode" = classic ] && flag="--classic"
    while IFS=$'\t' read -r cmd pn file cfg folder; do
      rm -f "$OUT/$pn.SLDDRW" "$OUT/$pn.pdf" "$OUT/$pn.checks.txt"
      bin/Draw.exe $cmd "${OW}${B}${folder}${B}${file}" "$cfg" "${OWO}${B}${pn}.SLDDRW" "$T" $flag > "$OUT/$pn.log" 2>&1
      echo "$mode $pn rc $?"; bin/Draw.exe close-mine > /dev/null 2>&1
    done <<'EOF'
make-section	08C04-HBP-01	08C04-HBP-01.SLDPRT	Default<As Machined>	SC08\LS-08X-04\ShaftC
make-section	08C04-HBP-02	08C04-HBP-02.SLDPRT	Default<As Machined>	SC08\LS-08X-04\ShaftC
make-assembly	08A04-SFA-5B	08A04-SFA-5B.SLDASM	Default	SC08\LS-08X-04\ShaftA
make-assembly	08C04-SFA-01	08C04-SFA-01.SLDASM	Default	SC08\LS-08X-04\ShaftC
EOF
  done > "$R/james-20nov.out" 2>&1
fi
echo done
