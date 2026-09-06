using System.Collections.Generic;
using Ember.Scopes.GameRun.Crossroads.Controllers;
using Ember.Scopes.GameRun.Crossroads.Views;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The crossroads after a shop (<see cref="CrossroadsController"/>): the game shows the next paths as
    /// gates in the world (a title over each, a description on hover) and picks one by mouse raycast.
    /// Here each gate is a button carrying its title and description, and Enter selects that path
    /// through the controller's own selection routine, so the game's feedback, sound and notification
    /// run as for a click on the gate.
    /// </summary>
    public sealed class CrossroadsScreen : Screen
    {
        public override string Key => "gamerun.crossroads";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private readonly Finder<CrossroadsController> _crossroads = new Finder<CrossroadsController>();

        public override bool IsActive()
        {
            var c = _crossroads.Get();
            return c != null && c.gameObject.activeInHierarchy && Gates(c).Count > 0;
        }

        public override void Build(GraphBuilder b)
        {
            var c = _crossroads.Get();
            if (c == null) return;

            string title = c._crossroadsTitle != null && !string.IsNullOrWhiteSpace(c._crossroadsTitle.text)
                ? c._crossroadsTitle.text : Strings.ScreenCrossroads;
            b.PushContext(title, null, positions: false);
            b.BeginStop("paths");
            b.PushContext(Strings.CrossroadsPaths, Strings.RoleList);
            foreach (var gate in Gates(c))
            {
                var g = gate;
                b.AddItem(ControlId.Structural("crossroads:" + g.PathIndex), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => g._titleText != null ? g._titleText.text : null),
                        new NodeAnnouncement(() => g._descriptionText != null ? g._descriptionText.text : null, kind: AnnouncementKinds.Tooltip),
                    },
                    SearchText = () => g._titleText != null ? g._titleText.text : null,
                    OnActivate = () => Select(c, g),
                    OnTooltip = () => GameNodes.SayTooltip(g._descriptionText != null ? g._descriptionText.text : null),
                });
            }
            b.PopContext();
            b.PopContext();
        }

        // The gates the controller laid out for this crossroads (active ones only).
        private static List<GateIconView> Gates(CrossroadsController c)
        {
            var list = new List<GateIconView>();
            var gates = c._gateIconViews;
            if (gates == null) return list;
            foreach (var g in gates)
                if (g != null && g.gameObject.activeInHierarchy) list.Add(g);
            return list;
        }

        // The controller selects the gate under the mouse; hand it this gate as the hovered one first so
        // its selection feedback lands on the right gate, then run its selection.
        private static void Select(CrossroadsController c, GateIconView gate)
        {
            foreach (var g in Gates(c))
                if (g.IsActivated) return; // a path is already chosen; the game is moving on
            c._lastHoveredIconView = gate;
            c.SelectPath(gate.PathIndex);
        }
    }
}
