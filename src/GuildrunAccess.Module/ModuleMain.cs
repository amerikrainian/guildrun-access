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

            FocusMode.Set(host.Settings.FocusModeOnLaunch);
            host.LogInfo("Module loaded: " + ScreenManager.Registered.Count + " screens, "
                + InputManager.Actions.Count + " input actions, focus " + (FocusMode.Active ? "on" : "off"));
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
            InputManager.Register(UiActions.Tooltip, "Read description", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Space));
            InputManager.Register(UiActions.RegionPrev, "Previous section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow, ctrl: true));
            InputManager.Register(UiActions.RegionNext, "Next section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow, ctrl: true));
            InputManager.Register(UiActions.ReadFocus, "Read current control", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Space, ctrl: true));

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
            ScreenManager.Register(new HeroesPanelScreen());
            ScreenManager.Register(new RunSettingsScreen());
            ScreenManager.Register(new RunEndScreen());
            ScreenManager.Register(new ProgressionScreen());
            ScreenManager.Register(new ComicScreen());
            ScreenManager.Register(new CompendiumScreen());
            ScreenManager.Register(new ModMenuScreen());
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
            FocusMode.Tick();
            InputManager.Tick();
            ScreenManager.Tick();
            Navigation.TickTypeahead();
            Safe(_comics.Tick, "comics");
            Safe(_tutorials.Tick, "tutorials");
            Safe(Run.BattleEvents.Tick, "battle events");
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
            try { FocusMode.Shutdown(restore); } catch (Exception e) { _host?.LogError("[dispose] focus: " + e); }
            try { ScreenManager.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] screens: " + e); }
            try { InputManager.Clear(); } catch (Exception e) { _host?.LogError("[dispose] input: " + e); }
            try { _harmony?.UnpatchSelf(); } catch (Exception e) { _host?.LogError("[dispose] harmony: " + e); }
            _harmony = null;
            _host = null;
            if (ReferenceEquals(Host, this)) Host = null;
        }

        // ---- IDevDriver: the dev server drives and inspects our navigation through these ----

        public string DispatchAction(string actionKey)
        {
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
