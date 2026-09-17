using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Relics;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.Equipment;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "inventory" stop: the reserve column's items and the run's relics side by side, each
    /// container a column of its own (Up and Down within it, Right from the items to the relics and Left
    /// back, Alt+arrows too), and only while it holds something: an empty container is not there, and
    /// with both empty neither is the stop. Enter on an item picks the hero to equip it to (and offers
    /// Sell while the shop is up); a relic reads its tooltip.</summary>
    internal sealed class InventorySection : ScreenSection
    {
        private readonly HeroActions _actions;

        public InventorySection(HeroActions actions) { _actions = actions; }

        public override void Build(GraphBuilder b)
        {
            b.BeginStop("inventory");
            AddItems(b);
            AddRelics(b);
            b.SetRegion(null);
        }

        private void AddItems(GraphBuilder b)
        {
            var reserve = GameScopes.Controller<ItemReserveUIController>();
            if (reserve == null || !reserve.gameObject.activeInHierarchy) return;
            var slots = new List<PlaceholderSlotView>();
            foreach (var slot in reserve.GetComponentsInChildren<PlaceholderSlotView>(false))
                if (ItemNodes.HasItem(slot)) slots.Add(slot);
            if (slots.Count == 0) return;
            b.SetRegion("run:items");
            b.PushContext(Strings.RunItems, Strings.RoleList);
            b.StartColumn();
            foreach (var slot in slots)
            {
                var s = slot;
                b.AddItem(ControlId.Structural("run:item:" + slot.GetInstanceID()), ItemNodes.Slot(slot, () => _actions.OpenEquipMenu(s)));
            }
            b.EndColumn();
            b.PopContext();
        }

        private static void AddRelics(GraphBuilder b)
        {
            var relics = GameScopes.Controller<RelicUIController>();
            if (relics == null || !relics.gameObject.activeInHierarchy) return;
            var views = new List<RelicView>();
            foreach (var relic in relics.GetComponentsInChildren<RelicView>(false))
                if (relic != null && relic.gameObject.activeInHierarchy) views.Add(relic);
            if (views.Count == 0) return;
            b.SetRegion("run:relics");
            b.PushContext(Strings.RunRelics, Strings.RoleList);
            b.StartColumn();
            foreach (var relic in views)
                b.AddItem(ControlId.Structural("run:relic:" + relic.GetInstanceID()), ItemNodes.Relic(relic));
            b.EndColumn();
            b.PopContext();
        }
    }
}
