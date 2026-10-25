"""Compare what the Document Manager says each assembly configuration uses with what SOLIDWORKS loaded (SwCheck).

After Replace Components, DM can report a stale referenced configuration for the replaced instance, even after a
SOLIDWORKS save (0811, SW 26 Oct): SOLIDWORKS is right. DM's component list in vault files also still names the
files as they were before the import, so names are mapped through the rename ledger before comparing.

Usage: python -I swcheck_used.py <probeDir> [--ledger <ledgerCsv>] <swcheck.txt> [<swcheck.txt> ...]
  Lists every assembly configuration both report whose unsuppressed children (file, configuration, count) differ.
"""
import collections, csv, os, re, sys

LINE = re.compile(r"^(\s*)(\S.*?) -> (.+?) \[(.*?)\](.*)$")

def parse(path):
    """SwCheck output -> {(assembly, config): Counter((child, child config))}, unsuppressed only; names lower case.
    SwCheck lists a sub-assembly's contents under every instance of it; each assembly configuration is counted once."""
    node_key, node_kids = [], []
    top, stack = None, []
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        for line in fh:
            line = line.rstrip("\n")
            m = re.match(r"^Open (.+?): errors=", line)
            if m:
                top = m.group(1).lower()
                continue
            m = re.match(r"^Configuration (.+): (shown|COULD NOT SHOW)$", line)
            if m:
                node_key.append((top, m.group(1))); node_kids.append(collections.Counter())
                stack = [(-1, len(node_key) - 1)]
                continue
            m = LINE.match(line)
            if not m or not stack:
                continue
            depth, f, cfg, rest = len(m.group(1)), os.path.basename(m.group(3)).lower(), m.group(4), m.group(5)
            while stack and stack[-1][0] >= depth:
                stack.pop()
            if not stack:
                continue
            parent = stack[-1][1]
            if parent is None or "(suppressed)" in rest:
                stack.append((depth, None))   # suppressed: neither counted nor a parent
                continue
            node_kids[parent][(f, cfg)] += 1
            node_key.append((f, cfg)); node_kids.append(collections.Counter())
            stack.append((depth, len(node_key) - 1))
    kids = {}
    for k, c in zip(node_key, node_kids):
        if k not in kids and (c or k[0].endswith(".sldasm")):
            kids[k] = c
    return kids

def dm_kids(probe, rename=None):
    rename = rename or {}
    kids = collections.defaultdict(collections.Counter)
    with open(os.path.join(probe, "components.csv"), encoding="utf-8-sig") as fh:
        for c in csv.DictReader(fh):
            if c["suppressed"] != "yes":
                f = c["component_file"].lower()
                kids[(c["assembly"].lower(), c["assembly_config"])][(rename.get(f, f), c["component_config"])] += 1
    return kids

def ledger_renames(path):
    with open(path, encoding="utf-8-sig") as fh:
        return {r["old_file"].lower(): r["new_file"].lower() for r in csv.DictReader(fh) if r["new_file"]}

def differences(dm, sw):
    """[(assembly, config, only DM, only SOLIDWORKS)] for assembly configurations both report."""
    return [(k[0], k[1], dm[k] - sw[k], sw[k] - dm[k]) for k in sorted(set(dm) & set(sw)) if dm[k] != sw[k]]

if __name__ == "__main__":
    args = sys.argv[1:]
    rename = {}
    if "--ledger" in args:
        i = args.index("--ledger")
        rename = ledger_renames(args[i + 1])
        del args[i:i + 2]
    probe, checks = args[0], args[1:]
    sw = {}
    for p in checks:
        sw.update(parse(p))
    dm = dm_kids(probe, rename)
    diffs = differences(dm, sw)
    print(f"Assembly configurations in both: {len(set(dm) & set(sw))}  differ: {len(diffs)}")
    fmt = lambda cnt: ", ".join(f"{n} x {f}[{fc}]" for (f, fc), n in sorted(cnt.items())) or "none"
    for a, c, only_dm, only_sw in diffs:
        print(f"  {a} [{c}]\n    DM only:  {fmt(only_dm)}\n    SW only:  {fmt(only_sw)}")
