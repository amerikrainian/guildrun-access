using System.Collections.Generic;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// A panel the game shows over the run between fights (the shop, the crossroads, an event): its own
    /// stops first, then the run HUD's sections that stay on screen and interactable under it: the party
    /// and reserve slots, the item reserve, relics, the shards and the rewind, the act map, the sidebar
    /// and the menu. So a player in the shop can equip what they bought, inspect a hero, read what the
    /// map holds next, or sell, as the mouse can; the board, Fight, battle speed and events stay with
    /// the battle. Escape cancels a pending move, else does what the panel's Escape does.
    /// </summary>
    internal abstract class RunPanelScreen : CompositeScreen
    {
        protected readonly HeroActions Actions;

        protected RunPanelScreen()
        {
            // The panel's stops and the HUD sections under them: Tab past the last comes round to the
            // first, as on the run HUD.
            Wrap = true;
            Actions = Add(new HeroActions()); // no nodes; its landing and pop cleanup ride the lifecycle
        }

        public override int Layer => 10;
        public override bool Exclusive => true;

        /// <summary>The HUD sections shared with the run screen, after the panel's own.</summary>
        protected void AddHud()
        {
            Add(new PartySection(Actions));
            Add(new ItemsSection(Actions));
            Add(new RelicsSection());
            Add(new InfoSection());
            Add(new MapSection());
            Add(new SidebarSection());
            Add(new MenuSection());
        }

        /// <summary>What Escape does on the panel itself (its Proceed, when it has one).</summary>
        protected abstract void PanelBack();

        protected override IEnumerable<ElementAction> OwnActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                if (Actions.Moves.Pending) { Actions.Moves.Cancel(); return; }
                PanelBack();
            });
        }
    }
}
