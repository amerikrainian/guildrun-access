using Ember.Scopes.Application.Compendium;
using Ember.Scopes.Application.GamePlatform.View;
using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.GameRun;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The main menu, our first navigable screen: the buttons of the live <see cref="MainMenuUIController"/>
    /// (Continue / New run, Co-op, Tutorial, Abandon run, Compendium, Settings, Community, Quit) as one
    /// arrow-navigable list, each confirming through its own onClick; then the endless-mode leaderboard
    /// the menu shows (its visibility toggle, tabs and entries, the shared reader), then the wishlist
    /// button and the game's version. Graph-native: declared fresh from the live controller every
    /// render; a button hidden by the game simply stops being declared.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override string Key => "ctx.mainmenu";
        public override int Layer => 0;
        // No ScreenName: the list context announces "Main menu" via the path diff on entry.

        private TMP_Text _version;

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

            b.BeginStop("menu");
            b.PushContext(Strings.ScreenMainMenu, Strings.RoleList);
            AddButton(b, "start", c._startButton);
            AddButton(b, "coop", c._startCoopButton);
            AddButton(b, "tutorial", c._playFromTutorialButton);
            AddButton(b, "abandon", c._abandonRunButton);
            AddButton(b, "compendium", CompendiumButton(c));
            AddButton(b, "settings", c._settingsButton);
            AddButton(b, "community", c._communityButton);
            AddButton(b, "quit", c._quitButton);
            b.PopContext();

            // The leaderboard panel beside the menu: the same reader as the difficulty screen's.
            var lb = GameScopes.Component<LeaderboardController>();
            if (lb != null && lb.gameObject.activeInHierarchy)
                LeaderboardNodes.Add(b, lb, "mainmenu:lb");

            // The Steam wishlist widget and the build number, a stop of their own.
            var wishlist = GameScopes.Component<WishlistWidgetView>();
            var wishButton = wishlist != null && wishlist.gameObject.activeInHierarchy ? wishlist._wishListButton : null;
            var version = VersionText(c);
            if (GameNodes.IsShown(wishButton) || version != null)
            {
                b.BeginStop("extras");
                if (GameNodes.IsShown(wishButton))
                {
                    var heading = WishlistHeading(wishlist, wishButton);
                    if (heading != null)
                        b.AddItem(ControlId.Structural("mainmenu:wishlist:heading"), GameNodes.Text(() => heading.text.Trim()));
                    b.AddItem(ControlId.Structural("mainmenu:wishlist"), GameNodes.Button(wishButton));
                }
                if (version != null)
                    b.AddItem(ControlId.Structural("mainmenu:version"), GameNodes.Text(() => Strings.MainMenuVersion(version.text.Trim())));
            }
        }

        private static void AddButton(GraphBuilder b, string key, Button button)
        {
            if (!GameNodes.IsShown(button)) return;
            b.AddItem(ControlId.Structural("mainmenu:" + key), GameNodes.Button(button));
        }

        // The Compendium button sits with the menu buttons but the controller holds no field for it:
        // it is the opener component next to the Start button (hero cards carry openers of their own).
        private static Button CompendiumButton(MainMenuUIController c)
        {
            var start = c._startButton;
            var holder = start != null ? start.transform.parent : null;
            if (holder == null) return null;
            foreach (var opener in holder.GetComponentsInChildren<OpenCompendiumButton>(true))
                if (opener != null && opener._button != null) return opener._button;
            return null;
        }

        // The widget's caption ("Wishlist Guildrun now"): its first text outside the button.
        private static TMP_Text WishlistHeading(WishlistWidgetView widget, Button button)
        {
            foreach (var tmp in widget.GetComponentsInChildren<TMP_Text>(false))
            {
                if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                if (button != null && tmp.transform.IsChildOf(button.transform)) continue;
                return tmp;
            }
            return null;
        }

        // The build number label on the menu canvas (no field for it either): found once, held live.
        private TMP_Text VersionText(MainMenuUIController c)
        {
            if (_version != null && _version.gameObject != null) return _version.gameObject.activeInHierarchy ? _version : null;
            var start = c._startButton;
            var root = start != null ? start.transform.root : c.transform.root;
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                if (tmp != null && tmp.gameObject.name == "VersionText") { _version = tmp; break; }
            return _version != null && _version.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(_version.text) ? _version : null;
        }
    }
}
