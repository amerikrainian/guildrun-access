using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.Equipment;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The "items" stop: the reserve column's items; Enter picks the hero to equip one to.</summary>
    internal sealed class ItemsSection : ScreenSection
    {
        private readonly HeroActions _actions;

        public ItemsSection(HeroActions actions) { _actions = actions; }

        public override void Build(GraphBuilder b)
        {
            var reserve = GameScopes.Controller<ItemReserveUIController>();
            if (reserve == null || !reserve.gameObject.activeInHierarchy) return;
            b.BeginStop("items");
            b.PushContext(Strings.RunItems, Strings.RoleList);
            int n = 0;
            foreach (var slot in reserve.GetComponentsInChildren<PlaceholderSlotView>(false))
            {
                if (!ItemNodes.HasItem(slot)) continue;
                n++;
                var s = slot;
                b.AddItem(ControlId.Structural("run:item:" + slot.GetInstanceID()), ItemNodes.Slot(slot, () => _actions.OpenEquipMenu(s)));
            }
            if (n == 0) b.AddItem(ControlId.Structural("run:item:none"), GameNodes.Text(() => Strings.RunNoItems));
            b.PopContext();
        }
    }
}
