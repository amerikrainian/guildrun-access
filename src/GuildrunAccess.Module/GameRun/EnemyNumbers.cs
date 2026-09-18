using System;
using System.Collections.Generic;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.GameRun.GameRegistry.Data.Characters;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using UnityEngine;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// "Slime 1", "Slime 2": the number that tells apart the enemies of one board that share a name.
    /// They are counted in the grid's reading order (the row farthest from the player first, left to
    /// right: the order the board stop lists its cells and Left and Right cycle the units in), so
    /// Slime 1 stands left of Slime 2 or behind it; an enemy whose name is its alone has no number.
    /// <para>The count is read off the board data's tiles, never kept: the board service fills the
    /// enemy tiles once per battle (<c>BoardService.Init</c>) and nothing in a fight writes them
    /// again (the simulation moves its own entities), so the placement cells answer all through the
    /// fight, for the fallen too, and a number means the same enemy from placement to the result.
    /// An enemy on no tile (one the fight itself brought in) has no number.</para>
    /// <para>One read scans the enemy rows (about a millisecond): a caller naming several units
    /// reads once and passes the numbers along.</para>
    /// </summary>
    internal static class EnemyNumbers
    {
        /// <summary>The board's numbers by enemy id (<see cref="Key"/>); empty off a board.</summary>
        public static Dictionary<string, int> Read()
        {
            var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                var board = RunData.Board();
                if (board == null) return numbers;
                int w = board.BoardWidth, h = board.BoardHeight;
                // Only enemies stand on the enemies' rows, which are the grid's top ones.
                var found = new List<KeyValuePair<string, string>>();
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int y = h - 1; y >= 0 && !RunData.IsPlayerCell(new Vector2Int(0, y)); y--)
                    for (int x = 0; x < w; x++)
                    {
                        if (!RunData.TryEnemyAt(new Vector2Int(x, y), out var id)) continue;
                        string name = RunData.EnemyName(id);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        found.Add(new KeyValuePair<string, string>(Key(id), name));
                        counts[name] = counts.TryGetValue(name, out int n) ? n + 1 : 1;
                    }
                var next = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var enemy in found)
                {
                    if (counts[enemy.Value] < 2) continue;
                    int number = next.TryGetValue(enemy.Value, out int last) ? last + 1 : 1;
                    next[enemy.Value] = number;
                    numbers[enemy.Key] = number;
                }
            }
            catch (Exception e) { CoreLog.Warning("EnemyNumbers: board unreadable: " + e.Message); }
            return numbers;
        }

        /// <summary>An enemy id as the numbers are keyed. A character view's entity id is the same
        /// Guid (<c>EnemyId.ToEntityId</c>), so a fighting unit finds its number by either.</summary>
        public static string Key(EnemyId id) => id.Guid.ToString();

        /// <summary>The enemy's name with its number when it shares the name ("Slime 2"), else the
        /// plain name; null when nothing names it.</summary>
        public static string Label(EnemyId id, Dictionary<string, int> numbers = null)
            => Numbered(RunData.EnemyName(id), id, numbers);

        /// <summary><paramref name="name"/> (the text a bar or a card shows for the enemy) with the
        /// enemy's number, when it has one.</summary>
        public static string Numbered(string name, EnemyId id, Dictionary<string, int> numbers = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            if (numbers == null) numbers = Read();
            return numbers.TryGetValue(Key(id), out int number) ? Strings.EnemyNumbered(name, number) : name;
        }

        /// <summary>The same for a board unit: a hero's name comes back as it is.</summary>
        public static string Numbered(string name, CharacterViewController unit, Dictionary<string, int> numbers = null)
        {
            if (unit == null || string.IsNullOrWhiteSpace(name)) return name;
            return Nullables.TryGet(() => unit.EnemyId, out EnemyId id) ? Numbered(name, id, numbers) : name;
        }
    }
}
