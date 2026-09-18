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
        private const string ChoiceKey = "run:menu:choice";
        private bool _choiceWasHidden;

        // A rank-up choice has just been hidden (the picker's Hide), and this screen is the one on
        // show again: land on the button that brings the choice back, so the line that greets the
        // player says where the way back is ("Run, menu, list, Show, button").
        public override void OnUpdate()
        {
            var nav = GameScopes.Controller<NavigationUIController>();
            bool hidden = nav != null && nav._isChoiceHidden && GameNodes.IsShown(nav._choiceNavigationButton);
            if (hidden && !_choiceWasHidden) GuildrunAccess.Core.UI.Navigation.FocusNode(ControlId.Structural(ChoiceKey));
            _choiceWasHidden = hidden;
        }

        public override void Build(GraphBuilder b)
        {
            var nav = GameScopes.Controller<NavigationUIController>();
            if (nav == null || !nav.gameObject.activeInHierarchy) return;
            b.BeginStop("menu");
            b.PushContext(Strings.RunMenu, Strings.RoleList);
            if (GameNodes.IsShown(nav._heroPanelButton))
                b.AddItem(ControlId.Structural("run:menu:heroes"), GameNodes.Button(nav._heroPanelButton, () => Strings.RunHeroPanel));
            AddChoiceButton(b, nav, ChoiceKey);
            if (GameNodes.IsShown(nav._showFeedbackPanelButton))
                b.AddItem(ControlId.Structural("run:menu:feedback"), GameNodes.Button(nav._showFeedbackPanelButton, () => Strings.RunFeedback));
            if (GameNodes.IsShown(nav._settingsButton))
                b.AddItem(ControlId.Structural("run:menu:settings"), GameNodes.Button(nav._settingsButton, () => Strings.RunSettings));
            b.PopContext();
        }

        /// <summary>The button that hides a pending rank-up choice and brings it back
        /// (<c>NavigationUIController.ToggleChoiceVisibility</c>), by the caption the game gives it
        /// for the state it is in: "Hide" on the picker, "Show" here once it is hidden, when the HUD
        /// (or the shop the choice came up over) is the screen again with the choice still owed.
        /// Nothing while the game shows no such button.</summary>
        internal static void AddChoiceButton(GraphBuilder b, NavigationUIController nav, string id)
        {
            if (nav == null || !GameNodes.IsShown(nav._choiceNavigationButton)) return;
            b.AddItem(ControlId.Structural(id), GameNodes.Button(nav._choiceNavigationButton,
                () => nav._choiceNavigationLabel != null && !string.IsNullOrWhiteSpace(nav._choiceNavigationLabel.text)
                    ? nav._choiceNavigationLabel.text : GameNodes.LabelOf(nav._choiceNavigationButton)));
        }
    }
}
