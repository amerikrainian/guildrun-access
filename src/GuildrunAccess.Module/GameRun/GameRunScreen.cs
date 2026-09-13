using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Navigation;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The run HUD: the base context while a run is loaded, between and during fights, composed of one
    /// section per Tab-stop in order: actions (Fight), the board (the placement grid, or the battlefield
    /// during a fight), the party (active and reserve slots), the item reserve, relics, info, the act
    /// map, the sidebar, battle events, battle speed, and the menu. Everything is read live from the
    /// game's own views; Space reads a control's tooltip through the game's tooltip pipeline. What Enter
    /// does to heroes and items lives in <see cref="HeroActions"/>, shared by the sections that need it.
    /// </summary>
    public sealed class GameRunScreen : CompositeScreen
    {
        private readonly HeroActions _actions;

        public GameRunScreen()
        {
            // The HUD has many stops and no natural end: Tab past the last one comes round to the first.
            Wrap = true;
            _actions = Add(new HeroActions()); // no nodes; its landing and pop cleanup ride the lifecycle
            Add(new ActionsSection());
            Add(new BoardSection(_actions));
            Add(new PartySection(_actions));
            Add(new ItemsSection(_actions));
            Add(new RelicsSection());
            Add(new InfoSection());
            Add(new MapSection());
            Add(new SidebarSection());
            Add(new EventsSection());
            Add(new SpeedSection());
            Add(new MenuSection());
        }

        public override string Key => "gamerun";
        public override int Layer => 0;
        public override bool AllowsTypeahead => true;
        protected override string ContextLabel => Strings.ScreenRun;

        // The HUD is the player's place only while the board is editable (placement) or a fight is on
        // (units with health bars). In the flow's other states (a result fading in, the shop, the
        // crossroads, an event, the run's start and end) it is covered or in transition, and being
        // the top screen for those frames only announces a landing nobody asked for ("battlefield,
        // no units on the board" at every battle end and between every two panels). Inactive then,
        // the screen pops and comes back fresh on the next placement, landing on Fight.
        public override bool IsActive()
        {
            var party = RunData.Party;
            if (party == null || !party.gameObject.activeInHierarchy) return false;
            return RunData.Placing() || BoardSection.HasUnits();
        }

        protected override IEnumerable<ElementAction> OwnActions()
        {
            // Escape: cancel a pending move; otherwise the game's own Escape (its settings panel).
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                if (_actions.Moves.Pending) { _actions.Moves.Cancel(); return; }
                var nav = GameScopes.Controller<NavigationUIController>();
                if (nav != null && GameNodes.IsShown(nav._settingsButton) && nav._settingsButton.interactable)
                    nav._settingsButton.onClick.Invoke();
            });
        }
    }
}
