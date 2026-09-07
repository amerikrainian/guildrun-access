using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Navigation;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The run's pause menu (<see cref="SettingsPanelView"/>, opened by the HUD's Settings button or
    /// the game's Escape): Continue, Settings (the full settings panel opens above it), Abandon Run,
    /// Quit to Menu, Quit Game, each with the game's own caption. Escape presses Continue.
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
            b.PushContext(Strings.RunSettings, Strings.RoleList);
            foreach (var button in new[] { panel.ContinueButton, panel.SettingsButton, panel.AbandonRunButton, panel.QuitToMenuButton, panel.QuitGameButton })
            {
                if (!GameNodes.IsShown(button)) continue;
                b.AddItem(ControlId.Structural("pause:" + button.GetInstanceID()), GameNodes.Button(button));
            }
            b.PopContext();
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
