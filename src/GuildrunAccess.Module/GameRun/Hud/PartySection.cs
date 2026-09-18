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
        // The control buffer: the full stats line (the card the landing showed, else the bar vitals),
        // the abilities and items tooltips, then the card's tag and stat tooltips.
        private static IEnumerable<string> SlotDetails(BottomHeroView view)
        {
            var lines = new List<string>();
            var card = HeroActions.ShownHeroCard(RunData.HeroName(view));
            string stats = card != null ? HeroCardNodes.StatsLine(card) : BoardSection.VitalsOf(view);
            if (!string.IsNullOrEmpty(stats)) lines.Add(stats);
            lines.AddRange(RunLabels.SlotTooltips(view));
            if (card != null) { lines.AddRange(HeroCardNodes.TagsTooltips(card)); lines.AddRange(HeroCardNodes.StatsTooltips(card)); }
            return lines;
        }

        // The hero buffer: the card rows when shown, else name, vitals, abilities.
        private static IEnumerable<string> SlotRows(BottomHeroView view)
        {
            var card = HeroActions.ShownHeroCard(RunData.HeroName(view));
            return card != null ? HeroLines.ForCard(card) : HeroLines.ForSlot(view, BoardSection.VitalsOf(view));
        }

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
                    // Landing shows the hero's card in the sidebar, as hovering the slot does; the line
                    // and the buffers read it.
                    OnFocus = () => { if (RunData.TryHeroId(view, out var id)) HeroActions.PeekHero(id); },
                    Details = () => SlotDetails(view),
                    Subject = () => view,
                    SideLines = HeroLines.Side(() => SlotRows(view), () => ItemNodes.ItemTooltips(view._itemSlotViews)),
                });
            }
            b.PopContext();
        }
    }
}
