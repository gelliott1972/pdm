"""Build an import plan workbook for one screen, for Grant to review before anything is renamed.

Inputs:  the DmProbe inventory CSVs for the screen, and the numbering register (Rev 3 workbook).
Output:  <screen>-import-plan.xlsx with three sheets:
           Files    - one row per file: class, proposed action, new name, new folder, flags, choice/override
           PartNos  - one row per configuration that gets a PartNo: file value, register value, proposed, choice/override
           Issues   - every flag in one list
Nothing here touches SOLIDWORKS files. The import step reads the reviewed workbook later.

Usage: python -I make_plan.py <inventoryDir> <registerXlsx> <outXlsx> [<ledgerCsv>] [--with <inventoryDir> <files.csv>] ...
  --with: another screen planned in the same pass (0811 D19: B and C with A); may be given more than once. Files
          that screen's exported plan (files.csv) imports are reused here, not imported again; their configurations
          and PartNos here are compared with that screen's probe and any difference is flagged.
Sheet "Vault changes": configurations of files already in the vault that a file this plan imports refers to and
that have no PartNo in the ledger. Each is a change to an existing vault file (check-out, write, check-in), not part of the import.
"""
import csv, collections, os, re, sys
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment
from openpyxl.worksheet.datavalidation import DataValidation

args = sys.argv[1:]
withs = []
while "--with" in args:
    i = args.index("--with")
    withs.append((args[i + 1], args[i + 2]))
    del args[i:i + 3]
inv, register_path, out_path = args[0], args[1], args[2]
ledger_path = args[3] if len(args) > 3 else None
screen = re.sub(r"-r\d+$", "", os.path.basename(os.path.normpath(inv)))   # LS-08A-06-r2 -> LS-08A-06

# Screens whose screen file holds several screens as configurations, decided screen by screen (not a rule).
SCREEN_FILE = {
    "LS-08A-06": "LS-08X-06",    # configurations LS-08A-06 and LS-08BC-06 (Grant, 19 Oct 2026)
    "LS-08BC-06": "LS-08X-06",
    "LS-08A-11": "LS-08X-11",    # configurations LS-08A-11 and LS-08BC-11 (Grant, 20 Oct 2026)
    "LS-08BC-11": "LS-08X-11",
}

def SHAFT_0811(x, sf):
    # 0811 D8 and D9, the same for each shaft: James's numbers, Provisional (docs\imports\LS-08X-11-decisions.md).
    # sf: place letter of SF01-LOWER's numbers (D20: Z in B), or None where that file isn't imported (C).
    d = {
        (f"LS-08{x}-11_TIE_BEAM_2.SLDPRT", "Default<As Machined>"): (f"08{x}11-HBP-02", True, "D9 (James's number; P checked by SW)"),
        (f"LS-08{x}-11_TIE_BEAM_3.SLDPRT", "Default<As Machined>"): (f"08{x}11-HBW-03", True, "D9 (James's number; W checked by SW)"),
    }
    if sf:
        src = "D8 (James's number)" + (", D20 (shared by B and C)" if sf == "Z" else "")
        d[(f"LS-08{x}-11_SF01-LOWER.SLDASM", "PART 1")] = (f"08{sf}11-SFA-1A", True, src)
        d[(f"LS-08{x}-11_SF01-LOWER.SLDASM", "PART 2")] = (f"08{sf}11-SFA-1B", True, src)
    return d

# 0811 D20: B's five subframes that are identical in C become 08Z11 files (folder ShaftBC); C's copies are left out
# and C's top points at B's files. SF01-UPPER differs, so it stays 08B11-SFA-1T / 08C11-SFA-1T.
D20 = {"SF01-LOWER": "08Z11-SFA", "SF02-LOWER": "08Z11-SFA-2B", "SF02-UPPER": "08Z11-SFA-2T", "SF03-LOWER": "08Z11-SFA-3B", "SF03-UPPER": "08Z11-SFA-3T"}

def STA_0811(x):
    # D7 and D22: the new structure assembly, PartNo at Default, Provisional. D25: no file-level properties.
    return {(f"08{x}11-STA-01.SLDASM", "Default"): (f"08{x}11-STA-01", True, "D7, D22")}

# D10: SW splits the brace in -r1; still here unsplit = an error. <As Welded> configurations of the new files get no
# PartNo (as the vault's GNRIC-BRP), so James's -BRP-01 numbers must be gone.
D10_OPEN = "D10: the brace should be split in -r1 (08X11-DBP-01 for A and C, 08B11-DBP-01 for B); still here"
D10_WELDED = {f: "D10 (As Welded has no PartNo, as GNRIC-BRP)" for f in ("08X11-DBP-01.SLDPRT", "08B11-DBP-01.SLDPRT")}
# D25 (revised, on hold for the title block): the STA keeps a file-level Description; PartNo and Part Number go.
D25_PROPS = ("D25", ("PartNo", "Part Number"))
D15_OPEN = "D15: the old grout kits should be gone from -r1 (SW removes them); still here"

