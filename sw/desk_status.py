# Write SW's Desk status: python -I sw/desk_status.py <state> <doing> <waitingOn>
import json, sys, datetime
d = {'name': 'sw', 'state': sys.argv[1], 'doing': sys.argv[2], 'waitingOn': sys.argv[3],
     'updatedAt': datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')}
open('desk/status/sw.json', 'w', encoding='utf-8').write(json.dumps(d, indent=1))
