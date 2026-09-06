"""After a game update: which game types and members the module references no longer exist?

    uv run python tools/python/check_members.py [--verbose]

Scans src/GuildrunAccess.Module for the game types it names (every identifier that is a type in a
game assembly per types.tsv) and every serialized-field access (`_camelCase` identifiers), then
checks them against the current dump.cs. A type that vanished, or a field name that exists on no
game type any more, is printed with the files that use it. It is a first pass to point at what an
update renamed; the interop proxies regenerated on the next launch plus `dotnet build` are the
final word, and holders.py / show.py find where a member went.
"""
from __future__ import annotations

import argparse
import re
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402

IDENT_RE = re.compile(r"\b([A-Z][A-Za-z0-9]+)\b")
FIELD_RE = re.compile(r"\._([a-z][A-Za-z0-9]*)\b")
# Our own identifiers that look like game types and would only add noise.
OWN_PREFIXES = ("GuildrunAccess",)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--verbose", action="store_true", help="also list everything that resolved")
    args = ap.parse_args()
    gamelib.require_dump()

    rows = gamelib.load_types()
    game_types = {r.name.split(".")[-1] for r in rows if r.is_game}
    all_types = {r.name.split(".")[-1] for r in rows}

    # Every field name that exists on a game type (backing fields count: interop exposes them).
    field_names: set[str] = set()
    typedef_is_game = {r.typedef: r.is_game for r in rows}
    for block in gamelib.iter_type_blocks():
        if not typedef_is_game.get(block.typedef, False):
            continue
        for _, name, _ in block.fields():
            field_names.add(name)
            m = re.fullmatch(r"<(\w+)>k__BackingField", name)
            if m:
                field_names.add(m.group(1))

    used_types: dict[str, set[str]] = defaultdict(set)
    used_fields: dict[str, set[str]] = defaultdict(set)
    for path in gamelib.MODULE_SRC.rglob("*.cs"):
        text = path.read_text(encoding="utf-8", errors="replace")
        rel = str(path.relative_to(gamelib.MODULE_SRC))
        for ident in set(IDENT_RE.findall(text)):
            if ident in game_types:
                used_types[ident].add(rel)
        for name in set(FIELD_RE.findall(text)):
            used_fields["_" + name].add(rel)

    missing_types = sorted(t for t in used_types if t not in all_types)
    missing_fields = sorted(f for f in used_fields if f not in field_names)
    # Our own private fields are named the same way; only report names no game type has AND that
    # are used through a member access on something (heuristic: the scan already requires "._x").
    own_fields = set()
    for path in gamelib.MODULE_SRC.rglob("*.cs"):
        for m in re.finditer(r"private (?:static |readonly )*[\w<>.,\[\] ]+ (_[a-z]\w*)", path.read_text(encoding="utf-8", errors="replace")):
            own_fields.add(m.group(1))
    missing_fields = [f for f in missing_fields if f not in own_fields]

    print(f"game types referenced: {len(used_types)}, field accesses: {len(used_fields)}")
    print(f"dump: {gamelib.read_version().get('steam_build_id', '?')} (steam build id)")
    if args.verbose:
        for t in sorted(used_types):
            print(f"  ok type  {t}")
    if missing_types:
        print(f"\n!!! {len(missing_types)} type(s) no longer in the dump:")
        for t in missing_types:
            print(f"  {t}  <- {', '.join(sorted(used_types[t]))}")
    if missing_fields:
        print(f"\n!!! {len(missing_fields)} field name(s) on no game type any more (renamed or removed):")
        for f in missing_fields:
            print(f"  {f}  <- {', '.join(sorted(used_fields[f]))}")
    if not missing_types and not missing_fields:
        print("everything the module names still exists in this dump")
        return 0
    return 1


if __name__ == "__main__":
    sys.exit(main())
