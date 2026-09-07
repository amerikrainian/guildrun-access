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
    /// Enter acting on the column's hero, Space reading the cell's tooltip text. Rows are composable:
    /// the standard name / stats / abilities rows plus whatever a screen adds (a relic, a price).
    /// </summary>
    internal static class HeroCardNodes
    {
        // ---- live readers over a card ----

        /// <summary>"Kai, Warrior, Shield": the name and the rendered class/archetype tags.</summary>
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
            return sb.ToString();
        }

        // A class tag carries its localized caption; an archetype tag (under the card's HeroArchetypes
        // holder) is icon-only with a placeholder caption, and its icon sprite is named for the
        // archetype ("Shield", "Fire", "Stall").
        private static string TagName(HeroTagView tag)
        {
            if (tag == null) return null;
            var parent = tag.transform.parent;
            bool archetype = parent != null && parent.name == "HeroArchetypes";
            if (archetype)
            {
                var sprite = tag._icon != null ? tag._icon.sprite : null;
                return sprite != null ? sprite.name.Replace("_NoFrame", "").Replace('_', ' ') : null;
            }
            return tag._name != null ? tag._name.text : null;
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

        public static string StatsTooltips(HeroCardView card)
        {
            if (card == null) return null;
            var sb = new StringBuilder();
            foreach (var stat in card.GetComponentsInChildren<StatView>(false))
            {
                if (stat == null) continue;
                var text = TooltipReader.Describe(stat._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
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

        public static string AbilitiesTooltips(UnityEngine.Component root)
        {
            var sb = new StringBuilder();
            foreach (var a in Abilities(root))
            {
                var text = TooltipReader.Describe(a._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // ---- the grid ----

        /// <summary>One detail row of the hero grid: a spoken caption, the cell text per card, and the
        /// cell's Space readout per card.</summary>
        public sealed class GridRow
        {
            public string Key;
            public Func<string> Caption;
            public Func<HeroCardView, string> Text;
            public Func<HeroCardView, string> Tooltip;

            public GridRow(string key, Func<string> caption, Func<HeroCardView, string> text, Func<HeroCardView, string> tooltip)
            {
                Key = key;
                Caption = caption;
                Text = text;
                Tooltip = tooltip;
            }
        }

        public static readonly GridRow StatsRow = new GridRow("stats", () => Strings.HeroStats, StatsLine, StatsTooltips);
        public static readonly GridRow AbilitiesRow = new GridRow("abilities", () => Strings.HeroAbilities, AbilitiesLine, AbilitiesTooltips);
        public static readonly GridRow ItemsRow = new GridRow("items", () => Strings.HeroItems,
            card => ItemNodes.ItemNames(Slots(card)) ?? Strings.HeroNoItems, card => ItemNodes.ItemTooltips(Slots(card)));

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
        /// Declare a grid of hero cards inside the current Tab-stop: row 1 is each card's name and
        /// class (plus <paramref name="nameSuffix"/>, e.g. a price), then <paramref name="rows"/> in
        /// order. Every cell of a column activates through <paramref name="activate"/> for that card;
        /// the name cell is a button only when it has an action (a picker), plain text otherwise (an
        /// inspected card).
        /// </summary>
        public static void AddGrid(GraphBuilder b, string keyPrefix, IReadOnlyList<HeroCardView> cards,
            Func<int, Action> activate, Func<int, string> nameSuffix, params GridRow[] rows)
        {
            if (cards == null || cards.Count == 0) return;
            string rowKey = keyPrefix + ":grid";

            b.StartRow(rowKey);
            for (int i = 0; i < cards.Count; i++)
            {
                int index = i;
                var card = cards[i];
                var action = activate != null ? activate(index) : null;
                b.AddItem(ControlId.Structural(keyPrefix + ":" + i + ":name"), Cell(card,
                    () =>
                    {
                        string name = NameAndClass(card);
                        string suffix = nameSuffix != null ? nameSuffix(index) : null;
                        return string.IsNullOrEmpty(suffix) ? name : name + ", " + suffix;
                    },
                    action != null ? ControlTypes.Button : null, () => AbilitiesTooltips(card), null, action));
            }
            b.EndRow();

            foreach (var row in rows)
            {
                if (row == null) continue;
                b.StartRow(rowKey);
                for (int i = 0; i < cards.Count; i++)
                {
                    int index = i;
                    var card = cards[i];
                    var r = row;
                    b.AddItem(ControlId.Structural(keyPrefix + ":" + i + ":" + r.Key), Cell(card,
                        () => r.Text(card), null, () => r.Tooltip != null ? r.Tooltip(card) : null, r.Caption,
                        activate != null ? activate(index) : null));
                }
                b.EndRow();
            }
        }

        /// <summary>Press the card's own compendium button (the game opens the compendium on that hero).</summary>
        public static void OpenCompendium(HeroCardView card)
        {
            var opener = card != null ? card.GetComponentInChildren<Ember.Scopes.Application.Compendium.OpenCompendiumButton>(false) : null;
            var button = opener != null ? opener._button : null;
            if (button != null && button.interactable) button.onClick.Invoke();
        }

        // A grid cell: "caption, text" (untyped, so parts speak in declaration order) or a typed name
        // cell; Enter runs the column's action; Space speaks the tooltip text.
        private static NodeVtable Cell(HeroCardView card, Func<string> text, ControlType type,
            Func<string> tooltip, Func<string> caption, Action activate)
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
                OnTooltip = () =>
                {
                    string t = tooltip != null ? tooltip() : null;
                    GameNodes.SayTooltip(t);
                },
            };
        }
    }
}
