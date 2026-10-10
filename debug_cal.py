#!/usr/bin/env python3
"""Dump raw events from personal and work calendars."""
import json, base64, urllib.request

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)
NC = creds["url"]; USER = creds["user"]; CAL_PW = creds["calendar_app_pass"]

body = '''<?xml version="1.0" encoding="utf-8"?>
<D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
  <D:prop>
    <D:displayname/>
    <D:resourcetype/>
    <C:calendar-data/>
  </D:prop>
</D:propfind>'''

for cal in ["personal", "work"]:
    url = f"{NC}/remote.php/dav/calendars/{USER}/{cal}/"
    req = urllib.request.Request(url, data=body.encode(), method="PROPFIND")
    b64 = base64.b64encode(f"{USER}:{CAL_PW}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    req.add_header("Depth", "1")
    req.add_header("Content-Type", "application/xml")
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            raw = r.read().decode()
    except Exception as e:
        raw = f"ERROR: {e}"
    print(f"=== {cal} ===")
    print(raw[:3000])
    print()
