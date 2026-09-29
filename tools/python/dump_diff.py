"""After a game update: what changed between the previous dump and the current one?

    uv run python tools/python/dump_diff.py [--prev PATH] [--cur PATH] [--all] [--grep RE]
                                            [--no-sizes] [--no-members]

Compares game/il2cppdump.prev/dump.cs (parked by `dump_game.py --keep`) with game/il2cppdump/dump.cs,
game assemblies only (ember.*, gg.leyline.*, Assembly-CSharp; nested and compiler-generated types
included, told apart by their TypeDefIndex against the dump's image table) unless --all:

- types added and removed;
- members added and removed per type: the declaration lines with RVAs, field offsets, tokens and
  metadata offsets stripped, so only a change of shape shows;
- method bodies that changed size: a body's size is the distance from its RVA to the next distinct
  RVA in the binary, compared per type and signature. The dump has no bodies, so this is how a
  behaviour change inside an unchanged signature shows up (a new call in a click handler, a tooltip
  source that builds its text differently). Expect noise: identical bodies the linker folded into
  one (`get_Priority` moving by the same amount across unrelated types), a neighbour's move shifting
  padding. Read each real candidate with `disasm.py Type.Method --calls`; only the new binary is on
  disk, so the old body cannot be disassembled, only reasoned about.
"""
from __future__ import annotations

import argparse
import bisect
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402

PREV_CS = gamelib.REPO / "game" / "il2cppdump.prev" / "dump.cs"

IMAGE_RE = re.compile(r"^// Image \d+: (?P<name>\S+) - (?P<start>\d+)")
RVA_RE = re.compile(r"^\s*// RVA: 0x(?P<rva>[0-9A-Fa-f]+) ")
NOISE = [
    re.compile(r"//\s*RVA:.*$"),
    re.compile(r"//\s*0x[0-9A-Fa-f]+"),
    re.compile(r"/\*Metadata offset 0x[0-9A-Fa-f]+\*/"),
    re.compile(r"// TypeDefIndex: \d+"),
    re.compile(r"\[FieldOffset\(Offset = \"0x[0-9A-Fa-f]+\"\)\]"),
    re.compile(r"\[Token\(Token = \"0x[0-9A-Fa-f]+\"\)\]"),
    re.compile(r"\[Address\([^)]*\)\]"),
]


@dataclass
class TypeInfo:
    game: bool
    members: list[str] = field(default_factory=list)
    methods: dict[str, int] = field(default_factory=dict)  # normalized signature -> RVA


@dataclass
class Dump:
    types: dict[str, TypeInfo]
    sizes: dict[int, int]  # RVA -> size up to the next distinct RVA


def normalize(line: str) -> str:
    for rx in NOISE:
        line = rx.sub("", line)
    return line.strip()


