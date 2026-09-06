"""Shared helpers for the tools: where the game and the dump live, and a parser for dump.cs.

Run every tool with uv (`uv run python tools/python/<tool>.py ...`); they need only the standard
library. The game directory resolves the same way the build does: GUILDRUN_DIR, then Steam's
library from the registry, then the default install path.
"""
from __future__ import annotations

import json
import os
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

REPO = Path(__file__).resolve().parents[2]
DUMP_DIR = REPO / "game" / "il2cppdump"
DUMP_CS = DUMP_DIR / "dump.cs"
ANALYSIS_DIR = REPO / "game" / "analysis"
TYPES_TSV = ANALYSIS_DIR / "types.tsv"
VERSION_JSON = ANALYSIS_DIR / "version.json"
MODULE_SRC = REPO / "src" / "GuildrunAccess.Module"
STEAM_APP_ID = "4425970"
DEV_URL = os.environ.get("GRA_DEV_URL", "http://127.0.0.1:" + os.environ.get("GRA_DEV_PORT", "8771"))

GAME_ASSEMBLY_PREFIXES = ("Assembly-CSharp", "ember.", "gg.leyline.")


def game_dir() -> Path:
    """The game install, found the way Directory.Build.props finds it."""
    env = os.environ.get("GUILDRUN_DIR")
    if env and Path(env).is_dir():
        return Path(env)
    for hive, key, value in (
        ("HKEY_CURRENT_USER", r"Software\Valve\Steam", "SteamPath"),
        ("HKEY_LOCAL_MACHINE", r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        ("HKEY_LOCAL_MACHINE", r"SOFTWARE\Valve\Steam", "InstallPath"),
    ):
        try:
            import winreg  # Windows only

            root = getattr(winreg, hive)
            with winreg.OpenKey(root, key) as k:
                steam, _ = winreg.QueryValueEx(k, value)
            candidate = Path(steam) / "steamapps" / "common" / "Guildrun Demo"
            if candidate.is_dir():
                return candidate
        except OSError:
            continue
    default = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Guildrun Demo")
    if default.is_dir():
        return default
    sys.exit("Guildrun Demo not found: set GUILDRUN_DIR")


def steam_build_id(game: Path | None = None) -> str | None:
    """Steam's build id for the installed demo (changes with every game update)."""
    game = game or game_dir()
    manifest = game.parents[1] / f"appmanifest_{STEAM_APP_ID}.acf"
    try:
        text = manifest.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None
    m = re.search(r'"buildid"\s+"(\d+)"', text)
    return m.group(1) if m else None


def require_dump() -> Path:
    if not DUMP_CS.is_file():
        sys.exit(f"{DUMP_CS} is missing: run `uv run python tools/python/dump_game.py` first")
    return DUMP_CS


# ---- types.tsv ----

@dataclass
class TypeRow:
    typedef: int
    image: str
    namespace: str
    kind: str
    name: str

    @property
    def full_name(self) -> str:
        return f"{self.namespace}.{self.name}" if self.namespace else self.name

    @property
    def is_game(self) -> bool:
        return self.image.startswith(GAME_ASSEMBLY_PREFIXES)


def load_types(path: Path = TYPES_TSV) -> list[TypeRow]:
    if not path.is_file():
        sys.exit(f"{path} is missing: run `uv run python tools/python/census.py`")
    rows = []
    with path.open(encoding="utf-8") as f:
        next(f)
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) != 5:
                continue
            rows.append(TypeRow(int(parts[0]), parts[1], parts[2], parts[3], parts[4]))
    return rows


def find_types(rows: Iterable[TypeRow], name: str) -> list[TypeRow]:
    """Types matching a simple name, a nested name (Outer.Inner) or a full name."""
    exact = [r for r in rows if r.name == name or r.full_name == name]
    if exact:
        return exact
    suffix = "." + name
    return [r for r in rows if r.name.endswith(suffix) or r.full_name.endswith(suffix)]


# ---- dump.cs ----

