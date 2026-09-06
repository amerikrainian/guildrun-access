using System.Collections.Generic;
using Ember.Scopes.Application.Settings;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The settings panel (<see cref="SettingsUIController"/>): a tab strip (General / Video / Audio /
    /// Debug) as one Tab-stop, the visible page's rows (dropdowns, toggles, sliders, buttons) as the
    /// next, and Reset / Back as the last. Rows are declared for every page and filtered by visibility,
    /// so switching a tab re-renders the body automatically. Row captions come from the game's own
    /// localized NamePanel titles; slider values from the game's status labels. Escape presses Back.
    /// </summary>
    public sealed class SettingsScreen : Screen
    {
        public override string Key => "settings";
        public override int Layer => 27; // above the run's pause menu, which opens it
        public override bool Exclusive => true;
        public override object InitialFocusStop => "body";

        private SettingsUIController _controller;
        private const int SearchEvery = 30;
        private int _lastSearchFrame = -SearchEvery;

        private SettingsUIController Controller()
        {
            if (_controller != null) return _controller;
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return null;
            _lastSearchFrame = Time.frameCount;
            var found = Object.FindObjectOfType(Il2CppType.Of<SettingsUIController>());
            _controller = found != null ? found.TryCast<SettingsUIController>() : null;
            return _controller;
        }

        public override bool IsActive()
        {
            var c = Controller();
            var root = c != null ? c._settingsUIRoot : null;
            return root != null && root.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;

            b.PushContext(PanelTitle(c) ?? Strings.ScreenSettings, Strings.RoleList, positions: false);

            // Tabs: the toggle group under the tab view, one row.
            b.BeginStop("tabs");
            var tabs = c._tabView != null ? c._tabView.GetComponentsInChildren<Toggle>(false) : null;
            if (tabs != null && tabs.Length > 0)
            {
                b.StartRow();
                int i = 0;
                foreach (var tab in tabs)
                {
                    // A caption-less toggle in the strip is decoration (the active-background), not a tab.
                    if (!GameNodes.IsShown(tab) || tab.GetComponentInChildren<TMP_Text>(true) == null) continue;
                    b.AddItem(ControlId.Structural("settings:tab" + i++), GameNodes.Tab(tab));
                }
                b.EndRow();
            }

            // The visible page's rows.
            b.BeginStop("body");
            Dropdown(b, "locale", c._localeDropDown);
            Toggle(b, "camerashake", c._cameraShakeToggle);
            Toggle(b, "gdpr", c._gdprToggle);
            Dropdown(b, "displaymode", c._displayModeModeDropDown);
            Dropdown(b, "resolution", c._resolutionDropDown);
            Dropdown(b, "display", c._displaySelectionDropDown);
            Toggle(b, "vsync", c._vSyncToggle);
            Dropdown(b, "framerate", c._frameRateDropDown);
            Dropdown(b, "quality", c._qualityLevelDropdown);
            Slider(b, "master", c._masterVolumeSlider, c._masterVolumeStatusLabel);
            Slider(b, "music", c._musicSlider, c._musicStatusLabel);
            Slider(b, "sfx", c._sfxSlider, c._sfxStatusLabel);
            Slider(b, "voice", c._voiceOverSlider, c._voiceOverStatusLabel);
            Button(b, "cleartutorials", c._clearTutorials);

            // Actions.
            b.BeginStop("actions");
            Button(b, "reset", c._resetSettings);
            Button(b, "back", c._backButton);

            b.PopContext();
        }

        private static string PanelTitle(SettingsUIController c)
        {
            var root = c._settingsUIRoot;
            if (root == null) return null;
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "PanelTitle" && !string.IsNullOrWhiteSpace(tmp.text))
                    return tmp.text;
            return null;
        }

        private static void Dropdown(GraphBuilder b, string key, TMP_Dropdown dd)
        {
            if (!GameNodes.IsShown(dd)) return;
            b.AddItem(ControlId.Structural("settings:" + key), GameNodes.Dropdown(dd, () => GameNodes.RowLabel(dd) ?? key));
        }

        private static void Toggle(GraphBuilder b, string key, Toggle toggle)
        {
            if (!GameNodes.IsShown(toggle)) return;
            b.AddItem(ControlId.Structural("settings:" + key), GameNodes.Toggle(toggle, () => GameNodes.RowLabel(toggle) ?? key));
        }

        private static void Slider(GraphBuilder b, string key, Slider slider, TMP_Text status)
        {
            if (!GameNodes.IsShown(slider)) return;
            b.AddItem(ControlId.Structural("settings:" + key), GameNodes.Slider(slider,
                () => GameNodes.RowLabel(slider) ?? key,
                status != null ? () => status.text : (System.Func<string>)null));
        }

        private static void Button(GraphBuilder b, string key, Button button)
        {
            if (!GameNodes.IsShown(button)) return;
            b.AddItem(ControlId.Structural("settings:" + key), GameNodes.Button(button));
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller();
                if (c != null && GameNodes.IsShown(c._backButton)) c._backButton.onClick.Invoke();
            });
        }
    }
}
