#!/usr/bin/env python3
"""
Chequeo de hoy — Nextcloud Deck + Calendar.
- Deck: TODOS los boards, TODAS las tarjetas (con y sin due-date)
- Calendar: eventos de hoy con RRULE correctamente calculado (respetando UNTIL)
"""
import json, base64, urllib.request, re, sys
from datetime import date, timedelta
import xml.etree.ElementTree as ET

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)

NC      = creds["url"]
USER    = creds["user"]
DECK_PW = creds["deck_app_pass"]
CAL_PW  = creds["calendar_app_pass"]
TODAY   = date.today()

def ns(tag):
    return tag.split("}")[-1] if "}" in tag else tag

def nc_get(url, password):
    req = urllib.request.Request(url)
    b64 = base64.b64encode(f"{USER}:{password}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    req.add_header("OCS-APIRequest", "true")
    req.add_header("Accept", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        return None

def nc_propfind(url, password, want_calendar_data=False):
    body = '''<?xml version="1.0" encoding="utf-8"?>
<D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
  <D:prop>
    <D:displayname/>
    <D:resourcetype/>'''
    if want_calendar_data:
        body += '\n    <C:calendar-data/>'
    body += '\n  </D:prop>\n</D:propfind>'''
    req = urllib.request.Request(url, data=body.encode(), method="PROPFIND")
    b64 = base64.b64encode(f"{USER}:{password}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    req.add_header("Depth", "1")
    req.add_header("Content-Type", "application/xml")
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            return r.read().decode()
    except Exception as e:
        return None

def parse_dtstart(line):
    """Extrae YYYY-MM-DD de un campo DTSTART."""
    val = line.split(":", 1)[1] if ":" in line else line
    raw = re.sub(r"[^0-9T]", "", val)
    if "T" in raw:
        return raw.split("T")[0]
    return raw[:8] if len(raw) >= 8 else raw

def expand_rrule(start_str, rrule_str, start_dt):
    """Calcula si un evento recurrente cae en TODAY.
    start_dt: datetime.date del DTSTART original.
    Devuelve True si cae en hoy, False si ya venció o no aplica hoy.
    """
    if not rrule_str:
        return start_dt == TODAY
    
    parts = {}
    for p in rrule_str.split(";"):
        if "=" in p:
            k, v = p.split("=", 1)
            parts[k] = v
    
    freq = parts.get("FREQ", "")
    until_raw = parts.get("UNTIL", "")
    count_raw = parts.get("COUNT", "")
    
    # Parse UNTIL
    until_dt = None
    if until_raw:
        u = re.sub(r"[^0-9]", "", until_raw)
        if len(u) >= 8:
            until_dt = date(int(u[:4]), int(u[4:6]), int(u[6:8]))
    
    # Parse COUNT
    count = None
    if count_raw:
        try:
            count = int(count_raw)
        except:
            pass
    
    day_names_map = {
        "MO": 0, "TU": 1, "WE": 2, "TH": 3,
        "FR": 4, "SA": 5, "SU": 6,
        "1SU": 6, "2SU": 6, "3SU": 6, "4SU": 6, "5SU": 6,
        "1MO": 0, "2MO": 0, "3MO": 0, "4MO": 0, "5MO": 0,
        "1TU": 1, "2TU": 1, "3TU": 1, "4TU": 1, "5TU": 1,
        "1WE": 2, "2WE": 2, "3WE": 2, "4WE": 2, "5WE": 2,
        "1TH": 3, "2TH": 3, "3TH": 3, "4TH": 3, "5TH": 3,
        "1FR": 4, "2FR": 4, "3FR": 4, "4FR": 4, "5FR": 4,
        "1SA": 5, "2SA": 5, "3SA": 5, "4SA": 5, "5SA": 5,
    }
    
    byday_map = {}
    byday_raw = parts.get("BYDAY", "")
    if byday_raw:
        for d in byday_raw.split(","):
            d = d.strip()
            if d in day_names_map:
                day_num = day_names_map[d]
                if day_num not in byday_map:
                    byday_map[day_num] = []
                byday_map[day_num].append(d)
    
    # Simplest: WEEKLY with BYDAY
    if freq == "WEEKLY" and byday_raw:
        # get week day numbers from BYDAY
        weekday_set = set()
        for k in byday_map:
            weekday_set.add(k)
        if not weekday_set:
            return False
        
        # find all occurrences from start_dt to today, check if today matches
        # First, restrict by UNTIL
        if until_dt and TODAY > until_dt:
            return False
        
        # Check if TODAY's weekday is in BYDAY set
        if TODAY.weekday() not in weekday_set:
            return False
        
        # Now verify TODAY is after or on start_dt
        if TODAY < start_dt:
            return False
        
        # Check COUNT limit
        if count is not None:
            # count occurrences from start_dt to TODAY
            occurrence_count = 0
            current = start_dt
            while current <= TODAY:
                if current.weekday() in weekday_set:
                    occurrence_count += 1
                    if occurrence_count > count:
                        return False
                current += timedelta(days=1)
        
        # Verify no UNTIL gap: check that TODAY is not past the end of a series
        # For weekly, if UNTIL is set, the last occurrence must be >= today
        if until_dt:
            # find the last occurrence on or before today
            # The last occurrence must be on a BYDAY that is >= start_dt
            # and <= until_dt
            if TODAY > until_dt:
                return False
            # check that today's occurrence is within the series
            # by counting from start
            occurrence_count = 0
            current = start_dt
            while current <= min(TODAY, until_dt):
                if current.weekday() in weekday_set:
                    occurrence_count += 1
                current += timedelta(days=1)
            # if we didn't reach today in the count, it doesn't happen today
            # Actually, we need to check if the nth occurrence where nth <= count lands on today
            # Simplify: generate occurrences and check
            gen_count = 0
            current = start_dt
            max_date = until_dt if until_dt else TODAY + timedelta(days=365)
            while current <= max_date and gen_count < (count or 99999):
                if current.weekday() in weekday_set:
                    gen_count += 1
                    if current == TODAY:
                        return True
                current += timedelta(days=1)
            return False
        
        # If no UNTIL and no COUNT, just check weekday match and >= start
        if TODAY >= start_dt and TODAY.weekday() in weekday_set:
            return True
        return False
    
    # DAILY
    if freq == "DAILY":
        if until_dt and TODAY > until_dt:
            return False
        if TODAY < start_dt:
            return False
        days_diff = (TODAY - start_dt).days
        if count is not None and days_diff >= count:
            return False
        if until_dt and days_diff > (until_dt - start_dt).days:
            return False
        return True
    
    # MONTHLY (simple: same day of month)
    if freq == "MONTHLY":
        if until_dt and TODAY > until_dt:
            return False
        if TODAY < start_dt:
            return False
        # check day of month match
        if TODAY.day == start_dt.day:
            # check COUNT
            months_diff = (TODAY.year - start_dt.year) * 12 + (TODAY.month - start_dt.month)
            if count is not None and months_diff >= count:
                return False
            return True
        return False
    
    # YEARLY
    if freq == "YEARLY":
        if until_dt and TODAY > until_dt:
            return False
        if TODAY < start_dt:
            return False
        # check month and day match
        if TODAY.month == start_dt.month and TODAY.day == start_dt.day:
            years_diff = TODAY.year - start_dt.year
            if count is not None and years_diff >= count:
                return False
            return True
        return False
    
    return False

def parse_ics_date(raw):
    """Parse YYYY-MM-DD from raw DTSTART string."""
    val = raw.split(":", 1)[1] if ":" in raw else raw
    clean = re.sub(r"[^0-9]", "", val)
    if len(clean) >= 8:
        return f"{clean[:4]}-{clean[4:6]}-{clean[6:8]}"
    return ""

def process_calendar(cal_name, cal_label):
    """Busca eventos de hoy en un calendario con RRULE."""
    url = f"{NC}/remote.php/dav/calendars/{USER}/{cal_name}/"
    xml = nc_propfind(url, CAL_PW, want_calendar_data=True)
    if not xml:
        return []
    
    root = ET.fromstring(xml)
    results = []
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
        for vevent in re.findall(r"BEGIN:VEVENT(.*?)END:VEVENT", cd, re.DOTALL):
            vtext = "BEGIN:VEVENT" + vevent + "END:VEVENT"
            summary = ""
            dtstarts = []
            rrule = ""
            dtend = ""
            for line in vtext.strip().split("\n"):
                if line.startswith("SUMMARY:"):
                    summary = line[8:]
                elif line.startswith("DTSTART"):
                    dtstarts.append(parse_dtstart(line))
                elif line.startswith("DTEND"):
                    dtend = parse_dtstart(line)
                elif line.startswith("RRULE:"):
                    rrule = line[7:]
            
            if not dtstarts:
                continue
            
            start_date = date.fromisoformat(dtstarts[0])
            
            # single event (no RRULE)
            if not rrule:
                if start_date == TODAY:
                    results.append((cal_label, summary, dtstarts[0], rrule))
                continue
            
            # recurrent: expand
            if expand_rrule(dtstarts[0], rrule, start_date):
                results.append((cal_label, summary, dtstarts[0], rrule))
    
    return results

# ─────────────────────────────────────────────
#  DECK
# ─────────────────────────────────────────────
print("=" * 60)
print(f"📊 DECK — TODOS LOS TABLEROS  ({TODAY.isoformat()})")
print("=" * 60)

boards = nc_get(f"{NC}/index.php/apps/deck/api/v1.0/boards", DECK_PW)
deck_items = []

if boards:
    for b in boards:
        bid = b["id"]
        bname = b.get("title", "")
        archived = b.get("archived", False)
        if archived:
            continue
        stacks = nc_get(f"{NC}/index.php/apps/deck/api/v1.0/boards/{bid}/stacks", DECK_PW)
        if not stacks:
            continue
        for st in stacks:
            sid = st.get("id", "?")
            sname = st.get("title", "?")
            for c in st.get("cards", []):
                if c.get("archived", False):
                    continue
                due = c.get("duedate", "")
                deck_items.append({
                    "board": bname,
                    "stack": sname,
                    "title": c.get("title", "?"),
                    "due": due,
                    "labels": c.get("labels", []),
                    "has_due": bool(due)
                })
else:
    print("  ⚠ No se pudieron cargar los boards")

# resumen por board
print()
current_board = None
for item in deck_items:
    if item["board"] != current_board:
        current_board = item["board"]
        print(f"\n  ▢ {current_board}")
    due_display = item["due"][:16] if item["due"] else "—"
    # has_due indica si tiene due, pero lo mostramos siempre
    labels = ", ".join(
        l.get("title", str(l)) if isinstance(l, dict) else str(l)
        for l in item["labels"]
    ) if item["labels"] else ""
    print(f"    • {item['title']}  | due: {due_display}  | {labels}".rstrip(" |"))

if not deck_items:
    print("  (ninguna tarjeta visible)")

# ─────────────────────────────────────────────
#  CALENDARIO
# ─────────────────────────────────────────────
print("\n" + "=" * 60)
print(f"🗓️  CALENDARIO — Eventos de HOY ({TODAY.isoformat()})")
print("=" * 60)

cal_xml = nc_propfind(f"{NC}/remote.php/dav/calendars/{USER}/", CAL_PW)
cal_names = []
if cal_xml:
    root = ET.fromstring(cal_xml)
    for resp in root:
        if not resp.tag.endswith("response"):
            continue
        href = ""; is_cal = False
        for child in resp:
            tag = ns(child.tag)
            if tag == "href":
                href = child.text or ""
            elif tag == "propstat":
                for gp in child:
                    if ns(gp.tag) == "prop":
                        for gc in gp:
                            if ns(gc.tag) == "resourcetype":
                                for sub in gc:
                                    if ns(sub.tag) == "calendar":
                                        is_cal = True
            if href and is_cal:
                parts = href.strip("/").split("/")
                if len(parts) >= 3:
                    cal_names.append(parts[-1])

print(f"  Calendarios: {', '.join(cal_names)}")

today_events = []
for cn in cal_names:
    events = process_calendar(cn, cn)
    today_events.extend(events)

if today_events:
    for cal, summary, dtstart, rrule in today_events:
        rrule_short = rrule if rrule else "—"
        print(f"  • [{cal}] {summary}")
        print(f"    inicio: {dtstart}  recurrencia: {rrule_short}")
else:
    print("  ✔ No hay eventos de hoy")

print()
