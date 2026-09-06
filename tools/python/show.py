"""Print a game type's shape from dump.cs: fields with offsets, properties, methods.

    uv run python tools/python/show.py TypeName [TypeName ...] [--all] [--grep REGEX] [--raw]

Names may be simple (HeroCardView), nested (HeroPanelView.TrackedStatDisplay) or full
(Ember.Scopes.GameRun.UI.HeroCard.HeroCardView). Compiler-generated members and accessors are
hidden unless --all; --grep keeps only member lines matching a regex; --raw prints the block as is.
This is the "grep the dump before guessing a shape" step made cheap.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("names", nargs="+")
    ap.add_argument("--all", action="store_true", help="include accessors and compiler-generated members")
    ap.add_argument("--grep", help="only members matching this regex")
    ap.add_argument("--raw", action="store_true", help="print the raw block")
    args = ap.parse_args()
    gamelib.require_dump()
    rows = gamelib.load_types()
    by_name: dict[str, list[gamelib.TypeRow]] = {}
    for r in rows:
        by_name.setdefault(r.name, []).append(r)
    grep = re.compile(args.grep) if args.grep else None

    wanted = set(args.names)
    found = 0
    for block in gamelib.iter_type_blocks():
        simple = block.name.split(".")[-1]
        hit = None
        for name in wanted:
            if block.name == name or simple == name or block.name.endswith("." + name) or block.name == name.split(".")[-1]:
                hit = name
                break
        if hit is None:
            continue
        found += 1
        candidates = by_name.get(block.name, [])
        row = next((r for r in candidates if r.typedef == block.typedef), candidates[0] if candidates else None)
        where = f"{row.namespace or '(global)'} [{row.image}]" if row else "?"
        print(f"=== {block.kind} {block.name}  ({where})  dump.cs:{block.line_no}")
        print("    " + block.declaration.strip()[:200])
        if args.raw:
            for line in block.body:
                if not grep or grep.search(line):
                    print(line)
            continue
        consts = block.consts()
        if consts:
            print("  values: " + ", ".join(f"{n}={v}" for n, v in consts))
        for t, n, off in block.fields():
            if not args.all and ("k__BackingField" in n or n.startswith("<")):
                continue
            line = f"  field  0x{off:>4}  {t} {n}"
            if not grep or grep.search(line):
                print(line)
        for t, n, acc in block.properties():
            line = f"  prop   {t} {n} {{ {acc} }}"
            if not grep or grep.search(line):
                print(line)
        for ret, n, params in block.methods():
            if not args.all and (n.startswith("<") or "b__" in n):
                continue
            line = f"  method {ret} {n}({params})"
            if not grep or grep.search(line):
                print(line)
        print()
    if not found:
        print("no such type; try a suffix or check game/analysis/types.tsv", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
