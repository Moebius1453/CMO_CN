#!/usr/bin/env python3
"""Locate which DB table/column holds the Database Viewer prose for an entity."""
from __future__ import annotations

import sqlite3
import sys
from pathlib import Path

DB = Path("G:/soft/steam/steamapps/common/Command - Modern Operations/DB/DB3K_519.db3")
needle = sys.argv[1] if len(sys.argv) > 1 else "counter-countermeasures"

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
cur = con.cursor()
tables = [r[0] for r in cur.execute(
    "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]

for t in tables:
    try:
        cols = [r[1] for r in cur.execute(f'PRAGMA table_info("{t}")')]
    except sqlite3.Error:
        continue
    for c in cols:
        try:
            rows = cur.execute(
                f'SELECT "{c}" FROM "{t}" WHERE "{c}" LIKE ? LIMIT 2',
                (f"%{needle}%",)).fetchall()
        except sqlite3.Error:
            continue
        for r in rows:
            v = r[0]
            if isinstance(v, (bytes, bytearray)):
                v = f"<blob {len(v)} bytes>"
            v = str(v)
            print(f"--- {t}.{c} ---")
            print(v[:900])
            print()
