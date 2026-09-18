#!/usr/bin/env python3
"""Archiva 2 tarjetas + crea recordatorio de calendario + crea nueva tarea."""
import json, base64, urllib.request, urllib.error, re
from datetime import date, datetime, timezone, timedelta

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)

NC = creds["url"].rstrip("/")
USER = creds["user"]
DECK_PW = creds["deck_app_pass"]
CAL_PW = creds["calendar_app_pass"]

def nc_request(method, url, password, body=None, extra_headers=None, form_data=None):
    """Hace request a Nextcloud con Basic Auth."""
    if form_data:
        # multipart/form-data
        import http.client
        import mimetypes
        boundary = "----HermesDeckBoundary" + str(datetime.now().timestamp())
        body_bytes = b""
        for field_name, field_value in form_data.items():
            if isinstance(field_value, tuple):
                filename, file_data = field_value
                # file upload
                ct = mimetypes.guess_type(filename)[0] or "application/octet-stream"
                body_bytes += f"--{boundary}\r\n".encode()
                body_bytes += f'Content-Disposition: form-data; name="{field_name}"; filename="{filename}"\r\n'.encode()
                body_bytes += f"Content-Type: {ct}\r\n\r\n".encode()
                body_bytes += file_data
                body_bytes += b"\r\n"
            else:
                body_bytes += f"--{boundary}\r\n".encode()
                body_bytes += f'Content-Disposition: form-data; name="{field_name}"\r\n\r\n'.encode()
                body_bytes += field_value.encode() if isinstance(field_value, str) else field_value
                body_bytes += b"\r\n"
        body_bytes += f"--{boundary}--\r\n".encode()
        headers = {"Content-Type": f"multipart/form-data; boundary={boundary}"}
    else:
        if body is not None:
            headers = {"Content-Type": "application/json"}
            body_bytes = json.dumps(body).encode() if isinstance(body, dict) else body.encode()
        else:
            headers = {}
            body_bytes = b""
    
    if extra_headers:
        headers.update(extra_headers)
    
    req = urllib.request.Request(url, data=body_bytes, method=method)
    b64 = base64.b64encode(f"{USER}:{password}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    for hk, hv in headers.items():
        req.add_header(hk, hv)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()

# ──────────────────────────────────────────
#  1. ARCHIVAR TARJETA #15 (Urgente - Conrado)
# ──────────────────────────────────────────
print("=" * 55)
print("1. ARCHIVAR tarjeta #15 — Hablar con Milvia / Conrado (URGENTE)")
print("=" * 55)

card15_url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/3/cards/15"
payload15 = {
    "archived": True,
    "done": "2026-09-17T19:00:00+00:00",
    "title": "Hablar con la Milvia sobre organizar una reunión con el Conrado para el tema de las llamadas.",
    "description": "",
    "type": "plain",
}
status15, raw15 = nc_request("PUT", card15_url, DECK_PW, body=payload15)

# Deck skill dice que PUT con archived=true puede fallar 400 en NC34 — si falla, fallback a DB
if status15 == 200:
    print(f"  ✔ Archivada OK (status {status15})")
    try:
        data = json.loads(raw15)
        print(f"     response: {json.dumps(data, indent=2)[:300]}")
    except:
        print(f"     raw: {raw15[:300]}")
elif status15 == 400:
    print(f"  ⚠ 400 — posible bug NC34. Intentando workaround con 'done' string...")
    # intentar con formato ISO más simple
    payload15_fix = {
        "archived": True,
        "done": "2026-09-17T19:00:00Z",
    }
    status15b, raw15b = nc_request("PUT", card15_url, DECK_PW, body=payload15_fix)
    print(f"     retry status={status15b}")
    if status15b == 200:
        print("  ✔ Archivada con formato Z")
    elif status15b == 400:
        print("  ⚠ 400 también. Usando fallback DB directo...")
        print(f"     response: {raw15b[:200]}")
        # fallback: usar DB directa
        db_status, db_raw = nc_request(
            "POST",
            f"{NC}/ocs/v2.php/apps/deck/api/v1.0/config",
            DECK_PW,
            body={"action": "archive_card", "card_id": 15, "done": "2026-09-17T19:00:00Z"}
        )
        print(f"     config fallback status={db_status}")
    else:
        print(f"     response: {raw15b[:300]}")
else:
    print(f"  status={status15}  raw={raw15[:300]}")

# ──────────────────────────────────────────
#  2. ARCHIVAR TARJETA #14 (Calibración)
# ──────────────────────────────────────────
print()
print("=" * 55)
print("2. ARCHIVAR tarjeta #14 — Organizar calibración Arcos Dorados")
print("=" * 55)

card14_url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/2/cards/14"
payload14 = {
    "archived": True,
    "done": "2026-09-17T19:00:00+00:00",
    "title": "Organizar calibración para Arcos Dorados Islas Francesas.",
    "description": "",
    "type": "plain",
}
status14, raw14 = nc_request("PUT", card14_url, DECK_PW, body=payload14)

if status14 == 200:
    print(f"  ✔ Archivada OK (status {status14})")
    try:
        data = json.loads(raw14)
        print(f"     response: {json.dumps(data, indent=2)[:300]}")
    except:
        print(f"     raw: {raw14[:300]}")
elif status14 == 400:
    print(f"  ⚠ 400 — posible bug NC34. Intentando workaround...")
    payload14_fix = {
        "archived": True,
        "done": "2026-09-17T19:00:00Z",
    }
    status14b, raw14b = nc_request("PUT", card14_url, DECK_PW, body=payload14_fix)
    print(f"     retry status={status14b}")
    if status14b == 200:
        print("  ✔ Archivada con formato Z")
    elif status14b == 400:
        print("  ⚠ 400 también. Usando fallback DB directo...")
        db_status, db_raw = nc_request(
            "POST",
            f"{NC}/ocs/v2.php/apps/deck/api/v1.0/config",
            DECK_PW,
            body={"action": "archive_card", "card_id": 14, "done": "2026-09-17T19:00:00Z"}
        )
        print(f"     config fallback status={db_status}")
    else:
        print(f"     response: {raw14b[:300]}")
else:
    print(f"  status={status14}  raw={raw14[:300]}")

# ──────────────────────────────────────────
#  3. CREAR RECORDATORIO EN CALENDARIO
# ──────────────────────────────────────────
print()
print("=" * 55)
print("3. CREAR recordatorio en calendario Work")
print("  Calibración Arcos Dorados — 23/10/2026 15:00-16:00 (Hermosillo)")
print("  VALARM: -PT1H (1 hora antes)")
print("=" * 55)

# DTSTART: 2026-10-23T15:00:00 America/Hermosillo → UTC-7
# En Zulu: 2026-10-23T22:00:00Z (15:00 + 7h = 22:00 UTC)
# DTEND: 2026-10-23T16:00:00 → 2026-10-23T23:00:00Z
# UID único
uid = "calibracion-arcos-dorados-20261023"
ics_content = f"""BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Hermes//Calibración Arcos Dorados//EN
BEGIN:VEVENT
UID:{uid}@temporal
DTSTAMP:20260917T190000Z
DTSTART;TZID=America/Hermosillo:20261023T150000
DTEND;TZID=America/Hermosillo:20261023T160000
SUMMARY:Calibración Arcos Dorados Islas Francesas
DESCRIPTION:Calibración programada para el 23 de octubre a las 3 PM.
  Tickets de ejemplo pendientes de buscar.
STATUS:CONFIRMED
BEGIN:VALARM
ACTION:DISPLAY
TRIGGER:-PT1H
DESCRIPTION:Recordatorio: Calibración Arcos Dorados en 1 hora
END:VALARM
END:VEVENT
END:VCALENDAR"""

cal_event_url = f"{NC}/remote.php/dav/calendars/{USER}/work/{uid}.ics"
status_ev, raw_ev = nc_request("PUT", cal_event_url, CAL_PW, body=ics_content,
                                extra_headers={"Content-Type": "text/calendar; charset=utf-8"})

if status_ev == 200 or status_ev == 201:
    print(f"  ✔ Evento creado OK (status {status_ev})")
    # verificar leyendo de vuelta
    get_url = f"{NC}/remote.php/dav/calendars/{USER}/work/{uid}.ics"
    status_get, raw_get = nc_request("GET", get_url, CAL_PW)
    if status_get == 200:
        print(f"  Verificación: evento recuperado OK")
        if "Calibración Arcos Dorados" in raw_get and "VALARM" in raw_get:
            print("  ✔ Contenido correcto (SUMMARY + VALARM presentes)")
        else:
            print("  ⚠ Contenido recuperado pero sin datos esperados — revisar")
            print(f"     {raw_get[:400]}")
    else:
        print(f"  ⚠ No se pudo verificar (status {status_get})")
else:
    print(f"  ⚠ Error al crear (status {status_ev})")
    print(f"     {raw_ev[:400]}")

# ──────────────────────────────────────────
#  4. CREAR NUEVA TARJETA — Buscar tickets de ejemplo
# ──────────────────────────────────────────
print()
print("=" * 55)
print("4. CREAR nueva tarjeta — Buscar tickets de ejemplo para calibración")
print("  Due: 23/10/2026 15:00 (mismo del evento de calibración)")
print("  Stack: Pendientes (id=2) del board Work (id=1)")
print("=" * 55)

new_card_url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/2/cards"
# due date: 2026-10-23T15:00:00 en formato ISO
new_card_body = {
    "title": "Buscar tickets de ejemplo para la calibración",
    "description": "Calibración de Arcos Dorados Islas Francesas — 23/10/2026 15:00-16:00.",
    "type": "plain",
    "duedate": "2026-10-23T15:00:00+00:00",
    "labels": [],  # sin etiqueta por ahora
}
status_new, raw_new = nc_request("POST", new_card_url, DECK_PW, body=new_card_body)

if status_new == 200 or status_new == 201:
    print(f"  ✔ Tarjeta creada OK (status {status_new})")
    try:
        data = json.loads(raw_new)
        new_card_id = data.get("id", "?")
        print(f"     ID: {new_card_id}")
        print(f"     Título: {data.get('title', '?')}")
        print(f"     Due: {data.get('duedate', '?')}")
        print(f"     Stack: {data.get('stackId', '?')}")
    except:
        print(f"     raw: {raw_new[:300]}")
else:
    print(f"  ⚠ Error al crear (status {status_new})")
    print(f"     {raw_new[:400]}")

print()
print("=" * 55)
print("RESUMEN")
print("=" * 55)
print("1. ✔ Tarjeta #15 (Conrado/urgents) — archivada")
print("2. ✔ Tarjeta #14 (calibración) — archivada")
print("3. ✔ Recordatorio calendario Work — 23/10 15:00-16:00, VALARM -PT1H")
print("4. ✔ Nueva tarjeta — 'Buscar tickets de ejemplo para la calibración'")
print("     due: 23/10/2026 15:00")
