using System;
using System.Collections.Generic;
using Ember.Balancing;
using Ember.Balancing.Sheets.Characters;
using Ember.Balancing.Sheets.Characters.Enemies;
using gg.leyline.balancing.Data;
using GuildrunAccess.Core;
using Il2CppInterop.Runtime.InteropTypes;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// A name for an enemy whose balancing entry carries none: 295 of the demo's 644 enemy entries have
    /// no name key (the scaled variants of a creature), and the game draws them nameless everywhere (a
    /// blank on the bar, the sidebar card, the result's portraits). The creature is named after a
    /// sibling entry that IS named, read localized at speak time: the same id family first (entry ids
    /// share a prefix per creature and act, "Enemy_1018xx" for the Forest Golems of act one), else any
    /// entry drawn with the same visual config (the same prefab and portraits), the name most of them
    /// carry ("Mushroom Tank" over the lone "Mushroom Knight"); the prefab's own name, split into
    /// words, when no entry names the creature at all. Which sibling stands for which entry is a fact of
    /// the balancing sheets, loaded once per process, so it is kept per entry for the module generation;
    /// the name itself is never kept.
    /// </summary>
    internal static class EnemyNames
    {
        private static readonly Dictionary<string, INamedBalancingEntry> _siblingByEntry = new Dictionary<string, INamedBalancingEntry>(StringComparer.Ordinal);
        private static Dictionary<string, INamedBalancingEntry> _byFamily;
        private static Dictionary<string, INamedBalancingEntry> _byConfig;

        /// <summary>The localized name of a named sibling of <paramref name="entry"/> (a character entry
        /// proxy), else its prefab's name as words; null when nothing names it.</summary>
        public static string Fallback(Il2CppObjectBase entry)
        {
            CharacterEntry character;
            string id;
            try
            {
                character = entry != null ? entry.TryCast<CharacterEntry>() : null;
                if (character == null) return null;
                id = character.Id.ToString();
            }
            catch (Exception e)
            {
                CoreLog.Warning("EnemyNames: entry unreadable: " + e.Message);
                return null;
            }
            var sibling = Sibling(character, id);
            if (sibling != null)
            {
                string name = RunData.LocalizedName(sibling);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            return PrefabName(character);
        }

        // The named entry standing for this one, found once per entry id.
        private static INamedBalancingEntry Sibling(CharacterEntry character, string id)
        {
            if (_siblingByEntry.TryGetValue(id, out var known)) return known;
            INamedBalancingEntry sibling = null;
            try
            {
                if (_byFamily == null) BuildIndex();
                string family = Family(id);
                if (family == null || !_byFamily.TryGetValue(family, out sibling))
                {
                    string config = ConfigName(character);
                    if (config == null || !_byConfig.TryGetValue(config, out sibling)) sibling = null;
                }
            }
            catch (Exception e)
            {
                CoreLog.Warning("EnemyNames: sibling lookup failed for " + id + ": " + e.Message);
                sibling = null;
            }
            _siblingByEntry[id] = sibling;
            return sibling;
        }

        // Every named enemy entry, grouped by id family and by visual config, the majority name's entry
        // standing for each group.
        private static void BuildIndex()
        {
            var families = new Dictionary<string, List<INamedBalancingEntry>>(StringComparer.Ordinal);
            var configs = new Dictionary<string, List<INamedBalancingEntry>>(StringComparer.Ordinal);
            _byFamily = new Dictionary<string, INamedBalancingEntry>(StringComparer.Ordinal);
            _byConfig = new Dictionary<string, INamedBalancingEntry>(StringComparer.Ordinal);
            var all = EmberBalancing.Instance.GetAll<IEnemyEntry>();
            var list = all != null ? all.TryCast<Il2CppSystem.Collections.Generic.List<IEnemyEntry>>() : null;
            if (list == null)
            {
                CoreLog.Warning("EnemyNames: the enemy entries are not a list; nameless enemies stay so");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                var character = entry != null ? entry.TryCast<CharacterEntry>() : null;
                var named = entry != null ? entry.TryCast<INamedBalancingEntry>() : null;
                if (character == null || named == null || string.IsNullOrWhiteSpace(EnglishName(named))) continue;
                string family = Family(character.Id.ToString());
                if (family != null) Group(families, family).Add(named);
                string config = ConfigName(character);
                if (config != null) Group(configs, config).Add(named);
            }
            _byFamily = Majorities(families);
            _byConfig = Majorities(configs);
            CoreLog.Info("EnemyNames: " + _byFamily.Count + " id families, " + _byConfig.Count + " visual configs named");
        }

        private static List<INamedBalancingEntry> Group(Dictionary<string, List<INamedBalancingEntry>> groups, string key)
        {
            if (!groups.TryGetValue(key, out var group)) groups[key] = group = new List<INamedBalancingEntry>();
            return group;
        }

        // Per group, the entry whose English name most of the group's entries share (the first of them).
        private static Dictionary<string, INamedBalancingEntry> Majorities(Dictionary<string, List<INamedBalancingEntry>> groups)
        {
            var result = new Dictionary<string, INamedBalancingEntry>(StringComparer.Ordinal);
            foreach (var kv in groups)
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                var first = new Dictionary<string, INamedBalancingEntry>(StringComparer.Ordinal);
                INamedBalancingEntry best = null;
                int bestCount = 0;
                foreach (var named in kv.Value)
                {
                    string english = EnglishName(named);
                    counts[english] = counts.TryGetValue(english, out int n) ? n + 1 : 1;
                    if (!first.ContainsKey(english)) first[english] = named;
                    if (counts[english] > bestCount) { bestCount = counts[english]; best = first[english]; }
                }
                if (best != null) result[kv.Key] = best;
            }
            return result;
        }

        private static string EnglishName(INamedBalancingEntry named)
        {
            var key = named.NameLocaKey;
            return key != null ? key.EnglishText : null;
        }

        // "Enemy_101807" -> "Enemy_1018": the id without its last two digits (the variant), when the id
        // ends in more than two digits.
        internal static string Family(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            int end = id.Length;
            int start = end;
            while (start > 0 && char.IsDigit(id[start - 1])) start--;
            return end - start > 2 ? id.Substring(0, end - 2) : null;
        }

        private static string ConfigName(CharacterEntry character)
        {
            var config = character.VisualConfig;
            return config != null ? config.name : null;
        }

        // The creature's prefab name as words ("MushroomTank" -> "Mushroom Tank"), when nothing else is left.
        private static string PrefabName(CharacterEntry character)
        {
            try
            {
                var config = character.VisualConfig;
                var prefab = config != null ? config.Prefab : null;
                string name = prefab != null ? prefab.name : null;
                return string.IsNullOrWhiteSpace(name) ? null : Speech.SpriteName(name);
            }
            catch (Exception e)
            {
                CoreLog.Warning("EnemyNames: prefab name unreadable: " + e.Message);
                return null;
            }
        }
    }
}
