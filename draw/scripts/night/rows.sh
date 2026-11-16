# rows.sh CHECKFILE NOTESFILE : one markdown row per filed drawing (PartNo, drawing path, model refs, check result, other CHECK notes)
# NOTESFILE: PartNo<TAB>note (optional rows; "none" when absent)
L=/c/code/pdm/scratch/draw-test/load/night-1
DM=/c/code/pdm/tools/DmEdit/bin/DmEdit.exe
sort "$1" | while IFS=$'\t' read -r pn chk; do
  u=$(find $L -name "$pn.SLDDRW" | head -1)
  rel=$(cygpath -w "$u"); rel=${rel#*night-1\\}
  m=$($DM refs "$(cygpath -w "$u")" < /dev/null | awk -F'\t' '$1=="reference"{n=split($2,a,"\\");print a[n]}' | tr -d '\r' | sort -u | tr '\n' ' ')
  x=$(awk -F'\t' -v p="$pn" '$1==p{print $2}' "$2" 2>/dev/null); [ -z "$x" ] && x="none"
  echo "| $pn | $rel | $m| ${chk%%;*} | $x |"
done
