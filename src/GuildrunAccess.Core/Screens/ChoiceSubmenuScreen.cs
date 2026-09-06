using System;
using System.Collections.Generic;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.UI;

namespace GuildrunAccess.Core.Screens
{
    // Inside this namespace the sibling namespace GuildrunAccess.Core.Strings shadows the class.
    using Strings = GuildrunAccess.Core.Strings.Strings;

    /// <summary>One entry of a <see cref="ChoiceSubmenuScreen"/>.</summary>
    public sealed class ChoiceOption
    {
        public string Label;
        /// <summary>Extra spoken detail after the label (a hero's items, a cost), or null.</summary>
        public string Detail;
        public bool Enabled = true;
        public Action OnSelect;

        public ChoiceOption(string label, Action onSelect, string detail = null, bool enabled = true)
        {
            Label = label;
            OnSelect = onSelect;
            Detail = detail;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// A list of options to pick from (a dropdown's values, "equip this item to which hero", a
    /// control's context actions), pushed as a CHILD SCREEN of whatever screen opened it
    /// (<see cref="Screen.PushChild"/>). As a child it is the focused screen while open and owns the
    /// keyboard; selecting an option or backing out removes it, and ScreenManager re-focuses the parent
    /// on its remembered control automatically. Reusable for any "open a list and pick one" interaction.
    /// The option list is immutable per instance; focus starts on <c>current</c> when one is given, so
    /// opening a value picker reads the selected value first.
    /// </summary>
    public sealed class ChoiceSubmenuScreen : Screen
    {
        private readonly string _title;
        private readonly IReadOnlyList<ChoiceOption> _options;
        private readonly int _current;

        public ChoiceSubmenuScreen(string title, IReadOnlyList<ChoiceOption> options, int current = -1)
        {
            _title = title;
            _options = options ?? new ChoiceOption[0];
            _current = current;
            Wrap = true;
        }

        /// <summary>Open the submenu as a child of the current screen.</summary>
        public static void Open(string title, IReadOnlyList<ChoiceOption> options, int current = -1)
        {
            ScreenManager.Current?.PushChild(new ChoiceSubmenuScreen(title, options, current));
        }

        public override string Key => "overlay.choice";
        public override string ScreenName => _title;
        public override bool IsActive() => false; // never poll-pushed: only ever a child screen

        public override IEnumerable<ElementAction> GetActions()
        {
            // Back closes the submenu without choosing.
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Close());
        }

        private void Close() => ParentScreen?.RemoveChild(this);

        public override void Build(GraphBuilder b)
        {
            b.PushContext(_title, Strings.RoleList);
            for (int i = 0; i < _options.Count; i++)
            {
                int idx = i;
                var option = _options[i];
                // Snapshots are safe: the submenu is ephemeral (a fresh instance per open, closed by the
                // selection itself), so an option cannot change while it lives.
                b.AddItem(ControlId.Structural("choice:" + i), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => option.Label, kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => option.Detail, kind: AnnouncementKinds.Value),
                        new NodeAnnouncement(() => idx == _current ? Strings.StateSelected : null, kind: AnnouncementKinds.Selected),
                        new NodeAnnouncement(() => option.Enabled ? null : Strings.StateDisabled, kind: AnnouncementKinds.Enabled),
                    },
                    SearchText = () => option.Label,
                    OnActivate = () =>
                    {
                        if (!option.Enabled) return;
                        Close();
                        option.OnSelect?.Invoke();
                    },
                });
            }
            b.PopContext();
            if (_current >= 0 && _current < _options.Count)
                b.SetStart(ControlId.Structural("choice:" + _current)); // land on the current option
        }
    }
}
