#!/usr/bin/env python3
"""Raw dump of /boards/1/stacks to see exact structure."""
import json, base64, urllib.request

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)
NC = creds["url"]; USER = creds["user"]; DECK_PW = creds["deck_app_pass"]

url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks"
req = urllib.request.Request(url)
b64 = base64.b64encode(f"{USER}:{DECK_PW}".encode()).decode()
req.add_header("Authorization", f"Basic {b64}")
req.add_header("OCS-APIRequest", "true")
req.add_header("Accept", "application/json")
with urllib.request.urlopen(req, timeout=10) as r:
    raw = r.read().decode()

data = json.loads(raw)
print(f"Type: {type(data)}, len: {len(data)}")
for st in data:
    print(f"  Stack {st['id']}: {st['title']}  keys={list(st.keys())}  cards={st.get('cards', 'NO KEY')}")
    if isinstance(st.get('cards'), list):
        for c in st['cards']:
            print(f"    - Card {c['id']}: {c.get('title','')}  due={c.get('duedate','')}")
