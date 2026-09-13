# GuildrunAccess — Accessibility Mod for Guildrun Demo

Screen-reader accessibility mod for blind players. Speaks UI focus, menus, dialogs, and (later) the
run and battle UI via Prism. Sibling project to wotr-access (the UI graph) and NonVisualCalculus (the
host/module hot reload); reuse their patterns where they fit. Speech is the sole interface: a silent
failure is invisible to the player, so every catch logs, and nothing caches game state.

## Game facts
- **Engine**: Unity **6000.0.64f1**, **IL2CPP** x64, metadata v31, unobfuscated. Game code is native
  (`GameAssembly.dll` + `Guildrun_Data/il2cpp_data/Metadata/global-metadata.dat`); we work through
  Il2CppInterop proxies.
- **Install**: `C:\Program Files (x86)\Steam\steamapps\common\Guildrun Demo` (Steam app **4425970**).
  The build resolves it itself (`Directory.Build.props`: registry, `GUILDRUN_DIR`, `-p:GameDir=`, or a
  gitignored `Directory.Build.local.props`).
- **Game code** lives in `ember.*` (gameplay: `ember.scopes.{application,mainMenu,gameRun,battle,shop,
  campfire,event}`, `ember.simulation.core`, `ember.balancing`) and `gg.leyline.*` (studio framework);
  `Assembly-CSharp` is tiny. DI is **VContainer** (one `BootstrappedScope` per area); UI is **uGUI +
  TextMeshPro + Unity Localization**; input is the **new Input System** with legacy Input also enabled
  (`activeInputHandler = Both`), so `UnityEngine.Input.GetKey(KeyCode)` works. `Input.inputString` is
  stripped: type-ahead polls letter keys.
