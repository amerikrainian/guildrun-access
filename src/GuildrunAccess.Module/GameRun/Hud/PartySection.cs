using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "party" stop: the active slots, then the reserve slots, each reading its hero's
    /// name, abilities and items; Enter opens the hero's menu.</summary>
    internal sealed class PartySection : ScreenSection
    {
        private readonly HeroActions _actions;

        public PartySection(HeroActions actions) { _actions = actions; }

        public override void Build(GraphBuilder b)
        {
            var party = RunData.Party;
            if (party == null) return;
            b.BeginStop("party");
            AddHeroPanel(b, party._activeHeroPanel, Strings.RunParty, "party", false);
            AddHeroPanel(b, party._reserveHeroPanel, Strings.RunReserve, "reserve", true);
        }

        private void AddHeroPanel(GraphBuilder b, BottomHeroPanelView panel, string label, string key, bool reserve)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            var views = panel.HeroViews;
            if (views == null) return;
            b.PushContext(label, Strings.RoleList);
            int n = 0;
            foreach (var v in views)
            {
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                n++;
                int index = n;
                var view = v;
                b.AddItem(ControlId.Structural("run:" + key + ":" + view.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => reserve ? Strings.RunReserveSlot(index) : Strings.RunPartySlot(index), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => view.IsEmpty ? Strings.RunSlotEmpty : RunLabels.SlotSummary(view), live: true, kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => RunLabels.SlotSummary(view),
                    OnActivate = () => _actions.OpenHeroMenu(view, reserve),
                    Details = () => RunLabels.SlotTooltips(view),
                    SideLines = HeroLines.Side(() => HeroLines.ForSlot(view), () => ItemNodes.ItemTooltips(view._itemSlotViews)),
                });
            }
            b.PopContext();
        }
    }
}
