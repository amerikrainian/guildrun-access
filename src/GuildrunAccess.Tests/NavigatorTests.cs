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
            public bool QuietRows; // rows that vanish without their replacement being spoken
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
                        QuietVanish = QuietRows,
                        Announcements = new[] { new NodeAnnouncement(() => item, kind: AnnouncementKinds.Label) },
                    });
                b.PopContext();
            }
        }

        private static InputAction Action(string key) => new InputAction(key, key) { Category = InputCategory.UI };

        // A tab strip: landing on a tab selects it (the vtable's focus hook), Enter selects it too.
        private sealed class TabScreen : Screen
        {
            public int Selected;
            public int FocusCalls;
            public override string Key => "test.tabs";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b)
            {
                b.PushContext("Sections", null);
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    b.AddItem(ControlId.Structural("tab" + i), new NodeVtable
                    {
                        ControlType = ControlTypes.Tab,
                        Announcements = new[] { new NodeAnnouncement(() => "Tab " + index, kind: AnnouncementKinds.Label) },
                        OnFocus = () => { FocusCalls++; Selected = index; },
                    });
                }
                b.PopContext();
            }
        }

        [Fact]
        public void LandingOnATabRunsItsFocusHookButFirstFocusDoesNot()
        {
            var input = new FakeNavInput { FrameCount = 1 };
            Speech.Speak = (t, i) => { };
            NavInput.Current = input;
            try
            {
                var nav = new GraphNavigator();
                var screen = new TabScreen();
                nav.Attach(screen);
                nav.EnsureFocus();
                Assert.Equal(0, screen.FocusCalls); // the screen's first focus is not a choice

                Assert.True(nav.OnInputJustPressed(Action(UiActions.Down)));
                Assert.Equal(1, screen.FocusCalls);
                Assert.Equal(1, screen.Selected);

                Assert.True(nav.OnInputJustPressed(Action(UiActions.End)));
                Assert.Equal(2, screen.FocusCalls);
                Assert.Equal(2, screen.Selected);
            }
            finally
            {
                Speech.Speak = (t, i) => { };
            }
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

        [Fact]
        public void AQuietRowVanishingUnderFocusMovesSilentlyAndTheNextKeySpeaks()
        {
            var spoken = new List<string>();
            var input = new FakeNavInput { FrameCount = 1 };
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = input;
            GraphAnnouncer.PositionText = (i, n) => i + " of " + n;
            try
            {
                var nav = new GraphNavigator();
                var screen = new MenuScreen { QuietRows = true };
                nav.Attach(screen);
                nav.EnsureFocus();
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Down)));
                Assert.Equal("New run, button, 2 of 4", spoken[spoken.Count - 1]);

                // The focused quiet row goes: focus slides to the nearest survivor without a word.
                screen.Items.Remove("New run");
                int before = spoken.Count;
                input.FrameCount += 10;
                nav.EnsureFocus();
                Assert.Equal(before, spoken.Count);
                input.FrameCount += 10;
                nav.EnsureFocus(); // and stays quiet on later frames
                Assert.Equal(before, spoken.Count);

                // The next key reads from where focus now sits (the plain nearest-survivor walk went to
                // the previous row, Continue).
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Down)));
                Assert.Equal("Settings, button, 2 of 3", spoken[spoken.Count - 1]);
            }
            finally
            {
                Speech.Speak = (t, i) => { };
                GraphAnnouncer.PositionText = null;
            }
        }
    }
}
