#!/usr/bin/env python3
"""Dump todos los eventos .ics de los calendarios personal y work, parseando DTSTART, RRULE, SUMMARY."""
import json, base64, urllib.request, re
from datetime import date, timedelta
import xml.etree.ElementTree as ET

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)
NC = creds["url"]; USER = creds["user"]; CAL_PW = creds["calendar_app_pass"]

TODAY = date.today()  # 2026-09-17 (Thursday)

def parse_dtstart(s):
    # DTSTART;VALUE=DATE:20260917  OR  DTSTART:20260917T150000Z
    if ";" in s:
        s = s.split(":", 1)[1]
    else:
        s = s.split(":", 1)[1]
    raw = re.sub(r"[^0-9T]", "", s)
    if "T" in raw:
        return raw
    else:
        # all-day date
        return raw + "T000000"

def expand_vevent(vevent_text, today=TODAY):
    """Check if a VEVENT falls on 'today' considering RRULE."""
    summary = ""; dtstart_raw = ""; rrule = ""
    for line in vevent_text.strip().split("\n"):
        if line.startswith("SUMMARY:"):
            summary = line[len("SUMMARY:"):]
        elif line.startswith("DTSTART"):
            dtstart_raw = line.split(":", 1)[1] if ":" in line else ""
        elif line.startswith("RRULE:"):
            rrule = line[len("RRULE:"):]
    
    if not dtstart_raw:
        return None
    
    # parse start date
    is_date_only = "VALUE=DATE" in (vevent_text.split("\n")[0] if vevent_text else "")  # rough check
    raw_date = re.sub(r"[^0-9]", "", dtstart_raw)[:8]
    start_date = date(int(raw_date[:4]), int(raw_date[4:6]), int(raw_date[6:8]))
    
    # simple RRULE expansion for common cases
    # check if today matches
    if "RRULE" not in vevent_text and not rrule:
        # single event
        if start_date == today:
            return summary
        return None
    
    # expand RRULE (simplified)
    if rrule:
        # parse RRULE
        freq = None
        byday = []
        until = None
        count = None
        for part in rrule.split(";"):
            if part.startswith("FREQ="):
                freq = part[5:]
            elif part.startswith("BYDAY="):
                byday = [part[6:]]
            elif part.startswith("UNTIL="):
                u = re.sub(r"[^0-9]", "", part[6:])[:8]
                until = date(int(u[:4]), int(u[4:6]), int(u[6:8]))
            elif part.startswith("COUNT="):
                count = int(part[6:])
            elif part.startswith("INTERVAL="):
                interval = int(part[9:])
            else:
                interval = 1
        
        # expand events from start_date to today
        current = start_date
        day_names = {"MO": 0, "TU": 1, "WE": 2, "TH": 3, "FR": 4, "SA": 5, "SU": 6}
        
        if freq == "WEEKLY" and byday:
            target_weekday = day_names.get(byday[0], 0)  # 0=Mon
            # find next occurrence of target weekday from start_date
            # generate weekly occurrences
            generated = 0
            check_date = start_date
            while check_date <= today + timedelta(days=1):
                if check_date.weekday() == target_weekday:
                    if check_date == today:
                        return summary
                    generated += 1
                    if count and generated >= count:
                        break
                check_date += timedelta(days=1)
                if until and check_date > until:
                    break
        elif freq == "DAILY":
            current = start_date
            while current <= today:
                if current == today:
                    return summary
                current += timedelta(days=1)
                if until and current > until:
                    break
                if count and (current - start_date).days > count:
                    break
    
    return None

body = '''<?xml version="1.0" encoding="utf-8"?>
<D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
  <D:prop>
    <D:resourcetype/>
    <C:calendar-data/>
  </D:prop>
</D:propfind>'''

for cal_name, cal_label in [("personal", "Personal"), ("work", "Work")]:
    url = f"{NC}/remote.php/dav/calendars/{USER}/{cal_name}/"
    req = urllib.request.Request(url, data=body.encode(), method="PROPFIND")
    b64 = base64.b64encode(f"{USER}:{CAL_PW}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    req.add_header("Depth", "1")
    req.add_header("Content-Type", "application/xml")
    with urllib.request.urlopen(req, timeout=10) as r:
        xml_str = r.read().decode()
    
    root = ET.fromstring(xml_str)
    print(f"\n=== {cal_label} ({cal_name}) ===")
    found = False
    for resp in root:
        if not resp.tag.endswith("response"):
            continue
        cd = ""
        for child in resp:
            if not child.tag.endswith("propstat"):
                continue
            for gp in child:
                if not gp.tag.endswith("prop"):
                    continue
                for gc in gp:
                    if gc.tag.endswith("calendar-data"):
                        cd = gc.text or ""
        if "VEVENT" in cd:
            for vevent in re.findall(r"BEGIN:VEVENT(.*?)END:VEVENT", cd, re.DOTALL):
                vtext = "BEGIN:VEVENT" + vevent + "END:VEVENT"
                result = expand_vevent(vtext)
                if result:
                    print(f"  📅 HOY: {result}")
                    found = True
    if not found:
        print("  (sin eventos de hoy)")

# also check the full personal calendar for all events to see what's there
print("\n=== Todos los eventos (sample) ===")
for cal_name in ["personal"]:
    url = f"{NC}/remote.php/dav/calendars/{USER}/{cal_name}/"
    req = urllib.request.Request(url, data=body.encode(), method="PROPFIND")
    b64 = base64.b64encode(f"{USER}:{CAL_PW}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    req.add_header("Depth", "1")
    req.add_header("Content-Type", "application/xml")
    with urllib.request.urlopen(req, timeout=10) as r:
        xml_str = r.read().decode()
    root = ET.fromstring(xml_str)
    for resp in root:
        if not resp.tag.endswith("response"):
            continue
        cd = ""
        for child in resp:
            if not child.tag.endswith("propstat"):
                continue
            for gp in child:
                if not gp.tag.endswith("prop"):
                    continue
                for gc in gp:
                    if gc.tag.endswith("calendar-data"):
                        cd = gc.text or ""
        if "VEVENT" in cd:
            for vevent in re.findall(r"BEGIN:VEVENT(.*?)END:VEVENT", cd, re.DOTALL):
                summary = ""
                dtstart = ""
                rrule = ""
                for line in vevent.strip().split("\n"):
                    if line.startswith("SUMMARY:"):
                        summary = line[8:]
                    elif line.startswith("DTSTART"):
                        dtstart = line.split(":",1)[1] if ":" in line else line
                    elif line.startswith("RRULE:"):
                        rrule = line[7:]
                print(f"  - {dtstart} | {summary} | RRULE={rrule}")
