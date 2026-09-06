"""Rebuild game/analysis/types.tsv from game/il2cppdump/dump.cs (one row per type).

    uv run python tools/python/census.py [--report]

types.tsv is committed (the dump is not) and is what show.py, holders.py and check_members.py use
to map a simple type name to its namespace and assembly. --report prints the per-assembly counts
and a name-quality census (the obfuscation check from the first investigation).
"""
from __future__ import annotations

import argparse
import collections
import random
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402

IMG_RE = re.compile(r"^// Image (\d+): (\S+) - (\d+)")
NS_RE = re.compile(r"^// Namespace: ?(.*)$")
DECL_RE = re.compile(r"^(?:\w+ )*?(class|struct|enum|interface) (\S+).*// TypeDefIndex: (\d+)")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--report", action="store_true", help="print the assembly and name-quality census")
    args = ap.parse_args()
    dump = gamelib.require_dump()

    images, types, ns = [], [], ""
    with dump.open(encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.rstrip("\n")
            m = IMG_RE.match(line)
            if m:
                images.append((int(m.group(3)), m.group(2)))
                continue
            m = NS_RE.match(line)
            if m:
                ns = m.group(1).strip()
                continue
            if line.startswith(("\t", "//", "[", " ")):
                continue
            m = DECL_RE.match(line)
            if m:
                types.append((int(m.group(3)), ns, m.group(1), m.group(2)))
    images.sort()

    def image_for(typedef: int) -> str:
        lo, hi = 0, len(images) - 1
        while lo < hi:
            mid = (lo + hi + 1) // 2
            if images[mid][0] <= typedef:
                lo = mid
            else:
                hi = mid - 1
        return images[lo][1]

    per = collections.defaultdict(list)
    gamelib.ANALYSIS_DIR.mkdir(parents=True, exist_ok=True)
    with gamelib.TYPES_TSV.open("w", encoding="utf-8", newline="\n") as o:
        o.write("typedef\timage\tnamespace\tkind\tname\n")
        for td, n, k, nm in types:
            im = image_for(td)
            per[im].append((n, k, nm))
            o.write(f"{td}\t{im}\t{n}\t{k}\t{nm}\n")
    print(f"{len(types)} types -> {gamelib.TYPES_TSV.relative_to(gamelib.REPO)}")

    if not args.report:
        return 0
    game_imgs = [i for i in per if i.startswith(gamelib.GAME_ASSEMBLY_PREFIXES)]
    print("\n=== per-assembly type counts (game code) ===")
    for i in sorted(game_imgs, key=lambda x: -len(per[x])):
        print(f"{len(per[i]):6d}  {i}")
    names = [nm.split(".")[-1] for i in game_imgs for (_, _, nm) in per[i] if "<" not in nm]

    def randomish(n: str) -> bool:
        base = re.sub(r"`\d+$", "", n)
        return re.fullmatch(r"[a-zA-Z0-9_]{6,}", base) is not None and not re.search(r"[A-Z][a-z]{2,}|^[a-z]{3,}", base)

    print("\n=== name-quality census (obfuscation check) ===")
    print("named types:", len(names))
    print("1-2 char names:", sum(1 for n in names if re.fullmatch(r"[A-Za-z_]{1,2}", n)))
    print("non-ASCII names:", sum(1 for n in names if any(ord(c) > 127 for c in n)))
    print("random-looking:", sum(map(randomish, names)), [n for n in names if randomish(n)][:15])
    print("\n=== namespaces (top 30) ===")
    counter = collections.Counter(n for i in game_imgs for (n, _, _) in per[i])
    for n, c in counter.most_common(30):
        print(f"{c:5d}  {n or '(global)'}")
    random.seed(1)
    print("\n=== random sample of 40 type names ===")
    print(" ".join(random.sample(names, min(40, len(names)))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
