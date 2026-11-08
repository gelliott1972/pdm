# Ledger rows for the 0804 restructure round (moves, the new STA-01s, the retired _WITH_SCREEN files).
# Usage: python -I make_ledger_rows.py <round.csv> <rename-ledger.csv> <out.csv> <date YYYY-MM-DD>
# Writes rows in the ledger's own columns to <out.csv>; appending them to the ledger is a separate, deliberate step.
import csv
import sys

BS = chr(92)
rnd, ledger, out, date = sys.argv[1:5]
led = list(csv.DictReader(open(ledger, encoding="utf-8-sig")))
cols = list(led[0].keys())


def screen_of(name, folder):
    """The screen of the last ledger row that put this file in this folder."""
    hits = [r["screen"] for r in led if r["new_file"].upper() == name.upper() and r["new_folder"].upper() == folder.upper()]
    if not hits:
        sys.exit("no ledger row for " + folder + BS + name)
    return hits[-1]


def split(p):
    i = p.rfind(BS)
    return p[:i], p[i + 1:]


rows = []
for r in csv.DictReader(open(rnd, encoding="utf-8-sig")):
    a = r["action"]
    if a in ("moved", "moved+changed"):
        of, name = split(r["old_path"])
        nf, nname = split(r["new_path"])
        assert name == nname
        why = r["why"].split(":")[0]
        rows.append(dict(date=date, screen=screen_of(name, of), old_file=name, old_config="", old_location=of,
                         new_file=name, new_config="", new_folder=nf, partno="",
                         decided_by=why + " (Grant 7 Nov)", source="restructure move"))
    elif a == "new":
        nf, name = split(r["new_path"])
        x = name[2]  # 08A04-STA-01 -> A
        top = "LS-08" + x + "-04+STRUCTURE.SLDASM"
        scr = "LS-08" + x + "-04"
        rows.append(dict(date=date, screen=scr, old_file=top, old_config="", old_location=nf, new_file=name, new_config="",
                         new_folder=nf, partno="", decided_by="S1 (Grant 7 Nov); D4", source="restructure (Form New Subassembly)"))
        rows.append(dict(date=date, screen=scr, old_file=name, old_config="Default", old_location=nf, new_file=name,
                         new_config="Default", new_folder=nf, partno=name.rsplit(".", 1)[0],
                         decided_by="S1; D4; as 0811 D22", source="Provisional"))
    elif a == "retired":
        of, name = split(r["old_path"])
        rows.append(dict(date=date, screen=screen_of(name, of), old_file=name, old_config="", old_location=of,
                         new_file="", new_config="", new_folder="", partno="",
                         decided_by="S2 (Grant 7 Nov)", source="retired (Remove from vault)"))

w = csv.DictWriter(open(out, "w", newline="", encoding="utf-8"), fieldnames=cols)
w.writeheader()
w.writerows(rows)
print(len(rows), "rows ->", out)
