"""Turn a reviewed import plan workbook into the two action lists the importer reads.

Honours the green columns: 'Your choice', override name/folder on Files, override PartNo on PartNos.
Stops if any file still needs a decision (decide, needs number, split family).

Output, next to the workbook:
  <plan>-files.csv    source (relative to the Pack and Go root), action (import | leave out), new name, new folder
  <plan>-partnos.csv  source file, configuration, level (config | file), PartNo, status (Provisional or blank), from

Usage: python -I export_actions.py <plan.xlsx>
"""
import csv, os, sys
import openpyxl

plan = sys.argv[1]
base = os.path.splitext(plan)[0]
wb = openpyxl.load_workbook(plan, read_only=True, data_only=True)

def rows(name):
    it = wb[name].iter_rows(values_only=True)
    header = next(it)
    return [dict(zip(header, r)) for r in it]

def text(v):
    return "" if v is None else str(v).strip()

problems, files_out, kept = [], [], set()
for r in rows("Files"):
    name, choice = text(r["File"]), text(r["Your choice"]).lower()
    action = text(r["Proposed action"])
    new_name = text(r["Override name"]) or text(r["Proposed new name"])
    new_folder = text(r["Override folder"]) or text(r["Proposed new folder (under P:\\)"])
    if choice in ("", "accept"):
        pass
    elif choice in ("import", "leave out"):
        action = choice
    else:
        problems.append(f"{name}: choice '{choice}' isn't supported by the importer yet")
        continue
    if action == "leave out":
        files_out.append([os.path.join(text(r["Inbox folder"]), name), "leave out", "", ""])
        continue
    if not action.startswith("import"):
        problems.append(f"{name}: still '{action}'")
        continue
    if not new_name or not new_folder or "{" in new_name:
        problems.append(f"{name}: no complete new name or folder")
        continue
    if not os.path.splitext(new_name)[1]:
        new_name += os.path.splitext(name)[1].upper()
    files_out.append([os.path.join(text(r["Inbox folder"]), name), "import", new_name, new_folder])
    kept.add(name.lower())

pn_out = []
for r in rows("PartNos"):
    f, cfg, level = text(r["File"]), text(r["Configuration"]), text(r["Level"])
    if f.lower() not in kept or not level:
        continue
    choice = text(r["Your choice"]).lower()
    pn = {"": r["Proposed PartNo"], "proposed": r["Proposed PartNo"], "file": r["PartNo in file"],
          "register": r["PartNo in register"], "override": r["Override PartNo"]}.get(choice)
    pn = text(pn)
    if not pn:
        continue
    status = text(r["Status"]) if choice in ("", "proposed") else ""
    pn_out.append([f, cfg, level, pn, status, text(r["From"]) if choice in ("", "proposed") else "Grant (plan review)"])

if problems:
    print("Not exported, the plan still needs decisions:")
    for p in problems:
        print("  " + p)
    sys.exit(1)

def save(path, header, data):
    with open(path, "w", newline="", encoding="utf-8-sig") as fh:
        w = csv.writer(fh)
        w.writerow(header)
        w.writerows(data)

save(base + "-files.csv", ["source", "action", "new_name", "new_folder"], files_out)
save(base + "-partnos.csv", ["file", "config", "level", "partno", "status", "from"], pn_out)
print(f"Files: {sum(1 for x in files_out if x[1] == 'import')} to import, {sum(1 for x in files_out if x[1] == 'leave out')} left out")
print(f"PartNos: {len(pn_out)} ({sum(1 for x in pn_out if x[4] == 'Provisional')} provisional)")
print("Saved:", base + "-files.csv", base + "-partnos.csv")
