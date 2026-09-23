using System.Collections.Generic;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// A panel the game shows over the run between fights (the shop, the crossroads, an event): its own
    /// stops first, then the run HUD's sections that stay on screen and interactable under it: the party
    /// and reserve slots, the inventory (the item reserve and the relics), the shards and the rewind, the
    /// act map, the sidebar and the menu. So a player in the shop can equip what they bought, inspect a hero, read what the
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
            Add(new InventorySection(Actions));
            Add(new InfoSection());
            Add(new MapSection());
            Add(new SidebarSection());
            Add(new MenuSection());
        }

        /// <summary>What Escape does on the panel itself (its Proceed, when it has one).</summary>
        protected abstract void PanelBack();

        /// <summary>What Escape does on this panel, for the key help: the caption of the button it
        /// presses (the shop's and an event's Proceed), the pause menu where it opens that.</summary>
        protected virtual string PanelBackLabel => Strings.Get("bind.ui.back");

        /// <summary>The panel's own main stop, where Alt+B lands here in place of the board it covers
        /// (the shop's offers, an event's story and choices, the crossroads' paths); null for none.</summary>
        protected virtual object PanelStop => null;

        /// <summary>What <see cref="PanelStop"/> is called in the key help ("Jump to offers").</summary>
        protected virtual string PanelStopLabel => null;

        /// <summary>Alt+B on this panel: the landing on <see cref="PanelStop"/>, or null while it is
        /// not up. A panel with a better landing than the stop's own (an event's first choice)
        /// overrides this.</summary>
        protected virtual ElementAction PanelJump => JumpKeys.Stop(JumpKeys.Board, PanelStop, PanelStopLabel);

        protected override IEnumerable<ElementAction> OwnActions()
        {
            yield return new ElementAction(ActionIds.Back, Actions.Moves.Pending ? Strings.HelpCancelMove : PanelBackLabel, _ =>
            {
                if (Actions.Moves.Pending) { Actions.Moves.Cancel(); return; }
                PanelBack();
            });
            // Alt+B the panel's own stop, Alt+T the party, Alt+I the items and relics: each where up.
            var panel = PanelJump;
            if (panel != null) yield return panel;
            foreach (var jump in JumpKeys.Stops((JumpKeys.Party, "party", Strings.RunParty), (JumpKeys.Items, "inventory", Strings.RunInventory)))
                yield return jump;
        }
    }
}
