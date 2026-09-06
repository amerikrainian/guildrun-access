using System.Collections.Generic;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Input;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.UI;
using Xunit;

namespace GuildrunAccess.Tests
{
    // The navigator over a stub screen, driven through the same action keys the input layer uses, with
    // speech captured through the Core seam.
    public class NavigatorTests
    {
        private sealed class MenuScreen : Screen
        {
            public List<string> Items = new List<string> { "Continue", "New run", "Settings", "Quit" };
            public override string Key => "test.menu";
            public override string ScreenName => "Test menu";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b)
            {
                b.PushContext("Main menu", "list");
                foreach (var item in Items)
                    b.AddItem(ControlId.Structural(item), new NodeVtable
                    {
                        ControlType = ControlTypes.Button,
                        Announcements = new[] { new NodeAnnouncement(() => item, kind: AnnouncementKinds.Label) },
                    });
                b.PopContext();
            }
        }

        private static InputAction Action(string key) => new InputAction(key, key) { Category = InputCategory.UI };

        // The engine inputs the navigator reads, with a frame counter the test advances by hand (the
        // idle rebuild is throttled to every few frames, as in the game).
        private sealed class FakeNavInput : INavInput
        {
            public int FrameCount { get; set; }
            public float UnscaledTime => FrameCount / 60f;
            public float UnscaledDeltaTime => 1f / 60f;
            public bool CtrlHeld => false;
            public bool AltHeld => false;
            public bool ShiftHeld => false;
            public bool EscapeDown => false;
            public bool UpHeld => false;
            public bool DownHeld => false;
            public string TypedText => "";
        }

        [Fact]
        public void EntryReadsContextThenArrowsReadLeaves()
        {
            var spoken = new List<string>();
            var input = new FakeNavInput { FrameCount = 1 };
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = input;
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            try
            {
                var nav = new GraphNavigator();
                var screen = new MenuScreen();
                nav.Attach(screen);
                nav.EnsureFocus();
                Assert.Equal("Main menu, list, Continue, button, 1 of 4", spoken[spoken.Count - 1]);

                Assert.True(nav.OnInputJustPressed(Action(UiActions.Down)));
                Assert.Equal("New run, button, 2 of 4", spoken[spoken.Count - 1]);

                Assert.True(nav.OnInputJustPressed(Action(UiActions.End)));
                Assert.Equal("Quit, button, 4 of 4", spoken[spoken.Count - 1]);

                // A rebuild that removes the focused item lands on the nearest survivor, spoken once.
                screen.Items.Remove("Quit");
                int before = spoken.Count;
                input.FrameCount += 10; // past the idle-rebuild throttle
                nav.EnsureFocus();
                Assert.Equal(before + 1, spoken.Count);
                Assert.Equal("Settings, button, 3 of 3", spoken[spoken.Count - 1]);
            }
            finally
            {
                Speech.Speak = (t, i) => { };
                GraphAnnouncer.PositionText = null;
            }
        }
    }
}
