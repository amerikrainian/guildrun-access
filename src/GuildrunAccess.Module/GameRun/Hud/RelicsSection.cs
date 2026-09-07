using Ember.Scopes.GameRun.UI.Relics;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "relics" stop: every relic held, or a "no relics" line.</summary>
    internal sealed class RelicsSection : ScreenSection
    {
        public override void Build(GraphBuilder b)
        {
            var relics = GameScopes.Controller<RelicUIController>();
            if (relics == null || !relics.gameObject.activeInHierarchy) return;
            b.BeginStop("relics");
            b.PushContext(Strings.RunRelics, Strings.RoleList);
            int n = 0;
            foreach (var relic in relics.GetComponentsInChildren<RelicView>(false))
            {
                if (relic == null || !relic.gameObject.activeInHierarchy) continue;
                n++;
                b.AddItem(ControlId.Structural("run:relic:" + relic.GetInstanceID()), ItemNodes.Relic(relic));
            }
            if (n == 0) b.AddItem(ControlId.Structural("run:relic:none"), GameNodes.Text(() => Strings.RunNoRelics));
            b.PopContext();
        }
    }
}
