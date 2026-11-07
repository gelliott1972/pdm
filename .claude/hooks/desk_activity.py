"""Claude Code hook: record what this session is doing, for the PDM Desk status panel.

Called with the session's role (admin, dev, sw, app) as the only argument and the
hook event JSON on stdin. Writes desk/activity/<role>.json (whole-file write).
Never fails the hook: any error is swallowed.
"""
import json, os, sys, datetime

def main():
    role = (sys.argv[1] if len(sys.argv) > 1 else "unknown").lower()
    try:
        ev = json.load(sys.stdin)
    except Exception:
        ev = {}
    name = ev.get("hook_event_name", "")
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    rec = {"name": role, "event": name, "at": now}
    if name == "UserPromptSubmit":
        rec.update(activity="working", detail="Thinking")
    elif name == "PreToolUse":
        tool = ev.get("tool_name", "")
        ti = ev.get("tool_input") or {}
        detail = ti.get("description") or ti.get("summary") or ""
        if not detail and isinstance(ti.get("file_path"), str):
            detail = os.path.basename(ti["file_path"])
        rec.update(activity="working", running=True, detail=(tool + (": " + detail if detail else ""))[:160])
    elif name in ("PostToolUse", "PostToolUseFailure"):
        rec.update(activity="working", running=False, detail="Thinking")
    elif name == "Stop":
        rec.update(activity="idle", detail="")
    elif name == "Notification":
        msg = (ev.get("message") or "")[:200]
        waiting_input = "waiting for your input" in msg.lower()
        rec.update(activity="idle" if waiting_input else "needs-you", detail=msg)
    else:
        return
    here = os.path.dirname(os.path.abspath(__file__))
    d = os.path.normpath(os.path.join(here, "..", "..", "desk", "activity"))
    os.makedirs(d, exist_ok=True)
    p = os.path.join(d, role + ".json")
    tmp = p + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(rec, f)
    os.replace(tmp, p)

try:
    main()
except Exception:
    pass
sys.exit(0)
