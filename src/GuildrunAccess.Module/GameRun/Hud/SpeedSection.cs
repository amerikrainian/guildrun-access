using Ember.Scopes.GameRun.UI.BattleSpeed;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "battle speed" stop: auto and the fixed speeds as one row of radio buttons.</summary>
    internal sealed class SpeedSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var speed = GameScopes.Controller<BattleSpeedController>();
            if (speed == null || !speed.gameObject.activeInHierarchy) return;
            b.BeginStop("speed");
            b.PushContext(Strings.RunSpeed, null, positions: false);
            b.StartRow();
            if (speed._autoView != null && GameNodes.IsShown(speed._autoView.Toggle))
                b.AddItem(ControlId.Structural("run:speed:auto"), GameNodes.Radio(speed._autoView.Toggle, () => Strings.RunSpeedAuto));
            var views = speed._speedViews;
            if (views != null)
                for (int i = 0; i < views.Length; i++)
                {
                    var v = views[i];
                    if (v == null || !GameNodes.IsShown(v.Toggle)) continue;
                    int n = i + 1;
                    b.AddItem(ControlId.Structural("run:speed:" + n), GameNodes.Radio(v.Toggle, () => Strings.RunSpeedN(n)));
                }
            b.EndRow();
            b.PopContext();
        }
    }
}
