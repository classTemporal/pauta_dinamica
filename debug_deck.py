#!/usr/bin/env python3
"""Debug: listar stacks para cada board con verbose de response."""
import json, base64, urllib.request

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)
NC = creds["url"]; USER = creds["user"]; DECK_PW = creds["deck_app_pass"]

for bid in [1, 2, 3]:
    url = f"{NC}/index.php/apps/deck/api/v1.0/boards/{bid}/stacks"
    req = urllib.request.Request(url)
    b64 = base64.b64encode(f"{USER}:{DECK_PW}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    req.add_header("OCS-APIRequest", "true")
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            raw = r.read().decode()
            status = r.status
    except Exception as e:
        raw = str(e)
        status = "ERR"
    print(f"Board {bid}: status={status}  raw[:500]={raw[:500]}")
    print()
