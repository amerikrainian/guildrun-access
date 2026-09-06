# Guildrun Access: architecture plan

Port of the wotr-access UI graph onto the NonVisualCalculus host/module hot-reload layout,
for Guildrun Demo (Unity 6000.0.64f1, IL2CPP x64, BepInEx 6 IL2CPP, Prism speech).
Written 2026-09-06 from a read of both reference mods.

**Status (2026-09-06):** phases 0-4 are implemented and verified in the running game: BepInEx
be.788 loads on Unity 6 (pre.2 does not), the host + collectible-context loader hot-reloads Core
and Module on `POST /reload` / F6, the graph engine is in Core with 54 passing tests, and the
main menu plus the modal dialogs (privacy consent first among them) are navigable screens. See
`CLAUDE.md` for the build and dev loop. Phase 5 (settings, the run UI, battle) is next.

## Verdict

Yes, both fit together with no tension. The two mods already share the same host/module
split (wotr's CLAUDE.md calls it "the tbw-access / NonVisualCalculus pattern"), and wotr's
graph lives entirely inside its reloadable module. NonVisualCalculus's loader is the one to
take: it was written for exactly our stack (BepInEx 6 IL2CPP, CoreCLR, collectible
`AssemblyLoadContext`), whereas wotr's is the Mono/net48 variant that cannot unload and works
around simple-name binding with timestamped assembly names.

## Facts that shape the plan

| Fact | Evidence | Consequence |
|---|---|---|
| Guildrun metadata is version 31 | `global-metadata.dat` header; Il2CppDumper | See next row |
| Vendored BepInEx 6.0.0-pre.2 rejects it | Decompiled `LibCpp2IL.dll` from the zip throws `"We support 23-29, got N"` | pre.2 will fail at interop generation. Use a Bleeding Edge build: be.755 added "metadata v23-106 (Unity 6+)"; be.788 (2026-09-01) is current. Download from builds.bepinex.dev. |
| Unity 6 + Il2CppInterop is in use elsewhere | BepInEx issue tracker shows be.752 on 6000.0.58f2 | Expect "Class::Init signatures exhausted" warnings; not fatal. Smoke-test first. |
| `activeInputHandler = 2` (Both) | PlayerSettings in `globalgamemanagers` via UnityPy | Legacy `UnityEngine.Input.GetKey*` works: wotr's `KeyboardBinding` ports unchanged. |
| `Input.inputString` is stripped | dump.cs has no `get_inputString` | Type-ahead must read text via `Keyboard.current.onTextInput` (Input System). |
| `runInBackground = true` already | PlayerSettings | Dev driver can drive the game unfocused without forcing it. |
| UI is uGUI + TextMeshPro + Unity Localization | Assembly list; `BaseUiView : MonoBehaviour`; `EventSystem` present | NVC's `/gui` inspector and TMP reading port as-is; wotr's Owlcat-VM node factories do not. |
| DI is VContainer with per-scope assemblies | `ember.scopes.{application,mainMenu,gameRun,battle,...}`; `BootstrappedScope` | Screen activity resolves from live scope/controller objects, not a root UI context like wotr's `RootUIContext`. |
| Anti-cheat is telemetry only | `ObscuredCheatingDetectorService` sets a flag nobody reads; no code-hash or injection checks | Mod injection is safe. Never write to `Obscured*` fields. |
| Steam app id 4425970 | `appmanifest_4425970.acf` | Launch with `steam.exe -applaunch 4425970`. |

## Target layout

Five projects. The split follows NonVisualCalculus, with one change: the engine-free logic
is itself hot-reloadable, so adding a Core type no longer needs a restart (NVC's documented
gotcha).

```
src/
  GuildrunAccess.Contracts/   netstandard2.0  PERMANENT. IModHost, IModModule, IDevDriver,
                                              ISpeechBackend, SpeechPipeline, ISettingsStore/ModSettings,
                                              IAudioEngine. Tiny; identity shared by host and module.
  GuildrunAccess.Core/        netstandard2.0  RELOADABLE, engine-free, unit-tested. The graph engine
                                              (see port map), TypeAheadSearch, Strings table, TextFilter,
                                              announcement composers. References Contracts only.
  GuildrunAccess.Module/      net6.0          RELOADABLE, engine-coupled. Screens, node factories,
                                              ScreenManager, InputManager + bindings, Harmony patches,
                                              speech/tooltip adapters. References Core + interop proxies.
  GuildrunAccess/             net6.0          PERMANENT host plugin. BepInPlugin entry, Prism P/Invoke,
                                              HostPump (the one injected MonoBehaviour), ModuleLoader,
                                              DevServer + Roslyn REPL, settings store. References Contracts
                                              + interop. Knows Core/Module only through Contracts.
  GuildrunAccess.Tests/       net8.0 xUnit    Core + Contracts only. Carries wotr's four graph test files.
```

Load contexts: the host reads `Core.dll` and `Module.dll` bytes and loads BOTH into one
collectible `AssemblyLoadContext` per generation (NVC's `ModuleAlc.Load` returns null for
everything; ours returns Core when asked for it, null otherwise so Contracts, interop, BepInEx
and Harmony resolve to the default context). Reload = build Module (which builds Core), copy
both DLLs, `POST /reload` or F6. The host loads the new generation before disposing the old
(NVC's failed-reload safety) and the module uses a per-load GUID Harmony id with
`UnpatchSelf()` in `Dispose` (NVC's fix for a fixed id stripping the fresh generation's
patches).

Rule from NVC, kept verbatim: only entry, native handles (Prism, NAudio), sockets, and
IL2CPP type injection live in the host. A module never calls
`ClassInjector.RegisterTypeInIl2Cpp`.

## Port map (wotr -> here)

Zero game references in the graph engine (census over `src/UI`): it moves wholesale.

| wotr file | Lines | Destination | Change |
|---|---|---|---|
| `UI/Graph/GraphTypes.cs` | 285 | Core | none |
| `UI/Graph/ControlId.cs` | 64 | Core | none |
| `UI/Graph/GraphBuilder.cs` | 470 | Core | none |
| `UI/Graph/KeyGraph.cs` | 677 | Core | none |
| `UI/Graph/GraphAnnouncer.cs` | 217 | Core | none (PartFilter/PositionText/ExpandedStateText are already seams) |
| `UI/GraphSheet.cs` | 232 | Core | `Loc.T` -> Strings table |
| `UI/ControlTypes.cs` | 122 | Core | `Loc.T("role.*")` -> Strings table |
| `UI/TypeAheadSearch.cs` | 312 | Core | none |
| `UI/Navigator.cs`, `Navigation.cs`, `NavTypes.cs`, `ElementAction.cs`, `ActionArgs.cs` | ~190 | Core | `Tts.Speak` -> injected speak delegate; `Main.Log` -> host log |
| `UI/GraphNavigator.cs` | 685 | Core | Its 14 Unity references are all `Input.GetKey`/`inputString`/`Time.frameCount` inside `TickTypeahead` and the idle throttle. Inject a small `INavInput` (typed chars, modifier state, arrow held, frame number) so the navigator is testable and the module supplies the Input System-backed implementation. |
| `Screens/Screen.cs` | 162 | Core | `Tts.Speak` in `OnFocus` -> speak delegate; `InputCategory` type moves with it |
| `Input/InputCategory.cs`, `InputAction.cs`, `InputBinding.cs`, `OsKeyboard.cs` | ~240 | Core | none |
| `Input/InputManager.cs` | 160 | Core | Its one game reference is `FocusMode.Active`; make it a `Func<bool>`. Keep the focus-first category walk and chord shadowing: it is what lets a dialogue keep the base screen's keys alive. |
| `Input/KeyboardBinding.cs` | 75 | Module | none (legacy Input is enabled) |
| `Screens/ScreenManager.cs` | 204 | Module | Replace `RootUIContext` polling with Guildrun scope/controller polling; stack/diff/child-screen logic unchanged |
| `UI/GraphNodes.cs`, `ItemNodes.cs`, `SpellNodes.cs`, `CharGenNodes.cs` | ~1,270 | Module | Rewrite. These are Owlcat-VM factories. Keep the part helpers (`LabelPart`, `DisabledPart`, `SelectedPart`, `TooltipPart`, `Position`) and the Button/Toggle/Slider/Tab/Text shapes; back them with uGUI `Button`/`Toggle`/`Slider`, TMP text and Guildrun's tooltip controllers. |
| `UI/Tooltips/*` | ~980 | Module | Rewrite over `GameRunTooltipController` / `ATooltipUI` / keyword parsers (Guildrun composes tooltips from keyword behaviors, not Owlcat bricks) |
| `Screens/*Screen.cs` | ~54 files | Module | Rewrite per Guildrun screen; the shape (Key, Layer, IsActive, Build) stays |
| `UI/Announcements/*` (settings per control type) | ~275 | Core | none; feed `GraphAnnouncer.PartFilter` |
| `Message.cs`, `Localization/*`, `assets/locale/*.json` | ~275 | Core | Either port as-is or merge into NVC's key/plural Strings table with `lang/<lang>.txt`. Recommend NVC's table: it is the one the tests pin. |
| `FocusMode.cs` | 60 | Module | Suppress game input through `gg.leyline.input.InputService` or by disabling the active `InputActionAsset` maps while our navigator owns the keyboard |
| `Speech/*` (Prism + SAPI COM + clipboard) | ~1,500 | Host | Take NVC's `PrismNative`/`PrismBackend` (~200 lines) first; add wotr's SAPI fallback later if testers need it |

Everything wotr keeps under `Exploration/`, `Buffers/`, `Events/`, `Audio/` is out of scope
for the UI port and Guildrun-specific anyway.

## Taken from NonVisualCalculus verbatim or near-verbatim

- `Modularity/ModuleLoader.cs`, `Host/HostPump.cs`, `Plugin.cs` (host shape).
- `Core/Modularity/IModHost.cs`, `IModModule.cs`, `IDevDriver.cs` -> Contracts.
- `Dev/*`: `DevServer` (+ `/eval`, `/wait`, `/speech`, `/log`, `/module`, `/reload`, `/gui`,
  `/focus`, `/screenshot`, `/typeinfo`), `CSharpEvaluator` (Roslyn), `ModuleInspector`
  (Harmony patch table with live counts), `GuiInspector` (uGUI dump; Guildrun is uGUI so it
  works unchanged), `LineLog`.
- `Directory.Build.props` game-dir resolution (registry, env var, local props) with
  `Guildrun Demo` / `Guildrun.exe` substituted, plus the `_CheckGameDir` warning.
- Deploy targets: host `DeployToGame` (Debug only, `Retries=0`, `ContinueOnError`) and the
  module's own `DeployModule` for the hot loop.
- `setup-bepinex.ps1`, `build.ps1`, and the CLAUDE.md conventions (never cache game state,
  no silent failures, strings table, announcement rules).
- Headless flags: `GRA_NO_SPEECH`, `GRA_NO_DEV`, `GRA_DEV_PORT`.

## Phases

Each phase ends with something a screen reader can hear, or a curl can verify.

**0. Loader bring-up (blocking; nothing else can be tested without it).**
Replace `third_party/bepinex` with a Bleeding Edge IL2CPP win-x64 build (be.788 or newer);
update its README. Write `setup-bepinex.ps1` for Guildrun. Install, launch through Steam
(`-applaunch 4425970`), confirm `BepInEx/interop/` contains `ember.*.dll` and
`gg.leyline.*.dll`, and read `LogOutput.log` for Il2CppInterop errors. If interop generation
fails on this Unity version, that is the moment to stop and pick a build, not later.

**1. Skeleton and speech.**
Solution with the five projects, `Directory.Build.props`, host plugin with Prism,
HostPump, ModuleLoader (two-assembly ALC), DevServer with Roslyn. Module says "Guildrun
Access loaded" through the pipeline. Verify: `curl /health`, `/speech` shows the line,
edit the string, `dotnet build src/GuildrunAccess.Module`, `POST /reload`, hear the new line
without a restart, `/module` shows generation 2.

**2. Graph engine into Core with tests.**
Move the port-map "none" rows, apply the seams listed, port wotr's `GraphAnnouncerTests`,
`GraphBuilderTests`, `KeyGraphTests`, `SingleItemStopTests`. `dotnet test` green before any
screen exists. Also port `Screen`, `Navigator`, `GraphNavigator` (with `INavInput`),
`InputManager`. Verify with tests only; no game.

**3. Module substrate.**
`KeyboardBinding` (legacy Input), `InputSystemTextInput` for type-ahead
(`Keyboard.current.onTextInput`), `ScreenManager` polling Guildrun scopes, `FocusMode`
over `InputService`, node factories for Button/Toggle/Slider/Tab/Text over uGUI + TMP, a
`ModuleMain` implementing `IModModule` + `IDevDriver` with per-load Harmony id. Verify:
`/nav` reports ownership; `/input down` moves focus on a stub screen.

**4. First screen: main menu.**
`ember.scopes.mainMenu`: `MainMenuUIController` (a `MonoBehaviourController`) is the
activity signal; its buttons are uGUI. One `MainMenuScreen.Build` declaring a `PushContext`
list of buttons, exactly as wotr's `MainMenuScreen` does. Verify: launch, hear the menu,
arrow through it, Enter starts a run; then `/reload` after editing a label and hear the
change. This is the milestone that proves both halves together.

**5. Breadth.**
Settings (sliders, toggles, key bindings), then `ember.scopes.gameRun` UI
(`SlotsUIController`, `BottomHeroPanelUIController`, `HeroCard`, `ChunkUIController`
nodes) with `GraphSheet` for tabular hero/relic data, tooltips through
`GameRunTooltipController`, then battle. Add screens one at a time; each is one file.

## Risks and open questions

- **Il2CppInterop on Unity 6** is the only real unknown; phase 0 answers it in an hour.
- **Type identity across the ALC boundary**: anything the host holds must be typed by
  Contracts, never Core. A Core type leaking into a host field breaks the second reload with
  `TypeLoadException` every frame (NVC's gotcha, now confined to Contracts).
- **Roslyn cannot reference byte-loaded assemblies** (no `Location`); the REPL sees game and
  Contracts types, and reaches module internals via `IDevDriver` and reflection, as in NVC.
- **Guildrun updates**: the demo is at build 0.5.7; each Steam update regenerates interop and
  may rename controllers. Screens should resolve objects by type, never by GameObject path.
- **Multiplayer**: `Fusion`/`Quantum` netcode assemblies are present. Do not patch simulation
  code; read UI only.
