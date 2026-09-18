#!/usr/bin/env python3
"""Archivar tarjeta 14 y 15 vía DB directa (workaround NC34)."""
import subprocess, sys

COMPOSE = "/home/ubuntu/nextcloud-docker/compose.yml"
DB_CONTAINER = "nextcloud-docker-db-1"

def db_exec(sql):
    result = subprocess.run(
        ["docker", "exec", "-T", DB_CONTAINER,
         "mariadb", "-u", "root", "-p0101", "nextcloud", "-e", sql],
        capture_output=True, text=True, timeout=15
    )
    # el -p fuerza prompt; usar -e con -B y pasar pass por env
    return result

def db_exec2(sql):
    result = subprocess.run(
        ["docker", "exec", "-T", DB_CONTAINER,
         "sh", "-c", f"mariadb -u root -p0101 nextcloud -e '{sql}'"],
        capture_output=True, text=True, timeout=15
    )
    return result

# 1. Archivar 14 y 15
print("Archivando tarjetas 14 y 15...")
sql = "UPDATE oc_deck_cards SET archived=1, done=NOW() WHERE id IN (14, 15);"
r = db_exec2(sql)
print(r.stdout)
if r.stderr:
    print("STDERR:", r.stderr[:200])

# 2. Verificar
print("\nVerificando estado actual de las tarjetas:")
sql2 = "SELECT id, title, archived, done FROM oc_deck_cards WHERE id IN (7, 14, 15, 18);"
r2 = db_exec2(sql2)
print(r2.stdout)
if r2.stderr:
    print("STDERR:", r2.stderr[:200])
