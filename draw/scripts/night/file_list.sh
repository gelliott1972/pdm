# file_list.sh LIST BATCH : file_draft.sh for each row (PartNo, folder), results to night-1 results.tsv
S=${S:-/c/code/pdm/scratch/draw-test/night-logs}
while IFS=$'\t' read -r pn folder; do bash "$(dirname "$0")/file_draft.sh" "$2" "$pn" "$folder" >> $S/filed-${3:-$2}.tsv 2>&1; done < "$1"
echo DONE >> $S/filed-${3:-$2}.tsv
