r"""Description, Finish and Weight for the files an import brings in, set on the Pack and Go copies before load
(0804 D10 / R10, agreed by Grant 7 Nov). Read-only except for the files it writes (.xlsx, .tsv).

  sheet:   python -I props_pack.py sheet <out.xlsx> <shaft>=<probeDir>,<planBase> [...]
  actions: python -I props_pack.py actions <sheet.xlsx> <outDir> <shaft>=<probeDir> [...]

<probeDir> is the DmProbe of the pack (e.g. scratch\inventory\LS-08A-04-r1); <planBase> is the exported plan without
its suffix (scratch\import\LS-08A-04-r1-import-plan, giving -files.csv and -partnos.csv). Shafts in order, e.g. A, B, C.

Rows: every configuration with a PartNo on a file the plan imports (files reused from the vault or from another shaft
get nothing here: their own pack carries them). Following J2 / J3 (28 Oct): Description and Weight per configuration,
Weight = "SW-Mass@@<config>@<file>" (the importer rewrites the file name when it renames); Finish once per file.

Description proposals, first that applies:
  1 the configuration's own Description (James), kept;
  2 the Description of another configuration of the same file with the same PartNo (As Welded children);
  3 the file-level Description (James), copied down;
  4 the same number in an earlier shaft (08A04-SFA-2T for 08B04-SFA-2T), with the screen and shaft swapped;
  5 a pattern: SFA "LS-08x-04 SUB FRAME n TOP|BOTTOM ASSY", K-beam "LS-08x-04 K-BEAM [Sn] ASSY" / "K-BEAM Sn";
  6 the file name, cleaned (marked "check").
Finish by R10: SFA "HDG & PAINTED"; loose bolt-on parts "HDG" (a part none of whose assemblies here is welded);
weldments (W), welded-in parts (in an SFA or W), K-beam by others (KB) and STAs blank. Anything R10 doesn't settle
(other assemblies, parts in them) is "?" for Grant. Where R10 clears a value James set, the row says so.

actions: reads the (reviewed) Proposed columns and a fresh probe of each pack, and writes <outDir>\<shaft>-actions.tsv
for DmEdit batch with root = the pack folder. Only differences are written; each line carries the value the probe saw.
A "?" anywhere stops it. Blank proposed Finish where the pack has one = delete (R10).
"""
import collections, csv, os, re, sys
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment
from openpyxl.utils import get_column_letter

def rd(path):
    return list(csv.DictReader(open(path, encoding="utf-8-sig")))

def load_probe(d):
    files = {f["file"].lower(): f for f in rd(os.path.join(d, "files.csv"))}
    props = {}
    for p in rd(os.path.join(d, "properties.csv")):
        if p["name"]:
            props[(p["file"].lower(), p["config"], p["name"].lower())] = (p["expression"] or p["value"] or "").strip()
    comps = [c for c in rd(os.path.join(d, "components.csv")) if c["suppressed"] != "yes"]
    return files, props, comps

def weight_formula(f, cfg):
    return '"SW-Mass@@%s@%s"' % (cfg, f)

def parse_pn(pn):
    m = re.match(r"^(\w+?)-([A-Z]{2})([AWP])-(\w+)$", pn or "")
    return (m.group(1), m.group(2), m.group(3), m.group(4)) if m else (None, None, None, None)

COLS = ["Shaft", "PartNo", "File", "Configuration", "New name", "Pack folder", "Kind",
        "Current Description", "Proposed Description", "Description from",
        "Current Finish (file level)", "Proposed Finish (file level)", "Finish rule",
        "Current Weight", "Proposed Weight", "Notes", "Comment"]

