"""Disassemble a game method's native body from GameAssembly.dll, by name.

    uv run --with capstone python tools/python/disasm.py Type.Method [Type.Method ...] [--calls] [--bytes N]

The dump has no method bodies (IL2CPP), only each method's RVA and file offset; this reads the
bytes at that offset and prints x64 assembly up to the first ret, with every direct call or jump
target resolved to its dump name, and `[reg + off]` reads annotated with the method's own class
field at that offset (a heuristic: only the first field of that offset, never for rsp/rbp). Names
are the dump's: `MainMenuUIController.UpdateButtonStates`, a lambda
`NavigationUIController.<OnStart>b__50_8`, a coroutine `RunSessionService.<GoBackToMenuAsync>d__39.MoveNext`.
--calls keeps only the resolved calls and field lines: the method's shape in a dozen lines. This is
how "what does Quit to Menu actually do" gets answered without the live game (needs capstone, hence
`uv run --with capstone`).
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gamelib  # noqa: E402

TYPE_RE = re.compile(r"^(?:public |private |internal |protected )?(?:sealed |static |abstract )*(?:class|struct|interface|enum) ([\w.<>`]+)")
RVA_RE = re.compile(r"RVA: 0x([0-9A-Fa-f]+) Offset: 0x([0-9A-Fa-f]+)")
METHOD_RE = re.compile(r"^\t(?:[\w<>\[\],.`]+ )*?([\w<>.`]+)\(.*\)")
FIELD_RE = re.compile(r"^\t.* (\w+); // 0x([0-9A-Fa-f]+)")
MEM_RE = re.compile(r"\[(r\w+) \+ 0x([0-9a-f]+)\]")


def index_dump(dump: Path, wanted: set[str]):
    """RVA -> 'Type.Method' for every method, (rva, offset) for the wanted names, and the fields
    of every class by (class, offset)."""
    rva_to_name: dict[int, str] = {}
    targets: dict[str, tuple[int, int]] = {}
    fields: dict[tuple[str, int], str] = {}
    cls = None
    pending = None
    with dump.open(encoding="utf-8", errors="replace") as f:
        for line in f:
            m = TYPE_RE.match(line)
            if m:
                cls = m.group(1)
                continue
            if line.lstrip().startswith("//"):
                m = RVA_RE.search(line)
                if m:
                    pending = (int(m.group(1), 16), int(m.group(2), 16))
                continue
            if pending and line.startswith("\t") and "(" in line:
                m = METHOD_RE.match(line)
                name = (cls or "?") + "." + (m.group(1) if m else line.strip())
                rva_to_name.setdefault(pending[0], name)
                if name in wanted:
                    targets[name] = pending
                pending = None
            elif line.startswith("\t") and "//" in line and cls:
                m = FIELD_RE.match(line)
                if m:
                    fields.setdefault((cls, int(m.group(2), 16)), m.group(1))
    return rva_to_name, targets, fields


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("names", nargs="+", help="Type.Method names as dump.cs spells them")
    ap.add_argument("--calls", action="store_true", help="only the lines with a resolved call/jump or a field read")
    ap.add_argument("--bytes", type=int, default=0x2000, help="bytes to read past the method start (default 8 KiB)")
    args = ap.parse_args()

    try:
        import capstone
    except ImportError:
        print("capstone is not installed: run with `uv run --with capstone python tools/python/disasm.py ...`", file=sys.stderr)
        return 2

    dump = gamelib.require_dump()
    assembly = gamelib.game_dir() / "GameAssembly.dll"
    if not assembly.is_file():
        print(f"GameAssembly.dll not found at {assembly}", file=sys.stderr)
        return 2
    data = assembly.read_bytes()

    wanted = set(args.names)
    rva_to_name, targets, fields = index_dump(dump, wanted)
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)

    status = 0
    for name in args.names:
        if name not in targets:
            print(f"not found in dump.cs: {name}")
            status = 1
            continue
        rva, offset = targets[name]
        owner = name.rsplit(".", 1)[0]
        print(f"=== {name}  RVA 0x{rva:X}  offset 0x{offset:X}")
        for insn in md.disasm(data[offset:offset + args.bytes], rva):
            note = ""
            if insn.mnemonic in ("call", "jmp") and insn.op_str.startswith("0x"):
                target = rva_to_name.get(int(insn.op_str, 16))
                if target:
                    note = "    ; " + target
            m = MEM_RE.search(insn.op_str)
            if m and m.group(1) not in ("rsp", "rbp"):
                field = fields.get((owner, int(m.group(2), 16)))
                if field:
                    note += "    ; " + owner + "." + field
            if not args.calls or note:
                print(f"  {insn.address:08X}  {insn.mnemonic:<8} {insn.op_str}{note}")
            if insn.mnemonic in ("ret", "int3"):
                break
    return status


if __name__ == "__main__":
    sys.exit(main())
