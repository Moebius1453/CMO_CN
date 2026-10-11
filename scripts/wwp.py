#!/usr/bin/env python3
"""Parse World Wind Placename (.wwp) files used by CMO for map labels.

Layout (little-endian):
    int32  count
    repeat count times:
        uint8  nameLen
        bytes  name (UTF-8 / ASCII)
        float32 lon
        float32 lat
        uint32 flags
"""
import struct
import sys


def parse(path):
    with open(path, "rb") as fh:
        raw = fh.read()
    count = struct.unpack_from("<i", raw, 0)[0]
    off = 4
    items = []
    for _ in range(count):
        nlen = raw[off]
        off += 1
        name = raw[off:off + nlen].decode("utf-8", "replace")
        off += nlen
        lon, lat, flags = struct.unpack_from("<ffI", raw, off)
        off += 12
        items.append((name, lon, lat, flags))
    return count, off, len(raw), items


if __name__ == "__main__":
    for p in sys.argv[1:]:
        c, off, size, items = parse(p)
        print(f"{p}\n  count={c} consumed={off} filesize={size} trailing={size-off}")
        for it in items[:5]:
            print("   ", it)
