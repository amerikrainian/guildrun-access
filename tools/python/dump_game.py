"""Regenerate the decompiled reference for the installed game version.

    uv run python tools/python/dump_game.py [--dumper DIR] [--check] [--keep]

Runs Il2CppDumper over the game's GameAssembly.dll + global-metadata.dat into game/il2cppdump/
(gitignored), rebuilds game/analysis/types.tsv through census.py, records the Steam build id in
game/analysis/version.json, and with --check reports the game members the module references that
no longer exist (check_members.py). Do this after every game update, before rebuilding: the interop
proxies under <game>\\BepInEx\\interop regenerate themselves on the next launch, but the dump is what
you grep to find where a renamed member went.

Il2CppDumper is looked for in --dumper, IL2CPPDUMPER_DIR, tools/Il2CppDumper, then C:\\tools\\Il2CppDumper.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402


def find_dumper(explicit: str | None) -> Path:
    candidates = [
        explicit,
        os.environ.get("IL2CPPDUMPER_DIR"),
        str(gamelib.REPO / "tools" / "Il2CppDumper"),
        r"C:\tools\Il2CppDumper",
    ]
    for c in candidates:
        if not c:
            continue
        exe = Path(c) / "Il2CppDumper.exe"
        if exe.is_file():
            return exe
    sys.exit("Il2CppDumper.exe not found: pass --dumper DIR or set IL2CPPDUMPER_DIR "
             "(https://github.com/Perfare/Il2CppDumper releases)")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dumper", help="Il2CppDumper directory")
    ap.add_argument("--check", action="store_true", help="run check_members.py afterwards")
    ap.add_argument("--keep", action="store_true", help="keep the previous dump as game/il2cppdump.prev for diffing")
    args = ap.parse_args()

    game = gamelib.game_dir()
    assembly = game / "GameAssembly.dll"
    metadata = game / "Guildrun_Data" / "il2cpp_data" / "Metadata" / "global-metadata.dat"
    for p in (assembly, metadata):
        if not p.is_file():
            sys.exit(f"missing: {p}")
    dumper = find_dumper(args.dumper)

    out = gamelib.DUMP_DIR
    if out.exists():
        if args.keep:
            prev = out.with_name("il2cppdump.prev")
            shutil.rmtree(prev, ignore_errors=True)
            out.rename(prev)
            print(f"previous dump kept as {prev}")
        else:
            shutil.rmtree(out)
    out.mkdir(parents=True)

    print(f"game:     {game}")
    print(f"build id: {gamelib.steam_build_id(game)}")
    print(f"dumper:   {dumper}")
    started = time.time()
    # Il2CppDumper writes into its output dir; it ends with "Press any key to exit" and does not
    # return a meaningful exit code, so success is judged by the dump it produced.
    result = subprocess.run([str(dumper), str(assembly), str(metadata), str(out)],
                            cwd=str(dumper.parent), capture_output=True, text=True, stdin=subprocess.DEVNULL)
    sys.stdout.write(result.stdout[-2000:].replace("Press any key to exit...", "").rstrip() + "\n")
    dump_cs = out / "dump.cs"
    if not dump_cs.is_file() or dump_cs.stat().st_size < 1_000_000:
        sys.stderr.write(result.stderr)
        sys.exit("Il2CppDumper produced no dump.cs")
    print(f"dump.cs written in {time.time() - started:.0f}s ({(out / 'dump.cs').stat().st_size // 1_000_000} MB)")

    # The type census (types.tsv) and the version record.
    subprocess.check_call([sys.executable, str(Path(__file__).with_name("census.py"))])
    version = {
        "steam_build_id": gamelib.steam_build_id(game),
        "game_assembly_bytes": assembly.stat().st_size,
        "game_assembly_mtime": datetime.fromtimestamp(assembly.stat().st_mtime, timezone.utc).isoformat(),
        "dumped_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "dumper": str(dumper),
    }
    gamelib.ANALYSIS_DIR.mkdir(parents=True, exist_ok=True)
    gamelib.VERSION_JSON.write_text(json.dumps(version, indent=2) + "\n", encoding="utf-8")
    print(f"recorded {gamelib.VERSION_JSON.relative_to(gamelib.REPO)}")

    if args.check:
        return subprocess.call([sys.executable, str(Path(__file__).with_name("check_members.py"))])
    print("next: `uv run python tools/python/check_members.py`, then launch once (interop regenerates) and `dotnet build`")
    return 0


if __name__ == "__main__":
    sys.exit(main())
