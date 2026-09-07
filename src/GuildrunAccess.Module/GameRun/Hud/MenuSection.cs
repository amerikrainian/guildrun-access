using Ember.Scopes.GameRun.UI.Navigation;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "menu" stop: the HUD's navigation buttons (heroes panel, the choice screen, feedback,
    /// settings), which the game otherwise binds to hotkeys we mute.</summary>
    internal sealed class MenuSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var nav = GameScopes.Controller<NavigationUIController>();
            if (nav == null || !nav.gameObject.activeInHierarchy) return;
            b.BeginStop("menu");
            b.PushContext(Strings.RunMenu, Strings.RoleList);
            if (GameNodes.IsShown(nav._heroPanelButton))
                b.AddItem(ControlId.Structural("run:menu:heroes"), GameNodes.Button(nav._heroPanelButton, () => Strings.RunHeroPanel));
            if (GameNodes.IsShown(nav._choiceNavigationButton))
                b.AddItem(ControlId.Structural("run:menu:choice"), GameNodes.Button(nav._choiceNavigationButton,
                    () => nav._choiceNavigationLabel != null && !string.IsNullOrWhiteSpace(nav._choiceNavigationLabel.text)
                        ? nav._choiceNavigationLabel.text : GameNodes.LabelOf(nav._choiceNavigationButton)));
            if (GameNodes.IsShown(nav._showFeedbackPanelButton))
                b.AddItem(ControlId.Structural("run:menu:feedback"), GameNodes.Button(nav._showFeedbackPanelButton, () => Strings.RunFeedback));
            if (GameNodes.IsShown(nav._settingsButton))
                b.AddItem(ControlId.Structural("run:menu:settings"), GameNodes.Button(nav._settingsButton, () => Strings.RunSettings));
            b.PopContext();
        }
    }
}
