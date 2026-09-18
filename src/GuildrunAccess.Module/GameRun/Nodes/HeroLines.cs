using System;
using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core.Buffers;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The hero and item buffers' lines for a control that concerns a hero: a card (the shop, the
    /// picker, the Heroes panel, the sidebar's inspect card), a party or reserve slot, a board cell,
    /// a unit in a fight. The hero buffer is the whole hero: name and class, stats, the abilities line,
    /// then every tooltip the card carries, so nothing about the hero has to be looked for in the
    /// control buffer; the item buffer is one line per item worn, each its tooltip. Every line is
    /// read live.
    /// </summary>
    internal static class HeroLines
    {
        /// <summary>A side-lines provider: the hero lines under <see cref="BufferKeys.Hero"/>, the item
        /// lines under <see cref="BufferKeys.Item"/>, nothing for any other buffer.</summary>
        public static Func<string, IEnumerable<string>> Side(Func<IEnumerable<string>> hero, Func<IEnumerable<string>> items)
            => Side(hero, items, null);

        /// <summary>The same with the hero's quests under <see cref="BufferKeys.Quest"/>.</summary>
        public static Func<string, IEnumerable<string>> Side(Func<IEnumerable<string>> hero, Func<IEnumerable<string>> items, Func<IEnumerable<string>> quests)
        {
            return key =>
            {
                if (key == BufferKeys.Hero) return hero != null ? hero() : null;
                if (key == BufferKeys.Item) return items != null ? items() : null;
                if (key == BufferKeys.Quest) return quests != null ? quests() : null;
                return null;
            };
        }

        /// <summary>A hero whose items stand in slots (a card, a party slot, a health bar): the item
        /// buffer is their tooltips and the quest buffer their quests, the Rift Seal's charges
        /// among them.</summary>
        public static Func<string, IEnumerable<string>> SideOfSlots(Func<IEnumerable<string>> hero, Func<IEnumerable<PlaceholderSlotView>> slots)
        {
            var side = Side(hero, () => ItemNodes.ItemTooltips(slots()), () => ItemNodes.QuestLines(slots()));
            return key => key == BufferKeys.QuestBrief ? ItemNodes.QuestLines(slots(), rewards: false) : side(key);
        }

        /// <summary>A hero card, the rows of the hero buffer everywhere: "Karsu, Duelist, Frost", its
        /// stats line, its abilities line ("Killshot, Active Ability; ..."), then the tooltips behind
        /// them: every ability's, every class and archetype tag's (what "Shard" or "Assassin" means),
        /// every stat's.</summary>
        public static IEnumerable<string> ForCard(HeroCardView card)
        {
            if (card == null) yield break;
            yield return HeroCardNodes.NameAndClass(card);
            yield return HeroCardNodes.StatsLine(card);
            yield return HeroCardNodes.AbilitiesLine(card);
            foreach (var line in HeroCardNodes.Tooltips(card)) yield return line;
        }

        /// <summary>A party or reserve slot's hero in the same rows: its name, its vitals when it stands
        /// on the board ("health 675, mana 40 of 75"; the slot shows no other stats), its abilities line.</summary>
        public static IEnumerable<string> ForSlot(BottomHeroView view, string vitals = null)
        {
            if (view == null || view.IsEmpty) yield break;
            yield return RunData.HeroLabel(view);
            if (!string.IsNullOrEmpty(vitals)) yield return vitals;
            yield return HeroCardNodes.AbilitiesLine(view._abilitiesView);
        }
    }
}
