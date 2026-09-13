using System;
using System.Text.RegularExpressions;

namespace GuildrunAccess.Core
{
    /// <summary>
    /// The speech seam for Core: the module points <see cref="Speak"/> at the host pipeline in Load;
    /// tests capture it. Convention carried from the reference mods: interrupt for focus MOVES (so a
    /// held key-repeat reads the item you land on), queue for screen entry and feedback lines.
    /// Game text arrives as TMP rich text; the pipeline strips tags, but inline icon sprites carry
    /// meaning ("150 (&lt;sprite name=Attack&gt;)" scales with Attack), so those are turned into their
    /// names here first.
    /// </summary>
    public static class Speech
    {
        public static Action<string, bool> Speak = (text, interrupt) => { };

        public static void Say(string text, bool interrupt = false)
        {
            if (!string.IsNullOrEmpty(text)) Speak(SpriteNames(text), interrupt);
        }

        private static readonly Regex NamedSprite = new Regex("<sprite\\s+name=\"?([^\"\\s>]+)\"?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex IndexedSprite = new Regex("<sprite[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EmptyParens = new Regex("\\(\\s*\\)", RegexOptions.Compiled);

        private static readonly Regex SizeSuffix = new Regex("_(S|M|L|XL|Small|Medium|Large)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // A lower-case letter or digit followed by an upper-case one: the seam of a camel-cased name.
        private static readonly Regex CamelSeam = new Regex("(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

        /// <summary>Named icon sprites become their name ("&lt;sprite name=Crit&gt;" -> "Crit",
        /// "&lt;sprite name=Shard_S&gt;" -> "Shard", "&lt;sprite name=ManaRegen&gt;" -> "Mana Regen");
        /// unnamed ones vanish; parentheses left empty by that are dropped.</summary>
        public static string SpriteNames(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) < 0) return text;
            text = NamedSprite.Replace(text, m => " " + SpriteName(m.Groups[1].Value) + " ");
            text = IndexedSprite.Replace(text, " ");
            return EmptyParens.Replace(text, "");
        }

        /// <summary>The spoken name of one sprite: its size suffix dropped, underscores and camel-case
        /// seams as spaces ("Shard_S" -> "Shard", "AttackSpeed" -> "Attack Speed").</summary>
        public static string SpriteName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return "";
            string name = SizeSuffix.Replace(spriteName, "").Replace('_', ' ');
            return CamelSeam.Replace(name, " ");
        }
    }
}