# Names and numbers decided for one screen's import (docs\imports\<screen>-decisions.md). Not rules for other screens.
#   files:   old file -> (new name, new folder or None to keep the proposed one, where decided)
#   partnos: (old file, configuration or "@file") -> (PartNo, provisional?, where decided)
#   fasteners: (grade, finish) for the bought-in screw, nut and washer families
#   same_as: old file -> (screen, that screen's old file, where decided): left out here, this screen's files point at
#            the other screen's (needs --with that screen)
#   no_file_props: old file -> (where decided, property names): must not have these at file level (prep deletes them)
#   no_welded_partno: old file -> where decided: its <As Welded> configurations carry no PartNo
DECIDED = {
    "LS-08A-06": {
        "files": {
            "MEMBER_SHS_40x80x8.SLDPRT": ("GNRIC-LM", r"APEX\GNRIC", "decision 5"),
            "LS-10X-06_SF02A_CANTILEVER_BRACE.SLDPRT": ("08X06-SFP-03", r"APEX\SC08\LS-08X-06\_Shared", "decision 2"),
            "LS-08X-06_NARROW_SIDE_CROSS_BRACE.SLDPRT": ("08X06-DBP", None, "Grant, 21 Oct 2026"),
            "LS-10A-06_SF01A_TIE_RIGHT_ANGLE.SLDPRT": ("08X06-SFW", None, "decision 12 (welded: W)"),
            "LS-10X-06_SF02A_CANTILEVER_RETURN.SLDPRT": ("08X06-SFW-04", r"APEX\SC08\LS-08X-06\_Shared", "decision 12 (welded: W)"),
        },
        "partnos": {
            ("LS-10X-06_SF02A_CANTILEVER_BRACE.SLDPRT", "Default<As Machined>"): ("08X06-SFP-03", True, "decision 2"),
            ("LS-10X-06_SF02A_CANTILEVER_RETURN.SLDPRT", "Default<As Machined>"): ("08X06-SFW-04", True, "decision 12 (welded: W)"),
            ("LS-08A-06_SF02_03-CANTILEVERED_SUB_ASSSY.SLDASM", "Default"): ("08X06-SFW-01", True, "decision 2"),
            ("LS-08X-06_NARROW_SIDE_CROSS_BRACE.SLDPRT", "319X319<As Machined>"): ("08X06-DBP-01", True, "Grant, 21 Oct 2026"),
            ("LS-08X-06_NARROW_SIDE_CROSS_BRACE.SLDPRT", "319X394<As Machined>"): ("08X06-DBP-02", True, "Grant, 21 Oct 2026"),
            ("LS-10A-06_SF01A_TIE_RIGHT_ANGLE.SLDPRT", "SF01_DEEP<As Machined>"): ("08X06-SFW-02", True, "decision 12 (welded: W)"),
            ("LS-10A-06_SF01A_TIE_RIGHT_ANGLE.SLDPRT", "SF04_DEEP<As Machined>"): ("08X06-SFW-03", True, "decision 12 (welded: W)"),
            ("08A06-STA-01.SLDASM", "Default"): ("08A06-STA-01", True, "pending spec change (ST category)"),
            ("LS-08X-X_TIE_BEAM_INTERFACE_PLATE.SLDPRT", "PL_180x140"): ("08XXX-PLP-30", False, "decision 1 (James's number)"),
            ("LS-08X-X_TIE_BEAM_INTERFACE_PLATE.SLDPRT", "PL_180x140-Perpendicular slots"): ("08XXX-PLP-31", False, "decision 1 (James's number)"),
            ("LS-08X-X_TIE_BEAM_INTERFACE_PLATE.SLDPRT", "PL_200x140"): ("08XXX-PLP-32", False, "decision 1 (James's number)"),
            ("LS-08X-X_TIE_BEAM_INTERFACE_PLATE.SLDPRT", "PL_200x140-Perpendicular slots"): ("08XXX-PLP-33", False, "decision 1 (James's number)"),
        },
        "fasteners": ("8.8", "HDG"),   # decision 6
        "leave_out": {   # old file -> where decided
            "LS-08A-06_SF04-LOWER_FLOOR_PLATE.SLDPRT": "decision 12 (component deleted in r4; file unused)",
        },
    },
    # docs\imports\LS-08BC-06-decisions.md
    "LS-08B-06": {
        "files": {
            "SHAFTB_EXP_JOINT_BRACKET_FOR_CONCRETE_BEAM.SLDPRT": ("08BXX-MSW-01", r"APEX\SC08\_Shared", "decision 9 (PLATE ONLY = 08BXX-PLP-01, same file)"),
            "SHAFTB_EXP_JOINT_100x100_EA_BRACKET.SLDPRT": ("08BXX-MSP-01", r"APEX\SC08\_Shared", "decision 9"),
        },
        "partnos": {
            ("08B06-STA-01.SLDASM", "Default"): ("08B06-STA-01", True, "decision 1 (as in A; pending ST category)"),
            ("SHAFTB_EXP_JOINT_BRACKET_FOR_CONCRETE_BEAM.SLDPRT", "Default"): ("08BXX-MSW-01", True, "decision 9"),
            ("SHAFTB_EXP_JOINT_BRACKET_FOR_CONCRETE_BEAM.SLDPRT", "PLATE ONLY"): ("08BXX-PLP-01", True, "decision 9 (name differs from the file, decided)"),
            ("SHAFTB_EXP_JOINT_100x100_EA_BRACKET.SLDPRT", "Default<As Machined>"): ("08BXX-MSP-01", True, "decision 9"),
            ("SHAFTB_EXP_JOINT_100x100_EA_BRACKET.SLDPRT", "Default<As Welded>"): ("08BXX-MSP-01", True, "decision 9"),
        },
    },
    "LS-08C-06": {
        "partnos": {
            ("08C06-STA-01.SLDASM", "Default"): ("08C06-STA-01", True, "decision 1 (as in A; pending ST category)"),
        },
    },
    # docs\imports\LS-08X-11-decisions.md
    "LS-08A-11": {
        "files": {   # D23: both kits job-wide
            "SPLICE_JOINT_ASSY.SLDASM": ("MDL-SPLICE-JOINT-GNRIC", r"APEX\GNRIC", "D13, D23"),
            "Hex Bolt, washers & Nuts assy.SLDASM": ("MDL-HEX-BOLT-WASHERS-NUTS-GNRIC", r"APEX\GNRIC", "D13, D23"),
        },
        "partnos": {**SHAFT_0811("A", "A"), **STA_0811("A"),
            # D12: STOCK -> GNRIC, Provisional
            ("PLATE_Bolting_Connector.SLDPRT", "Member 2"): ("GNRIC-MSP-02", True, "D12"),
            ("PLATE_Bolting_Connector.SLDPRT", "Member 3"): ("GNRIC-MSP-03", True, "D12"),
            ("PLATE_Bolting_Connector.SLDPRT", "Member 11"): ("GNRIC-MSP-11", True, "D12"),
            ("PLATE_Bolting_Connector.SLDPRT", "Member 14"): ("GNRIC-MSP-14", True, "D12"),
        },
        "modelling_groups": {"SPLICE_JOINT_ASSY.SLDASM": "D13 (splice kit is a modelling aid for now)"},
        "open": {"GROUT+ANCHOR_BOLTS.SLDASM": D15_OPEN, "GROUT.SLDPRT": D15_OPEN, "LS-08X-11_BRACE.SLDPRT": D10_OPEN},
        "no_file_props": {"08A11-STA-01.SLDASM": D25_PROPS},
        "no_welded_partno": D10_WELDED,
        "leave_out": {   # D3: A's older drawings duplicate the numbered ones; listed in the report, never in the PDM
            f: "D3 (older drawing, duplicates a numbered one)" for f in (
                "LS-08A-11-TIE_BEAM_1.SLDDRW", "LS-08A-11-TIE_BEAM_2.SLDDRW", "LS-08A-11-TIE_BEAM_3.SLDDRW",
                "LS-08A-11_SF01-LOWER.SLDDRW", "LS-08A-11_SF01-UPPER.SLDDRW", "LS-08A-11_SF02-LOWER.SLDDRW",
                "LS-08A-11_SF02-UPPER.SLDDRW", "LS-08A-11_SF03-LOWER.SLDDRW", "LS-08A-11_SF03-UPPER.SLDDRW")
        },
    },
    "LS-08B-11": {
        "files": {f"LS-08B-11_{k}.SLDASM": (v, None, "D20 (shared by B and C)") for k, v in D20.items()},
        "partnos": {**SHAFT_0811("B", "Z"), **STA_0811("B"),
            **{(f"LS-08B-11_{k}.SLDASM", "Default"): (v, True, "D20 (shared by B and C)") for k, v in D20.items() if k != "SF01-LOWER"},
        },
        "open": {"GROUT+ANCHOR_BOLTS.SLDASM": D15_OPEN, "GROUT.SLDPRT": D15_OPEN, "LS-08X-11_BRACE.SLDPRT": D10_OPEN},
        "no_file_props": {"08B11-STA-01.SLDASM": D25_PROPS},
        "no_welded_partno": D10_WELDED,
    },
    "LS-08C-11": {
        # D20 replaces D4: C's SF03-UPPER (and the drawing repointed to it) is left out, B's file is used.
        "partnos": {**SHAFT_0811("C", None), **STA_0811("C")},
        "same_as": {f"LS-08C-11_{k}.SLDASM": ("LS-08B-11", f"LS-08B-11_{k}.SLDASM", "D20") for k in D20},
        "open": {"GROUT+ANCHOR_BOLTS.SLDASM": D15_OPEN, "GROUT.SLDPRT": D15_OPEN, "LS-08X-11_BRACE.SLDPRT": D10_OPEN},
        "no_file_props": {"08C11-STA-01.SLDASM": D25_PROPS},
        "no_welded_partno": D10_WELDED,
    },
}

