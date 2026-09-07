using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core.Strings;
using UnityEngine;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The run HUD's composed labels, so a hero reads the same wherever it appears: on a board cell,
    /// in a party or reserve slot, on the battlefield, in the equip-target list.
    /// </summary>
    internal static class RunLabels
    {
        /// <summary>"Pimenta, wearing Freezing Tome"; the bare name when the slots hold nothing.</summary>
        public static string WithItems(string name, IEnumerable<PlaceholderSlotView> itemSlots)
        {
            string items = ItemNodes.ItemNames(itemSlots);
            return items == null ? name : name + ", " + Strings.RunWearing(items);
        }

        /// <summary>"wearing Freezing Tome", or null when the slots hold nothing (a list entry's detail).</summary>
        public static string Wearing(IEnumerable<PlaceholderSlotView> itemSlots)
        {
            string items = ItemNodes.ItemNames(itemSlots);
            return items == null ? null : Strings.RunWearing(items);
        }

        /// <summary>A slot's hero: "Irini: Limitless, Passive Ability, wearing Freezing Tome".</summary>
        public static string SlotSummary(BottomHeroView view)
        {
            string abilities = HeroCardNodes.AbilitiesLine(view._abilitiesView);
            string items = ItemNodes.ItemNames(view._itemSlotViews);
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(abilities)) parts.Add(abilities);
            if (items != null) parts.Add(Strings.RunWearing(items));
            string body = string.Join(", ", parts);
            string name = RunData.HeroName(view);
            return string.IsNullOrEmpty(name) ? body : body.Length == 0 ? name : name + ": " + body;
        }

        /// <summary>The tooltips of a slot's hero: its abilities', then its items'; null when neither has one.</summary>
        public static string SlotTooltips(BottomHeroView view)
        {
            string abilities = HeroCardNodes.AbilitiesTooltips(view._abilitiesView);
            string items = ItemNodes.ItemTooltips(view._itemSlotViews);
            if (abilities == null) return items;
            return items == null ? abilities : abilities + ". " + items;
        }

        /// <summary>The game's own grid, one-based: columns left to right, rows from the player's back
        /// line up through the enemy side (a hero on row 4 and an enemy on row 6 are two cells apart).</summary>
        public static string CellName(Vector2Int cell) => Strings.RunCellPos(cell.x + 1, cell.y + 1);
    }
}
