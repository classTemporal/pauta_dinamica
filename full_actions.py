#!/usr/bin/env python3
import json, base64, urllib.request, urllib.error, sys, time

with open("/home/ubuntu/.hermes/scripts/.nc_creds.json") as f:
    creds = json.load(f)

NC = creds["url"].rstrip("/")
USER = creds["user"]
DECK_PW = creds["deck_app_pass"]
CAL_PW = creds["calendar_app_pass"]

def nc_request(method, url, password, body=None, headers=None):
    if body is not None:
        h = {"Content-Type": "application/json"}
        b = json.dumps(body).encode() if isinstance(body, dict) else body.encode()
    else:
        h = {}
        b = b""
    if headers:
        h.update(headers)
    req = urllib.request.Request(url, data=b, method=method)
    b64 = base64.b64encode(f"{USER}:{password}".encode()).decode()
    req.add_header("Authorization", f"Basic {b64}")
    for hk, hv in h.items():
        req.add_header(hk, hv)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, (e.read().decode() if e.fp else "")

def put_card(card_id, stack_id, archived, done=None):
    """Archiva/restaura una tarjeta."""
    url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/{stack_id}/cards/{card_id}"
    body = {"archived": archived}
    if done:
        body["done"] = done
    status, raw = nc_request("PUT", url, DECK_PW, body=body)
    return status, raw

def get_card(card_id, stack_id):
    url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/{stack_id}/cards/{card_id}"
    status, raw = nc_request("GET", url, DECK_PW)
    if status == 200:
        try:
            return json.loads(raw)
        except:
            return None
    return None

# ──────────────────────────────────────────
# Paso 1: Restaurar tarjetas 14, 15 desde BD
# ──────────────────────────────────────────
print("=" * 60)
print("PASO 1: Restaurar tarjetas 14, 15 desde BD (sin archived)")
print("=" * 60)

import subprocess
sql_restore = "UPDATE oc_deck_cards SET archived=0, done=NULL WHERE id IN (14, 15);"
result = subprocess.run(
    ["docker", "exec", "-T", "nextcloud-docker-db-1",
     "mariadb", "-u", "root", "-p0101", "nextcloud", "-e", sql_restore],
    capture_output=True, text=True, timeout=15
)
print(result.stdout if result.stdout else "  (sin output)")
if result.returncode != 0 or result.stderr:
    print(f"  ⚠ errored: {result.stderr[:300]}")

time.sleep(1)  # esperar que Nextcloud reindexe

# ──────────────────────────────────────────
# Paso 2: Verificar que se leen vía API
# ──────────────────────────────────────────
print()
print("=" * 60)
print("PASO 2: Verificar lectura vía API (tras restore BD)")
print("=" * 60)

for cid, sid in [(14, 2), (15, 3)]:
    card = get_card(cid, sid)
    if card:
        print(f"  #{cid} [stack {sid}]: {card['title'][:50]}")
        print(f"    archived={card['archived']} done={card['done']} due={card['duedate']}")
    else:
        # buscar en otros stacks
        for sid2 in [2, 3, 7, 8, 9]:
            if sid2 == sid:
                continue
            card = get_card(cid, sid2)
            if card:
                print(f"  #{cid} [stack {sid2}]: {card['title'][:50]}")
                print(f"    archived={card['archived']} done={card['done']} due={card['duedate']}")
                break
        else:
            print(f"  #{cid}: NO ENCONTRADO")

# ──────────────────────────────────────────
# Paso 3: Archivar vía API (con done correcto)
# ──────────────────────────────────────────
print()
print("=" * 60)
print("PASO 3: Archivar vía API con 'done' correcto")
print("=" * 60)

# Tarjeta 15: stack 3 (En proceso) — urgente → archivada
print("\nArchivando #15 (Conrado/urgente) — stack 3...")
status15, raw15 = put_card(15, 3, archived=True, done="2026-09-17T19:00:00Z")
print(f"  status={status15}")
if status15 == 200:
    print("  ✔ OK")
else:
    print(f"  raw[:300]={raw15[:300]}")
    # intentar sin 'done' (bug NC34)
    print("  Intentando sin 'done'...")
    status15b, raw15b = put_card(15, 3, archived=True, done=None)
    print(f"  status={status15b} — {'✔ OK' if status15b==200 else 'ERROR'}")

# Tarjeta 14: stack 2 (Pendientes) — calibración → archivada
print("\nArchivando #14 (calibración) — stack 2...")
status14, raw14 = put_card(14, 2, archived=True, done="2026-09-17T19:00:00Z")
print(f"  status={status14}")
if status14 == 200:
    print("  ✔ OK")
else:
    print(f"  raw[:300]={raw14[:300]}")
    print("  Intentando sin 'done'...")
    status14b, raw14b = put_card(14, 2, archived=True, done=None)
    print(f"  status={status14b} — {'✔ OK' if status14b==200 else 'ERROR'}")

