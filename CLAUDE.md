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
- `game/il2cppdump/dump.cs` — Il2CppDumper output (gitignored, regenerate with the command in
  `game/README.md`): every type, field with offset, method with RVA. **Grep this before guessing a
  shape** (`grep -n '^public class SettingsUIController ' game/il2cppdump/dump.cs`).
- `game/il2cppdump/DummyDll/` — stub assemblies for ilspycmd browsing (signatures only, no bodies).
- `game/analysis/types.tsv` — one row per type: typedef, assembly, namespace, kind, name (committed).
- For real behavior use the live game: `GET /gui` for structure, `POST /eval` for values (see Dev driver).

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
by finding a live controller/panel by scene scan (throttled) and checking `activeInHierarchy`.

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
  shadowing), `Screens/` (`Screen`, `ScreenManager`), `Strings/` (the authored strings table).
  Engine seams are statics the module sets in Load: `CoreLog`, `Speech.Speak`, `NavInput.Current`,
  `Navigation.FocusActive`, `InputManager.FocusActive`, `GraphAnnouncer.PositionText/ExpandedStateText`.
- **`GuildrunAccess.Module`** (net6.0, RELOADABLE, engine-coupled; **day-to-day feature work goes
  here**): `ModuleMain` (IModModule + IDevDriver: seams, input registration, screen registration,
  focus mode, Tick), `Screens/` (one file per screen), `UI/GameNodes` (node factories over uGUI
  Button/Toggle/Slider/TMP), `UI/FocusMode`, `Input/KeyboardBinding` + `UnityNavInput`.
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
action, Escape backs out, Home/End jump, Space reads a description, Ctrl+Space re-reads the focused
control, Ctrl+Up/Down jump sections, typing letters searches the focused group.

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
   game's own tooltip pipeline. Reusable readers: `UI/HeroCardNodes`, `UI/ItemNodes`,
   `UI/LeaderboardNodes`, `UI/TooltipReader`; run data and moves through `Run/RunData`.
6. **(done)** End screen, progression, the sidebar (inspect cards, damage tracker), battle events
   (`Run/BattleEvents`: Harmony postfixes on the HUD views: floating numbers, status icons, empties
   bars, cast animations: a run-HUD stop plus narration; only what the game draws is reported: the
   simulation's own `BattleLogger` is developer debug text and is NOT used), the compendium, the
   mod menu (Ctrl+Shift+M: settings, key help), comics. Open: the shop's "sold" filter is a
   canvas-group heuristic; Escape on the run HUD only cancels a pending move.

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
Some game prompts poll the pointer through the game's own input service instead of listening to a
widget event (the comics' click-anywhere, the run-over panel's Proceed): `onClick.Invoke()` does
nothing there. `Input/SyntheticMouse` queues a mouse move/press/release into Unity's Input System
(`InputSystem.QueueStateEvent<MouseState>`), which both uGUI and the game's service see as a real
click, without moving the OS cursor. Use it only where the widget event is not what the game hears.
