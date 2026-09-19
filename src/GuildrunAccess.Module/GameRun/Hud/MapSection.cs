using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.ChunkUI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "map" stop: the act's stages in order, the current one marked; a stage's tooltip
    /// is its buffer line. The game draws one chunk of the run at a time (six floors, their stops in
    /// between, the chunk's boss last) and, while a later chunk is still to come, three dots and the
    /// final boss after it: the dots are a bare image, the floors it does not draw yet, and get a
    /// line of their own, or the final boss would read as the stage right after this act's boss.</summary>
    internal sealed class MapSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var chunk = GameScopes.Controller<ChunkUIController>();
            if (chunk == null || !chunk.gameObject.activeInHierarchy) return;
            var nodes = MapNodes(chunk);
            if (nodes.Count == 0) return;
            b.BeginStop("map");
            b.PushContext(MapTitle(chunk), Strings.RoleList);
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    b.AddItem(ControlId.Structural("run:map:more"), GameNodes.Text(() => Strings.RunMapMore));
                    continue;
                }
                b.AddItem(ControlId.Structural("run:map:" + node.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => NodeTitle(node), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => IsCurrent(node) ? Strings.RunMapCurrent : null, live: true, kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => NodeTitle(node),
                    Details = () => TooltipReader.Lines(node.TooltipRaycastTarget),
                });
            }
            b.PopContext();
        }

        // The strip in the order drawn: its stage nodes, and null where the game draws its dots. Empty
        // when there is no stage node (the dots alone are no map).
        private static List<ActNodeView> MapNodes(ChunkUIController chunk)
        {
            var list = new List<ActNodeView>();
            var parent = chunk._nodeParent;
            if (parent == null) return list;
            var dots = chunk._dotsNode;
            bool any = false;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var node = child.GetComponent<ActNodeView>();
                if (node != null) { list.Add(node); any = true; }
                else if (dots != null && child.gameObject.GetInstanceID() == dots.GetInstanceID()) list.Add(null);
            }
            if (!any) list.Clear();
            return list;
        }

        // "map" plus the act indicator's own text ("Act 1") when it shows one.
        private static string MapTitle(ChunkUIController chunk)
        {
            var indicator = chunk._actIndicatorView;
            var text = indicator != null ? indicator._actIndicatorText : null;
            string act = text != null && text.gameObject.activeInHierarchy ? text.text : null;
            return string.IsNullOrWhiteSpace(act) ? Strings.RunMap : Strings.RunMap + ", " + act.Trim();
        }

        private static string NodeTitle(ActNodeView node)
        {
            string title = node.Title;
            if (string.IsNullOrWhiteSpace(title)) title = TooltipReader.Title(node.TooltipRaycastTarget);
            return string.IsNullOrWhiteSpace(title) ? node.gameObject.name : title;
        }

        private static bool IsCurrent(ActNodeView node)
        {
            var marker = node.CurrentMarker;
            return marker != null && marker.gameObject.activeInHierarchy && marker.enabled;
        }
    }
}