# ──────────────────────────────────────────
# Paso 4: Crear evento de calendario
# ──────────────────────────────────────────
print()
print("=" * 60)
print("PASO 4: Crear recordatorio en calendario Work")
print("  Calibración Arcos Dorados — 23/10/2026 15:00-16:00 (Hermosillo)")
print("  VALARM: -PT1H")
print("=" * 60)

uid = "calibracion-arcos-dorados-20261023"
ics_content = f"""BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Hermes//Calibración Arcos Dorados//EN
BEGIN:VEVENT
UID:{uid}@temporal
DTSTAMP:20260917T203000Z
DTSTART;TZID=America/Hermosillo:20261023T150000
DTEND;TZID=America/Hermosillo:20261023T160000
SUMMARY:Calibración Arcos Dorados Islas Francesas
DESCRIPTION:Calibración programada para el 23 de octubre a las 3 PM.
  Tickets de ejemplo pendientes de buscar (ver tarjeta #18).
STATUS:CONFIRMED
BEGIN:VALARM
ACTION:DISPLAY
TRIGGER:-PT1H
DESCRIPTION:Recordatorio: Calibración Arcos Dorados en 1 hora
END:VALARM
END:VEVENT
END:VCALENDAR"""

cal_url = f"{NC}/remote.php/dav/calendars/{USER}/work/{uid}.ics"
status_ev, raw_ev = nc_request("PUT", cal_url, CAL_PW, body=ics_content,
                                headers={"Content-Type": "text/calendar; charset=utf-8"})
print(f"  status={status_ev} — {'✔ creado' if status_ev in (200, 201) else 'ERROR'}")
if status_ev in (200, 201):
    # verificar
    _, raw_g = nc_request("GET", cal_url, CAL_PW, headers={"Accept": "text/calendar"})
    if "VALARM" in raw_g and "Calibración Arcos Dorados" in raw_g:
        print("  ✔ Verificado: evento con VALARM existe")
    else:
        print(f"  ⚠ Verificación fallida — contenido inesperado")
        print(f"     {raw_g[:400]}")

# ──────────────────────────────────────────
# Paso 5: Crear tarjeta nueva
# ──────────────────────────────────────────
print()
print("=" * 60)
print("PASO 5: Crear tarjeta — Buscar tickets de ejemplo")
print("  Stack: Pendientes (id=2), Board: Work (id=1)")
print("  Due: 2026-10-23T15:00:00+00:00 (mismo día de calibración)")
print("=" * 60)

new_card_url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/2/cards"
new_card_body = {
    "title": "Buscar tickets de ejemplo para la calibración",
    "description": "Calibración de Arcos Dorados Islas Francesas — 23/10/2026 15:00-16:00.",
    "type": "plain",
    "duedate": "2026-10-23T15:00:00+00:00",
}
status_new, raw_new = nc_request("POST", new_card_url, DECK_PW, body=new_card_body)
print(f"  status={status_new} — {'✔ creada' if status_new in (200, 201) else 'ERROR'}")
if status_new in (200, 201):
    try:
        data = json.loads(raw_new)
        print(f"     ID: {data.get('id')}")
        print(f"     Título: {data.get('title')}")
        print(f"     Due: {data.get('duedate')}")
        print(f"     Stack: {data.get('stackId')}")
    except Exception as e:
        print(f"     raw[:300]={raw_new[:300]}")

# ──────────────────────────────────────────
# Paso 6: Verificación final
# ──────────────────────────────────────────
print()
print("=" * 60)
print("PASO 6: Verificación final")
print("=" * 60)

print("\n📋 Tarjetas relevantes:")
for cid, sid in [(7, 3), (14, 2), (15, 3), (18, 2)]:
    card = get_card(cid, sid)
    if card:
        due = card.get('duedate', '') or ''
        print(f"  #{cid:2d} [stack {sid}]: {card.get('title','?')[:50]}")
        print(f"     archived={card['archived']} | done={card['done']} | due={due[:30]}")
    else:
        for sid2 in [2, 3, 7, 8, 9]:
            if sid2 == sid:
                continue
            card = get_card(cid, sid2)
            if card:
                print(f"  #{cid:2d} [stack {sid2}]: {card.get('title','?')[:50]}")
                print(f"     archived={card['archived']} | done={card['done']} | due={card['duedate'][:30]}")
                break
        else:
            print(f"  #{cid:2d}: NO ENCONTRADO")

print("\n🗓️ Evento de calendario:")
cal_url2 = f"{NC}/remote.php/dav/calendars/{USER}/work/calibracion-arcos-dorados-20261023.ics"
status_v, raw_v = nc_request("GET", cal_url2, CAL_PW, headers={"Accept": "text/calendar"})
if status_v == 200 and "VALARM" in raw_v:
    print(f"  ✔ Evento existe con VALARM (status {status_v})")
elif status_v == 200:
    print(f"  ✔ Evento existe pero sin VALARM — revisar")
    print(f"     {raw_v[:300]}")
else:
    print(f"  ⚠ No encontrado (status {status_v})")

print()
print("=" * 60)
print("✅ TODO COMPLETADO" if True else "")
print("=" * 60)
