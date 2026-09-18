using System;
using System.IO;
using System.Text;
using GuildrunAccess.Contracts;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Input;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Input;
using GuildrunAccess.Module.Screens;
using GuildrunAccess.Module.UI;
using HarmonyLib;
using UnityEngine;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.GameRun;

namespace GuildrunAccess.Module
{
    /// <summary>
    /// The reloadable feature module: wires the Core seams to the host, registers the mod's input
    /// actions and screens, owns focus mode, and drives the per-frame loop. Everything here is torn down
    /// in <see cref="Dispose"/> so a fresh generation can hot-swap in without a restart. Core statics are
    /// per-generation (Core loads into the same collectible context), so they need no manual reset.
    /// </summary>
    public sealed class ModuleMain : IModModule, IDevDriver
    {
        private IModHost _host;

        /// <summary>The host of the live generation (settings, logging), for screens that need it.</summary>
        public static IModHost Host { get; private set; }
        private Harmony _harmony;
        // Ambient readers: things the game shows without a focusable control (comics, tutorial text).
        // Owned per generation; they hold live references only and re-find them when destroyed.
        private readonly Readers.ComicReader _comics = new Readers.ComicReader();
        private readonly Readers.TutorialReader _tutorials = new Readers.TutorialReader();
        // The launch update check: asked once the module is up, its line spoken from Tick when the
        // request lands with a release newer than the running build.
        private readonly UpdateChecker _updateCheck = new UpdateChecker();
        private bool _updateAnnounced;

        public void Load(IModHost host)
        {
            _host = host;
            Host = host;

            // Core seams: logging, speech, engine inputs, focus ownership, announcement wording.
            CoreLog.Info = host.LogInfo;
            CoreLog.Warning = host.LogWarning;
            CoreLog.Error = host.LogError;
            Speech.Speak = (text, interrupt) => host.Speech.Speak(text, interrupt);
            NavInput.Current = new UnityNavInput();
            Navigation.FocusActive = () => FocusMode.Active;
            InputManager.FocusActive = () => FocusMode.Active;
            GraphAnnouncer.PositionText = (i, n) => host.Settings.SpeakPositions ? Strings.Position(i, n) : null;
            GraphAnnouncer.ExpandedStateText = Strings.ExpandedState;
            InputBinding.RegisterType("keyboard", KeyboardBinding.Deserialize);
            LoadLanguage(host.PluginDir);

            // A per-load UNIQUE id so a reload's Dispose unpatches exactly this load's patches: the host
            // loads the new module (which patches) before disposing the old one, and UnpatchSelf removes
            // by owner id, so a fixed id would let the old teardown strip the fresh load's patches.
            _harmony = new Harmony("com.amerikranian.guildrunaccess.module." + Guid.NewGuid().ToString("N"));

            RegisterInput();
            RegisterScreens();
            // The game-event hooks (the battle log). Patched by this load's Harmony id, unpatched in Dispose.
            try { _harmony.PatchAll(typeof(ModuleMain).Assembly); }
            catch (Exception e) { host.LogError("Harmony patching failed: " + e); }
            // The scopes that exist already (a reload); from here on the scope hooks feed GameScopes.
            GameScopes.Seed();

            FocusMode.Set(host.Settings.FocusModeOnLaunch);
            host.LogInfo("Module loaded: " + ScreenManager.Registered.Count + " screens, "
                + InputManager.Actions.Count + " input actions, focus " + (FocusMode.Active ? "on" : "off"));
            _updateCheck.Start(host.ModVersion);
        }

        // The mod's authored strings follow lang/<language>.txt beside the plugin when one exists for the
        // game language; English (the defaults) otherwise.
        private static void LoadLanguage(string pluginDir)
        {
            try
            {
                string code = Application.systemLanguage.ToString().ToLowerInvariant();
                string path = Path.Combine(pluginDir, "lang", code + ".txt");
                if (!File.Exists(path)) { Strings.LoadTranslation(null); return; }
                Strings.LoadTranslation(File.ReadAllLines(path));
                CoreLog.Info("Strings: loaded " + path);
            }
            catch (Exception e)
            {
                CoreLog.Warning("Strings: translation load failed: " + e.Message);
            }
        }

