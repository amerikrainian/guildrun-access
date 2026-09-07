using Ember.Scopes.Battle.UI.Sidebar;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Module.Interop;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The information sidebar: its switches as the "sidebar" stop and the shown panel (an
    /// inspected card, the damage tracker, the challenge) as the next (see <see cref="SidebarNodes"/>).</summary>
    internal sealed class SidebarSection : ScreenSection
    {
        /// <summary>The node id prefix of the sidebar's nodes (the inspect landing targets the card under it).</summary>
        public const string KeyPrefix = "run:sidebar";

        public override void Build(GraphBuilder b)
        {
            var sidebar = GameScopes.Controller<InformationSidebarController>();
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy) return;
            b.BeginStop("sidebar");
            SidebarNodes.Add(b, sidebar, KeyPrefix);
        }
    }
}
