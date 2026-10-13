using System.Collections.Generic;
using Ember.Scopes.Battle.UI.BattleFlow;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using UnityEngine.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The placement phase's action buttons (Fight): one stop, absent during a fight. Alt+F
    /// presses Fight from anywhere on the screen while it can be pressed.</summary>
    internal sealed class ActionsSection : ScreenSection
    {
        public const string Fight = "run.fight";

        public override IEnumerable<ElementAction> GetActions()
        {
            var board = RunData.BoardController;
            var button = board != null ? board._startBattleButton : null;
            if (!GameNodes.IsShown(button) || !button.interactable) yield break;
            // Labelled by the button's own caption ("Fight").
            yield return new ElementAction(Fight, GameNodes.LabelOf(button), _ => button.onClick.Invoke());
        }

        public override void Build(GraphBuilder b)
        {
            var flow = GameScopes.Controller<BattleFlowUIStateController>();
            var placement = flow != null ? flow._placementParent : null;
            if (placement == null || !placement.activeInHierarchy) return;
            b.BeginStop("actions");
            foreach (var button in placement.GetComponentsInChildren<Button>(false))
            {
                if (!GameNodes.IsShown(button) || !button.interactable) continue;
                var btn = button;
                b.AddItem(ControlId.Structural("run:action:" + button.gameObject.name + button.GetInstanceID()),
                    GameNodes.Button(btn));
            }
        }
    }
}
