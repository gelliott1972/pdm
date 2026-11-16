#!/bin/bash
# bodies.sh LIST PATTERN: SwCmd bodies for each row of an overnight list (PartNo, file, config, folder under APEX) whose PartNo matches PATTERN.
O=/c/code/pdm/scratch/draw-test/overnight/APEX
C=/c/code/pdm/tools/SwCmd/bin/SwCmd.exe
sed 's/\r$//' "$1" | grep -E "$2" | while IFS=$'\t' read -r pn f cfg dir; do
  :
  d=${dir//\\//}
  echo "=== $pn ($f / $cfg)"
  "$C" bodies "$O/$d/$f" --config "$cfg" --as draw 2>&1 | grep -v "^Attached" | head -14
done
