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

        /// <summary>A slot's hero: "Irini, health 650, mana 45 of 85, Limitless, Passive Ability, wearing
        /// Freezing Tome": name, then its vitals while it stands on the board, then its abilities and items,
        /// the shape every control standing for a hero has.</summary>
        public static string SlotSummary(BottomHeroView view)
        {
            var parts = new List<string>();
            string name = RunData.HeroName(view);
            if (!string.IsNullOrEmpty(name)) parts.Add(name);
            string vitals = BoardSection.VitalsOf(view);
            if (!string.IsNullOrEmpty(vitals)) parts.Add(vitals);
            var card = HeroActions.ShownHeroCard(name);
            string brief = card != null ? HeroCardNodes.StatsBrief(card, vitals: vitals == null) : null;
            if (!string.IsNullOrEmpty(brief)) parts.Add(brief);
            string abilities = HeroCardNodes.AbilitiesLine(view._abilitiesView);
            if (!string.IsNullOrEmpty(abilities)) parts.Add(abilities);
            string items = ItemNodes.ItemNames(view._itemSlotViews);
            if (items != null) parts.Add(Strings.RunWearing(items));
            return string.Join(", ", parts);
        }

        /// <summary>The tooltips of a slot's hero, one line each: its abilities', then its items'.</summary>
        public static List<string> SlotTooltips(BottomHeroView view)
        {
            var lines = HeroCardNodes.AbilitiesTooltips(view._abilitiesView);
            lines.AddRange(ItemNodes.ItemTooltips(view._itemSlotViews));
            return lines;
        }

        /// <summary>The game's own grid, one-based: columns left to right, rows from the player's back
        /// line up through the enemy side (a hero on row 4 and an enemy on row 6 are two cells apart).</summary>
        public static string CellName(Vector2Int cell) => Strings.RunCellPos(cell.x + 1, cell.y + 1);
    }
}
