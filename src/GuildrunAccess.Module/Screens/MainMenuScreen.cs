using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The main menu, our first navigable screen: the buttons of the live <see cref="MainMenuUIController"/>
    /// (Continue / New run, Co-op, Tutorial, Abandon run, Settings, Community, Quit) as one
    /// arrow-navigable list, each confirming through its own onClick. Graph-native: declared fresh from
    /// the live controller every render; a button hidden by the game simply stops being declared.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override string Key => "ctx.mainmenu";
        public override int Layer => 0;
        // No ScreenName: the list context announces "Main menu" via the path diff on entry.

        private static MainMenuUIController Controller() => GameScopes.Controller<MainMenuUIController>();

        public override bool IsActive()
        {
            var c = Controller();
            return c != null && c.gameObject.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller();
            if (c == null) return;

            b.PushContext(Strings.ScreenMainMenu, Strings.RoleList);
            AddButton(b, "start", c._startButton);
            AddButton(b, "coop", c._startCoopButton);
            AddButton(b, "tutorial", c._playFromTutorialButton);
            AddButton(b, "abandon", c._abandonRunButton);
            AddButton(b, "settings", c._settingsButton);
            AddButton(b, "community", c._communityButton);
            AddButton(b, "quit", c._quitButton);
            b.PopContext();
        }

        private static void AddButton(GraphBuilder b, string key, Button button)
        {
            if (!GameNodes.IsShown(button)) return;
            b.AddItem(ControlId.Structural("mainmenu:" + key), GameNodes.Button(button));
        }
    }
}
