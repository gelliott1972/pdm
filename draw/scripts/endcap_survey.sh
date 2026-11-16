#!/bin/bash
# endcap_survey.sh : for every part in the overnight model copies, the ACTIVE configuration's cut list and bodies
# (SwCmd, read-only), into scratch\draw-test\endcaps\survey.txt. Used to list end caps whose cut-list item has no
# Description (Grant 16 Nov: "END CAP 72x32x5", set in the model by SW).
O=/c/code/pdm/scratch/draw-test/overnight/APEX
E=/c/code/pdm/scratch/draw-test/endcaps
S=/c/code/pdm/tools/SwCmd/bin/SwCmd.exe
: > "$E/survey.txt"
while read -r f; do
  w=$(cygpath -w "$O/$f")
  echo "PART	$f" >> "$E/survey.txt"
  cl=$("$S" cut-list "$w" --as draw 2>&1)
  echo "$cl" | grep -E "^Configuration|^  [^ ]|^      body|Description =" >> "$E/survey.txt"
  cfg=$(echo "$cl" | grep -m1 "^Configuration: " | sed 's/^Configuration: //; s/\r$//')
  if echo "$cl" | grep -qi "body"; then "$S" bodies "$w" --config "$cfg" --as draw 2>&1 | grep -E "^BODY" >> "$E/survey.txt"; fi
done < "$E/parts.txt"
echo SURVEY DONE >> "$E/survey.txt"
