#!/usr/bin/env python3
"""Debug: GET /boards/{id} con details=1 para obtener cards inline."""
import json, base64, urllib.request

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)
NC = creds["url"]; USER = creds["user"]; DECK_PW = creds["deck_app_pass"]

for bid in [1, 3]:
    for suffix in ["", "?details=1"]:
        url = f"{NC}/index.php/apps/deck/api/v1.0/boards/{bid}{suffix}"
        req = urllib.request.Request(url)
        b64 = base64.b64encode(f"{USER}:{DECK_PW}".encode()).decode()
        req.add_header("Authorization", f"Basic {b64}")
        req.add_header("OCS-APIRequest", "true")
        req.add_header("Accept", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=10) as r:
                raw = r.read().decode()
                data = json.loads(raw) if raw else {}
        except Exception as e:
            raw = str(e); data = {}
        # count cards
        total_cards = 0
        if isinstance(data, dict):
            for st in data.get("stacks", []):
                cards = st.get("cards", [])
                total_cards += len(cards)
        elif isinstance(data, list):
            total_cards = len(data)
        print(f"Board {bid} {suffix}: status={r.status if 'r' in dir() else '?'}  total_cards={total_cards}")
        # show stack titles and card counts
        if isinstance(data, dict) and "stacks" in data:
            for st in data["stacks"]:
                cards = st.get("cards", [])
                print(f"  - Stack {st['id']}: {st['title']}  ({len(cards)} cards)")
        print()