DECL_RE = re.compile(r"^(?P<mods>(?:\w+ )*?)(?P<kind>class|struct|enum|interface) (?P<name>\S+)(?P<rest>.*)// TypeDefIndex: (?P<td>\d+)")
FIELD_RE = re.compile(r"^\s*(?P<mods>(?:public|private|protected|internal|readonly|static|const|new)\s+)+(?P<type>[^;=]+?)\s+(?P<name>[\w.<>`]+)(?:\s*=\s*[^;]+)?;\s*//\s*0x(?P<off>[0-9A-Fa-f]+)")
CONST_RE = re.compile(r"^\s*public const \S+ (?P<name>\w+) = (?P<value>[^;]+);")
METHOD_RE = re.compile(r"^\s*(?P<mods>(?:public|private|protected|internal|static|virtual|override|abstract|sealed|new|extern|unsafe)\s+)+(?P<ret>[^\s(]+(?:<[^(]*>)?)\s+(?P<name>[\w.<>|`$]+)\((?P<params>[^)]*)\)")
PROP_RE = re.compile(r"^\s*(?P<mods>(?:public|private|protected|internal|static|virtual|override|abstract)\s+)+(?P<type>[^\s{]+(?:<[^{]*>)?)\s+(?P<name>[\w.<>`]+)\s*\{(?P<acc>[^}]*)\}")


@dataclass
class TypeDump:
    """One type's declaration block from dump.cs."""
    name: str
    kind: str
    typedef: int
    line_no: int
    declaration: str
    body: list[str] = field(default_factory=list)

    def fields(self) -> list[tuple[str, str, str]]:
        """(type, name, offset) for every field, backing fields included."""
        out = []
        for line in self.body:
            m = FIELD_RE.match(line)
            if m and " const " not in line:
                out.append((m.group("type").strip(), m.group("name"), m.group("off")))
        return out

    def consts(self) -> list[tuple[str, str]]:
        return [(m.group("name"), m.group("value")) for m in map(CONST_RE.match, self.body) if m]

    def properties(self) -> list[tuple[str, str, str]]:
        out = []
        for line in self.body:
            if "{ get" not in line and "{ set" not in line:
                continue
            m = PROP_RE.match(line)
            if m:
                out.append((m.group("type"), m.group("name"), m.group("acc").strip()))
        return out

    def methods(self) -> list[tuple[str, str, str]]:
        """(return type, name, params) for every method, skipping accessors and compiler junk."""
        out = []
        for line in self.body:
            if "{ get" in line or "{ set" in line or " // 0x" in line:
                continue
            m = METHOD_RE.match(line)
            if not m:
                continue
            name = m.group("name")
            if name.startswith(("get_", "set_", "add_", "remove_", "<", ".cctor")) or "b__" in name:
                continue
            out.append((m.group("ret"), name, m.group("params").strip()))
        return out


def iter_type_blocks(dump: Path = DUMP_CS):
    """Yield every type block of dump.cs (namespace comments are attached to the declaration)."""
    with dump.open(encoding="utf-8", errors="replace") as f:
        current: TypeDump | None = None
        for i, raw in enumerate(f, 1):
            line = raw.rstrip("\n")
            m = DECL_RE.match(line)
            if m and not line.startswith(("\t", " ")):
                if current is not None:
                    yield current
                current = TypeDump(m.group("name"), m.group("kind"), int(m.group("td")), i, line)
                continue
            if current is None:
                continue
            if line.startswith("}"):
                yield current
                current = None
                continue
            current.body.append(line)
        if current is not None:
            yield current


def type_block(name: str, dump: Path = DUMP_CS) -> list[TypeDump]:
    """The declaration block(s) of a type by simple, nested or full name."""
    wanted = {name, name.split(".")[-1]}
    found = []
    for block in iter_type_blocks(dump):
        simple = block.name.split(".")[-1]
        if block.name in wanted or simple == name or block.name.endswith("." + name):
            found.append(block)
    return found


def read_version() -> dict:
    try:
        return json.loads(VERSION_JSON.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
