#!/usr/bin/env python3
import json, base64, urllib.request, xml.etree.ElementTree as ET

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)
NC = creds["url"]; USER = creds["user"]; PASS = creds["calendar_app_pass"]

body = '''<?xml version="1.0" encoding="utf-8"?>
<D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
  <D:prop>
    <D:displayname/>
    <D:resourcetype/>
  </D:prop>
</D:propfind>'''
req = urllib.request.Request(f"{NC}/remote.php/dav/calendars/{USER}/", data=body.encode(), method="PROPFIND")
b64 = base64.b64encode(f"{USER}:{PASS}".encode()).decode()
req.add_header("Authorization", f"Basic {b64}")
req.add_header("Depth", "1")
req.add_header("Content-Type", "application/xml")
with urllib.request.urlopen(req, timeout=10) as r:
    xml_str = r.read().decode()

root = ET.fromstring(xml_str)
for resp in root.iter():
    if resp.tag.endswith("response"):
        href = ""; rtype_children = []; dname = ""
        for child in resp:
            tag = child.tag.split("}")[-1]
            if tag == "href":
                href = child.text or ""
            elif tag == "propstat":
                for gp in child:
                    if gp.tag.split("}")[-1] == "prop":
                        for gc in gp:
                            gtn = gc.tag.split("}")[-1]
                            if gtn == "resourcetype":
                                for sub in gc:
                                    rtype_children.append(sub.tag)
                            elif gtn == "displayname":
                                dname = gc.text or ""
        if href:
            is_cal = any("calendar" in str(t) for t in rtype_children)
            print(f"href={href}  rtype_children={rtype_children}  is_cal={is_cal}  name={dname}")
