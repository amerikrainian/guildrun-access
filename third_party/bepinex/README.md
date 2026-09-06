# BepInEx (vendored loader)

Mod loader for the game. Source: https://github.com/BepInEx/BepInEx

**Version: 6.0.0-be.788+5b766a3** (Bleeding Edge, 2026-09-01), build `BepInEx-Unity.IL2CPP-win-x64`,
from https://builds.bepinex.dev/projects/bepinex_be. Bundles Il2CppInterop 1.5.3, HarmonyX, and a
Cpp2IL that accepts IL2CPP metadata v23-106. Guildrun Demo is Unity 6000.0.64f1 with metadata
v31; the earlier 6.0.0-pre.2 release only accepts v23-29 and fails at interop generation, so a
Bleeding Edge build is required.

## Install (into the game folder)
Run `setup-bepinex.ps1` from the repo root. It finds the Steam install of Guildrun Demo and
extracts the zip over it, dropping the `winhttp.dll` doorstop plus `BepInEx/` and `dotnet/`.
Fully reversible (delete those three).

## First launch
Launch the game **through Steam** (`steam.exe -applaunch 4425970`). The first run is slow: BepInEx
runs Il2CppInterop to generate managed proxy assemblies under `BepInEx/interop/`; those are the
build's compile targets (`Il2Cpp`-prefixed namespaces for game assemblies). It also writes
`BepInEx/config/` and `BepInEx/LogOutput.log`.

## Notes
Mod assemblies run on **.NET 6** (the bundled `dotnet/` CoreCLR). Target `net6.0`.
