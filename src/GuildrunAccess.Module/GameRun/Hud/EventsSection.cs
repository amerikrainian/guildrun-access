using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "battle events" stop: what the HUD showed during the fight, newest first
    /// (<see cref="BattleEvents"/>). Absent until a fight has shown something; cleared for the next
    /// fight when placement returns.</summary>
    internal sealed class EventsSection : ScreenSection
    {
        private bool _wasPlacing;

        public override void Build(GraphBuilder b)
        {
            var lines = BattleEvents.Lines;
            if (lines.Count == 0) return; // no stop until a fight has shown something
            b.BeginStop("events");
            b.PushContext(Strings.RunEvents, Strings.RoleList);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                b.AddItem(ControlId.Structural("run:event:" + line.Sequence), GameNodes.Text(() => line.Text));
            }
            b.PopContext();
        }

        public override void OnUpdate()
        {
            // The events belong to one fight: placement returning means the next one is being set up.
            bool placing = RunData.Placing();
            if (placing && !_wasPlacing)
            {
                if (BattleEvents.Lines.Count > 0) BattleEvents.Clear();
                Audio.CombatCues.Reset(); // the cues' edge-triggers and interval clocks too
            }
            _wasPlacing = placing;
        }
    }
}
