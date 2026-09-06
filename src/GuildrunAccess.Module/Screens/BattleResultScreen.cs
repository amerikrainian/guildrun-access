using System.Collections.Generic;
using Ember.Scopes.Battle.UI.BattleFlow;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The post-fight result panel (victory or defeat): its title, the total shards, every reward line,
    /// then the Proceed / Summary buttons. Active while the battle flow shows its result parent; sits
    /// above the run HUD and owns the keys. Entering it speaks the outcome as the context.
    /// </summary>
    public sealed class BattleResultScreen : Screen
    {
        public override string Key => "gamerun.result";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private readonly Finder<BattleFlowUIStateController> _flow = new Finder<BattleFlowUIStateController>();

        public override bool IsActive()
        {
            var flow = _flow.Get();
            var result = flow != null ? flow._resultParent : null;
            var panel = flow != null ? flow._battleResultPanelView : null;
            return result != null && result.activeInHierarchy && panel != null && panel.gameObject.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            var flow = _flow.Get();
            var panel = flow != null ? flow._battleResultPanelView : null;
            if (panel == null) return;

            string title = panel._panelTitleText != null && !string.IsNullOrWhiteSpace(panel._panelTitleText.text)
                ? panel._panelTitleText.text : Strings.ScreenBattleResult;
            b.PushContext(title, null, positions: false);

            // Rewards: every text under the reward parent, in order.
            b.BeginStop("rewards");
            var rewards = panel._rewardParent;
            int i = 0;
            if (panel._totalShardsText != null && panel._totalShardsText.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(panel._totalShardsText.text))
            {
                var shards = panel._totalShardsText;
                b.AddItem(ControlId.Structural("result:shards"), GameNodes.Text(() => Strings.RunShards + " " + shards.text));
            }
            if (rewards != null)
            {
                b.PushContext(Strings.RunRewards, Strings.RoleList);
                foreach (var tmp in rewards.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                    var t = tmp;
                    b.AddItem(ControlId.Structural("result:reward" + i++), GameNodes.Text(() => t.text));
                }
                b.PopContext();
            }

            b.BeginStop("actions");
            if (GameNodes.IsShown(panel._proceedButton) && panel._proceedButton.interactable)
                b.AddItem(ControlId.Structural("result:proceed"), GameNodes.Button(panel._proceedButton));
            if (GameNodes.IsShown(panel._summaryButton) && panel._summaryButton.interactable)
                b.AddItem(ControlId.Structural("result:summary"), GameNodes.Button(panel._summaryButton));
            if (GameNodes.IsShown(panel._surveyButton) && panel._surveyButton.interactable)
                b.AddItem(ControlId.Structural("result:survey"), GameNodes.Button(panel._surveyButton));

            b.PopContext();
        }

        public override object InitialFocusStop => "actions";

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var flow = _flow.Get();
                var panel = flow != null ? flow._battleResultPanelView : null;
                if (panel != null && GameNodes.IsShown(panel._proceedButton) && panel._proceedButton.interactable)
                    panel._proceedButton.onClick.Invoke();
            });
        }
    }
}
