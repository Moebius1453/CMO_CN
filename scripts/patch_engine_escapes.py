#!/usr/bin/env python3
"""One-shot source patcher: teach the engine's JSON scanner full escape decoding.

The hand-rolled scanner in src/engine/CMOLocalizer.cs only understood \\" \\\\ \\n
\\r \\t.  Values containing \\uXXXX (for example a NUL in a network host string)
were decoded as literal text, so those entries never matched.

Run once; it is idempotent and reports what it did.
"""
from __future__ import annotations

import pathlib

SRC = pathlib.Path(__file__).resolve().parent.parent / "src/engine/CMOLocalizer.cs"
IND = " " * 24


BS = chr(92)          # a single backslash character
BS2 = BS + BS         # the two-character sequence expected by the C# source


def block(sb: str) -> list[str]:
    b = BS
    q = BS2
    return [
        IND + "if (c == '\"') sb" + sb + ".Append('\"');",
        IND + "else if (c == '" + q + "') sb" + sb + ".Append('" + q + "');",
        IND + "else if (c == '/') sb" + sb + ".Append('/');",
        IND + "else if (c == 'b') sb" + sb + ".Append('" + b + "b');",
        IND + "else if (c == 'f') sb" + sb + ".Append('" + b + "f');",
        IND + "else if (c == 'n') sb" + sb + ".Append('" + b + "n');",
        IND + "else if (c == 'r') sb" + sb + ".Append('" + b + "r');",
        IND + "else if (c == 't') sb" + sb + ".Append('" + b + "t');",
        IND + "else if (c == 'u' && p + 5 < block.Length)",
        IND + "{",
        IND + "    int code;",
        IND + "    if (int.TryParse(block.Substring(p + 2, 4),",
        IND + "            NumberStyles.HexNumber,",
        IND + "            CultureInfo.InvariantCulture, out code))",
        IND + "    {",
        IND + "        sb" + sb + ".Append((char)code);",
        IND + "        p += 6;",
        IND + "        continue;",
        IND + "    }",
        IND + "    sb" + sb + ".Append('" + q + "'); sb" + sb + ".Append(c);",
        IND + "}",
        IND + "else { sb" + sb + ".Append('" + q + "'); sb" + sb + ".Append(c); }",
        IND + "p += 2;",
    ]


def main() -> int:
    lines = SRC.read_text(encoding="utf-8").splitlines(keepends=True)

    if any("NumberStyles.HexNumber" in l for l in lines):
        print("already patched; nothing to do")
        return 0

    tab_arm = "Append('" + BS + "t')"
    for sb in ("Val", "Key"):
        idx = None
        for i, l in enumerate(lines):
            if tab_arm in l and ("sb%s." % sb) in l:
                idx = i
                break
        if idx is None:
            print("ERROR: could not locate the %s escape chain" % sb)
            return 1
        start, end = idx - 4, idx + 2
        assert ("sb%s.Append" % sb) in lines[start], lines[start]
        assert "Append(c); }" in lines[idx + 1], lines[idx + 1]
        lines[start:end] = [b + "\n" for b in block(sb)]
        print("patched %s escape chain (was line %d)" % (sb, start + 1))

    text = "".join(lines)
    if "using System.Globalization;" not in text:
        text = text.replace("using System.Collections.Generic;\n",
                            "using System.Collections.Generic;\n"
                            "using System.Globalization;\n", 1)
        print("added System.Globalization using")
    SRC.write_text(text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