def sheet(out, specs):
    rows, earlier = [], {}      # earlier: PartNo with the shaft letter blanked -> (shaft, description)
    for shaft, probe, base in specs:
        files, props, comps = load_probe(probe)
        plan = {r["source"].split("\\")[-1].lower(): r for r in rd(base + "-files.csv")}
        pns = rd(base + "-partnos.csv")
        pn_of = collections.defaultdict(set)                      # file -> its PartNos (any config)
        for r in pns:
            pn_of[r["file"].lower()].add(r["partno"])
        parents = collections.defaultdict(set)
        for c in comps:
            parents[c["component_file"].lower()].add(c["assembly"].lower())
        screen = "LS-08%s-04" % shaft
        g = lambda f, c, n: props.get((f.lower(), c, n), "")

        def welded(fk):
            """'yes' if the file is a weldment or sits in one, 'no' if every assembly it is in is unwelded, '?' else."""
            kinds = set()
            for a in parents.get(fk, ()):
                ts = {parse_pn(p)[1:3] for p in pn_of.get(a, ())} or {(None, None)}
                for cat, typ in ts:
                    if typ == "W" or (cat == "SF" and typ == "A"): kinds.add("welded")
                    elif typ is None or cat in ("KB",): kinds.add("plain")     # screen files, bolt kits, K-beam assemblies
                    else: kinds.add("other:" + cat + typ)
            if not kinds: return "?", "used only through part links, in no assembly here"
            if "welded" in kinds: return "yes", "in a welded assembly"
            if kinds and kinds <= {"plain"}: return "no", "only in unwelded assemblies"
            return "?", "in " + ", ".join(sorted(k[6:] for k in kinds if k.startswith("other:"))) + " (not settled by R10)"

        by_file = collections.defaultdict(list)
        for r in pns:
            fk = r["file"].lower()
            act = plan.get(fk, {}).get("action", "")
            if not act.startswith("import"):
                continue
            by_file[fk].append(r)
        for fk, frs in sorted(by_file.items(), key=lambda x: sorted(r["partno"] for r in x[1])[0]):
            f0 = frs[0]["file"]
            pl = plan[fk]
            folder = files[fk]["folder"] if fk in files else ""
            cur_fin = g(f0, "(file)", "finish")
            place, cat, typ, num = parse_pn(sorted(r["partno"] for r in frs)[0])
            # Finish for the file
            if cat == "SF" and typ == "A": fin, frule = "HDG & PAINTED", "R10: SFA"
            elif typ == "W": fin, frule = "", "R10: weldment, blank"
            elif cat == "KB": fin, frule = "", "R10: K-beam by others, blank"
            elif cat == "ST": fin, frule = "", "R10: STA, blank"
            elif typ == "P":
                w, why = welded(fk)
                fin, frule = {"yes": ("", "R10: welded-in part, blank (" + why + ")"),
                              "no": ("HDG", "R10: loose bolt-on part (" + why + ")"),
                              "?": ("?", "not settled: part " + why)}[w]
            else: fin, frule = "?", "not settled by R10: %s%s assembly" % (cat, typ)
            for r in sorted(frs, key=lambda r: r["config"]):
                cfg, pn = r["config"], r["partno"]
                notes = []
                cur_d = g(f0, cfg, "description")
                same = [g(f0, x["config"], "description") for x in frs if x["partno"] == pn and x["config"] != cfg]
                same = [s for s in same if s]
                file_d = g(f0, "(file)", "description")
                p2, c2, t2, n2 = parse_pn(pn)
                ekey = (re.sub(r"^(\d\d)[ABC](\d\d)$", r"\1?\2", p2 or ""), c2, t2, n2)
                if cur_d: d, src = cur_d, "James (configuration)"
                elif same: d, src = same[0], "same PartNo, other configuration"
                elif file_d: d, src = file_d, "James (file level), copied down"
                elif ekey in earlier and p2 and re.match(r"^\d\d[ABC]\d\d$", p2):
                    es, ed = earlier[ekey]
                    d = ed.replace("LS-08%s-04" % es, screen).replace("SHAFT " + es, "SHAFT " + shaft)
                    src = "same PartNo, other configuration" if es == shaft else "earlier shaft (as %s)" % (re.sub(r"\?", es, ekey[0]) + "-" + c2 + t2 + "-" + n2)
                else:
                    m = re.match(r"^(\d)([TB])$", num or "")
                    s_ = re.search(r"KBEAM_S(\d)", f0, re.I)
                    if cat == "SF" and typ == "A" and m:
                        d, src = "%s SUB FRAME %s %s ASSY" % (screen, m.group(1), "TOP" if m.group(2) == "T" else "BOTTOM"), "pattern (SFA)"
                    elif cat == "SF" and typ == "A" and re.match(r"^\d[X]$", num or ""):
                        d, src = "%s SUB FRAME %s ASSY" % (screen, num[0]), "pattern (SFA)"
                    elif cat == "BR" and re.match(r"^TIE-(\d+)", cfg):
                        d, src = "TIE x%smm" % re.match(r"^TIE-(\d+)", cfg).group(1), "configuration name: check (profile not known)"
                    elif cat == "SF" and typ == "A" and re.match(r"^\d\d?$", num or ""):
                        d, src = "%s SUB FRAME %d ASSY" % (screen, int(num)), "pattern (SFA)"
                    elif cat == "KB" and typ == "A":
                        d, src = ("%s K-BEAM S%s ASSY" % (screen, s_.group(1)) if s_ else "%s K-BEAM ASSY" % screen), "pattern (K-beam, by others)"
                    elif cat == "KB" and typ == "P" and s_:
                        d, src = "%s K-BEAM S%s" % (screen, s_.group(1)), "pattern (K-beam, by others)"
                    else:
                        n_ = os.path.splitext(f0)[0]
                        n_ = re.sub(r"^LS[-_]0?[8B][A-Z]{0,2}[-_]0?4[-_ ]*", "", n_, flags=re.I)
                        n_ = re.sub(r"v\d+$", "", n_).replace("_", " ").strip().upper()
                        n_ = re.sub(r"(?<=\d)X(?=\d)", "x", n_)
                        d, src = n_, "file name: check"
                if p2 and re.match(r"^\d\d[ABC]\d\d$", p2) and ekey not in earlier:
                    earlier[ekey] = (shaft, d)
                wf = weight_formula(f0, cfg)
                cur_w = g(f0, cfg, "weight")
                if cur_w and cur_w != wf:
                    notes.append("Weight changes (was %s)" % cur_w)
                if cur_fin and fin != "?" and cur_fin.upper() != fin.upper():
                    notes.append("R10 %s James's Finish %r" % ("clears" if fin == "" else "replaces", cur_fin))
                if src.startswith(("file name", "configuration name")) or fin == "?":
                    notes.append("for Grant")
                rows.append([shaft, pn, f0, cfg, pl.get("new_name", ""), folder, cat and (cat + typ) or "",
                             cur_d, d, src, cur_fin, fin, frule, cur_w, wf, "; ".join(notes), ""])
    wb = openpyxl.Workbook()
    ws = wb.active; ws.title = "Properties"
    ws.append(COLS)
    for r in rows: ws.append(r)
    hl = PatternFill("solid", fgColor="FFF2CC")
    for i, r in enumerate(rows, start=2):
        if "for Grant" in r[15]:
            for c in range(1, len(COLS) + 1): ws.cell(i, c).fill = hl
    for c in range(1, len(COLS) + 1):
        ws.cell(1, c).font = Font(bold=True)
        ws.column_dimensions[get_column_letter(c)].width = {1: 6, 2: 15, 3: 40, 4: 28, 5: 28, 6: 30, 7: 6, 8: 34, 9: 40, 10: 28,
                                                            11: 16, 12: 16, 13: 40, 14: 30, 15: 30, 16: 40, 17: 20}.get(c, 18)
    ws.freeze_panes = "C2"; ws.auto_filter.ref = ws.dimensions
    cs = wb.create_sheet("Counts")
    cnt = collections.Counter((r[0], r[9].split(" (")[0].split(":")[0].split(",")[0]) for r in rows)
    fcnt = collections.Counter((r[0], r[11] or "(blank)") for r in {(r[0], r[2]): r for r in rows}.values())
    cs.append(["Shaft", "Description from", "Rows"])
    for k, v in sorted(cnt.items()): cs.append([k[0], k[1], v])
    cs.append([]); cs.append(["Shaft", "Finish (per file)", "Files"])
    for k, v in sorted(fcnt.items()): cs.append([k[0], k[1], v])
    wb.save(out)
    print("Rows:", len(rows), "| files:", len({(r[0], r[2]) for r in rows}), "| for Grant:", sum("for Grant" in r[15] for r in rows))
    for k, v in sorted(cnt.items()): print("  ", k, v)
    for k, v in sorted(fcnt.items()): print("  ", k, v)