def load(name):
    with open(os.path.join(inv, name), encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))

files, cfgs, props, comps, refs = (load(n) for n in ("files.csv", "configs.csv", "properties.csv", "components.csv", "references.csv"))
def load_from(d, name):
    with open(os.path.join(d, name), encoding="utf-8-sig") as f:
        return list(csv.DictReader(f))

imported_with = {}   # old file -> (new name, new folder, screen), imported by another screen's plan in this pass
with_cfgs, with_pns = collections.defaultdict(set), {}   # that screen's probe, by file
for with_inv, with_files in withs:
    with_screen = re.sub(r"-r\d+$", "", os.path.basename(os.path.normpath(with_inv)))
    for r in load_from(os.path.dirname(os.path.abspath(with_files)), os.path.basename(with_files)):
        if r["action"] == "import":
            imported_with.setdefault(os.path.basename(r["source"]).lower(), (r["new_name"], r["new_folder"], with_screen))
    for c in load_from(with_inv, "configs.csv"):
        with_cfgs[(with_screen, c["file"].lower())].add(c["config"])
    for p in load_from(with_inv, "properties.csv"):
        if p["name"].lower() == "partno":
            with_pns[(with_screen, p["file"].lower(), p["config"])] = (p["value"] or p["expression"]).strip()

views, others = ((load(n) if os.path.isfile(os.path.join(inv, n)) else None) for n in ("views.csv", "others.csv"))
key = lambda s: s.lower()
stem = lambda f: os.path.splitext(f)[0]
ext = lambda f: os.path.splitext(f)[1].upper()

def gnric(n):
    """The job-wide place tag is GNRIC (was STOCK). The register still uses STOCK."""
    return re.sub(r"^STOCK(?=-|$)", "GNRIC", n) if n else n

# ---------------------------------------------------------------- register
wb = openpyxl.load_workbook(register_path, read_only=True, data_only=True)
reg_file = {}   # stem -> (place, proposed PartNo, note)
for r in wb["Rename Register SC08"].iter_rows(min_row=5, values_only=True):
    if r[0]:
        reg_file.setdefault(key(str(r[0]).strip()), (gnric(r[4]), gnric(r[8]), r[10]))
reg_cfg = {}    # (stem, config) -> PartNo
for r in wb["Configurations SC08"].iter_rows(min_row=5, values_only=True):
    if r[0] and r[1] and r[5]:
        reg_cfg[(key(str(r[0]).strip()), key(str(r[1]).strip()))] = gnric(str(r[5]))
for r in wb["Examples"].iter_rows(min_row=5, values_only=True):   # worked examples fill a few gaps (e.g. bolt kits)
    if r[3] and r[4] and " config " not in str(r[4]) and "(" not in str(r[4]):
        reg_file.setdefault(key(str(r[4]).strip()), (gnric(r[0]), gnric(r[3]), "from Examples tab"))

