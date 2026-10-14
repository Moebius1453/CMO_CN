#!/usr/bin/env python3
"""Batch translate a range of chunks using concurrent workers, saving to dict/db_desc_out/."""
import json
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
from translate_worker import translate_chunk_data

CHUNKS_DIR = ROOT / "dict/db_desc_chunks"
OUT_DIR = ROOT / "dict/db_desc_out"
OUT_DIR.mkdir(parents=True, exist_ok=True)

def process_chunk(chunk_path: Path):
    out_file = OUT_DIR / chunk_path.name
    if out_file.exists() and out_file.stat().st_size > 10:
        try:
            d = json.loads(out_file.read_text(encoding="utf-8"))
            if isinstance(d, list) and len(d) > 0:
                return chunk_path.stem, len(d), True # already done
        except Exception:
            pass

    items = json.loads(chunk_path.read_text(encoding="utf-8"))
    res = translate_chunk_data(items, max_retries=4)
    if res:
        out_file.write_text(json.dumps(res, ensure_ascii=False, indent=1), encoding="utf-8")
        return chunk_path.stem, len(res), False
    else:
        return chunk_path.stem, 0, False

def main():
    start = int(sys.argv[1]) if len(sys.argv) > 1 else 1
    end = int(sys.argv[2]) if len(sys.argv) > 2 else 300
    workers = int(sys.argv[3]) if len(sys.argv) > 3 else 5

    chunk_files = []
    for i in range(start, end + 1):
        p = CHUNKS_DIR / f"chunk{i:03d}.json"
        if p.exists():
            chunk_files.append(p)

    print(f"==> Processing {len(chunk_files)} chunks (from chunk{start:03d} to chunk{end:03d}) with {workers} workers...")
    t0 = time.time()
    completed = 0
    skipped = 0
    failed = []

    with ThreadPoolExecutor(max_workers=workers) as executor:
        futures = {executor.submit(process_chunk, p): p for p in chunk_files}
        for fut in as_completed(futures):
            p = futures[fut]
            try:
                name, count, is_skip = fut.result()
                if is_skip:
                    skipped += 1
                elif count > 0:
                    completed += 1
                    if completed % 5 == 0 or completed == len(chunk_files):
                        print(f"[{completed + skipped}/{len(chunk_files)}] {name}: translated {count} lines ({time.time()-t0:.1f}s)")
                else:
                    failed.append(name)
                    print(f"FAILED: {name}")
            except Exception as e:
                failed.append(p.stem)
                print(f"EXCEPTION {p.stem}: {e}")

    t1 = time.time()
    print(f"\nFinished batch in {t1-t0:.1f}s: {completed} translated, {skipped} skipped, {len(failed)} failed.")
    if failed:
        print(f"Retrying {len(failed)} failed chunks...")
        for name in failed:
            p = CHUNKS_DIR / f"{name}.json"
            res_name, count, _ = process_chunk(p)
            print(f"Retry {res_name}: {count} lines")

if __name__ == "__main__":
    main()