        private static void RegisterInput()
        {
            // UI navigation keys: live only while a screen declares the UI category and focus mode is
            // on, routed into the navigator by InputManager. Directions and Tab auto-repeat while held.
            InputManager.Register(UiActions.Up, "Navigate up", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow)).Repeating();
            InputManager.Register(UiActions.Down, "Navigate down", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow)).Repeating();
            InputManager.Register(UiActions.Left, "Navigate left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.LeftArrow)).Repeating();
            InputManager.Register(UiActions.Right, "Navigate right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.RightArrow)).Repeating();
            InputManager.Register(UiActions.Next, "Next control group", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab)).Repeating();
            InputManager.Register(UiActions.Prev, "Previous control group", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab, shift: true)).Repeating();
            InputManager.Register(UiActions.Activate, "Activate", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Return)).AddBinding(new KeyboardBinding(KeyCode.KeypadEnter));
            InputManager.Register(UiActions.Secondary, "Secondary action", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Backspace));
            InputManager.Register(UiActions.Back, "Back", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Escape));
            InputManager.Register(UiActions.Home, "Jump to first", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Home));
            InputManager.Register(UiActions.End, "Jump to last", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.End));
            InputManager.Register(UiActions.RegionPrev, "Previous section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow, alt: true));
            InputManager.Register(UiActions.RegionNext, "Next section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow, alt: true));
            // The run's board, a pointy-top hex grid: Q E A D Z C step focus to the focused cell's six
            // neighbours (the letters' layout on the keyboard is the hexagon's: no cell lies straight up
            // or down), Shift+the same letter moves the focused hero there. The board section answers
            // them while a cell is focused; anywhere else the letters are the type-ahead search's.
            InputManager.Register(BoardSection.StepUpLeft, "Cell up left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Q)).Repeating();
            InputManager.Register(BoardSection.StepUpRight, "Cell up right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.E)).Repeating();
            InputManager.Register(BoardSection.StepLeft, "Cell left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.A)).Repeating();
            InputManager.Register(BoardSection.StepRight, "Cell right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.D)).Repeating();
            InputManager.Register(BoardSection.StepDownLeft, "Cell down left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Z)).Repeating();
            InputManager.Register(BoardSection.StepDownRight, "Cell down right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.C)).Repeating();
            InputManager.Register(BoardSection.MoveUpLeft, "Move hero up left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Q, shift: true));
            InputManager.Register(BoardSection.MoveUpRight, "Move hero up right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.E, shift: true));
            InputManager.Register(BoardSection.MoveLeft, "Move hero left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.A, shift: true));
            InputManager.Register(BoardSection.MoveRight, "Move hero right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.D, shift: true));
            InputManager.Register(BoardSection.MoveDownLeft, "Move hero down left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Z, shift: true));
            InputManager.Register(BoardSection.MoveDownRight, "Move hero down right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.C, shift: true));
            // Buffer review, the Harkest Dungeon keys: Ctrl+Left/Right switch buffers, Ctrl+Up/Down step lines.
            Buffers.Init();
            InputManager.Register("buffer.next", "Next buffer", InputCategory.UI, Buffers.Controls.NextBuffer).AddBinding(new KeyboardBinding(KeyCode.RightArrow, ctrl: true));
            InputManager.Register("buffer.prev", "Previous buffer", InputCategory.UI, Buffers.Controls.PreviousBuffer).AddBinding(new KeyboardBinding(KeyCode.LeftArrow, ctrl: true));
            InputManager.Register("buffer.line.next", "Next buffer line", InputCategory.UI, Buffers.Controls.NextLine).AddBinding(new KeyboardBinding(KeyCode.UpArrow, ctrl: true)).Repeating();
            InputManager.Register("buffer.line.prev", "Previous buffer line", InputCategory.UI, Buffers.Controls.PreviousLine).AddBinding(new KeyboardBinding(KeyCode.DownArrow, ctrl: true)).Repeating();
            // The glance keys: a digit speaks one fact group of the unit the focused control concerns,
            // in place (UnitGlance); Shift+2, 3, 4 the same group with each stat's breakdown; Ctrl+S
            // the run's shards, Ctrl+C the focused cell's or unit's board coordinates, Ctrl+T the
            // battle timer, Ctrl+N the units near the focused cell or hero while placing, Ctrl+H the
            // hostile ones, Ctrl+Q the quests of the focused hero, item or relic, Ctrl+M a Red Rift
            // run's missions (RunGlance). Digits and Ctrl chords never clash with the type-ahead search,
            // which owns the bare letters.
            Glance("glance.vitals", "Unit health, shield and mana", KeyCode.Alpha1, KeyCode.Keypad1, () => UnitGlance.Speak(UnitGlance.Group.Vitals));
            Glance("glance.attack", "Unit attack, magic and defense", KeyCode.Alpha2, KeyCode.Keypad2, () => UnitGlance.Speak(UnitGlance.Group.Attack));
            Glance("glance.tempo", "Unit attack speed, crit, range and move speed", KeyCode.Alpha3, KeyCode.Keypad3, () => UnitGlance.Speak(UnitGlance.Group.Tempo));
            Glance("glance.sustain", "Unit regen, omnivamp and resistances", KeyCode.Alpha4, KeyCode.Keypad4, () => UnitGlance.Speak(UnitGlance.Group.Sustain));
            Glance("glance.statuses", "Unit statuses", KeyCode.Alpha5, KeyCode.Keypad5, () => UnitGlance.Speak(UnitGlance.Group.Statuses));
            Glance("glance.target", "Unit target", KeyCode.Alpha6, KeyCode.Keypad6, () => UnitGlance.Speak(UnitGlance.Group.Target));
            Glance("glance.attack.detail", "Unit attack, magic and defense, with breakdown", KeyCode.Alpha2, KeyCode.Keypad2, () => UnitGlance.Speak(UnitGlance.Group.Attack, detail: true), shift: true);
            Glance("glance.tempo.detail", "Unit attack speed, crit, range and move speed, with breakdown", KeyCode.Alpha3, KeyCode.Keypad3, () => UnitGlance.Speak(UnitGlance.Group.Tempo, detail: true), shift: true);
            Glance("glance.sustain.detail", "Unit regen, omnivamp and resistances, with breakdown", KeyCode.Alpha4, KeyCode.Keypad4, () => UnitGlance.Speak(UnitGlance.Group.Sustain, detail: true), shift: true);
            InputManager.Register("run.shards", "Shards", InputCategory.UI, RunGlance.Shards).AddBinding(new KeyboardBinding(KeyCode.S, ctrl: true));
            InputManager.Register("run.position", "Board position", InputCategory.UI, RunGlance.Position).AddBinding(new KeyboardBinding(KeyCode.C, ctrl: true));
            InputManager.Register("run.timer", "Battle timer", InputCategory.UI, RunGlance.Timer).AddBinding(new KeyboardBinding(KeyCode.T, ctrl: true));
            InputManager.Register("run.nearby", "Nearby units", InputCategory.UI, () => RunGlance.Nearby(hostilesOnly: false)).AddBinding(new KeyboardBinding(KeyCode.N, ctrl: true));
            InputManager.Register("run.hostiles", "Nearby hostiles", InputCategory.UI, () => RunGlance.Nearby(hostilesOnly: true)).AddBinding(new KeyboardBinding(KeyCode.H, ctrl: true));
            InputManager.Register("run.quests", "Quests of the focused hero or item", InputCategory.UI, RunGlance.Quests).AddBinding(new KeyboardBinding(KeyCode.Q, ctrl: true));
            InputManager.Register("run.missions", "Red Rift missions", InputCategory.UI, RunGlance.Missions).AddBinding(new KeyboardBinding(KeyCode.M, ctrl: true));
            // The shop's reroll and freeze, answered by the shop screen's own actions (ShopScreen) and
            // by nothing else: no handler here.
            InputManager.Register("shop.reroll", "Shop reroll", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.R, ctrl: true));
            InputManager.Register("shop.freeze", "Shop freeze", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.F, ctrl: true));

            // Global: always live, so the player can hand the keyboard back to the game and reclaim it.
            InputManager.Register("mod.focus", "Toggle navigation", InputCategory.Global, ToggleFocus)
                .AddBinding(new KeyboardBinding(KeyCode.A, ctrl: true, shift: true));
            InputManager.Register("mod.menu", "Mod menu", InputCategory.Global, ModMenuScreen.Toggle)
                .AddBinding(new KeyboardBinding(KeyCode.M, ctrl: true, shift: true));
        }

        private static void ToggleFocus()
        {
            FocusMode.Toggle();
            Speech.Say(FocusMode.Active ? Strings.FocusOn : Strings.FocusOff, interrupt: true);
            if (FocusMode.Active) Navigation.AnnounceCurrent();
        }

        // A glance key on the digit row and the keypad alike.
        private static void Glance(string key, string label, KeyCode digit, KeyCode keypad, Action speak, bool shift = false)
        {
            InputManager.Register(key, label, InputCategory.UI, speak)
                .AddBinding(new KeyboardBinding(digit, shift: shift))
                .AddBinding(new KeyboardBinding(keypad, shift: shift));
        }

        private static void RegisterScreens()
        {
            ScreenManager.Register(new MainMenuScreen());
            ScreenManager.Register(new SettingsScreen());
            ScreenManager.Register(new DifficultyScreen());
            ScreenManager.Register(new HeroPickerScreen());
            ScreenManager.Register(new GameRunScreen());
            ScreenManager.Register(new BattleResultScreen());
            ScreenManager.Register(new ShopScreen());
            ScreenManager.Register(new CrossroadsScreen());
            ScreenManager.Register(new EventScreen());
            ScreenManager.Register(new PickerScreen());
            ScreenManager.Register(new RelicPickerScreen());
            ScreenManager.Register(new HeroesPanelScreen());
            ScreenManager.Register(new RunSettingsScreen());
            ScreenManager.Register(new RunEndScreen());
            ScreenManager.Register(new ProgressionScreen());
            ScreenManager.Register(new ComicScreen());
            ScreenManager.Register(new CompendiumScreen());
            ScreenManager.Register(new ModMenuScreen());
            // The tutorial's modal phase (its timed texts): exclusive, so a step's action prompt is
            // listening by the time the player acts.
            ScreenManager.Register(new TutorialPromptScreen());
            // Modal dialogs (layer 30, exclusive): the privacy consent that greets a fresh install, the
            // generic confirmation, the error box, and the exit / survey prompts.
            ScreenManager.Register(new DialogScreen<Ember.System.UI.GdprDialogPanel>("dialog.privacy", () => Strings.ScreenPrivacy));
            ScreenManager.Register(new DialogScreen<Ember.System.UI.DialogPanel>("dialog.confirm", () => Strings.ScreenConfirm));
            ScreenManager.Register(new DialogScreen<Ember.System.UI.ErrorDialogPanel>("dialog.error", () => Strings.ScreenError));
            ScreenManager.Register(new DialogScreen<Ember.System.UI.ExitSteamDialogPanel>("dialog.exit", () => Strings.ScreenExit));
            ScreenManager.Register(new DialogScreen<Ember.System.UI.SteamSurveyDialogPanel>("dialog.steamsurvey", () => Strings.ScreenSurvey));
            ScreenManager.Register(new DialogScreen<Ember.Scopes.Application.UI.SurveyDialogPanel>("dialog.survey", () => Strings.ScreenSurvey));
        }

        public void Tick()
        {
            if (!_updateAnnounced && _updateCheck.NewerVersion != null)
            {
                _updateAnnounced = true;
                Speech.Say(Strings.UpdateAvailable(_updateCheck.NewerVersion));
            }
            FocusMode.Tick();
            InputManager.Tick();
            ScreenManager.Tick();
            Navigation.TickTypeahead();
            Safe(_comics.Tick, "comics");
            Safe(_tutorials.Tick, "tutorials");
            Safe(BattleEvents.Tick, "battle events");
            Safe(SyntheticMouse.Tick, "synthetic mouse");
            Safe(Later.Tick, "later");
            Safe(Buffers.Tick, "buffers");
        }

        // A reader that throws must not take the whole tick (and every other reader) down with it.
        private void Safe(Action tick, string what)
        {
            try { tick(); }
            catch (Exception e) { _host?.LogWarning("[" + what + "] " + e); }
        }

        /// <summary>Undo every persistent game-side effect Load created. Runs on reload (after the new
        /// generation is live) and on shutdown.</summary>
        public void Dispose()
        {
            // On a reload the successor already owns the keyboard and the EventSystem: drop our hooks
            // only. On a shutdown, give everything back to the game.
            bool restore = _host == null || !_host.SuccessorLoaded;
            try { SyntheticMouse.Reset(); } catch (Exception e) { _host?.LogError("[dispose] mouse: " + e); }
            try { FocusMode.Shutdown(restore); } catch (Exception e) { _host?.LogError("[dispose] focus: " + e); }
            try { ScreenManager.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] screens: " + e); }
            try { InputManager.Clear(); } catch (Exception e) { _host?.LogError("[dispose] input: " + e); }
            try { GameScopes.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] scopes: " + e); }
            try { _harmony?.UnpatchSelf(); } catch (Exception e) { _host?.LogError("[dispose] harmony: " + e); }
            _harmony = null;
            _host = null;
            if (ReferenceEquals(Host, this)) Host = null;
        }

        // ---- IDevDriver: the dev server drives and inspects our navigation through these ----

        public string DispatchAction(string actionKey)
        {
            // Not keys: the dev driver's own verbs (the coverage report; the run shortcuts).
            if (actionKey == "dev.audit") return Dev.ScreenAudit.Run();
            if (actionKey != null && actionKey.StartsWith("dev.floor:")) return Dev.RunJump.Floor(actionKey.Substring("dev.floor:".Length));
            if (actionKey == "dev.shop") return Dev.RunJump.Shop();
            if (string.IsNullOrEmpty(actionKey) || InputManager.Find(actionKey) == null) return null;
            InputManager.Dispatch(actionKey);
            var nav = Navigation.Active as GraphNavigator;
            var node = nav?.FocusedNode;
            return "fired " + actionKey + (node != null ? " -> " + GraphAnnouncer.ComposeFull(node) : "");
        }

        public string DescribeNav()
        {
            var sb = new StringBuilder();
            sb.Append("focus mode: ").Append(FocusMode.Active ? "on" : "off").Append('\n');
            sb.Append("stack: ");
            foreach (var s in ScreenManager.Stack) sb.Append(s.Key).Append(' ');
            sb.Append('\n');
            var cur = ScreenManager.Current;
            sb.Append("screen: ").Append(cur != null ? cur.Key + " (" + (cur.ScreenName ?? "") + ")" : "(none)").Append('\n');
            var nav = Navigation.Active as GraphNavigator;
            var node = nav?.FocusedNode;
            if (node == null)
            {
                sb.Append("focus: (none)\n");
                return sb.ToString();
            }
            sb.Append("focus: ").Append(node.Id).Append('\n');
            sb.Append("readout: ").Append(GraphAnnouncer.ComposeFull(node)).Append('\n');
            var render = nav.CurrentRender;
            if (render != null)
            {
                sb.Append("nodes (").Append(render.Order.Count).Append("):\n");
                foreach (var n in render.Order)
                    sb.Append(ReferenceEquals(n, node) ? "  > " : "    ").Append(n.Id.StructuralKey)
                      .Append(": ").Append(GraphAnnouncer.LeafText(n)).Append('\n');
            }
            return sb.ToString();
        }

        public string ListActions()
        {
            var sb = new StringBuilder();
            foreach (var a in InputManager.Actions)
                sb.Append(a.Key).Append(" [").Append(a.Category).Append("] ").Append(a.BindingsDisplay).Append('\n');
            return sb.ToString();
        }
    }
}
