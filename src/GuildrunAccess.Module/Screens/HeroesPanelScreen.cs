using System.Collections.Generic;
using Ember.Scopes.Application.UI.Tooltips;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.Navigation;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The run's Heroes panel (<see cref="HeroPanelView"/>, opened from the HUD menu): the run's
    /// tracked counters (shards generated, rush, stall, rerolls; named through their tooltips), the
    /// heroes shown (the board team or the reserve, as the shared hero grid with stats, abilities and
    /// items), and the button that switches between the two. Escape closes the panel.
    /// </summary>
    public sealed class HeroesPanelScreen : Screen
    {
        public override string Key => "gamerun.heroes";
        public override int Layer => 15;
        public override bool Exclusive => true;

        private readonly Finder<NavigationUIController> _nav = new Finder<NavigationUIController>();
        private readonly Finder<Ember.Scopes.Battle.UI.BattleFlow.BattleFlowUIStateController> _flow = new Finder<Ember.Scopes.Battle.UI.BattleFlow.BattleFlowUIStateController>();

        // The HUD's own Heroes panel, not the summary form the result panel reuses.
        private HeroPanelView Panel()
        {
            var nav = _nav.Get();
            var panel = nav != null ? nav._heroPanelView : null;
            if (panel == null || !panel.gameObject.activeInHierarchy || panel._isSummary) return null;
            var flow = _flow.Get();
            var result = flow != null ? flow._resultParent : null;
            return result != null && result.activeInHierarchy ? null : panel;
        }

        public override bool IsActive() => Panel() != null;

        public override void Build(GraphBuilder b)
        {
            var panel = Panel();
            if (panel == null) return;

            b.PushContext(Title(panel), null, positions: false);

            // The run's tracked counters.
            b.BeginStop("counters");
            b.PushContext(Strings.HeroesCounters, null, positions: false);
            int n = 0;
            foreach (var target in panel.GetComponentsInChildren<TooltipRaycastTarget>(false))
            {
                if (target == null || target.GetComponentInParent<HeroCardView>() != null) continue;
                var count = target.GetComponentInChildren<TMP_Text>(false);
                if (count == null || string.IsNullOrWhiteSpace(count.text)) continue;
                var t = target;
                var c = count;
                b.AddItem(ControlId.Structural("heroes:counter:" + n++), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => TooltipReader.Title(t) ?? t.gameObject.name),
                        new NodeAnnouncement(() => c.text, live: true, kind: AnnouncementKinds.Value),
                    },
                    OnTooltip = () => Core.Speech.Say(TooltipReader.Describe(t) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            b.PopContext();

            // The heroes shown: the board team or the reserve.
            var cards = new List<HeroCardView>();
            foreach (var card in panel.GetComponentsInChildren<HeroCardView>(false))
                if (card != null && card.gameObject.activeInHierarchy) cards.Add(card);
            b.BeginStop("heroes");
            b.PushContext(panel._isReserveShown ? Strings.RunReserve : Strings.RunParty, null, positions: true);
            if (cards.Count == 0)
                b.AddItem(ControlId.Structural("heroes:none"), GameNodes.Text(() => Strings.HeroesNone));
            else
                HeroCardNodes.AddGrid(b, "heroes", cards, i => null, null,
                    HeroCardNodes.StatsRow, HeroCardNodes.AbilitiesRow, HeroCardNodes.ItemsRow);
            b.PopContext();

            b.BeginStop("actions");
            var toggle = panel._toggleReserveButton;
            if (GameNodes.IsShown(toggle))
            {
                var caption = panel._buttonText != null ? panel._buttonText.GetComponent<TMP_Text>() : null;
                b.AddItem(ControlId.Structural("heroes:toggle"), GameNodes.Button(toggle,
                    () => caption != null && !string.IsNullOrWhiteSpace(caption.text) ? caption.text : GameNodes.LabelOf(toggle)));
            }
            b.AddItem(ControlId.Structural("heroes:close"), GameNodes.Button(() => Strings.HeroesClose, Close));

            b.PopContext();
        }

        private static string Title(HeroPanelView panel)
        {
            var tmp = panel._titleText != null ? panel._titleText.GetComponent<TMP_Text>() : null;
            return tmp != null && !string.IsNullOrWhiteSpace(tmp.text) ? tmp.text : Strings.RunHeroPanel;
        }

        // The HUD's Heroes button toggles the panel; pressing it again closes it.
        private void Close()
        {
            var nav = _nav.Get();
            var button = nav != null ? nav._heroPanelButton : null;
            if (GameNodes.IsShown(button)) button.onClick.Invoke();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Close());
        }
    }
}
