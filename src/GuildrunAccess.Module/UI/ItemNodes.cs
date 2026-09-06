using System;
using System.Collections.Generic;
using Ember.Scopes.Application.UI.Common;
using Ember.Scopes.GameRun.UI.Relics;
using Ember.Scopes.GameRun.UI.Slots;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// Readers and node factories for the game's item and relic views, reused wherever they appear:
    /// the reserve column, a hero's equipment slots, the battlefield health bars, and the relic bar.
    /// Names come from the view's own text or its tooltip title; Space reads the tooltip.
    /// </summary>
    internal static class ItemNodes
    {
        /// <summary>Whether a placeholder slot currently holds an item (its active-slot visuals shown).</summary>
        public static bool HasItem(PlaceholderSlotView slot)
            => slot != null && slot.gameObject.activeInHierarchy && slot._activeSlotParent != null && slot._activeSlotParent.activeSelf;

        /// <summary>The item's name from the slot's caption, else its tooltip title; null when empty.</summary>
        public static string ItemName(PlaceholderSlotView slot)
        {
            if (!HasItem(slot)) return null;
            string name = ShownText(slot._activeNameText);
            if (name == null) name = TooltipReader.Title(slot._tooltipRaycastTarget);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        /// <summary>An item entry view's name (an event's or picker's offered item): its caption, else
        /// its tooltip title; null when it shows nothing.</summary>
        public static string ItemName(ItemView item)
        {
            if (item == null || !item.gameObject.activeInHierarchy) return null;
            string name = ShownText(item._itemNameText);
            if (name == null) name = TooltipReader.Title(item.TooltipRaycastTarget);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        // A caption's text only when the caption is actually displayed: hidden captions keep their
        // prefab placeholder ("Very Long relic Name").
        private static string ShownText(TMPro.TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return null;
            return text.text;
        }

        /// <summary>The names of the items in a set of slots, comma-joined; null when none.</summary>
        public static string ItemNames(IEnumerable<PlaceholderSlotView> slots)
        {
            if (slots == null) return null;
            var sb = new System.Text.StringBuilder();
            foreach (var slot in slots)
            {
                string name = ItemName(slot);
                if (name == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(name);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>The tooltip text of every item in a set of slots, period-joined; null when none.</summary>
        public static string ItemTooltips(IEnumerable<PlaceholderSlotView> slots)
        {
            if (slots == null) return null;
            var sb = new System.Text.StringBuilder();
            foreach (var slot in slots)
            {
                if (!HasItem(slot)) continue;
                var text = TooltipReader.Describe(slot._tooltipRaycastTarget);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>An item slot as a control: its name (no role word: the list's context already says
        /// items); Space reads its tooltip; Enter runs <paramref name="activate"/> when given.</summary>
        public static NodeVtable Slot(PlaceholderSlotView slot, Action activate = null)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => ItemName(slot) ?? Strings.RunItemSlotEmpty) },
                SearchText = () => ItemName(slot),
                OnActivate = activate,
                OnTooltip = () => Core.Speech.Say(TooltipReader.Describe(slot._tooltipRaycastTarget) ?? Strings.NoTooltip, interrupt: true),
            };
        }

        /// <summary>A relic's name from its caption, else its tooltip title, else its object name.</summary>
        public static string RelicName(RelicView relic)
        {
            if (relic == null) return null;
            string name = ShownText(relic._nameText);
            if (name == null) name = TooltipReader.Title(relic._tooltipRaycastTarget);
            return string.IsNullOrWhiteSpace(name) ? relic.gameObject.name : name;
        }

        /// <summary>A relic as a control: its name; Space reads its tooltip.</summary>
        public static NodeVtable Relic(RelicView relic, Action activate = null)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => RelicName(relic)) },
                SearchText = () => RelicName(relic),
                OnActivate = activate,
                OnTooltip = () => Core.Speech.Say(TooltipReader.Describe(relic._tooltipRaycastTarget) ?? Strings.NoTooltip, interrupt: true),
            };
        }
    }
}
