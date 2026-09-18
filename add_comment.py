#!/usr/bin/env python3
import json, base64, urllib.request, urllib.error

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)

NC = creds["url"].rstrip("/")
USER = creds["user"]
# comentarios usa OCS - puede usa deck_app_pass o calendar_app_pass dependiendo de versión
# intentar con deck_app_pass primero
for pw_name, pw in [("deck_app_pass", creds["deck_app_pass"]), ("calendar_app_pass", creds["calendar_app_pass"])]:
    url = f"{NC}/ocs/v2.php/apps/deck/api/v1.0/cards/7/comments"
    body = json.dumps({"message": "Hoy 17/09/2026 le di una retro a Carlos Armenta — fueron 3/4. No serán 5 porque tuvimos día feriado.", "parentId": None})
    req = urllib.request.Request(url, data=body.encode(), method="POST")
    b64 = base64.b64encode(f"{USER}:{pw}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    req.add_header("OCS-APIRequest", "true")
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            raw = r.read().decode()
            status = r.status
            print(f"[{pw_name}] status={status}")
            print(f"raw[:500]={raw[:500]}")
            # check XML response
            import xml.etree.ElementTree as ET
            try:
                root = ET.fromstring(raw)
                for el in root.iter():
                    if "status" in el.tag:
                        print(f"  status element: {el.text}")
                    if "message" in el.tag:
                        print(f"  message element: {el.text}")
            except Exception as e:
                print(f"  XML parse error: {e}")
            break
    except urllib.error.HTTPError as e:
        print(f"[{pw_name}] HTTPError {e.code}: {e.read().decode()[:300]}")
    except Exception as e:
        print(f"[{pw_name}] error: {e}")
