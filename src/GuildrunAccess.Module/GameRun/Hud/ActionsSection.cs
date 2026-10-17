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

        // A press disables Fight at once while the placement panel stays up until later in the same
        // frame: a render in between drew the grid without Fight, landing focus on a grid cell, spoken
        // before the battlefield. For these frames after a press the pressed button stays in the
        // render as it was, so focus waits on it until the battlefield replaces the grid.
        private const int PressFrames = 30;
        private int _pressedFrame = int.MinValue / 2;
        private int _pressedId;

        private bool JustPressed(Button button)
            => button.GetInstanceID() == _pressedId && NavInput.Current.FrameCount - _pressedFrame <= PressFrames;

        private void Press(Button button)
        {
            if (!button.interactable) return;
            _pressedId = button.GetInstanceID();
            _pressedFrame = NavInput.Current.FrameCount;
            button.onClick.Invoke();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            var board = RunData.BoardController;
            var button = board != null ? board._startBattleButton : null;
            if (!GameNodes.IsShown(button) || !button.interactable) yield break;
            // Labelled by the button's own caption ("Fight").
            yield return new ElementAction(Fight, GameNodes.LabelOf(button), _ => Press(button));
        }

        public override void Build(GraphBuilder b)
        {
            var flow = GameScopes.Controller<BattleFlowUIStateController>();
            var placement = flow != null ? flow._placementParent : null;
            if (placement == null || !placement.activeInHierarchy) return;
            b.BeginStop("actions");
            foreach (var button in placement.GetComponentsInChildren<Button>(true))
            {
                bool pressed = JustPressed(button);
                if (!pressed && (!GameNodes.IsShown(button) || !button.interactable)) continue;
                var btn = button;
                var node = GameNodes.Button(() => GameNodes.LabelOf(btn), () => Press(btn), () => btn.interactable || JustPressed(btn));
                node.Details = () => GameNodes.Lines(GameNodes.HoverText(btn));
                b.AddItem(ControlId.Structural("run:action:" + button.gameObject.name + button.GetInstanceID()), node);
            }
        }
    }
}
