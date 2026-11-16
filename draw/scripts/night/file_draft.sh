# file_draft.sh BATCH PN FOLDER(under APEX, backslashes) : copy an overnight draft to load\BATCH (P:\ structure),
# repoint its references from the overnight copies to P:\, check it (opens P:\ read-only), PDF. Prints one result line.
set -u
b=$1; pn=$2; rel=$3
B='\'
OW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX${B}${rel}"
LW="C:${B}code${B}pdm${B}scratch${B}draw-test${B}load${B}${b}${B}APEX${B}${rel}"
PFX="C:${B}code${B}pdm${B}scratch${B}draw-test${B}overnight${B}APEX${B}"
DM=/c/code/pdm/tools/DmEdit/bin/DmEdit.exe; SW=/c/code/pdm/tools/SwCmd/bin/SwCmd.exe
mkdir -p "$(cygpath -u "$LW")"
cp "$(cygpath -u "${OW}${B}${pn}.SLDDRW")" "$(cygpath -u "$LW")/"
$DM refs "${LW}${B}${pn}.SLDDRW" 2>&1 | awk -F'\t' '$1=="reference"{print $2}' | while IFS= read -r r; do
  case "$r" in
    "$PFX"*) p="P:${B}APEX${B}${r#"$PFX"}"; $DM replace-ref "${LW}${B}${pn}.SLDDRW" "${r##*"$B"}" "$p" < /dev/null > /dev/null 2>&1 ;;
  esac
done
refs=$($DM refs "${LW}${B}${pn}.SLDDRW" 2>&1 | awk -F'\t' '$1=="reference"{print $2" "$4}' | tr '\n' ';')
cd /c/code/pdm/draw/src
chk=$(bin/Draw.exe check "${LW}${B}${pn}.SLDDRW" 2>&1 | tail -1)
bin/Draw.exe pdf "${LW}${B}${pn}.SLDDRW" "${LW}${B}${pn}.pdf" > /dev/null 2>&1
$SW close "${LW}${B}${pn}.SLDDRW" --as draw > /dev/null 2>&1; bin/Draw.exe close-mine > /dev/null 2>&1
printf '%s\t%s\t%s\n' "$pn" "$chk" "$refs"
