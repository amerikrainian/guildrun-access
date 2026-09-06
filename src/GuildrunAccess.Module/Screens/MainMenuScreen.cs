using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
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

        private MainMenuUIController _controller;
        private const int SearchEvery = 30; // frames between scene scans while the controller is absent
        private int _lastSearchFrame = -SearchEvery; // "long ago" without an int.MinValue subtraction overflow

        // The live controller, re-found by scene scan (throttled) once the last one was destroyed.
        private MainMenuUIController Controller()
        {
            if (_controller != null) return _controller; // Unity's lifetime-aware null check
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return null;
            _lastSearchFrame = Time.frameCount;
            var found = Object.FindObjectOfType(Il2CppType.Of<MainMenuUIController>());
            _controller = found != null ? found.TryCast<MainMenuUIController>() : null;
            return _controller;
        }

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
