using System;
using System.Collections.Generic;
using System.Text;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.HeroCard.Elements;
using Ember.Scopes.GameRun.UI.Slots;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// Readers and node factories for the game's hero card (<see cref="HeroCardView"/>), which the
    /// game reuses wherever a hero is shown: the starting-hero picker, the shop, the heroes panel, the
    /// sidebar inspect card, the end screen. Every screen that shows cards builds the same grid through
    /// <see cref="AddGrid"/>: heroes across (left/right), detail rows down (up/down, column preserved),
    /// Enter acting on the column's hero, the cell's tooltips its buffer lines. Rows are composable:
    /// the standard name / stats / abilities rows plus whatever a screen adds (a relic, a price).
    /// </summary>
    internal static class HeroCardNodes
    {
        // ---- live readers over a card ----

        /// <summary>"Kai, Warrior, Shield, rank C": the name, the rendered class/archetype tags, and the
        /// rank the card's badge shows.</summary>
        public static string NameAndClass(HeroCardView card)
        {
            if (card == null) return null;
            var sb = new StringBuilder();
            if (card._nameText != null) sb.Append(card._nameText.text);
            foreach (var tag in card.GetComponentsInChildren<HeroTagView>(false))
            {
                string name = TagName(tag);
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(name);
            }
            string rank = Rank(card);
            if (rank != null)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Strings.HeroRank(rank));
            }
            return sb.ToString();
        }

        private const string RankSpritePrefix = "Rank_";

        /// <summary>The rank letter the card's badge shows ("C", from its Rank_C sprite), or null when
        /// the card shows none.</summary>
        public static string Rank(HeroCardView card)
        {
            var image = card != null ? card._rankImage : null;
            if (image == null || !image.gameObject.activeInHierarchy || image.sprite == null) return null;
            string name = image.sprite.name;
            return name != null && name.StartsWith(RankSpritePrefix, StringComparison.Ordinal) ? name.Substring(RankSpritePrefix.Length) : null;
        }

        // A class tag carries its localized caption; an archetype tag (under the card's HeroArchetypes
        // holder) is icon-only with a placeholder caption ("assassindadsadsad", the hover's too), so
        // it is named by the title the game gives its tooltip ("Shard", "Crit", "Omnivamp": the
        // archetype's localized name). Its icon sprite is named for the art, not the archetype (the
        // Shard archetype's is "Economy"), and is only the fallback.
        private static string TagName(HeroTagView tag)
        {
            if (tag == null) return null;
            var parent = tag.transform.parent;
            bool archetype = parent != null && parent.name == "HeroArchetypes";
            if (archetype)
            {
                string title = TooltipReader.Title(tag._tooltipObject);
                if (title != null) return title;
                var sprite = tag._icon != null ? tag._icon.sprite : null;
                return sprite != null ? sprite.name.Replace("_NoFrame", "").Replace('_', ' ') : null;
            }
            return tag._name != null ? tag._name.text : null;
        }

        /// <summary>The class and archetype tags' tooltips as buffer lines, in the card's order: what a
        /// class plays like ("Assassins use Crit to deal bursts of damage...", its mechanics and their
        /// keyword definitions) and what an archetype stands for ("Shards: The currency used to
        /// purchase upgrades from the shop."). The game fills them on the tag's own tooltip object.</summary>
        public static List<string> TagsTooltips(HeroCardView card)
        {
            var lines = new List<string>();
            if (card == null) return lines;
            foreach (var tag in card.GetComponentsInChildren<HeroTagView>(false))
                if (tag != null) lines.AddRange(TooltipReader.Lines(tag._tooltipObject));
            return lines;
        }

        /// <summary>Every tooltip of the card as buffer lines: its abilities', its tags', its stats'.
        /// A keyword's definition is given once: an ability that makes Shards, the Assassin class
        /// and the Shard archetype each define "Shards", and the later ones are folded. The stats'
        /// lines are left whole: two stats may well share a "Value: 0 (Base: 0 + Bonus: 0)".</summary>
        public static List<string> Tooltips(HeroCardView card)
        {
            var lines = new List<string>();
            if (card == null) return lines;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in AbilitiesTooltips(card))
                if (seen.Add(GuildrunAccess.Contracts.TextFilter.Clean(line))) lines.Add(line);
            foreach (var line in TagsTooltips(card))
                if (seen.Add(GuildrunAccess.Contracts.TextFilter.Clean(line))) lines.Add(line);
            lines.AddRange(StatsTooltips(card));
            return lines;
        }

        /// <summary>The shop price shown on the card, or null when it carries none.</summary>
        public static string Price(HeroCardView card)
        {
            var price = card != null ? card._shopPriceText : null;
            return price != null && price.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(price.text) ? price.text : null;
        }

        /// <summary>Health, mana, then every active stat panel named by its own tooltip title.</summary>
        public static string StatsLine(HeroCardView card)
            => card == null ? null : StatsLine(card, card._healthText, card._manaText);

        /// <summary>"health 650, mana 85": the card's health and mana alone.</summary>
        public static string Vitals(HeroCardView card)
            => card == null ? null : Vitals(card._healthText, card._manaText);

        /// <summary>The focus line's stats: health and mana (when <paramref name="vitals"/>), then every
        /// stat panel whose value is not zero ("Mana Regen 4, Base Attack Damage 30, Magic 25, Crit 15,
        /// Defense 34, Attack Range 2"); the zeros are left to the full line in the buffer.</summary>
        public static string StatsBrief(HeroCardView card, bool vitals)
            => card == null ? null : StatsBrief(card, vitals ? card._healthText : null, vitals ? card._manaText : null);

        public static string StatsBrief(UnityEngine.Component root, TMPro.TMP_Text health, TMPro.TMP_Text mana)
        {
            if (root == null) return null;
            var parts = new List<string>();
            string v = Vitals(health, mana);
            if (!string.IsNullOrEmpty(v)) parts.Add(v);
            foreach (var stat in root.GetComponentsInChildren<StatView>(false))
            {
                if (stat == null || stat._statText == null || string.IsNullOrWhiteSpace(stat._statText.text) || IsZero(stat._statText.text)) continue;
                string name = TooltipReader.Title(stat._tooltipRaycastTarget) ?? StatNameFromObject(stat.gameObject.name);
                if (string.IsNullOrEmpty(name)) continue;
                parts.Add(Strings.HeroStat(name, stat._statText.text));
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        // "0", "0%", "0.0", "+0": a value that says nothing on a focus line.
        private static readonly System.Text.RegularExpressions.Regex Zero =
            new System.Text.RegularExpressions.Regex(@"^\s*[+-]?0+(?:[.,]0+)?\s*%?\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public static bool IsZero(string value) => value != null && Zero.IsMatch(value);

        public static string Vitals(TMPro.TMP_Text health, TMPro.TMP_Text mana)
        {
            var parts = new List<string>();
            if (health != null && health.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(health.text))
                parts.Add(Strings.HeroStat(Strings.HeroHealth, health.text));
            if (mana != null && mana.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(mana.text))
                parts.Add(Strings.HeroStat(Strings.HeroMana, mana.text));
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>The same line over any view carrying health/mana captions and stat panels (a mini card's stats view).</summary>
        public static string StatsLine(UnityEngine.Component root, TMPro.TMP_Text health, TMPro.TMP_Text mana)
        {
            if (root == null) return null;
            var sb = new StringBuilder();
            if (health != null && health.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(health.text))
                sb.Append(Strings.HeroStat(Strings.HeroHealth, health.text));
            if (mana != null && mana.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(mana.text))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Strings.HeroStat(Strings.HeroMana, mana.text));
            }
            // Active panels only: the card keeps inactive template panels with placeholder values.
            foreach (var stat in root.GetComponentsInChildren<StatView>(false))
            {
                if (stat == null || stat._statText == null || string.IsNullOrWhiteSpace(stat._statText.text)) continue;
                string name = TooltipReader.Title(stat._tooltipRaycastTarget) ?? StatNameFromObject(stat.gameObject.name);
                if (string.IsNullOrEmpty(name)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Strings.HeroStat(name, stat._statText.text));
            }
            return sb.ToString();
        }

        // "SingleStatPanel_AtkSpd" -> "AtkSpd" (a last-resort caption when the stat has no tooltip title).
        private static string StatNameFromObject(string objectName)
        {
            int us = objectName.LastIndexOf('_');
            return us >= 0 && us < objectName.Length - 1 ? objectName.Substring(us + 1) : null;
        }

        /// <summary>The stat tooltips on the card as buffer lines.</summary>
        public static List<string> StatsTooltips(HeroCardView card)
        {
            var lines = new List<string>();
            if (card == null) return lines;
            // The bars first (Max HP, Starting Mana: their tooltips define them), then every stat.
            lines.AddRange(TooltipReader.Lines(card._healthTooltip));
            lines.AddRange(TooltipReader.Lines(card._manaTooltip));
            foreach (var stat in card.GetComponentsInChildren<StatView>(false))
            {
                if (stat == null) continue;
                lines.AddRange(TooltipReader.Lines(stat._tooltipRaycastTarget));
            }
            return lines;
        }

        /// <summary>The card's ability views that carry a live tooltip (the ones actually shown).</summary>
        public static List<HeroCardAbilityView> Abilities(UnityEngine.Component root)
        {
            var list = new List<HeroCardAbilityView>();
            if (root == null) return list;
            foreach (var a in root.GetComponentsInChildren<HeroCardAbilityView>(true))
                if (a != null && a._tooltipRaycastTarget != null && a._tooltipRaycastTarget.TooltipSource != null && a._tooltipRaycastTarget.IsActive)
                    list.Add(a);
            return list;
        }

        /// <summary>"Shuriken Shadow Strike, Active Ability; Shadow Step, Passive Ability".</summary>
        public static string AbilitiesLine(UnityEngine.Component root)
        {
            var sb = new StringBuilder();
            foreach (var a in Abilities(root))
            {
                var head = TooltipReader.Heading(a._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(head)) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(head);
            }
            return sb.Length > 0 ? sb.ToString() : Strings.HeroNoAbilities;
        }

        /// <summary>The ability tooltips under the root as buffer lines: each its heading, summary and keyword lines.</summary>
        public static List<string> AbilitiesTooltips(UnityEngine.Component root)
        {
            var lines = new List<string>();
            foreach (var a in Abilities(root))
            {
                lines.AddRange(TooltipReader.Lines(a._tooltipRaycastTarget));
            }
            return lines;
        }

        // ---- the hero list ----

        /// <summary>An extra row a screen adds to a hero's buffers: a captioned line for the hero buffer
        /// ("relic, Starter Kit: Sustained Shard Boost") and its tooltip lines for the control buffer.</summary>
        public sealed class GridRow
        {
            public string Key;
            public Func<string> Caption;
            public Func<HeroCardView, string> Text;
            public Func<HeroCardView, IEnumerable<string>> Tooltip;

            public GridRow(string key, Func<string> caption, Func<HeroCardView, string> text, Func<HeroCardView, IEnumerable<string>> tooltip)
            {
                Key = key;
                Caption = caption;
                Text = text;
                Tooltip = tooltip;
            }
        }

        /// <summary>The card's equipment slots (the ones it shows).</summary>
        public static List<PlaceholderSlotView> Slots(HeroCardView card)
        {
            var list = new List<PlaceholderSlotView>();
            if (card == null) return list;
            foreach (var slot in card.GetComponentsInChildren<PlaceholderSlotView>(false))
                if (slot != null && slot.gameObject.activeInHierarchy) list.Add(slot);
            return list;
        }

        /// <summary>
        /// Declare hero cards as one vertical list inside the current Tab-stop, one control per hero:
        /// "Sal, Mage, Frost, cost Shard 15, health 725, mana 100, Magic 25, Crit 15, ..." (the
        /// <paramref name="nameSuffix"/>, a price, before the stats; only the non-zero stats). The rest
        /// waits in the buffers: the control buffer reads the full stats line, then every ability's,
        /// tag's and stat's tooltip lines (then the extras' tooltips); the hero buffer the whole hero
        /// (<see cref="HeroLines.ForCard"/>: name, stats, abilities, the same tooltips), then any
        /// <paramref name="extras"/> with their tooltips; the items buffer the items worn. Enter runs <paramref name="activate"/> for that card; the control is a button
        /// only when it has an action (a picker), plain text otherwise (an inspected card).
        /// </summary>
        public static void AddGrid(GraphBuilder b, string keyPrefix, IReadOnlyList<HeroCardView> cards,
            Func<int, Action> activate, Func<int, string> nameSuffix, params GridRow[] extras)
        {
            if (cards == null || cards.Count == 0) return;
            for (int i = 0; i < cards.Count; i++)
            {
                int index = i;
                var card = cards[i];
                var action = activate != null ? activate(index) : null;
                b.AddItem(ControlId.Structural(keyPrefix + ":" + i + ":name"), HeroNode(card,
                    () =>
                    {
                        var parts = new List<string> { NameAndClass(card) };
                        string suffix = nameSuffix != null ? nameSuffix(index) : null;
                        if (!string.IsNullOrEmpty(suffix)) parts.Add(suffix);
                        string stats = StatsBrief(card, vitals: true);
                        if (!string.IsNullOrEmpty(stats)) parts.Add(stats);
                        return string.Join(", ", parts);
                    },
                    action != null ? ControlTypes.Button : null, action, extras));
            }
        }

        // One hero as a control: its line; Enter runs the action; Backspace opens its compendium page;
        // the card fills the buffers.
        private static NodeVtable HeroNode(HeroCardView card, Func<string> text, ControlType type, Action activate, GridRow[] extras)
        {
            return new NodeVtable
            {
                ControlType = type,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(text) },
                SearchText = () => NameAndClass(card),
                OnActivate = activate,
                OnSecondary = () => OpenCompendium(card),
                Details = () => CardDetails(card, extras),
                Subject = () => card,
                SideLines = HeroLines.SideOfSlots(() => CardRows(card, extras), () => Slots(card)),
            };
        }

        // The hero buffer: the whole hero (name, stats, abilities, every tooltip), then each extra as
        // "caption, text" with its tooltip lines under it.
        private static IEnumerable<string> CardRows(HeroCardView card, GridRow[] extras)
        {
            foreach (var line in HeroLines.ForCard(card)) yield return line;
            if (extras == null) yield break;
            foreach (var r in extras)
            {
                if (r == null || r.Text == null) continue;
                string text = r.Text(card);
                if (string.IsNullOrWhiteSpace(text)) continue;
                string caption = r.Caption != null ? r.Caption() : null;
                yield return string.IsNullOrEmpty(caption) ? text : caption + ", " + text;
                var more = r.Tooltip != null ? r.Tooltip(card) : null;
                if (more != null)
                    foreach (var line in more) yield return line;
            }
        }

        // The control buffer: the full stats line, every ability's, tag's and stat's tooltip lines, then
        // the extras' tooltips.
        private static List<string> CardDetails(HeroCardView card, GridRow[] extras)
        {
            var lines = new List<string>();
            string stats = StatsLine(card);
            if (!string.IsNullOrEmpty(stats)) lines.Add(stats);
            lines.AddRange(Tooltips(card));
            if (extras != null)
                foreach (var r in extras)
                {
                    var more = r != null && r.Tooltip != null ? r.Tooltip(card) : null;
                    if (more != null) lines.AddRange(more);
                }
            return lines;
        }

        /// <summary>Press the card's own compendium button (the game opens the compendium on that hero).</summary>
        public static void OpenCompendium(HeroCardView card)
        {
            var opener = card != null ? card.GetComponentInChildren<Ember.Scopes.Application.Compendium.OpenCompendiumButton>(false) : null;
            var button = opener != null ? opener._button : null;
            if (button != null && button.interactable) button.onClick.Invoke();
        }

        // A grid cell: "caption, text" (untyped, so parts speak in declaration order) or a typed name
        // cell; Enter runs the column's action; the cell's tooltips are its ui-buffer details, and every
        // cell of a card fills the hero and item buffers with that card.
        private static NodeVtable Cell(HeroCardView card, Func<string> text, ControlType type,
            Func<IEnumerable<string>> tooltip, Func<string> caption, Action activate)
        {
            var parts = new List<NodeAnnouncement>();
            if (caption != null) parts.Add(new NodeAnnouncement(caption));
            parts.Add(GameNodes.LabelPart(text));
            return new NodeVtable
            {
                ControlType = caption != null ? null : type,
                Announcements = parts,
                SearchText = () => NameAndClass(card),
                OnActivate = activate,
                OnSecondary = () => OpenCompendium(card),
                Details = () => tooltip != null ? tooltip() : null,
                Subject = () => card,
                SideLines = HeroLines.SideOfSlots(() => HeroLines.ForCard(card), () => Slots(card)),
            };
        }
    }
}
