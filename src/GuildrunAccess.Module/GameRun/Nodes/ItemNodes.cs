using System;
using System.Collections.Generic;
using Ember.Scopes.Application.UI.Common;
using Ember.Scopes.GameRun.UI.Relics;
using Ember.Scopes.GameRun.UI.Slots;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// Readers and node factories for the game's item and relic views, reused wherever they appear:
    /// the reserve column, a hero's equipment slots, the battlefield health bars, and the relic bar.
    /// Names come from the view's own text or its tooltip title; the tooltip is the buffer line.
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

        /// <summary>Every item in a set of slots as buffer lines: each its heading, summary and keyword lines.</summary>
        public static List<string> ItemTooltips(IEnumerable<PlaceholderSlotView> slots)
        {
            var lines = new List<string>();
            if (slots == null) return lines;
            foreach (var slot in slots)
            {
                if (!HasItem(slot)) continue;
                lines.AddRange(TooltipReader.Lines(slot._tooltipRaycastTarget));
            }
            return lines;
        }

        /// <summary>The quests of the items in a set of slots, for the quest buffer and its glance key:
        /// one line per quest, opened by the item that carries it ("Hourglass: Quest: Trigger Stall 5
        /// times from any source., 0 / 5"; "Rift Seal: Tank or Vanguard, 1 / 3", one per charge), an
        /// ordinary quest's reward on the line after it when <paramref name="rewards"/>. Empty when
        /// nothing worn has a quest.</summary>
        public static List<string> QuestLines(IEnumerable<PlaceholderSlotView> slots, bool rewards = true)
        {
            var lines = new List<string>();
            if (slots == null) return lines;
            foreach (var slot in slots)
                if (HasItem(slot)) AddQuestLines(lines, ItemName(slot), slot._tooltipRaycastTarget, rewards);
            return lines;
        }

        /// <summary>A relic's quests, in the same lines.</summary>
        public static List<string> QuestLines(RelicView relic, bool rewards = true)
        {
            var lines = new List<string>();
            if (relic != null && relic.gameObject.activeInHierarchy) AddQuestLines(lines, RelicName(relic), relic._tooltipRaycastTarget, rewards);
            return lines;
        }

        private static void AddQuestLines(List<string> lines, string owner, Ember.Scopes.Application.UI.Tooltips.TooltipRaycastTarget target, bool rewards)
        {
            var quests = TooltipReader.Quests(target);
            if (quests.Count == 0) return;
            if (!rewards)
            {
                // The glance: the item named once, its quests after it ("Rift Seal: Tank or Vanguard,
                // 0 / 3; Assassin, Duelist, or Warrior, 1 / 3; Mystic or Mage, 0 / 3").
                var parts = new List<string>();
                foreach (var quest in quests) parts.Add(quest.Line);
                string all = string.Join("; ", parts);
                lines.Add(string.IsNullOrWhiteSpace(owner) ? all : Strings.QuestOfItem(owner, all));
                return;
            }
            // The buffer: a line per quest, each naming its item, so a line stands on its own.
            foreach (var quest in quests)
            {
                lines.Add(string.IsNullOrWhiteSpace(owner) ? quest.Line : Strings.QuestOfItem(owner, quest.Line));
                // The label of a quest that states its requirement apart is its reward.
                if (quest.Requirement != null && !string.IsNullOrWhiteSpace(quest.Label)) lines.Add(quest.Label);
            }
        }

        // An item's or a relic's own quests, for the quest buffer when the control is the item itself.
        private static Func<string, IEnumerable<string>> QuestSide(Func<bool, List<string>> quests)
            => key => key == GuildrunAccess.Core.Buffers.BufferKeys.Quest ? quests(true)
                : key == GuildrunAccess.Core.Buffers.BufferKeys.QuestBrief ? quests(false) : null;

        /// <summary>An item slot as a control: its name (no role word: the list's context already says
        /// items); its tooltip is its buffer line; Enter runs <paramref name="activate"/> when given.</summary>
        public static NodeVtable Slot(PlaceholderSlotView slot, Action activate = null)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => ItemName(slot) ?? Strings.RunItemSlotEmpty) },
                SearchText = () => ItemName(slot),
                OnActivate = activate,
                Details = () => TooltipReader.Lines(slot._tooltipRaycastTarget),
                SideLines = QuestSide(rewards => QuestLines(new[] { slot }, rewards)),
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

        /// <summary>A relic as a control: its name; its tooltip is its buffer line.</summary>
        public static NodeVtable Relic(RelicView relic, Action activate = null)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => RelicName(relic)) },
                SearchText = () => RelicName(relic),
                OnActivate = activate,
                Details = () => TooltipReader.Lines(relic._tooltipRaycastTarget),
                SideLines = QuestSide(rewards => QuestLines(relic, rewards)),
            };
        }
    }
}