- **Modding framework**: **BepInEx 6.0.0-be.788** (Bleeding Edge, vendored in `third_party/bepinex/`).
  The pre.2 release does NOT load this game (its Cpp2IL accepts metadata 23-29 only). Plugins run on
  **.NET 6** (BepInEx's bundled CoreCLR): engine projects target `net6.0`. Bundles HarmonyX + Il2CppInterop.
  - Interop proxies generate under `<game>\BepInEx\interop\` on the first launch; they are the compile
    targets. Game namespaces are NOT prefixed (`Ember.Scopes.MainMenu.UI.MainMenuUIController`);
    private `[SerializeField]` fields are exposed as public properties (`controller._startButton`).
  - Il2Cpp proxy generics: prefer the non-generic finders, e.g.
    `Object.FindObjectOfType(Il2CppType.Of<T>())?.TryCast<T>()`. `GetComponent<T>()` /
    `GetComponentsInChildren<T>()` work.
- **Speech**: Prism (`third_party/prism/prism.dll`, deployed next to `Guildrun.exe`) via hand-written
  P/Invoke in the host (`src/GuildrunAccess/Speech/`). Prism talks to NVDA/JAWS/SAPI itself.
- **Anti-cheat**: CodeStage ACTk ships, but only `ObscuredCheatingDetectorService` uses it and it merely
  flags analytics on tamper. Never write to `Obscured*` fields.

## Decompiled reference
- `game/il2cppdump/dump.cs` — Il2CppDumper output (gitignored; regenerate with
  `uv run python tools/python/dump_game.py`): every type, field with offset, method with RVA.
  **Look a shape up before guessing it**: `uv run python tools/python/show.py SettingsUIController`.
- `game/il2cppdump/DummyDll/` — stub assemblies for ilspycmd browsing (signatures only, no bodies).
- `game/analysis/types.tsv` — one row per type: typedef, assembly, namespace, kind, name;
  `game/analysis/version.json` — the Steam build id the dump was taken from. The whole of `game/`
  is gitignored and was purged from history: decompiled game material is never committed;
  `dump_game.py` regenerates all of it on a fresh clone.
- For real behavior use the live game: `GET /gui` for structure, `POST /eval` for values (see Dev driver).

## Tools (`tools/python/`, standard library only; run them with `uv run python tools/python/<tool>.py`)
- `dump_game.py [--keep] [--check]` — after a game update: rerun Il2CppDumper (found in `--dumper`,
  `IL2CPPDUMPER_DIR`, `tools/Il2CppDumper`, or `C:\tools\Il2CppDumper`), rebuild `types.tsv`, record
  the build id; `--keep` parks the old dump as `game/il2cppdump.prev` for diffing, `--check` runs
  `check_members.py`. Then launch the game once (the interop proxies regenerate) and `dotnet build`.
- `check_members.py` — every game type and `_field` the module names that the current dump no longer
  has, with the files that use them: the first pass at what an update renamed. The build against the
  regenerated proxies is the final word; `show.py` / `holders.py` find where a member went.
- `show.py Type [Type...] [--grep RE] [--all] [--raw]` — a type's fields (with offsets), properties,
  methods and enum values, with its namespace and assembly. Nested names work (`HeroPanelView.TrackedStatDisplay`).
- `holders.py Type [--game-only]` — who holds a field of that type (how to reach a service or view
  from a scene scan) and which methods take it (what to hook for a notification or event).
- `census.py [--report]` — rebuild `types.tsv`; `--report` is the obfuscation/name-quality census.
- `dev.py <cmd>` — the dev server from the command line: `launch`, `kill`, `reload`, `nav`, `input
  ui.down ui.activate`, `speech --tail 20`, `log --grep X`, `gui --grep RE --context 3`, `eval file.cs`,
  `wait "<bool expr>"`, `typeinfo Name`, `actions`, `module`, `screenshot out.png`, `click [x y]`
  (an OS click at Unity screen coordinates, the fallback for prompts the mod does not cover yet).
- `run_driver.py [--until placement|result|shop|crossroads|event|picker|heroes|end] [--buy]` — plays
  a run forward through the mod's own navigation and stops at the stage you want or at any screen it
  does not know, so new game screens surface for inspection.

Typical update loop: `dump_game.py --keep --check` -> fix what it lists (`show.py`, `holders.py`) ->
`dev.py launch` (proxies regenerate) -> `dotnet build` -> `dev.py reload` -> `run_driver.py` through a run.

## Build & deploy
```
dotnet build            # or .\build.ps1
```
Debug build compiles all five projects and deploys host + Contracts + Roslyn deps + Core + Module to
`<game>\BepInEx\plugins\GuildrunAccess\` and `prism.dll` next to `Guildrun.exe`. **A HOST or Contracts
change needs the game closed** (their DLLs are locked) and a relaunch. **Core/Module changes need NO
restart**: `dotnet build src/GuildrunAccess.Module/GuildrunAccess.Module.csproj` (or `.\build.ps1
-ModuleOnly`) copies just those two DLLs, then `curl -X POST 127.0.0.1:8771/reload` or **F6** in-game
(`GET /module` shows the generation and the DLL write times, so a stale deploy is visible).

- First-time setup: `.\setup-bepinex.ps1`, launch once through Steam to generate `BepInEx\interop`, build.
- Launch: `& "C:\Program Files (x86)\Steam\steam.exe" -applaunch 4425970`. Kill from the Bash tool:
  `MSYS_NO_PATHCONV=1 taskkill.exe /F /IM Guildrun.exe`. Wait for the dev server with
  `curl -s --retry 60 --retry-connrefused --retry-delay 2 --retry-all-errors http://127.0.0.1:8771/health`.
- Tests: `dotnet test src/GuildrunAccess.Tests/GuildrunAccess.Tests.csproj` (Core + Contracts; no game).
- Release: `dotnet build -c Release` compiles without deploying.

## Releases (the tooling is the dd2a11y / Non-Visual Calculus pattern)
The version lives in `Directory.Build.props` alone (`<Version>`, now 0.0.1; the host's
`BuildVersion` constant is generated from it). A release is: bump the version, add a `## Vx.y.z`
section to `CHANGELOG.md` (the release notes are read from it, an empty section fails), commit, tag
`vx.y.z` and push the tag, then:
- `build_release.ps1` — `releases\GuildrunAccess-vX.Y.Z.zip`: the vendored BepInEx 6 zip's game-folder
  layout (BepInEx\, dotnet\, winhttp.dll, doorstop_config.ini; its changelog.txt dropped), the four mod
  DLLs (host, Contracts, Core, Module) under `BepInEx\plugins\GuildrunAccess`, `prism.dll` at the root,
  `BepInEx\config\BepInEx.cfg` seeded with the console window off, `lang\*.txt` and the mdbook manual
  (`docs_src`) when they exist. The dev server's Roslyn assemblies are Debug-only and are NOT shipped.
  The zip root is the game folder: a manual user extracts it over the game dir.
- `build-installer.ps1` — `releases\GuildrunAccessInstaller.exe` from `installer\` (Rust + wxWidgets:
  needs cargo, libclang, ninja; `test-installer.ps1` runs its unit tests). It finds the Steam install
  (registry, library folders, `GUILDRUN_DIR`), downloads the newest release's zip from GitHub, verifies,
  installs with backups and an install manifest, updates, repairs, uninstalls. Its game facts are the
  constants in `installer\src\core\paths.rs` (exe, folder names, the IL2CPP metadata marker, the plugin
  path, the releases URL: `amerikrainian/guildrun-access`).
- `create-release.ps1 vX.Y.Z` — the GitHub release (gh) for the pushed tag, uploading the zip and the
  installer with the CHANGELOG section as notes. The installer matches assets by the exact name
  `GuildrunAccess-vX.Y.Z.zip`, so tags are strict three-part versions.
`releases\`, `installer\target\` and `docs_src\book\` are gitignored.

## Logs
Our lines go through the BepInEx logger with a `[Guildrun Access]` source into
`<game>\BepInEx\LogOutput.log` (truncated each launch), and in-band via `GET /log?since=N&grep=S`.
BepInEx/Il2CppInterop boot problems are at the top of that file.

## Navigation strategy (decided)
**Custom keyboard navigation over the live uGUI widgets** — NOT the game's own EventSystem selection.
We build a key graph per screen from the live views (`Build(GraphBuilder)`, immediate mode: declared
fresh every render, focus persisting by `ControlId` identity), read labels from TMP text at speak time,
and activate through the widget's own event (`button.onClick.Invoke()`), so the game's handler runs as
for a click. While focus mode owns the keyboard, `FocusMode` mutes uGUI navigation events
(`EventSystem.sendNavigationEvents = false`) and clears the selection. Screens resolve their activity
by asking `GameScopes` for their live controller/panel and checking `activeInHierarchy`.

**No scene scans.** `Module/Interop/GameScopes` holds the game's live VContainer scopes, registered by
Harmony postfixes on the scope build path (`BootstrappedScope.InjectControllers`, `LifetimeScope.Build`)
and dropped on `OnDestroy`; each bootstrapped scope lists its `MonoBehaviourController`s, so
`GameScopes.Controller<T>()` is a list walk cached per type (`Component<T>()` for non-controller views,
found once per scope under its hierarchy). `FindObjectOfType` walks ~43k objects and costs ~18 ms
here: never add one. The only scan is `GameScopes.Seed()` on a module (re)load.

## Architecture — HOST/MODULE split (hot reload; the NonVisualCalculus pattern, plus reloadable Core)
Five projects (see `docs/plan.md` for the port map):
- **`GuildrunAccess.Contracts`** (netstandard2.0, PERMANENT): `IModHost`, `IModModule`, `IDevDriver`,
  `ISpeechBackend`, `SpeechPipeline`, `ISettingsStore`/`ModSettings`, `TextFilter`. Loaded once into
  the default context; everything the host holds is typed here.
- **`GuildrunAccess`** (net6.0, PERMANENT host; **changing it needs a game restart — keep it
  minimal**): `Plugin` (entry), `Speech/` (Prism), `Host/HostPump` (the ONE injected MonoBehaviour;
  IL2CPP type registration is permanent), `Modularity/ModuleLoader` (loads Core + Module bytes into ONE
  collectible `AssemblyLoadContext` per generation), `BepInExSettingsStore`, `Dev/` (dev server).
  The host must NEVER reference Core at compile time.
- **`GuildrunAccess.Core`** (netstandard2.0, RELOADABLE, engine-free, unit-tested): `Graph/` (the
  key-graph engine: `KeyGraph`, `GraphBuilder`, `GraphAnnouncer`, `ControlId`), `UI/` (`GraphNavigator`,
  `GraphSheet`, `ControlTypes`, `TypeAheadSearch`), `Input/` (`InputManager` categories + chord
  shadowing), `Screens/` (`Screen`, `CompositeScreen` + `ScreenSection`, `ScreenManager`), `Strings/` (the authored strings table).
  Engine seams are statics the module sets in Load: `CoreLog`, `Speech.Speak`, `NavInput.Current`,
  `Navigation.FocusActive`, `InputManager.FocusActive`, `GraphAnnouncer.PositionText/ExpandedStateText`.
- **`GuildrunAccess.Module`** (net6.0, RELOADABLE, engine-coupled; **day-to-day feature work goes
  here**): `ModuleMain` (IModModule + IDevDriver: seams, input registration, screen registration,
  focus mode, Tick), `Screens/` (the menu, settings, dialogs, compendium, comics, mod menu: one file
  per screen), `GameRun/` (everything of a run: `GameRunScreen` is a `CompositeScreen` of one `Hud/`
  section per Tab-stop, the sections that act on heroes sharing `HeroActions` (menus, equip, inspect)
  and its `HeroMoves` (the pending keyboard drag); the other run screens; `RunData` (registry reads
  and moves), `BattleEvents` (the HUD hooks); `Nodes/` the run's reusable node readers: hero cards,
  items, sidebar, leaderboard), `UI/GameNodes` (node factories over uGUI Button/Toggle/Slider/TMP),
  `UI/TooltipReader`, `UI/FocusMode`, `Input/KeyboardBinding` + `UnityNavInput`. A long screen
  decomposes into sections when they carry state or act on each other; a screen that only reads one
  panel stays whole (never partial classes).
- **`GuildrunAccess.Tests`** (net8.0 xUnit): Core + Contracts only; runs serially (static seams).

Rules: new feature/screen/adapter/patch code goes in Module (Core when engine-free). Only entry, native
handles, sockets, and IL2CPP type injection go in the host. Module Harmony uses a per-load GUID id and
`UnpatchSelf()` in Dispose. The host loads the NEW generation BEFORE disposing the old one (failed-reload
safety) and sets `IModHost.SuccessorLoaded` around the old Dispose: a disposing module restores
suppressed game state (the keyboard device, EventSystem navigation) only when that flag is false (a
shutdown), and just drops its hooks on a reload. Core statics are per-generation (Core loads into the
same collectible context).

## Dev driver (Debug only; on by default, `GRA_NO_DEV=1` disables, `GRA_DEV_PORT` sets the port)
Loopback HTTP on `http://127.0.0.1:8771`, driven with curl. `GRA_NO_SPEECH=1` skips Prism for headless
runs; spoken text is still captured. The game already runs in the background when unfocused.
- `POST /input` body = `up|down|left|right|confirm|back|tab|prev|home|end|secondary|tooltip|read` or any
  registered action key (`ui.down`, `mod.focus`): routed through the module's real dispatch, exactly
  like a key press; the response echoes the new focus readout. `GET /actions` lists the keys.
- `GET /nav` our navigator's state (focus mode, screen stack, focused node, every node in the render).
- `GET /gui` raw uGUI hierarchy (paths, components, TMP text, CanvasGroup alpha) to reverse-engineer a
  screen; `GET /focus` the game's own uGUI selection. Diff `/gui` against `/nav` to find what we miss.
- `GET /speech?since=N` what the mod spoke, tagged interrupt/queue and speaking class (`&wait=MS`
  long-polls); `GET /log?since=N&grep=S`.
- `POST /eval` C# against the live game (Roslyn REPL; state persists; references every interop proxy;
  the first eval after boot fails once on a cold assembly resolve, absorbed at warmup); `POST
  /wait?timeout=MS` a per-frame bool expression; `GET /typeinfo?name=X`; `GET /screenshot`.