def load(path: Path) -> Dump:
    starts: list[tuple[int, str]] = []
    bounds: list[int] = []
    types: dict[str, TypeInfo] = {}
    namespace = ""
    current: TypeInfo | None = None
    pending_rva: int | None = None
    with path.open(encoding="utf-8", errors="replace") as f:
        for raw in f:
            line = raw.rstrip("\n")
            m = IMAGE_RE.match(line)
            if m:
                starts.append((int(m.group("start")), m.group("name")))
                continue
            if line.startswith("// Namespace:"):
                namespace = line[len("// Namespace:"):].strip()
                continue
            if not line.startswith(("\t", " ")):
                m = gamelib.DECL_RE.match(line)
                if m:
                    if len(bounds) != len(starts):  # the image table heads the file
                        starts.sort()
                        bounds = [s for s, _ in starts]
                    td = int(m.group("td"))
                    i = bisect.bisect_right(bounds, td) - 1
                    image = starts[i][1] if i >= 0 else ""
                    name = f"{namespace}.{m.group('name')}" if namespace else m.group("name")
                    current = types.setdefault(name, TypeInfo(image.startswith(gamelib.GAME_ASSEMBLY_PREFIXES)))
                    pending_rva = None
                    continue
                if line.startswith("}"):
                    current = None
                    continue
            if current is None:
                continue
            m = RVA_RE.match(line)
            if m:
                pending_rva = int(m.group("rva"), 16)
                continue
            s = line.strip()
            if not s or s.startswith(("|", "/*")) or (s.startswith("//") and "RVA" not in s):
                continue
            s = normalize(s)
            if not s:
                continue
            current.members.append(s)
            if pending_rva is not None and "(" in s and not s.startswith("["):
                key = s
                n = 2
                while key in current.methods:  # overloads that normalize alike
                    key = f"{s} #{n}"
                    n += 1
                current.methods[key] = pending_rva
                pending_rva = None
    rvas = sorted({r for t in types.values() for r in t.methods.values()})
    sizes = {rvas[i]: rvas[i + 1] - rvas[i] for i in range(len(rvas) - 1)}
    return Dump(types, sizes)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--prev", type=Path, default=PREV_CS, help="the older dump.cs (default: game/il2cppdump.prev)")
    ap.add_argument("--cur", type=Path, default=gamelib.DUMP_CS, help="the newer dump.cs (default: game/il2cppdump)")
    ap.add_argument("--all", action="store_true", help="include engine and library types, not only the game's")
    ap.add_argument("--grep", help="only types whose full name matches this regex")
    ap.add_argument("--no-members", action="store_true", help="skip the member diff")
    ap.add_argument("--no-sizes", action="store_true", help="skip the body-size diff")
    args = ap.parse_args()
    for p in (args.prev, args.cur):
        if not p.is_file():
            sys.exit(f"{p} is missing: `dump_game.py --keep` parks the previous dump before a new one")

    old, new = load(args.prev), load(args.cur)
    grep = re.compile(args.grep) if args.grep else None

    def wanted(name: str, info: TypeInfo) -> bool:
        if "<PrivateImplementationDetails>" in name or "f__AnonymousType" in name:
            return False
        return (args.all or info.game) and (grep is None or grep.search(name) is not None)

    added = sorted(t for t, i in new.types.items() if t not in old.types and wanted(t, i))
    removed = sorted(t for t, i in old.types.items() if t not in new.types and wanted(t, i))
    print(f"types added: {len(added)}")
    for t in added:
        print(f"  + {t}")
    print(f"types removed: {len(removed)}")
    for t in removed:
        print(f"  - {t}")

    common = sorted(t for t, i in new.types.items() if t in old.types and wanted(t, i))
    if not args.no_members:
        changed = 0
        print()
        for t in common:
            before, after = old.types[t].members, new.types[t].members
            before_set, after_set = set(before), set(after)
            gone = [m for m in before if m not in after_set]
            came = [m for m in after if m not in before_set]
            if not gone and not came:
                continue
            changed += 1
            print(f"~ {t}")
            for m in gone:
                print(f"    - {m}")
            for m in came:
                print(f"    + {m}")
        print(f"types with changed members: {changed}")

    if not args.no_sizes:
        rows = []
        for t in common:
            a, b = old.types[t].methods, new.types[t].methods
            for sig in a.keys() & b.keys():
                sa, sb = old.sizes.get(a[sig], 0), new.sizes.get(b[sig], 0)
                if sa and sb and sa != sb:
                    rows.append((t, sig, sa, sb))
        print()
        by_type = defaultdict(list)
        for t, sig, sa, sb in rows:
            by_type[t].append((sig, sa, sb))
        for t in sorted(by_type):
            print(f"~ {t}")
            for sig, sa, sb in sorted(by_type[t]):
                print(f"    {sa:6d} -> {sb:6d}  {sig}")
        print(f"method bodies resized: {len(rows)} in {len(by_type)} types")
    return 0


if __name__ == "__main__":
    sys.exit(main())
