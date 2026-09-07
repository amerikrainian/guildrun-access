using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.ChunkUI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "map" stop: the act's stages in order, the current one marked; Space reads a
    /// stage's tooltip.</summary>
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
                b.AddItem(ControlId.Structural("run:map:" + node.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => NodeTitle(node), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => IsCurrent(node) ? Strings.RunMapCurrent : null, live: true, kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => NodeTitle(node),
                    OnTooltip = () => GameNodes.SayTooltip(TooltipReader.Describe(node.TooltipRaycastTarget)),
                });
            }
            b.PopContext();
        }

        private static List<ActNodeView> MapNodes(ChunkUIController chunk)
        {
            var list = new List<ActNodeView>();
            var parent = chunk._nodeParent;
            if (parent == null) return list;
            foreach (var node in parent.GetComponentsInChildren<ActNodeView>(false))
                if (node != null && node.gameObject.activeInHierarchy) list.Add(node);
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