def actions(sheet_path, outdir, specs):
    ws = openpyxl.load_workbook(sheet_path, read_only=True)["Properties"]
    it = ws.iter_rows(values_only=True); head = list(next(it)); col = {h: i for i, h in enumerate(head)}
    v = lambda r, h: ("" if r[col[h]] is None else str(r[col[h]])).strip()
    rows = [r for r in it if r and r[0]]
    bad = [(v(r, "Shaft"), v(r, "File"), v(r, "Configuration")) for r in rows
           if "?" in (v(r, "Proposed Finish (file level)"), v(r, "Proposed Description"), v(r, "Proposed Weight"))]
    if bad:
        sys.exit("Stopped: %d rows still have '?', e.g. %s" % (len(bad), bad[:3]))
    os.makedirs(outdir, exist_ok=True)
    for shaft, probe in specs:
        files, props, _ = load_probe(probe)
        acts, fin = [], collections.defaultdict(set)
        for r in rows:
            if v(r, "Shaft") != shaft: continue
            f, cfg = v(r, "File"), v(r, "Configuration")
            if f.lower() not in files: sys.exit("Not in the probe of %s: %s" % (shaft, f))
            rel = files[f.lower()]["folder"] + "\\" + files[f.lower()]["file"]
            for name, h in (("Description", "Proposed Description"), ("Weight", "Proposed Weight")):
                new, old = v(r, h), props.get((f.lower(), cfg, name.lower()), "")
                if new != old and new:
                    acts.append([rel, cfg, name, "set", new, old or "(none)"])
            fin[rel].add(v(r, "Proposed Finish (file level)"))
        for rel, vals in sorted(fin.items()):
            if len(vals) > 1: sys.exit("Rows of %s disagree on Finish: %s" % (rel, vals))
            new = vals.pop(); fk = rel.split("\\")[-1].lower(); old = props.get((fk, "(file)", "finish"), "")
            if new != old:
                acts.append([rel, "@file", "Finish", "set" if new else "del", new, old or "(none)"])
        out = os.path.join(outdir, "%s-actions.tsv" % shaft)
        with open(out, "w", encoding="utf-8", newline="") as fh:
            fh.write("file\tlevel\tname\top\tvalue\told\r\n")
            for a in acts: fh.write("\t".join(a) + "\r\n")
        c = collections.Counter((a[2], a[3]) for a in acts)
        print(shaft, out, len(acts), "changes in", len({a[0] for a in acts}), "files:", dict(c))

if __name__ == "__main__":
    a = sys.argv[1:]
    if len(a) >= 3 and a[0] == "sheet":
        sheet(a[1], [(s.split("=", 1)[0],) + tuple(s.split("=", 1)[1].split(",", 1)) for s in a[2:]])
    elif len(a) >= 4 and a[0] == "actions":
        actions(a[1], a[2], [tuple(s.split("=", 1)) for s in a[3:]])
    else:
        sys.exit(__doc__)
