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
- **Run structure**: the demo is ONE `ActEntry` of two `ActChunk`s, six floor nodes each (a fight per
  floor, a crossroads or event between them; `RunSessionData.ActChunks`, `CurrentChunkIndex`,
  `CurrentFloorNodeIndex`; `CurrentActIndex` stays 0 and `IsLastAct` is true from the first floor).
  What the game's text calls "Act 1" is chunk 0, its last floor the "Act Boss" (`FloorNodeData.
  TryGetBossName`); chunk 1 ends on the "Final boss". The map strip (`ChunkUIController.
  InitializeActStructure`) draws the CURRENT chunk alone and, while a later one is to come, a dots
  image (`_dotsNode`: no text, no tooltip) and the final boss after it; `MapSection` gives the dots
  a line (`Strings.RunMapMore`), or the final boss reads as the stage after the act boss.
- **Saves**: `%LocalAppData%Low\Leyline\Guildrun\Saves\steam-<id>\{Profile,Run}` (Steam Cloud
  synced). The game saves a run only while `GameRunPersistenceService.Data.IsSavingActive` is on,
  which it turns on once the tutorial's save-point step (`Ftue_11_ThirdShop`) completes; Quit to
  Menu saves through the same gate, so before that point the run is dropped, and the main menu's
  `HandleUnfinishedTutorial` resets the tutorial progress whenever a run ends short of the save
  point (`ProgressionReader.HasActiveTutorialProgress`), so the first run replays: its comic, its
  fixed hero choices, the difficulty locked. The tutorial (`gg.leyline.tutorialsystem`: one
  `TutorialStepFlow` per step under the run scope, a list of `TutorialFlowElement`s) shows timed
  texts behind a modal (`TimedInfoFlowElement`, 15 s each, a legacy `Input.GetMouseButtonDown`
  click skipping one by setting `_elapsedTime = _waitDuration`) BEFORE its action prompt starts
  listening (`WaitForHeroPickElement` and kin subscribe when they start), so a player who acts
  through the modal is not heard and the step never completes. `Screens/TutorialPromptScreen` is
  the keyboard's modal: exclusive while an executing flow's first incomplete element is a timed
  text or a delay, each text a control, Enter/Escape the click. The game's own logs are in
  `Guildrun_Data\Logs\<date>-game.log` ("Completing step", "Tutorial save point reached"). The
  main menu's `UpdateButtonStates` runs from its `OnStart`, a frame or more after the scope is
  injected: until then every prefab button is active.

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
- `disasm.py Type.Method [...] [--calls]` (run with `uv run --with capstone python ...`) — the native
  body of a game method from `GameAssembly.dll` at the dump's RVA, calls resolved to dump names and
  field reads annotated; `--calls` is the dozen-line shape. The dump has no bodies: this is how a
  game rule (what Quit to Menu does, what gates a save) is read without guessing. Lambdas and
  coroutines by their dump names (`NavigationUIController.<OnStart>b__50_8`,
  `RunSessionService.<GoBackToMenuAsync>d__39.MoveNext`). `--callers` (no capstone needed) is the
  reverse: every direct call/jump site of the method, named by its containing method (~9 s a scan).
  It misses virtual, interface and delegate calls and inlined bodies, so a tiny accessor with no
  sites proves nothing (`TileInfo.set_EnemyId` shows none; `BoardService.InitializeBoard` shows `Init`).
- `dev.py <cmd>` — the dev server from the command line: `launch`, `kill`, `reload`, `nav`, `input
  ui.down ui.activate`, `speech --tail 20`, `log --grep X`, `gui --grep RE --context 3`, `eval file.cs`,
  `wait "<bool expr>"`, `typeinfo Name`, `actions`, `module`, `screenshot out.png` (its
  output is UTF-8 whatever the console's codepage: leaderboard names are any script), `click [x y]`
  (an OS click at Unity screen coordinates, the fallback for prompts the mod does not cover yet).
- `run_driver.py [--until placement|result|shop|crossroads|event|picker|heroes|end] [--buy]` — plays
  a run forward through the mod's own navigation and stops at the stage you want or at any screen it
  does not know, so new game screens surface for inspection. `dev.floor:N` / `dev.shop` through
  `POST /input` skip to a floor's crossroads or the shop when a stage is all you need.

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
The version lives in `Directory.Build.props` alone (`<Version>`; the host's
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
  and moves), `BattleEvents` (the HUD hooks); the shop, crossroads and event panels are
  `RunPanelScreen`s: their own stops first, then the HUD sections the game leaves on screen and
  interactable under them (party, inventory, info, map, sidebar, menu), where a hero's or item's
  menu offers Sell while the shop is up (`ShopService.SellItem/SellHero`); `Nodes/` the run's reusable
  node readers: hero cards, items, sidebar, leaderboard), `UI/GameNodes` (node factories over uGUI Button/Toggle/Slider/TMP),
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
- `POST /input` with body `dev.audit` (not a key: `Module/Dev/ScreenAudit`) is the coverage report of
  the screen on show: every node of the render with its control/hero/items buffer lines and the
  global buffers, then the visible texts, tooltips and interactable widgets none of that covers.
  Substring matching over tag-stripped text, so it over-reports: a listed text is a lead, not a
  verdict (a widget with no caption, such as a portrait button, always lists). It scans the scene,
  so it is for the dev driver only. Before a `confirm` through the driver, read the readout the
  previous input returned: a guessed focus has bought a relic on a live run.
- `POST /input` with `dev.floor:N` or `dev.shop` (`Module/Dev/RunJump`) skips through a run for
  inspection: the run session's floor-node index is writable and its service proceeds from wherever
  it is, so `dev.floor:4` shows floor 4's crossroads next (act one: 0 the starter kit, 1 and 3 random
  events, 2 the challenge combat, 4 the campfire) and `dev.shop` opens the shop as a result would.
  The game keeps whatever that skips. The game's `ScopeService.SwitchToScope` is NOT a shortcut: its
  Campfire scope config names a scene the demo does not ship (an error dialog, then a dead run); the
  campfire is an event (`FirstCampfireEvent`: Train, Study, Recharge, Rest) and reads as one.
- `POST /input` with `dev.crossroads:N` (`RunJump.Crossroads`): the crossroads of that sequential id
  on the current floor node (`FloorNodeData.Crossroads` is writable), entered as the game enters one.
  Both it and `dev.floor` need the battle scene, where the crossroads panel lives: under an event
  they refuse (proceed out of it first), and from a crossroads they do nothing (the flow state is
  already Crossroads: take a path first). The second chunk's floor 2 is a fixed crossroads, 113, one
  path to event 1013 (The Guild Banner), whose title the game left untranslated ("No translation
  found for 'Crossroads.Crossroads_113.Name' in Crossroads": Unity Localization's own text, read as
  the game draws it).
- `POST /input` with `dev.event:N` (`RunJump.Event`), while ANY event is on show (`dev.floor:1`, a
  path): makes the event of that sequential id the active one (`EventService.ClearEvents`,
  `AddActiveEvent`, `SetActiveEvent`) and has `EventUIController` show it (`ClearChoiceButtons`,
  `OnStart`), so any of the 93 events is read without playing to it: the authored ones are 1001 to
  1035 (1016 is the game's own untranslated stub), the rest are templates (items 800-806 / 871-877,
  shards 809-812, stat bonuses 819-822 / 902-915, heroes 830-832, rerolls 780 / 2004, the campfires
  600 / 601). The crossroads is bypassed, so the templates that take their numbers from it show the
  game's own format errors; the authored events, the item and relic templates and every branch
  behind a choice come up whole. Leave through Proceed (`RunJump.Event` clears the Proceed
  button's listeners before `OnStart` adds its own again: with two, one press advanced the floor
  twice and loaded the Battle scene twice, and the scene left over kept a `CrossroadsController`
  whose gates were gone, throwing in `Update` every frame at the next crossroads, the game's error
  dialog reopening as fast as its "ok" closed it. Two "Battle" scenes in `SceneManager` is that
  state: restart the game). The list of ids: `balancing.GetAll<IEventEntry>()` in `/eval` (the
  compendium controller's `_heroInfoAdapter.Balancing`), cast to `IReadOnlyCollection` for the
  count and `IReadOnlyList` for the index.
- Re-show a dismissed panel for testing: find its scene instance with `Resources.FindObjectsOfTypeAll`
  in `/eval` and `SetActive(true)`; `SetActive(false)` afterwards. Never press a consent button for the player.

## Keys (focus mode on at launch; Ctrl+Shift+A toggles it)
F1 is the key help (below). Arrows navigate, Tab/Shift+Tab cycle control groups, Enter activates,
Backspace is the secondary action, Escape backs out, Home/End jump, Alt+Up/Down jump sections, typing letters searches the
focused group. Ctrl+arrows review the buffers (below). Space is unbound: nothing is read on demand
by a key; everything a control carries beyond its focus line waits in a buffer, except the glance
keys: digits 1-6 speak one fact group of the unit the focused control concerns (`NodeVtable.Subject`,
set wherever the hero buffer finds a hero; `GameRun/UnitGlance`), in place, focus unmoved: 1 vitals,
2 attack/magic/defense, 3 attack speed/crit/range/move speed, 4 sustain (nonzero), 5 statuses, 6 the
target; Shift+2/3/4 add each stat's breakdown. Live from the simulation entity and the bar while the
unit stands on the board, else the registry sheet, else a data-less card's own panels (shop, picker).
Read stats as the `CharacterStat` struct off `CharacterStatsComponent`'s properties, NEVER through
the `IReadOnlyCharacterStat` proxy `GetStat` returns (it misreads the boxed struct: the value carries
the type ordinal, base 0, IsIntValue false). Ctrl+S the shards, Ctrl+C the focused cell's or unit's
board coordinates (a unit's by the board grid's `WorldToCell` of its view's position:
`CharacterViewController._cellPosition` is never written), Ctrl+T the battle timer's text as the top
panel draws it, Ctrl+N the units near the focused cell (or near the cell of the hero a slot or card
concerns) with their hex distances (`HexGrid.Distance`), nearest first then by name ("Pollen 1,
Slime 2 4": the last number is the distance, the one before it a numbered enemy's own), Ctrl+H the hostile ones alone (the origin's unit's hostiles: the heroes from an enemy, the
enemies from a hero, silent on an empty cell), both placement-only (`RunData.Placing`) and silent in a
fight, Ctrl+Q the quests of the focused hero, item or relic (the control's
`BufferKeys.QuestBrief` side lines: the item named once, its quests after it, no rewards), Ctrl+M a
Red Rift run's missions from anywhere (`GameRun/Nodes/MissionNodes`) (`RunGlance`); Ctrl+R/Ctrl+F reroll
and freeze from anywhere on the shop (the shop section's `GetActions`; feedback deferred a few frames
through `UI/Later`). Digits and Ctrl chords never clash with type-ahead; bare letters do. The battle board is a
pointy-top hex grid (Unity's hexagon `Grid`, odd rows half a cell to the right, no cell straight up
or down): Q E A D Z C step focus to the focused cell's six neighbours (`Core/UI/HexGrid`), Shift+the
same letter moves the focused hero there (from the hero's PARTY SLOT too while placing, focus staying
on the slot: `PartySection.GetActions`, the hero's cell read off the board's tiles by id,
`RunData.TryCellOf`: the tiles are what the move itself acts on, so a second press starts from where
the first one put the hero, whatever its view is doing; type-ahead,
which ignores Ctrl and Alt chords but not Shift, stands down there only while Shift is held,
`PartySection.OwnsShiftLetters`), the arrows jump between units (Up toward the enemies, Down
toward the heroes, Left/Right round a side, Home/End to its ends; the same over the units of a
fight), and type-ahead stands down while a cell is focused. The board's nodes are raw, with no edges:
a UI key the navigator has no meaning for, or an arrow/Home/End the graph has no edge for, is offered
to the focused screen by its action key through `Screen.InvokeAction` before it falls through to the
action's handler, and `Navigation.MoveTo` lands on and announces the answer as a move.

**Key help (F1, `Module/Screens/HelpScreen`, `Core/UI/KeyHelp`)**: the keys that would do something
RIGHT NOW, for the focused screen and control, nothing declared for it. An action is listed when a
key of it is live (`InputManager.IsLive`: category active, chord unshadowed) and something answers
it: the navigator asked without acting (`Navigator.WouldHandle`: an arrow with a transition that
way, Enter on a node with `OnActivate`, any key the focused screen offers through `GetActions`,
which screens already offer only where it applies), else its own handler when
`InputAction.IsAvailable`. So a handler key that is silent somewhere must say so with `.When(...)`:
every glance is registered as a LINE (`ModuleMain.Glance(key, label, Func<string> line)`: the key
speaks the line, `RunGlance.Say`, and is available where the line is not null, `RunGlance.Has`), and
a new glance must be one too. A key the screen offers reads by the SCREEN's label, so an action's
label says what the key does here: Escape is `Strings.HelpPause` or `HelpCancelMove` on the run, the
Proceed button's own caption in the shop and the events (`RunPanelScreen.PanelBackLabel`). Order:
the screen's keys, the handler keys with a `When`, navigation, the always-on handlers (buffers),
global. The list is taken when the help OPENS: once the overlay is up the focused node is its own.
Enter on a row runs the key as a press would, and says nothing for the Enter itself (the player has
just read the row): `Speech.BeginHold()` (the hold's first line interrupts, cutting the row's
readout off, every later line queues, and all are remembered, until `EndHold`, the next real key
press or 3 s), `Navigation.QuietNextLanding()` (the landing a closing overlay brings is recorded, not
spoken), close, two frames on `InputManager.Dispatch(key)`, sixteen on (past the shop reroll's
deferred feedback) read the focus as a leaf, unless the action moved focus, opened another screen,
or already said the focused line (`Speech.Held`). A newly attached screen renders on its first
frame (`GraphNavigator.Attach` resets the idle throttle): until a render there is no focused node
for a dispatched key to act on. The help opens with "Keys here" alone, interrupting, and its rows sit in a context with no
label and no role word, so the landing is the first row, not "Keys here, list" again. The mod menu's
"All keys" is the unconditional list.

## Buffers (the Harkest Dungeon pattern: `Core/Buffers`, `Module/UI/Buffers`)
Review lists for the information a focus announcement leaves out, read live on every keypress:
Ctrl+Right/Left switch buffers (speaking "name: current line"), Ctrl+Up/Down step lines (the edges
re-read). Empty buffers are skipped; a focus change re-homes review to the control's buffer. The
roster, in cycling order: **control** (the focused node's head line: label, value, state, never the
role word or the position, then one line per description part and per `NodeVtable.Details` tooltip,
repeats folded: `Core/Buffers/NodeLines`), **hero** and **items** (`NodeVtable.SideLines`: the hero a
slot, cell, unit or card concerns and the items it carries: `GameRun/Nodes/HeroLines`; the hero buffer
is the WHOLE hero, `HeroLines.ForCard`: name with tags and rank, the full stats line, the abilities
line, then every tooltip of the card, `HeroCardNodes.Tooltips`: abilities, class and archetype tags,
stats, a keyword's definition given once across the abilities and tags; the control buffer of a hero
control holds the same tooltips, so neither buffer sends the player to the other), **quests**
(right after hero, empty and so skipped for most controls: the quests of the items the hero wears, or
of the focused item or relic, `ItemNodes.QuestLines`: "Rift Seal: Tank or Vanguard, 1 / 3", "Hourglass:
Quest: Trigger Stall 5 times from any source, 0 / 5" with the reward on the line after), **relics**
(the run's, one line each), **party** and **enemies** (one line per unit, placement and fights),
**combat** (the battle events log, following its latest line). Conventions: a tooltip is ONE line,
never joined with others; helpers return `List<string>` (`ItemTooltips`, `AbilitiesTooltips`,
`SlotTooltips`...); a node's details go in `Details = () => ...`, never spoken directly. An item's
tooltip lines end with one definition per stat it modifies ("Attack Speed: Increases how often a
character auto attacks.", the game's `StatTextHelper` text, appended by `TooltipReader.Lines`), since
the game's own item tooltip defines none. The dev
server drives them through the action keys: `POST /input` with `buffer.next`, `buffer.prev`,
`buffer.line.next`, `buffer.line.prev`.

## Languages (`Module/LanguageSync`, `Core/Strings`, `lang/`; the dd2a11y pattern)
The mod's own words follow the GAME's language, never the system's: Unity Localization's selected
locale (nine in the build: `zh-Hans zh-Hant en fr de ja pt-BR ru es`; the settings dropdown offers
seven, no French or Japanese yet), which the dropdown sets through `SettingsService.SetLocale` ->
`LocalizationSettings.SetSelectedLocale` and Unity's own `PlayerPrefLocaleSelector` (`selected-locale`)
restores at boot. `lang/<locale code>.txt` beside the plugin (`de.txt`, `pt-BR.txt`, `zh-Hans.txt`;
`LanguageFiles.Candidates`: the code, its lowercase, the bare language), English when there is none.
Nothing is polled:
- a switch arrives through `LocalizationSettings.SelectedLocaleChanged` (a managed delegate converted
  with `DelegateSupport`, removed in Dispose), raised INSIDE the setter, so the table has changed
  before the dropdown that committed it is read back ("Sprache, Kombinationsfeld, Deutsch");
- the first read waits for the first game scope (`GameScopes.Registered`: the application scope's
  build at boot, the seed on a reload). At plugin load there are no localization settings to ask,
  and `get_SelectedLocale` forces their load.
The launch line is the MODULE's (`ModuleMain.AnnounceLaunch`, once per launch through
`IModHost.LaunchAnnounced`), spoken when that first language is in place, so a German game says
"Guildrun Access 0.1.0 geladen"; the update line waits for it too. The host itself speaks only the
module-failed line, in English (it has no strings table).
`Strings.LoadTranslation` swaps the whole file (never a blend) and reports what it set aside, which
`LanguageSync` logs: a line with no key, an unknown key, an empty value, a value with a `{n}` its
English lacks. `lang/en.txt` is `Strings.DumpTemplate()`: after adding or rewording a string run the
tests once with `GRA_WRITE_LANG_TEMPLATE=1`, then add the key to every other file
(`LangFilesTests`: one file per game locale, complete, same slots as the English). The strings have
no plural or gender machinery, so a template must hold for any count and any name ("продано: {0}",
"{1} сек."). Game terms are the game's own: `/eval` can dump its `UI` string table for every locale
(`LocalizationSettings.StringDatabase.GetTable("UI", locale)`, `SharedData.Entries`, `GetEntry(id)`).
A module-only build deploys `lang/` too, and a reload reads the file again. Key names in the key
help ("Ctrl+Shift+A", "Up Arrow") are not translated.

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
5. **(done)** The game run: hero picker (its reroll, `ReRollPanelView`, shows only past the
   tutorial's save point and while the profile holds a Boss Token: `ProgressionData.BonusTokens`,
   a reactive int reached through `GameRegistryService._progressionReader._data`; set it in `/eval`
   to see the panel, the press consumes one; "cost 1 Boss Token" with the game's tooltip in the
   buffer, a press landing on the first new hero), run HUD (placement grid with keyboard moves, party/reserve
   with equip/unequip menus: a hero with every slot taken is listed disabled, since the registry's
   `EquipItem` checks no slot itself, only the game's drag does, and with none it takes the item out
   of the reserve and loses it; `HeroData.EquippedItemCount` is the slot list's length, room is
   `GetFreeItemSlotCount`), inventory (the reserve's items and the relics as side-by-side columns of
   one stop, `GraphBuilder.StartColumn`: Up/Down within one, Right/Left across, an empty container
   dropped; the shop's heroes, items and relics for sale are one such stop too), info, speed, menu),
   battle result (all forms), shop,
   crossroads, events (campfire included; the event UI is one controller and four views: a choice
   is its caption, title and text in ONE label split by a line break, which the audit's substring
   match reports as unread and is not; an `ArtifactChoiceButtonView` adds an item or relic, named
   when the caption does not name it, its tooltip in the buffer; a `HeroListChoiceButtonView` adds
   hero PORTRAITS, icons alone, the hero a rank-up, a retrain or a stat bonus goes to, sixteen of
   the game's builders, some captions naming the hero ("Rank up, Nyx") and some not ("Choose a
   Hero. Retrain one of their upgrades."): `EventScreen.HeroesPart` says the hero's name unless the
   caption has it, always its classes and rank, and the choice carries the hero's buffers and the
   glance keys through the party slot, `RunData.OwnedHero(portrait._characterEntry)`; the outcome
   summary's portrait likewise. A disabled choice has no reason but its own text
   (`EventChoiceValidationResult`: Valid, NonInteractable, Hidden)), rank-up pickers (a choice card is more than its name
   and description: its banners are `HeroTagView` icons, the class a path adds under `ClassBanner`
   and its archetypes under `StatsBanner`, read by `HeroCardNodes.TagNames`, and a B-rank path that
   adds a class says so, with its stat gains, in `SpecializationChoiceView._additionalClassView`,
   live only while `_additionalClassContainer` is active; a path whose ability is an ACTIVE one
   (nine in the demo, Nyx's The Wild) shows the mana it brings, `_activeAbilityView._mana`
   "[20/50 Mana]", and the one path that comes with an item (Irini's The Olympic, found by
   `ITooltipSource.TryGetAdditionalItem` over the ability's effects) shows `_itemView`, an icon
   whose name text is filled but hidden, the description naming the item: its tooltip follows the
   ability's in the control buffer. The picker's prefab has NO mastery markers
   (`_masteryAvailableContainer` / `_masteryCompletedContainer` are null there): those belong to
   the compendium's copies of the card (`SpecializationCompendium*`), which the compendium screen
   reads by every visible text. For a path no hero at hand has, `/eval` can fill a live card for
   display: `view.SetChoiceFromEntry(spec, heroEntry)`, the entries from
   `CompendiumUIController._specializationsByHeroId` / `_allHeroes`. The pickers are the one panel
   the game lets a player HIDE: `NavigationUIController._choiceNavigationButton` (a sibling of the
   picker, caption `_choiceNavigationLabel`: "Hide", then "Show") runs `ToggleChoiceVisibility`,
   which turns the picker and its modal backdrop off, uncovers the shop (`ShopUIController.SetCovered`)
   and flips `_isChoiceHidden`, leaving the run HUD, or the shop a duplicate was bought in, fully
   live with the choice still owed. The picker lists it last (`MenuSection.AddChoiceButton`); the
   screen underneath takes over by itself (the picker object is inactive) and its menu stop
   carries the same button, where `MenuSection.OnUpdate` lands focus the moment a choice is hidden
   ("Run, menu, list, Show, button"). The game drops the button once the choice is made.
   `IGameRegistryService.RankUpHero(heroId)`
   in `/eval` brings a picker up, the ids from `GameRegistryService.Data.Heroes.Keys.CopyTo`), the
   relic reward picker
   (`RelicPickerScreen`: `RelicPickerController._panelParent` after a challenge fight), the Heroes
   panel, tooltips through the game's own tooltip pipeline. A hero card's class and archetype tags
   (`HeroTagView`) are the exception: they carry no tooltip source but the tooltip asset's own
   `TooltipObject`, which the game fills with a title and a description
   (`TooltipHelper.PopulateTitleDescriptionTooltipObject`: a `FlexibleTooltipInformation` of
   `TextDataValue`s by identifier, `tooltip_title` / `tooltip_details` / `tooltip_extra_information`),
   read by `TooltipReader.Title/Lines(TooltipObject)`. An archetype tag is icon-only and its caption
   and hover text are a leftover placeholder ("assassindadsadsad"): it is NAMED by that tooltip's
   title ("Shard"), never by its icon sprite, which is named for the art ("Economy"). A QUEST is
   read by the tooltip's structure, never by its words (`TooltipReader.Quests` / `Sections`): the game
   draws one as a `ConditionalBonusView` section (its text, a locked or an unlocked icon: done) and a
   `QuestProgressView` after it (the bar's text, "0 / 100"), an ordinary quest's requirement being the
   description section right before the bonus (a separator in between means it is not: the Rift
   Seal's three charges stand under one). `TooltipReader.Lines` folds the pair into one line
   ("Tank or Vanguard, 0 / 3, complete"): read apart, the counts are bare numbers, and equal ones
   fold into one in a buffer. TextMesh Pro draws a written-out "\n" as a line break, so `AddLines`
   splits on it too (the Rift Seal's description has two). Reusable readers: `GameRun/Nodes/HeroCardNodes`, `ItemNodes`,
   `LeaderboardNodes`, `UI/TooltipReader`; run data and moves through `GameRun/RunData`.
6. **(done)** End screen (a boss victory shows it in the game's short form, `Show(_, true)` from the
   flow controller's OnStart timer: every navigation button hidden, one caption-less click-anywhere
   button whose click hides it back to the result panel, listed as Continue and pressed by Escape),
   progression, the sidebar (inspect cards, damage tracker), battle events
   (`GameRun/BattleEvents`: Harmony postfixes on the HUD views: floating numbers, status icons, empties
   bars, cast animations: the battle events log, a run-HUD stop, and NOTHING spoken as it happens;
   only what the game draws is reported: the simulation's own `BattleLogger` is developer debug text
   and is NOT used), the compendium, the mod menu (Ctrl+Shift+M: settings, key help), comics. The run
   HUD is active only while placing or fighting (`GameRunScreen.IsActive`), so no landing is spoken
   at a battle's end or between panels; a unit falling under focus moves focus silently
   (`NodeVtable.QuietVanish`). A focused node that vanishes lands on the nearest survivor, which is
   the control BEFORE it; a placeholder names its successor instead (`NodeVtable.VanishTo`, asked at
   that rebuild and honoured when the node is in the new render): the leaderboard's "Loading..."
   line names the first entry, so waiting on it lands on the loaded list, not back on the tab.
   A vertical move INTO a row lands on its first item unless the row names another
   (`GraphBuilder.StartRow(entry:)`, asked when the render is wired): a row of tabs that sits
   inside a stop, between a toggle and the content it switches, names its selected tab, since a tab
   selects on landing and Up from the friends' entries would otherwise switch the board to Global.
   The leaderboard (`LeaderboardNodes`, one reader for the main menu, the difficulty screen and the
   final result) is that stop: toggle, tab row, lines, the filled entries (the board keeps its blank
   rows active), the reset countdown. Between fights the shop, crossroads and event panels carry the HUD
   sections the game keeps interactable under them (`RunPanelScreen`), with Sell in the shop. The
   campfire is an event (Train, Study, Recharge, Rest), not a screen: the game's Campfire scope has
   no scene in the demo. Open: a milestone's
   hero and token-slot rewards are icons without tooltips (its title names them).
   The Red Rift (the difficulty screen's eighth tier, `DifficultyUIController._playRiftButton`, the
   game's challenge mode: `ChallengeReader.IsChallengeRun`) adds a relic, the Rift Seal (a Unique
   Item whose tooltip is `ItemInstanceTooltipSource.PopulateRiftAnchorItem`: three class groups, each
   a count in the registry's global custom data, `riftAnchorAssassinDuelistWarriorCount` and kin) and
   six missions (`ChallengeModeController`: a title counting the done ones, a `ChallengeItemView` per
   mission whose done and failed states are feedback OBJECTS, icons, never a word: `MissionNodes`
   adds the word). It unlocks once `ProgressionData.HighestDifficultyBeaten` reaches a threshold
   the game takes from its balancing (`ProgressionReader.IsChallengeModeUnlocked`; 7 unlocks it,
   verified); for a test, set that in `/eval`, call the scene
   `DifficultyUIController.FillProgressionInfo()` (the locks are filled once, in OnStart), and
   restore the `Profile` save afterwards. `IGameRegistryService.SetPermanentGlobalCustomData(key,
   FP)` moves a Seal charge, `ChallengeInstance.State.Value` a mission's state, and
   `reg.CreateItem(BalancingRef<IItemEntry>.From(entry))` drops a quest item (26 of the demo's 174
   items have `HasQuestEffect`) into the reserve.
7. **(done)** Nameless enemies: 295 of the 644 enemy entries have no name key (the scaled variants),
   and the game draws them blank on the bar, the sidebar card and the result's portraits.
   `GameRun/EnemyNames` names one after a named sibling (same id family "Enemy_1018xx", else the
   same visual config's majority name), read localized at speak time, through
   `RunData.CharacterName` / `EnemyName` / `UnitName`; the battle-event hooks resolve a blank bar's
   unit while the flow state is Resolution (`BoardSection.UnitOf`), and the sidebar's shown enemy
   card is matched by `EnemyId`, never by name. Enemies of one board that share a name are numbered
   ("Slime 1", "Slime 2": `GameRun/EnemyNumbers`, through `RunData.EnemyLabel` / `UnitName`) in the
   grid's reading order, read off the board data's tiles every time, never kept: `BoardService`
   fills the enemy tiles once per battle (`Init`) and nothing in a fight rewrites them, so a number
   means the same enemy from placement to the result, the fallen included. One read scans the enemy
   rows (~1 ms): a caller naming several units reads once and passes the numbers
   (`BoardSection.AddUnits`, `RunGlance.Nearby`). The result's portraits carry no enemy id and stay
   unnumbered. The launch update check (`Module/UpdateChecker`,
   `Core/UpdateCheck`) speaks "update X available" when the newest GitHub release outranks the
   running build; anything else stays silent with a log line.

## The game's own hotkeys
The game's Input System action maps bind Tab, Space, Enter, Escape, arrows and letters (Navigation:
Heroes panel, reserve/shop toggle, feedback, back; UI: navigate/submit/cancel; a Player map). While
focus mode is on, `FocusMode` disables the keyboard as an Input System DEVICE
(`InputSystem.DisableDevice`), so no game action hears a key; our own keys are polled through the
legacy input path, which is unaffected. `InputSystem.onDeviceChange` (a managed delegate converted
with `DelegateSupport`) re-disables a keyboard the moment anything enables it again (window focus,
a reload's old generation, a new device), so there is no scanning. Turning focus mode off re-enables
it. Anything a game hotkey did must be offered through our screens instead (Heroes is in the menu).
The game's Escape (`NavigationUIController.UpdateInput`, its back action) closes whatever is open, the
compendium, a dialog, the feedback, settings and hero panels, and with nothing open calls
`SetSettingsPanelActive(true)`: the pause menu, anywhere in a run, whether or not the HUD shows its
Settings button (the first hero picker shows none, and no HUD at all). So a run screen with nothing of
its own for Escape opens the pause menu through `RunSettingsScreen.Open()`: the hero picker, the
crossroads, the run HUD (after cancelling a pending move; by the HUD's own Settings button when it
is up). Where Escape already has a meaning it keeps it: Proceed on the shop, an event and a result,
Continue or Back on the pickers (the shop and the events keep the pause menu a Tab away, in the
menu stop they carry).

## Click-only widgets
The comics' click-anywhere polls the pointer through the game's own input service instead of
listening to a widget event (there is no Button): `Input/SyntheticMouse` queues a mouse
move/press/release into Unity's Input System (`InputSystem.QueueStateEvent<MouseState>`), one step
per module tick, which both uGUI and the game's service see as a real click, without moving the OS
cursor. Use it only where there is no widget event to invoke: every Button the game wires at
runtime (`onClick.AddListener`, so `GetPersistentEventCount()` reads 0, the run-over result's
Proceed included) still hears `onClick.Invoke()`, and a synthetic click on it is flaky.
