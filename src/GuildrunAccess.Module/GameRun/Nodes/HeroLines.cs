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
    /// a unit in a fight. The hero buffer is the hero at a glance (name and class, stats, one line per
    /// ability); the item buffer is one line per item worn, each its tooltip. Every line is read live.
    /// </summary>
    internal static class HeroLines
    {
        /// <summary>A side-lines provider: the hero lines under <see cref="BufferKeys.Hero"/>, the item
        /// lines under <see cref="BufferKeys.Item"/>, nothing for any other buffer.</summary>
        public static Func<string, IEnumerable<string>> Side(Func<IEnumerable<string>> hero, Func<IEnumerable<string>> items)
        {
            return key =>
            {
                if (key == BufferKeys.Hero) return hero != null ? hero() : null;
                if (key == BufferKeys.Item) return items != null ? items() : null;
                return null;
            };
        }

        /// <summary>A hero card: "Karsu, Duelist, Frost", its stats line, then one line per ability tooltip.</summary>
        public static IEnumerable<string> ForCard(HeroCardView card)
        {
            if (card == null) yield break;
            yield return HeroCardNodes.NameAndClass(card);
            yield return HeroCardNodes.StatsLine(card);
            foreach (var line in HeroCardNodes.AbilitiesTooltips(card)) yield return line;
        }

        /// <summary>A party or reserve slot's hero: its name, then one line per ability tooltip (the
        /// slot shows no stats).</summary>
        public static IEnumerable<string> ForSlot(BottomHeroView view)
        {
            if (view == null || view.IsEmpty) yield break;
            yield return RunData.HeroName(view);
            foreach (var line in HeroCardNodes.AbilitiesTooltips(view._abilitiesView)) yield return line;
        }
    }
}
