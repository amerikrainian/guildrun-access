"""Find who holds or handles a game type: fields of that type, and methods taking it.

    uv run python tools/python/holders.py TypeName [--game-only] [--limit N]

Answers "which controller has a reference to X" (the way to reach a service or view from a scene
scan) and "who receives X" (the handler to hook with Harmony for an event or notification type).
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
    ap.add_argument("name")
    ap.add_argument("--game-only", action="store_true", help="skip engine and framework assemblies")
    ap.add_argument("--limit", type=int, default=60)
    args = ap.parse_args()
    gamelib.require_dump()
    rows = {r.typedef: r for r in gamelib.load_types()}
    simple = args.name.split(".")[-1]
    field_re = re.compile(r"(?:^|[\s<,])" + re.escape(simple) + r"(?:[>\s,\[])")
    param_re = re.compile(r"\(([^)]*)\)")

    holders, handlers = [], []
    for block in gamelib.iter_type_blocks():
        row = rows.get(block.typedef)
        if args.game_only and (row is None or not row.is_game):
            continue
        if block.name.split(".")[-1] == simple:
            continue
        for t, n, off in block.fields():
            if field_re.search(t + " "):
                holders.append((block.name, f"{t} {n}  // 0x{off}"))
        for ret, n, params in block.methods():
            m = param_re.search("(" + params + ")")
            if params and field_re.search(params + " "):
                handlers.append((block.name, f"{ret} {n}({params})"))

    print(f"=== fields typed {simple} ({len(holders)}) ===")
    for owner, text in holders[: args.limit]:
        print(f"  {owner}: {text}")
    print(f"=== methods taking {simple} ({len(handlers)}) ===")
    for owner, text in handlers[: args.limit]:
        print(f"  {owner}: {text}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
