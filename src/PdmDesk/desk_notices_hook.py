"""Claude Code hook: keep this session's Desk answer-notice watch running (26 Nov: after a restart no session
re-armed it, and Grant's answers sat unread for hours).

  python -I desk_notices_hook.py <session>      (hook event JSON on stdin)

SessionStart (startup, resume, clear, compact): tells Claude to start its watch now (desk-notices.ps1 with Monitor,
re-armed on every expiry) and how many answers are already waiting.
UserPromptSubmit (the backstop): says nothing, unless an answer has waited 5 minutes or more, which means the watch
isn't running: then it tells Claude to start it again and act on them.

The notices are in <desk>\\notices\\<session>\\*.json; <desk> is PDM_DESK, else dataFolder in settings.json beside this
script, else C:\\code\\pdm\\desk. Installed beside the Desk by install.ps1, with desk-notices.ps1.
Never fails or blocks the hook: any error is swallowed, and it always exits 0.
"""
import datetime, json, os, re, sys

STALE_MINUTES = 5
TIMEOUT_MS = 1800000


def desk_folder(here):
    if os.environ.get("PDM_DESK"):
        return os.environ["PDM_DESK"]
    try:
        with open(os.path.join(here, "settings.json"), encoding="utf-8-sig") as f:
            folder = json.load(f).get("dataFolder")
        if isinstance(folder, str) and folder:
            return folder
    except Exception:
        pass
    return "C:\\code\\pdm\\desk"


def written(path, name):
    # The notice's time from its name (yyyyMMddTHHmmssfffZ-<ticket>.json), else the file's time.
    try:
        return datetime.datetime.strptime(name[:18], "%Y%m%dT%H%M%S%f").replace(tzinfo=datetime.timezone.utc)
    except Exception:
        return datetime.datetime.fromtimestamp(os.path.getmtime(path), datetime.timezone.utc)


def unread(folder):
    # Minutes each unread notice has waited.
    now = datetime.datetime.now(datetime.timezone.utc)
    ages = []
    if os.path.isdir(folder):
        for name in os.listdir(folder):
            p = os.path.join(folder, name)
            if name.lower().endswith(".json") and os.path.isfile(p):
                try:
                    ages.append((now - written(p, name)).total_seconds() / 60)
                except Exception:
                    ages.append(0)
    return ages


def answers(n):
    return f"{n} answer" if n == 1 else f"{n} answers"


def main():
    session = (sys.argv[1] if len(sys.argv) > 1 else "").strip().lower()
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,63}", session):
        return
    try:
        ev = json.load(sys.stdin)
    except Exception:
        ev = {}
    event = ev.get("hook_event_name", "")
    here = os.path.dirname(os.path.abspath(__file__))
    folder = os.path.join(desk_folder(here), "notices", session)
    ages = unread(folder)
    script = os.path.join(here, "desk-notices.ps1").replace("\\", "/")
    command = f'powershell -NoProfile -ExecutionPolicy Bypass -File "{script}" -Session {session}'
    how = (f'Use the Monitor tool with command: {command} , description "Desk answer notices for {session}", '
           f"timeout_ms {TIMEOUT_MS}. Every time it expires, start it again at once with the same command. "
           f'Each line it prints ("Desk: ...") is Grant\'s answer to one of your Desk tickets: act on it '
           f"(the full notice is kept in {os.path.join(folder, 'seen')}).")

    if event == "SessionStart":
        source = ev.get("source", "")
        waiting = (f" {answers(len(ages))} already waiting: the watch prints {'it' if len(ages) == 1 else 'them'} as soon as it starts."
                   if ages else " No answers are waiting now.")
        if source in ("compact", "clear"):
            text = (f"Desk answer notices: if your Desk notice watch (Monitor \"Desk answer notices for {session}\") "
                    f"is still running, leave it; otherwise start it now, before other work. {how}{waiting}")
        else:
            text = f"Desk answer notices: start your Desk notice watch now, before other work. {how}{waiting}"
    elif event == "UserPromptSubmit":
        stale = [a for a in ages if a >= STALE_MINUTES]
        if not stale:
            return
        text = (f"Desk: {answers(len(ages))} to your tickets unread, the oldest for {int(max(stale))} min, so your "
                f"Desk notice watch isn't running. Start it again now and act on them. {how}")
    else:
        return
    print(json.dumps({"hookSpecificOutput": {"hookEventName": event, "additionalContext": text}}))


try:
    main()
except Exception:
    pass
sys.exit(0)
