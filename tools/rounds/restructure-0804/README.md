# 0804 restructure round (8 Nov 2026)

Copies of the files the run used; the working copies are in `scratch\import\restructure-0804` (not in git).

- `round.csv`: the round list (action, old_path, new_path, who, why), read by App's `pdm checkout / move / load / remove`.
- `restructure-run.ps1 -Stage 1..5 -Pdm <pdm.exe> [-Real -Go] [-From <step>] [-Engineer <pdm.exe>]`: Dev's run in stages
  (1 checkout, move, repoint; 2 SOLIDWORKS dissolve and form-sub, with the read-only guard; 3 resave, gate, STA-01
  properties, checks, masses; 4 load and remove; 5 rehearsal only: an engineer PC's get).
- `make_sta_props.py`: DmEdit batch actions for the three STA-01s.
- `make_ledger_rows.py`: rename-ledger rows for the round.