- `POST /reload`, `GET /module` (generation, DLL write times, Harmony patch table), `GET /health`.
- Re-show a dismissed panel for testing: find its scene instance with `Resources.FindObjectsOfTypeAll`
  in `/eval` and `SetActive(true)`; `SetActive(false)` afterwards. Never press a consent button for the player.

## Keys (focus mode on at launch; Ctrl+Shift+A toggles it)
Arrows navigate, Tab/Shift+Tab cycle control groups, Enter activates, Backspace is the secondary
action, Escape backs out, Home/End jump, Alt+Up/Down jump sections, typing letters searches the
focused group. Ctrl+arrows review the buffers (below). Space is unbound: nothing is read on demand
by a key; everything a control carries beyond its focus line waits in a buffer.

## Buffers (the Harkest Dungeon pattern: `Core/Buffers`, `Module/UI/Buffers`)
Review lists for the information a focus announcement leaves out, read live on every keypress:
Ctrl+Right/Left switch buffers (speaking "name: current line"), Ctrl+Up/Down step lines (the edges
re-read). Empty buffers are skipped; a focus change re-homes review to the control's buffer. The
roster, in cycling order: **control** (the focused node's head line: label, value, state, never the
role word or the position, then one line per description part and per `NodeVtable.Details` tooltip,
repeats folded: `Core/Buffers/NodeLines`), **hero** and **items** (`NodeVtable.SideLines`: the hero a
slot, cell, unit or card concerns and the items it carries: `GameRun/Nodes/HeroLines`), **relics**
(the run's, one line each), **party** and **enemies** (one line per unit, placement and fights),
**combat** (the battle events log, following its latest line). Conventions: a tooltip is ONE line,
never joined with others; helpers return `List<string>` (`ItemTooltips`, `AbilitiesTooltips`,
`SlotTooltips`...); a node's details go in `Details = () => ...`, never spoken directly. The dev
server drives them through the action keys: `POST /input` with `buffer.next`, `buffer.prev`,
`buffer.line.next`, `buffer.line.prev`.

## Hard rules
- **All speech through `Speech.Say`** (Core) -> the host `SpeechPipeline`; never call Prism directly.
  Navigation moves interrupt; screen entry and feedback queue.
- **Never cache game state.** Hold live widgets and read them at speak time.
- **Reuse game text** (already localized) wherever it exists; every mod-authored word lives in
  `Core/Strings/Strings.cs` (keyed, overridable by `lang/<language>.txt`), never inline in a Speak call.
- **No silent failures**: every catch logs through `CoreLog` / the host logger.
- **Announcements** (mod-authored text only; never reword game text): distinguishing word first, no
  nav hints, no fluff, never less information. Users are expert screen-reader users.
- In Module files with `using UnityEngine;`, alias `Screen` and `Navigation` to the Core types.
- Beware `int.MinValue` "long ago" sentinels: `frameCount - int.MinValue` overflows negative and
  disables a throttle forever. Use `-Interval` or `int.MinValue / 2`.

## Roadmap
1. **(done)** Loader bring-up on Unity 6 (BepInEx be.788), host + collectible-context hot reload, Prism.
2. **(done)** Graph engine + navigator + input substrate in Core, 54 tests.
3. **(done)** Main menu and the modal dialogs (privacy consent, confirm, error, exit, survey).
4. **(done)** Settings (tabs, sliders, toggles, dropdowns) and the run-start flow (difficulty).
5. **(done)** The game run: hero picker, run HUD (placement grid with keyboard moves, party/reserve
   with equip/unequip menus, items, relics, info, speed, menu), battle result (all forms), shop,
   crossroads, events (campfire included), rank-up pickers, the Heroes panel, tooltips through the
   game's own tooltip pipeline. Reusable readers: `GameRun/Nodes/HeroCardNodes`, `ItemNodes`,
   `LeaderboardNodes`, `UI/TooltipReader`; run data and moves through `GameRun/RunData`.
6. **(done)** End screen, progression, the sidebar (inspect cards, damage tracker), battle events
   (`GameRun/BattleEvents`: Harmony postfixes on the HUD views: floating numbers, status icons, empties
   bars, cast animations: the battle events log, a run-HUD stop, and NOTHING spoken as it happens;
   only what the game draws is reported: the simulation's own `BattleLogger` is developer debug text
   and is NOT used), the compendium, the mod menu (Ctrl+Shift+M: settings, key help), comics. The run
   HUD is active only while placing or fighting (`GameRunScreen.IsActive`), so no landing is spoken
   at a battle's end or between panels; a unit falling under focus moves focus silently
   (`NodeVtable.QuietVanish`). Open: Escape on the run HUD only cancels a pending move.

## The game's own hotkeys
The game's Input System action maps bind Tab, Space, Enter, Escape, arrows and letters (Navigation:
Heroes panel, reserve/shop toggle, feedback, back; UI: navigate/submit/cancel; a Player map). While
focus mode is on, `FocusMode` disables the keyboard as an Input System DEVICE
(`InputSystem.DisableDevice`), so no game action hears a key; our own keys are polled through the
legacy input path, which is unaffected. `InputSystem.onDeviceChange` (a managed delegate converted
with `DelegateSupport`) re-disables a keyboard the moment anything enables it again (window focus,
a reload's old generation, a new device), so there is no scanning. Turning focus mode off re-enables
it. Anything a game hotkey did must be offered through our screens instead (the run HUD's Escape
opens the game's settings, Heroes is in the menu).

## Click-only widgets
The comics' click-anywhere polls the pointer through the game's own input service instead of
listening to a widget event (there is no Button): `Input/SyntheticMouse` queues a mouse
move/press/release into Unity's Input System (`InputSystem.QueueStateEvent<MouseState>`), one step
per module tick, which both uGUI and the game's service see as a real click, without moving the OS
cursor. Use it only where there is no widget event to invoke: every Button the game wires at
runtime (`onClick.AddListener`, so `GetPersistentEventCount()` reads 0, the run-over result's
Proceed included) still hears `onClick.Invoke()`, and a synthetic click on it is flaky.
