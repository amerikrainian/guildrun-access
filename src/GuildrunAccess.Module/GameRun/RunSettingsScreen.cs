using System.Collections.Generic;
using Ember.Scopes.Application.Compendium;
using Ember.Scopes.GameRun.UI.Navigation;
using TMPro;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The run's pause menu (<see cref="SettingsPanelView"/>, opened by the HUD's Settings button or
    /// the game's Escape), under the window's own title ("Game paused"): Continue, Settings (the full
    /// settings panel opens above it), Compendium, Abandon Run, Quit to Menu, Quit Game, each with
    /// the game's own caption. Escape presses Continue.
    /// </summary>
    public sealed class RunSettingsScreen : Screen
    {
        public override string Key => "gamerun.pause";
        public override int Layer => 26;
        public override bool Exclusive => true;

        private static NavigationUIController Nav => GameScopes.Controller<NavigationUIController>();

        private SettingsPanelView Panel()
        {
            var nav = Nav;
            var panel = nav != null ? nav._settingsPanelView : null;
            return panel != null && panel.gameObject.activeInHierarchy ? panel : null;
        }

        public override bool IsActive() => Panel() != null;

        public override void Build(GraphBuilder b)
        {
            var panel = Panel();
            if (panel == null) return;
            b.PushContext(Title(panel), Strings.RoleList);
            foreach (var button in new[] { panel.ContinueButton, panel.SettingsButton, CompendiumButton(panel), panel.AbandonRunButton, panel.QuitToMenuButton, panel.QuitGameButton })
            {
                if (!GameNodes.IsShown(button)) continue;
                b.AddItem(ControlId.Structural("pause:" + button.GetInstanceID()), GameNodes.Button(button));
            }
            b.PopContext();
        }

        // The window's own title text ("Game paused"), else our name for it.
        private static string Title(SettingsPanelView panel)
        {
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "TitleText" && !string.IsNullOrWhiteSpace(tmp.text)) return tmp.text.Trim();
            return Strings.RunSettings;
        }

        // The window's Compendium button has no field on the view: it is the opener component inside it.
        private static UnityEngine.UI.Button CompendiumButton(SettingsPanelView panel)
        {
            var opener = panel.GetComponentInChildren<OpenCompendiumButton>(true);
            return opener != null ? opener._button : null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var panel = Panel();
                var cont = panel != null ? panel.ContinueButton : null;
                if (GameNodes.IsShown(cont) && cont.interactable) cont.onClick.Invoke();
            });
        }
    }
}
