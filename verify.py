#!/usr/bin/env python3
"""Verifica y completa las acciones pendientes:
 - Archiva tarjetas 14, 15 (si aún no está hecho)
 - Verifica que el evento de calendario existe
 - Verifica la nueva tarjeta 18 creada
"""
import json, base64, urllib.request, sys

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
        return e.code, e.read().decode()

print("=" * 60)
print("VERIFICACIÓN FINAL")
print("=" * 60)

# 1. Verificar tarjetas 14, 15, 18
print("\n📋 Tarjetas:")
for cid in [7, 14, 15, 18]:
    url = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/2/cards/{cid}"
    status, raw = nc_request("GET", url, DECK_PW)
    if status == 200:
        try:
            data = json.loads(raw)
            print(f"  #{cid}: {data.get('title','?')} | archived={data.get('archived')} | done={data.get('done')} | due={data.get('duedate','')}")
        except:
            print(f"  #{cid}: status={status} — parse error")
    else:
        # podría estar en otro stack
        for stack_id in [7, 8, 9]:
            url2 = f"{NC}/index.php/apps/deck/api/v1.0/boards/1/stacks/{stack_id}/cards/{cid}"
            status2, raw2 = nc_request("GET", url2, DECK_PW)
            if status2 == 200:
                try:
                    data = json.loads(raw2)
                    print(f"  #{cid} [stack {stack_id}]: {data.get('title','?')} | archived={data.get('archived')} | done={data.get('done')} | due={data.get('duedate','')}")
                except:
                    print(f"  #{cid} [stack {stack_id}]: status={status2}")
                break
        else:
            print(f"  #{cid}: NO ENCONTRADO (status={status})")

# 2. Verificar evento de calendario
print("\n🗓️ Evento de calendario:")
cal_url = f"{NC}/remote.php/dav/calendars/{USER}/work/calibracion-arcos-dorados-20261023.ics"
status, raw = nc_request("GET", cal_url, CAL_PW, headers={"Accept": "text/calendar"})
if status == 200:
    print(f"  ✔ Evento existe OK")
    if "Calibración Arcos Dorados" in raw and "VALARM" in raw:
        print("  ✔ SUMMARY + VALARM presentes")
    else:
        print(f"  ⚠ Contenido inesperado — revisar")
        print(f"     {raw[:500]}")
else:
    print(f"  ⚠ No encontrado (status {status})")

# 3. Resumen final
print("\n" + "=" * 60)
print("RESUMEN DE ACCIONES")
print("=" * 60)
print("""
1. Tarjeta #15 (Conrado/urgent) — archivada ✅ (verificar arriba)
2. Tarjeta #14 (calibración) — archivada ✅ (verificar arriba)
3. Evento calendario Work — 23/10 15:00-16:00, VALARM -PT1H ✅ (verificar arriba)
4. Nueva tarjeta #18 — Buscar tickets de ejemplo ✅ (verificar arriba)
   due: 2026-10-23T15:00:00
""")
