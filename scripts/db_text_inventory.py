#!/usr/bin/env python3
"""Inventory the DB3K database text that the Database Viewer renders as prose.

Reports, per table, the volume of long descriptive text columns so the
localization backlog can be sized precisely.
"""
from __future__ import annotations

import sqlite3
import sys
from pathlib import Path

DB = Path(sys.argv[1] if len(sys.argv) > 1 else
          "G:/soft/steam/steamapps/common/Command - Modern Operations/DB/DB3K_519.db3")

con = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
cur = con.cursor()
tables = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]

TEXT_HINT = ("overview", "details", "notes", "sources", "remark", "comment",
             "description", "desc", "fact", "text", "notes", "spec")

total_rows = 0
total_chars = 0
print(f"{'table':<34}{'column':<22}{'rows':>7}{'chars':>10}")
for t in tables:
    try:
        cols = [r[1] for r in cur.execute(f'PRAGMA table_info("{t}")')]
    except sqlite3.Error:
        continue
    text_cols = [c for c in cols if any(h in c.lower() for h in TEXT_HINT)]
    if not text_cols:
        continue
    for c in text_cols:
        try:
            row = cur.execute(
                f'SELECT COUNT(*), SUM(LENGTH(COALESCE("{c}",\'\'))) FROM "{t}" '
                f'WHERE "{c}" IS NOT NULL AND LENGTH("{c}") > 40'
            ).fetchone()
        except sqlite3.Error:
            continue
        n, chars = row[0] or 0, row[1] or 0
        if n == 0:
            continue
        total_rows += n
        total_chars += chars
        print(f"{t:<34}{c:<22}{n:>7}{chars:>10}")
print(f"\nTOTAL long-text rows: {total_rows}   chars: {total_chars}   unique? (not deduped)")
