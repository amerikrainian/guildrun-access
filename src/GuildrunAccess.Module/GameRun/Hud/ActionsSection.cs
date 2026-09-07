using Ember.Scopes.Battle.UI.BattleFlow;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using UnityEngine.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The placement phase's action buttons (Fight): one stop, absent during a fight.</summary>
    internal sealed class ActionsSection : ScreenSection
    {
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