# ---------------------------------------------------------------- ledger of files already imported
ledger = {}
ledger_pn = {}   # (old file, old config or "") -> PartNo given at import
if ledger_path and os.path.exists(ledger_path):
    with open(ledger_path, encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            ledger.setdefault(key(r["old_file"]), r)
            if r.get("partno"):
                ledger_pn.setdefault((key(r["old_file"]), r["old_config"]), r["partno"])

# ---------------------------------------------------------------- facts from the inventory
file_partno = {}                                  # file -> PartNo at file level
cfg_partno = {}                                   # (file, config) -> PartNo
for p in props:
    if p["name"].lower() == "partno":
        v = (p["value"] or p["expression"]).strip()
        if p["config"] == "(file)":
            file_partno[key(p["file"])] = gnric(v)
        else:
            cfg_partno[(key(p["file"]), p["config"])] = gnric(v)

file_status, cfg_status = {}, {}                 # PartNoStatus already in the files
for p in props:
    if p["name"].lower() == "partnostatus":
        v = (p["value"] or p["expression"]).strip()
        if p["config"] == "(file)":
            file_status[key(p["file"])] = v
        else:
            cfg_status[(key(p["file"]), p["config"])] = v

dec = DECIDED.get(screen, {})
dec_files = {key(k): v for k, v in dec.get("files", {}).items()}
dec_pn = {(key(f), c): v for (f, c), v in dec.get("partnos", {}).items()}
fast_spec = dec.get("fasteners")
dec_leave = {key(k): v for k, v in dec.get("leave_out", {}).items()}
dec_mdl = {key(k): v for k, v in dec.get("modelling_groups", {}).items()}
dec_open = {key(k): v for k, v in dec.get("open", {}).items()}   # still to be decided, or must not be here: blocks the export
dec_same = {key(k): (sc, key(f), w) for k, (sc, f, w) in dec.get("same_as", {}).items()}
dec_nofp = {key(k): v for k, v in dec.get("no_file_props", {}).items()}
dec_nowp = {key(k): v for k, v in dec.get("no_welded_partno", {}).items()}
file_props = collections.defaultdict(list)       # file -> names of its file-level custom properties
for p in props:
    if p["config"] == "(file)":
        file_props[key(p["file"])].append(p["name"])
MASS_RE = re.compile(r"SW-Mass@(?:@[^@]*@)?([^@\"]+\.SLD(?:PRT|ASM))", re.I)
mass_refs = {(key(p["file"]), (p["config"], p["name"])): p["expression"] for p in props if "SW-Mass@" in (p["expression"] or "")}

cfg_list = collections.defaultdict(list)          # file -> [(config, parent)]
for c in cfgs:
    cfg_list[key(c["file"])].append((c["config"], c["parent_config"]))

parents = collections.defaultdict(set)
virtual = collections.defaultdict(set)
stored_dirs = collections.defaultdict(collections.Counter)
for c in comps:
    f = key(c["component_file"])
    parents[f].add(c["assembly"])
    if c["virtual"] == "yes":
        virtual[key(c["assembly"])].add(c["component_file"])
    stored_dirs[f][os.path.dirname(c["stored_path"])] += 1

# A tidied re-run (<screen>-r2) stores paths to the work copy, so take the original locations
# from the first inventory of the screen when it is there.
first_inv = re.sub(r"-r\d+$", "", os.path.normpath(inv))
if first_inv != os.path.normpath(inv) and os.path.isfile(os.path.join(first_inv, "components.csv")):
    with open(os.path.join(first_inv, "components.csv"), encoding="utf-8-sig") as fh:
        stored_dirs = collections.defaultdict(collections.Counter)
        for c in csv.DictReader(fh):
            stored_dirs[key(c["component_file"])][os.path.dirname(c["stored_path"])] += 1
part_links = collections.defaultdict(set)      # file -> files it is built from (skeletons, derived parts, STEP, ...)
drawing_models = collections.defaultdict(set)  # drawing -> models it shows (a drawing doesn't make its model a child)
for r in refs:
    if r.get("kind") == "model":
        drawing_models[key(r["file"])].add(key(os.path.basename(r["reference"])))
        continue
    if r.get("kind", "component") != "component":
        part_links[key(r["file"])].add(key(os.path.basename(r["reference"])))
    parents[key(os.path.basename(r["reference"]))].add(r["file"])
linked_found = {key(os.path.basename(r["reference"])): r["found_in_inbox"] for r in refs if r.get("kind") == "linked file"}

# What this screen really uses: walk down from the top-level assemblies through the configurations
# each parent actually references, skipping suppressed components. Pack and Go also copies files that
# only appear in other configurations of a family; those are reported as "carried along".
children = collections.defaultdict(list)          # (assembly, config) -> [(file, config)]
for c in comps:
    if c["suppressed"] != "yes":
        children[(key(c["assembly"]), c["assembly_config"])].append((key(c["component_file"]), c["component_config"]))
all_cfgs = collections.defaultdict(list)
for c in cfgs:
    all_cfgs[key(c["file"])].append(c["config"])
tops = [key(f["file"]) for f in files if not parents.get(key(f["file"])) and f["type"] == "assembly"]
used = collections.defaultdict(collections.Counter)   # file -> config -> instances reached
stack = [(t, cfg) for t in tops for cfg in all_cfgs[t]]
for t in tops:
    used[t].update({cfg: 1 for cfg in all_cfgs[t]})
seen = set()
while stack:
    node = stack.pop()
    for child in children.get(node, []):
        used[child[0]][child[1]] += 1
        if child not in seen:
            seen.add(child)
            stack.append(child)
    # A part built from other files (skeleton, derived part) needs them too.
    for linked in part_links.get(node[0], ()):
        if not used[linked]:
            used[linked].update({c: 1 for c in all_cfgs[linked]} or {"": 1})
            stack.extend((linked, c) for c in all_cfgs[linked])

def original_location(f):
    """Where the file lived before Pack and Go, from the paths stored in the assemblies that use it."""
    if not stored_dirs[f]:
        return ""
    d = stored_dirs[f].most_common(1)[0][0]
    for marker in ("\\Screen Models\\", "\\Models\\"):
        i = d.find(marker)
        if i >= 0:
            return d[i + len(marker):]
    return re.sub(r"^[A-Za-z]:\\", "", d)

def norm_cfg(c):
    return re.sub(r"(<As (Machined|Welded)>)+$", "", c).strip()

# ---------------------------------------------------------------- rules
PLACE_RE = re.compile(r"^(\d\d)([ABCZX0])(\d\d|XX)$")
SHAFT_FOLDER = {"A": "ShaftA", "B": "ShaftB", "C": "ShaftC", "Z": "ShaftBC", "X": "_Shared", "0": ""}

def place_of(partno):
    if not partno:
        return None
    return partno.split("-")[0]

def folder_for(place, top_level=False):
    """Folder = place. Returns (folder relative to P:\\, flag or '')."""
    if place in ("STOCK", "GNRIC"):
        return r"APEX\GNRIC", ""
    m = PLACE_RE.match(place or "")
    if not m:
        return "", f"place tag '{place}' not recognised"
    scene, shaft, nn = m.groups()
    if nn == "XX":
        return rf"APEX\SC{scene}\_Shared", ""
    screen_dir = rf"APEX\SC{scene}\LS-{scene}X-{nn}"
    if top_level:
        return screen_dir, ""
    sub = SHAFT_FOLDER[shaft]
    return (screen_dir + ("\\" + sub if sub else "")), ""

this_screen = re.match(r"LS-(\d\d)[A-Z]*-(\d\d)", screen)
scene_no, screen_no = (this_screen.group(1), this_screen.group(2)) if this_screen else ("", "")

def cross_screen(place):
    m = PLACE_RE.match(place or "")
    return bool(m and m.group(3) != "XX" and (m.group(1) != scene_no or m.group(3) != screen_no))

FAST_TYPE = [("hex screw", "HEX"), ("hex bolt", "HEX"), ("hex nut", "NUT"), ("plain washer", "WSH"), ("spring washer", "SPW"), ("socket", "SKT"), ("countersunk", "CSK")]

def fastener_size(cfg):
    m = re.search(r"M(\d+)\s*x\s*(\d+)", cfg)
    if m:
        return f"M{m.group(1)}x{m.group(2)}"
    m = re.search(r"\bM(\d+)\b", cfg) or re.search(r"ISO 7089 - (\d+)", cfg)
    return f"M{m.group(1)}" if m else cfg

def base_class(f):
    n, folder = key(f["file"]), key(f["folder"].replace("/", "\\").split("\\")[-1])
    if f["toolbox"] not in ("NotAToolboxPart", "") or "hilti" in n or "fastener" in folder or folder in ("hex nuts", "hex bolts and screws", "plain washers"):
        return "fastener"
    if folder == "panels":
        return "panel"
    if folder == "skus" or re.match(r"^\d+x\d+-sc\d", n):
        return "sku"
    return None

base = {key(f["file"]): base_class(f) for f in files}
contents = collections.defaultdict(set)           # assembly -> classes of its unsuppressed components
for c in comps:
    if c["suppressed"] != "yes" and c["virtual"] != "yes":
        contents[key(c["assembly"])].add(base.get(key(c["component_file"])) or "other")

def classify(f):
    n = key(f["file"])
    if f["type"] == "drawing":
        return "drawing"
    if base[n]:
        return base[n]
    if re.match(r"^mdl[-_]", n) or n in dec_mdl:
        return "modelling group"
    if re.match(r"^nonpart[-_]", n) or n.startswith("grout"):
        return "not a part"
    if "skeleton" in n or n.endswith("-skel.sldprt"):
        return "skeleton"
    if f["type"] == "assembly" and contents[n] and contents[n] <= {"fastener"}:
        return "modelling group"      # bolt kits are modelling groups, not bagged sets (decided 18 Oct 2026)
    if f["type"] == "assembly" and contents[n] and contents[n] <= {"sku", "panel"}:
        return "screen assembly"
    if "+structure" in n:
        return "combined"
    return "steel"

def planned_partno(m, c):
    """The PartNo model m's configuration c gets: this plan's (models come before drawings), else the ledger's, else the file's."""
    for r in pn_rows:
        if key(r[0]) == m and r[1] == c and r[6]:
            return r[6]
    return ledger_pn.get((m, c)) or ledger_pn.get((m, "")) or cfg_partno.get((m, c)) or file_partno.get(m, "")

def inside(x, others, seen=()):
    """x is a component of (or built into) one of others, at any depth."""
    ps = {key(p) for p in parents.get(x, ())}
    return bool(ps & others) or any(inside(p, others, seen + (x,)) for p in ps if p not in seen)

def partno_flagged(m, c):
    return any(key(r[0]) == m and r[1] == c and r[10] for r in pn_rows)

PN_RE = re.compile(r"^(\d\d[ABCZX0](\d\d|XX)|GNRIC|STOCK|\d\dMG[LR])-[A-Z]{2}[AWP]-\w+$", re.I)

def level_from_location(loc):
    """Place tag for the level a shared modelling file sat at before, from its old folder."""
    m = re.search(r"SC(\d\d)\\LS-(\d\d)X-(\d\d)", loc)
    if m:
        return f"{m.group(1)}X{m.group(3)}"
    m = re.search(r"SC(\d\d)\\_Shared", loc)
    if m:
        return f"{m.group(1)}XXX"
    return "GNRIC"

def tidy(stem_):
    t = re.sub(r"[^A-Z0-9]+", "-", stem_.upper()).strip("-")
    return re.sub(r"-(ASSY|ASSSY|ASM|ASSEMBLY)$", "", t)

# ---------------------------------------------------------------- build rows
file_rows, pn_rows, issues = [], [], []
destination = {}
named = {}

def flag(where, text):
    issues.append((where, text))
    return text

for f in sorted(files, key=lambda f: (classify(f) == "drawing", classify(f), f["folder"], f["file"])):
    fk, fname, s = key(f["file"]), f["file"], stem(f["file"])
    cls = classify(f)
    flags = []
    action, new_name, new_folder, source = "import", "", "", ""
    is_top = not parents.get(fk)

    reached = bool(used.get(fk)) or cls in ("sku", "panel")
    if not reached and not is_top:
        action, new_name, source = "leave out", s, "only in suppressed or unused configurations"
        new_folder = ""
        flags.append(flag(fname, "carried along by Pack and Go but not used by this screen (only in suppressed or other configurations of " + ", ".join(sorted(parents[fk])) + ")"))
    elif fk in dec_leave:
        action, new_name, new_folder, source = "leave out", s, "", dec_leave[fk]
    elif fk in ledger:
        led = ledger[fk]
        action, new_name, new_folder, source = "reuse (already imported)", stem(led["new_file"]), led["new_folder"], "ledger"
    elif fk in dec_same:
        sc, other, why = dec_same[fk]
        if other not in imported_with:
            action, new_name, source = "decide", s, why
            flags.append(flag(fname, f"{why}: replaced by {sc}'s {other}; plan {sc} first and pass --with its probe and exported files.csv"))
        else:
            nn, nf, _ = imported_with[other]
            action, new_name, new_folder, source = f"reuse ({why}: {sc}'s file)", stem(nn), nf, f"{why} (same as {sc}'s {other}; this copy stays out, listed)"
            mine, theirs = {c for c, _ in cfg_list[fk]}, with_cfgs[(sc, other)]
            if mine != theirs:
                flags.append(flag(fname, f"configurations differ from {sc}'s {other}: only here " + (", ".join(sorted(mine - theirs)) or "none")
                                  + "; only there " + (", ".join(sorted(theirs - mine)) or "none")))
    elif fk in imported_with:
        nn, nf, with_screen = imported_with[fk]
        action, new_name, new_folder, source = f"reuse (imported with {with_screen})", stem(nn), nf, f"D19 (imported with {with_screen})"
        mine, theirs = {c for c, _ in cfg_list[fk]}, with_cfgs[(with_screen, fk)]
        if mine != theirs:
            flags.append(flag(fname, f"configurations differ from {with_screen}'s copy: only here " + (", ".join(sorted(mine - theirs)) or "none")
                              + "; only there " + (", ".join(sorted(theirs - mine)) or "none")))
        mine_pn = {c: v for (ff, c), v in cfg_partno.items() if ff == fk}
        if file_partno.get(fk):
            mine_pn["(file)"] = file_partno[fk]
        theirs_pn = {c: gnric(v) for (sc, ff, c), v in with_pns.items() if sc == with_screen and ff == fk}
        diff = sorted(f"{c}: here {mine_pn.get(c) or '-'}, {with_screen} {theirs_pn.get(c) or '-'}" for c in set(mine_pn) | set(theirs_pn) if mine_pn.get(c) != theirs_pn.get(c))
        if diff:
            flags.append(flag(fname, f"PartNos differ from {with_screen}'s copy: " + "; ".join(diff)))
    elif cls == "fastener":
        t = next((code for words, code in FAST_TYPE if words in fk), None)
        if "hilti" in fk:
            m = re.search(r"(HST3-R|HST3|HUS3)_?(M\d+x\d+)", fname, re.I)
            sz = m.group(2) if m else "?"
            grade = "-A4" if m and m.group(1).upper() == "HST3-R" else ""
            new_name, source = f"FAST-{m.group(1).upper() if m else 'HILTI'}-{sz}{grade}", "rule (single part, named for the model)"
            if sz != "M12x125":   # Grant measured the job anchor at 125 mm (19 Oct 2026)
                flags.append(flag(fname, f"model is {sz} but job anchors are HST3-R M12x125: check the model"))
        elif fast_spec:
            new_name, source = f"FAST-{t or '?'}-{fast_spec[0]}-{fast_spec[1]}", "rule (family, sizes as configurations); grade and finish decided"
        else:
            new_name, source = f"FAST-{t or '?'}-{{grade}}-{{finish}}", "rule (family, sizes as configurations)"
            flags.append(flag(fname, "grade and finish not recorded in the Toolbox file: fill in"))
        new_folder = r"_Shared\_Fasteners"
        if f["toolbox"] not in ("NotAToolboxPart", ""):
            flags.append(flag(fname, "Toolbox flag set: clear it at import so SOLIDWORKS treats it as an ordinary part"))
    elif cls in ("sku", "panel"):
        action, new_name, source = "import, keep name", s, "Grant's area"
        new_folder = r"APEX\SC08\SKUS" if cls == "sku" else r"APEX\PANELS"
        flags.append(flag(fname, f"{cls}: folder is provisional until the panel and SKU rebuild decides where they live"))
    elif cls == "drawing":
        # D2 (0811): a drawing is named by the PartNo of the model configuration its views show. Name already
        # that PartNo = kept; exactly one PartNo = renamed to it; otherwise decide. Beside its model, no PartNo.
        if views is None:
            action, new_name, source = "decide", s, "probe has no drawing views"
            flags.append(flag(fname, "re-probe with the current DmProbe (views.csv) to name this drawing"))
        else:
            pairs = sorted({(key(v["model"]), v["config"]) for v in views if key(v["drawing"]) == fk})
            # A view keeps the model name it had when SOLIDWORKS last saved the drawing; a drawing repointed with
            # the Document Manager (0811 D4) still names the old model. Its reference is the truth.
            refs_here = drawing_models.get(fk, set())
            tops = [x for x in refs_here if not inside(x, refs_here - {x})]
            fixed = []
            for m, c in pairs:
                if m in refs_here:
                    fixed.append((m, c))
                    continue
                cand = [x for x in tops if any(c == y for y, _ in cfg_list[x])]
                if len(cand) == 1:
                    fixed.append((cand[0], c))
                    flags.append(flag(fname, f"its views still name {m}, but the drawing refers to {cand[0]} (repointed): {cand[0]} used"))
                else:
                    fixed.append((m, c))
            pairs = sorted(set(fixed))
            shown = [(m, c) for m, c in pairs if m in destination and any(c == x for x, _ in cfg_list[m])]
            missing = sorted({m for m, c in pairs if m not in destination})
            numbers = {}
            for m, c in shown:
                n = planned_partno(m, c)
                if n:
                    numbers.setdefault(key(n), (n, m, c))
            pick = numbers.get(key(s)) or (next(iter(numbers.values())) if len(numbers) == 1 else None)
            same = sorted({m for m, c in shown if m in dec_same})
            if same and len(same) == len({m for m, c in shown}):
                action, new_name, source = "leave out", s, dec_same[same[0]][2] + f": draws {', '.join(same)}, which is left out; {dec_same[same[0]][0]}'s drawing is the one in the vault"
            elif same:
                action, new_name, source = "decide", s, dec_same[same[0]][2]
                flags.append(flag(fname, "draws a left-out copy (" + ", ".join(same) + ") and other models: which drawing goes in?"))
            elif pick:
                n, m, c = pick
                action = "leave out" if destination[m][0] == "leave out" else "import with its model"
                new_name, new_folder = n, destination[m][1]
                source = ("its name is " if key(n) == key(s) else "renamed to ") + f"the PartNo of {m} [{c}] (D2)"
                if destination[m][0].startswith("reuse"):
                    flags.append(flag(fname, "its model is already in the vault: the drawing is a new file beside it"))
                if partno_flagged(m, c):
                    flags.append(flag(fname, f"named after a PartNo with a flag ({m} [{c}]): if that PartNo changes in the review, change this name too"))
            else:
                action, new_name, source = "decide", s, "D2: not exactly one PartNo"
                listed = "; ".join(f"{m} [{c}] = {planned_partno(m, c) or 'no PartNo'}" for m, c in shown) or "none in this set"
                flags.append(flag(fname, "which configuration does this drawing belong to? Its views show: " + listed))
            if missing:
                flags.append(flag(fname, "views show a model that isn't in this set: " + ", ".join(missing)))
    elif cls == "not a part":
        action, source = "import, no PartNo", "rule (NONPART)"
        tag = f"{scene_no}X{screen_no}"
        new_name = tidy(s) if re.match(r"^nonpart[-_]", fk) else f"NONPART-{tidy(s)}-{tag}"
        new_folder, _ = folder_for(new_name.rsplit("-", 1)[-1])
        flags.append(flag(fname, "not a part: must be set to Exclude from BOM; one per screen, shared across its shafts"))
    elif cls == "modelling group":
        action, source = "import, no PartNo", "rule (MDL)"
        if fk in dec_files:
            new_name, new_folder, source = dec_files[fk]
        elif re.match(r"^mdl[-_]", fk):
            new_name = tidy(s)
        else:
            new_name = f"MDL-{tidy(s)}-{level_from_location(original_location(fk))}"
            flags.append(flag(fname, "modelling group: level taken from its old folder, check it covers every screen that uses it"))
        if not new_folder:
            new_folder, _ = folder_for(new_name.rsplit("-", 1)[-1])
        flags.append(flag(fname, "modelling group: must be set to Promote so its contents show in the BOM"))
    elif cls == "skeleton":
        action, source = "import, no PartNo", "rule (named for what it drives)"
        users = sorted(set(f2 for f2, links in part_links.items() if fk in links))
        tags = set()
        def tag_of(v):
            parts = str(v).split("-")
            return parts[0] + "-" + parts[1][:2] if len(parts) > 1 else None
        for u in users:
            vals = [v for (ff, _), v in cfg_partno.items() if ff == u and v not in ("", "?")]
            vals += [v for (rs, _), v in reg_cfg.items() if rs == key(stem(u))]
            r_ = reg_file.get(key(stem(u)))
            if r_ and r_[1]:
                vals.append(r_[1])
            tags.update(t for t in map(tag_of, vals) if t)
        if len(tags) == 1:
            new_name = next(iter(tags)) + "-SKEL"
            new_folder, _ = folder_for(new_name.split("-")[0])
        else:
            new_name = s
            flags.append(flag(fname, "skeleton drives files with different numbers (" + ", ".join(sorted(tags)) + "): name it by hand"))
        flags.append(flag(fname, "skeleton: drives " + ", ".join(users) + ". It may link an equations .txt file, which the Document Manager can't see: check Tools > Equations and untick Link to external file (as 0806, 0811 D21)"))
    elif cls == "screen assembly":
        action, new_name, source = "import, keep name", screen, "screen name (LS- names are fixed)"
        new_folder = rf"APEX\SC{scene_no}\LS-{scene_no}X-{screen_no}"
        if screen in SCREEN_FILE:
            new_name, source = SCREEN_FILE[screen], "screen file decided for this screen (one file, screens as configurations)"
            flags.append(flag(fname, f"screen file holding several screens as configurations: becomes {new_name}"))
        elif key(s) != key(screen):
            flags.append(flag(fname, f"screen assembly (panels and SKUs only): becomes {screen}; its other screens' configurations must be removed first"))
    elif cls == "combined":
        action, new_name, source = "import, keep name", f"{screen}+STRUCTURE", "rule (screen + structure)"
        new_folder = rf"APEX\SC{scene_no}\LS-{scene_no}X-{screen_no}"
    elif fk in dec_files and cls == "steel":
        new_name, new_folder, source = dec_files[fk]
        if not new_folder:
            new_folder, ff = folder_for(place_of(new_name))
    elif is_top and cls == "steel" and key(s) not in reg_file and fk not in file_partno and not PN_RE.match(s):
        action, new_name, source = "decide", s, "nothing in this screen uses it"
        flags.append(flag(fname, "not used by any assembly in this Pack and Go: import, or leave out?"))
    else:  # steel
        reg = reg_file.get(key(s))
        # Collect the PartNo each configuration should carry.
        numbers = set(v for (ff, _), v in cfg_partno.items() if ff == fk and v not in ("", "?"))
        if fk in file_partno and file_partno[fk]:
            numbers.add(file_partno[fk])
        for (rs, rc), v in reg_cfg.items():
            if rs == key(s):
                numbers.add(v)
        if not numbers and reg and reg[1]:
            numbers.add(str(reg[1]))
        if not numbers and PN_RE.match(s):
            numbers.add(gnric(s.upper()))
        if not numbers and reg and not reg[1] and reg[0]:
            # Top-level structure assembly: named by its place tag.
            new_name, source = str(reg[0]), "register (place tag)"
            new_folder, ff = folder_for(str(reg[0]), top_level=True)
            if "SCREEN" in fname.upper():
                flags.append(flag(fname, "screen and structure in one assembly: rename to the place tag, or keep the LS- name as the screen assembly?"))
        elif not numbers:
            action = "needs number"
            flags.append(flag(fname, "steel part not in the register and no PartNo in the file"))
        else:
            prefixes = sorted(set(n.rsplit("-", 1)[0] for n in numbers))
            places = sorted(set(place_of(n) for n in numbers))
            if len(numbers) == 1 and PLACE_RE.match(next(iter(numbers))):
                # Top-level structure assembly: named by its place tag, lives at the screen folder root.
                new_name, source = next(iter(numbers)), "register (place tag)"
                new_folder, ff = folder_for(new_name, top_level=True)
                if "SCREEN" in fname.upper():
                    flags.append(flag(fname, "screen and structure in one assembly: rename to the place tag, or keep the LS- name as the screen assembly?"))
            elif len(numbers) == 1 and re.match(r"^\w{5}-STA-\d+$", next(iter(numbers))):
                # Complete structure assembly (ST category): lives at the screen folder root.
                new_name, source = next(iter(numbers)), "register" if reg else "file"
                new_folder, ff = folder_for(place_of(new_name), top_level=True)
            elif len(numbers) == 1:
                new_name, source = next(iter(numbers)), "register" if reg else "file"
            elif len(prefixes) == 1:
                new_name, source = prefixes[0], "family (place + category)"
            else:
                action = "split family"
                flags.append(flag(fname, "family mixes place or type (" + ", ".join(prefixes) + "): split before import, one file per prefix"))
                used_prefixes = sorted(set(cfg_partno.get((fk, c), "").rsplit("-", 1)[0] for c in used[fk] if cfg_partno.get((fk, c))))
                if used_prefixes:
                    flags[-1] += "; this screen uses " + ", ".join(used_prefixes)
            used_numbers = set((cfg_partno.get((fk, c), "") or reg_cfg.get((key(s), key(norm_cfg(c))), "")) for c in used[fk]) - {"", "?"}
            used_places = sorted(set(place_of(n) for n in used_numbers)) if used_numbers else places
            for pl in used_places:
                if cross_screen(pl):
                    flags.append(flag(fname, f"numbered for another screen ({pl}) but used here: renumber to scene level?"))
            pl = place_of(new_name) if new_name else (places[0] if len(places) == 1 else None)
            if pl and not new_folder:
                new_folder, ff = folder_for(pl)
                if ff:
                    flags.append(flag(fname, ff))
        if virtual.get(fk):
            flags.append(flag(fname, "contains a virtual component (" + ", ".join(sorted(virtual[fk])) + "): save it to its own file first"))

    if cls in ("not a part", "modelling group") and virtual.get(fk):
        flags.append(flag(fname, "contains a virtual component (" + ", ".join(sorted(virtual[fk])) + "): save it to its own file first"))

    # A Weight formula should read the file's own mass. One naming another file (often another screen's copy, left by
    # Save As) isn't rewritten by the importer, which only knows this plan's renames.
    foreign = sorted({m.group(1) for (ff, _), e in mass_refs.items() if ff == fk for m in [MASS_RE.search(e)] if m and key(m.group(1)) != fk})
    if foreign and action.startswith("import"):
        flags.append(flag(fname, "Weight formula reads another file's mass (" + ", ".join(foreign) + "): D26, each Weight formula reads its own file; SW fixes it in prep (DmEdit), or Admin says it's deliberate"))
    left = [n for n in file_props.get(fk, ()) if fk in dec_nofp and key(n) in {key(x) for x in dec_nofp[fk][1]}]
    if left:
        action = "decide"
        flags.append(flag(fname, f"{dec_nofp[fk][0]}: file-level " + ", ".join(left) + " should be deleted in prep; still here"))
    welded = sorted(f"{c} = {v}" for (ff, c), v in cfg_partno.items() if ff == fk and fk in dec_nowp and v and "<As Welded>" in c)
    if welded:
        action = "decide"
        flags.append(flag(fname, f"{dec_nowp[fk]}: delete these PartNos in prep: " + "; ".join(welded)))
    if fk in dec_open and not action.startswith("reuse"):
        action = "decide"
        flags.append(flag(fname, dec_open[fk]))
    destination[fk] = (action, new_folder)
    named[fk] = new_name
    file_rows.append([f["folder"] or ".", fname, original_location(fk), cls, action, new_name + (ext(fname) if new_name else ""),
                      new_folder, source, int(f["config_count"] or 0), len(used[fk]), "; ".join(flags), "", "", "", ""])

    # ---- PartNo rows: steel and fasteners, every configuration used here or already numbered
    if cls not in ("steel", "fastener") or action.startswith("reuse") or action in ("leave out", "decide"):
        continue
    for cfg, parent in cfg_list[fk]:
        in_file = cfg_partno.get((fk, cfg), "") or file_partno.get(fk, "")   # a file-level PartNo covers configurations without their own
        in_reg = reg_cfg.get((key(s), key(norm_cfg(cfg))), "")
        n_used = used[fk].get(cfg, 0)
        decided = dec_pn.get((fk, cfg))
        if not (in_file or in_reg or n_used or decided):
            continue
        pflags = []
        if cls == "fastener":
            if "hilti" in fk:
                proposed = new_name
            elif fast_spec:   # FAST-HEX-M20x35-8.8-HDG (SPEC.md, Part numbers and file names)
                proposed = f"FAST-{new_name.split('-')[1]}-{fastener_size(cfg)}-{fast_spec[0]}-{fast_spec[1]}"
            else:
                proposed = f"{new_name}-{fastener_size(cfg)}" if new_name else ""
            src = "rule"
        else:
            single = len(set(v for (ff, _), v in cfg_partno.items() if ff == fk and v not in ("", "?"))) <= 1 and not any(rs == key(s) for (rs, _) in reg_cfg)
            if single and not in_file and not in_reg:
                in_reg = str(reg_file.get(key(s), (None, ""))[1] or "")
            if in_file and in_reg and in_file != in_reg:
                proposed, src = in_file, "file (differs from register)"
                pflags.append("file and register differ")
            elif in_file and in_file != "?":
                proposed, src = in_file, "file"
            elif in_reg:
                proposed, src = in_reg, "sample register: provisional, needs James's number"
                pflags.append("number from the sample register only")
            else:
                proposed, src = "", "none"
                pflags.append("no number")
            if decided:
                proposed, src = decided[0], decided[2]
                pflags = []
            if proposed and n_used and cross_screen(place_of(proposed)):
                pflags.append(f"numbered for another screen")
            if not n_used:
                pflags.append("not used in this screen")
        for pf in pflags:
            if pf not in ("not used in this screen",):
                issues.append((f"{fname} / {cfg}", pf))
        # Where the number goes: the level James used; a configuration with none of its own and no parent
        # (not an <As Welded> child) gets it at configuration level.
        if cfg_partno.get((fk, cfg)):
            level = "config"
        elif file_partno.get(fk):
            level = "file"
        elif parent and not decided:
            level = ""
        else:
            level = "config"
        provisional = (decided and decided[1]) or src.startswith("sample register")             or (cfg_status.get((fk, cfg)) if level == "config" else file_status.get(fk, "")) == "Provisional"
        status = "Provisional" if provisional and proposed else ""
        pn_rows.append([fname, cfg, parent, n_used, in_file, in_reg, proposed, src, level, status, "; ".join(pflags), "", ""])

# Two files can't end up with the same name: SOLIDWORKS matches references by file name.
by_new_name = collections.defaultdict(list)
for r in file_rows:
    if r[5] and r[4] not in ("leave out", "decide"):
        by_new_name[r[5].lower()].append(r)
for name, rows_ in by_new_name.items():
    if len(rows_) > 1:
        msg = "same new name as " + ", ".join(x[1] for x in rows_) + ": merge them into one family file, or give each configuration its own file"
        for r in rows_:
            r[10] = "; ".join(x for x in (r[10], flag(r[1], msg)) if x)

# Linked files (STEP imports, equations files) travel with the part that uses them.
for lf, where in sorted(linked_found.items()):
    owners = sorted(f2 for f2, links in part_links.items() if lf in links)
    dest = next((destination[o] for o in owners if o in destination), ("", ""))
    fl = "" if where != "MISSING" else flag(lf, "linked file not in the Pack and Go: find it and bring it along")
    if dest[0] == "leave out":
        act = "leave out"
    elif lf.endswith(".txt"):   # equations file: unlinked in prep, not a vault file (as 0806; 0811 D21)
        act = "decide"
        fl = "; ".join(x for x in (fl, flag(lf, "equations file still linked: untick Link to external file in the skeleton (D21), it isn't a vault file")) if x)
    elif dest[0].startswith("reuse"):
        act = "leave out"
        fl = "; ".join(x for x in (fl, flag(lf, "its part is already in the vault, so nothing to bring")) if x)
    else:
        act = "import with its part"
    file_rows.append([os.path.dirname(where) or ".", os.path.basename(where) or lf, "", "linked file", act, os.path.basename(where) or lf,
                      dest[1], "used by " + ", ".join(owners), "", "", fl, "", "", "", ""])

# Changes to files already in the vault: configurations of a reused file that a file this plan imports refers to
# (in any of its configurations) and that the ledger gives no PartNo (0811 D17).
vault_rows = []
refd = collections.defaultdict(lambda: collections.defaultdict(set))   # reused file -> config -> assemblies here
for c in comps:
    a = key(c["assembly"])
    if a in destination and destination[a][0].startswith("import"):
        refd[key(c["component_file"])][c["component_config"]].add(c["assembly"])
for r in file_rows:
    if r[4] != "reuse (already imported)" or r[3] not in ("steel", "fastener"):
        continue
    fk, led_new = key(r[1]), r[5]
    for cfg, asms in sorted(refd[fk].items()):
        if ledger_pn.get((fk, cfg)) or ledger_pn.get((fk, "")):
            continue
        m = re.match(r"^FAST-([A-Z]+)-(.+)$", stem(led_new), re.I) if r[3] == "fastener" else None
        proposed = f"FAST-{m.group(1)}-{fastener_size(cfg)}-{m.group(2)}" if m and "hilti" not in fk else ""
        vault_rows.append([led_new, r[6], r[1], cfg, ", ".join(sorted(asms)), used[fk].get(cfg, 0), proposed,
                           "rule (FAST-type-size-grade-finish)" if proposed else "", "add PartNo (check-out, write, check-in)", ""])
        issues.append((f"{led_new} / {cfg}", "vault file needs a PartNo for this configuration: change by check-out (Vault changes sheet)"))

# Everything else in the Pack and Go (CSV, PDF, STEP, TXT exports) stays out of the vault (Admin, 25 Oct 2026):
# only SOLIDWORKS files and files they reference go onto P:\. Listed so nothing goes missing unseen.
for o in others or ():
    if key(o["file"]) not in linked_found:
        file_rows.append([o["folder"] or ".", o["file"], "", "other file", "leave out", "", "",
                          "not a SOLIDWORKS file and no model links to it", "", "", "", "", "", "", ""])

# ---------------------------------------------------------------- write workbook
out = openpyxl.Workbook()
HDR = Font(bold=True, color="FFFFFF")
HFILL = PatternFill("solid", fgColor="305496")
FLAG = PatternFill("solid", fgColor="FCE4D6")
CHOICE = PatternFill("solid", fgColor="E2EFDA")

def sheet(ws, header, rows, widths, flag_col, choice_cols, dv_formula=None, dv_col=None):
    ws.append(header)
    for c in ws[1]:
        c.font, c.fill, c.alignment = HDR, HFILL, Alignment(wrap_text=True, vertical="top")
    for r in rows:
        ws.append(r)
    for i, w in enumerate(widths, 1):
        ws.column_dimensions[openpyxl.utils.get_column_letter(i)].width = w
    for row in ws.iter_rows(min_row=2):
        if flag_col is not None and row[flag_col].value:
            for c in row[:flag_col + 1]:
                c.fill = FLAG
        for ci in choice_cols:
            row[ci].fill = CHOICE
        row[flag_col].alignment = Alignment(wrap_text=True, vertical="top")
    ws.freeze_panes = "C2"
    ws.auto_filter.ref = ws.dimensions
    if dv_formula:
        dv = DataValidation(type="list", formula1=dv_formula, allow_blank=True)
        ws.add_data_validation(dv)
        col = openpyxl.utils.get_column_letter(dv_col + 1)
        dv.add(f"{col}2:{col}{max(2, len(rows) + 1)}")

ws = out.active
ws.title = "Files"
sheet(ws, ["Inbox folder", "File", "Original location", "Class", "Proposed action", "Proposed new name", "Proposed new folder (under P:\\)",
           "Name from", "Configs in file", "Configs used here", "Flags", "Your choice", "Override name", "Override folder", "Comment"],
      file_rows, [16, 46, 40, 12, 18, 30, 34, 22, 8, 8, 60, 14, 26, 30, 30], 10, [11, 12, 13, 14],
      '"accept,import,leave out,split family,reuse existing"', 11)

ws = out.create_sheet("PartNos")
sheet(ws, ["File", "Configuration", "Parent config", "Used here (count)", "PartNo in file", "PartNo in register", "Proposed PartNo", "From", "Level", "Status", "Flags", "Your choice", "Override PartNo"],
      pn_rows, [44, 44, 22, 8, 18, 18, 26, 22, 8, 12, 40, 14, 22], 10, [11, 12],
      '"proposed,file,register,override"', 11)

ws = out.create_sheet("Vault changes")
sheet(ws, ["Vault file", "Vault folder", "File here", "Configuration", "Referred to by", "Reached by the tops (count)", "Proposed PartNo", "From", "Change", "Flags"],
      vault_rows, [34, 26, 44, 34, 44, 10, 30, 30, 36, 30], 9, [])

ws = out.create_sheet("Issues")
sheet(ws, ["Where", "Issue"], issues, [70, 100], 1, [])

ws = out.create_sheet("Read me")
for line in [
    f"Import plan for {screen}. Nothing has been renamed or moved.",
    "Files: one row per file. Leave 'Your choice' blank to accept the proposal, or pick another action and fill the override columns.",
    "PartNos: one row per configuration. Blank choice = proposed. 'override' uses the Override PartNo column.",
    "Orange rows have flags that need a look. Green columns are yours to fill.",
    "When the import runs, every rename is written to the rename ledger, so later screens reuse files already imported.",
]:
    ws.append([line])
ws.column_dimensions["A"].width = 130
out.move_sheet("Read me", offset=-4)

out.save(out_path)
print(f"Files: {len(file_rows)}  PartNo rows: {len(pn_rows)}  Issues: {len(issues)}")
print("Classes:", dict(collections.Counter(r[3] for r in file_rows)))
print("Actions:", dict(collections.Counter(r[4] for r in file_rows)))
print(f"Vault changes: {len(vault_rows)}")
print("Saved:", out_path)
