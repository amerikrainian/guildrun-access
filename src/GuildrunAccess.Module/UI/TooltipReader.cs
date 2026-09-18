using System.Collections.Generic;
using System;
using System.Text;
using System.Text.RegularExpressions;
using Ember.Balancing.Sheets.Items;
using Ember.Scopes.Application.UI.Tooltips;
using Ember.Scopes.Application.UI.Tooltips.Sources;
using Ember.Scopes.Application.Utilities;
using Ember.Scopes.GameRun.Utilities.Tooltips;
using Ember.Scopes.GameRun.Utilities.Tooltips.Sources;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using GuildrunAccess.Module.Interop;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// Reads what a control's tooltip WOULD show, without a mouse: the game composes every tooltip by
    /// populating one shared <see cref="TooltipView"/> from the control's <c>ITooltipSource</c>, so we
    /// run that same population on the live view, read the title, subtitle, and the sections the
    /// summary (or details) mode would display, and clear the view again. Game-run sources (abilities,
    /// relics, items) need the controller's context first, exactly as the controller gives it to them.
    /// Text is the game's own localized, keyword-parsed tooltip text; nothing is cached.
    /// </summary>
    internal static class TooltipReader
    {

        /// <summary>The tooltip's title alone (a stat's or ability's name), or null.</summary>
        public static string Title(TooltipRaycastTarget target)
        {
            var view = Populate(target);
            if (view == null) return null;
            string title = view._titleText != null ? view._titleText.text : null;
            view.Clear();
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }

        /// <summary>Title and subtitle ("Shuriken Shadow Strike, Active Ability"), or null.</summary>
        public static string Heading(TooltipRaycastTarget target)
        {
            var view = Populate(target);
            if (view == null) return null;
            string title = view._titleText != null ? view._titleText.text : null;
            string sub = view._subtitleText != null ? view._subtitleText.text : null;
            view.Clear();
            if (string.IsNullOrWhiteSpace(title)) return null;
            return string.IsNullOrWhiteSpace(sub) ? title : title + ", " + sub;
        }

        /// <summary>The tooltip as buffer lines: the heading ("Vault Spark. Active Ability"), then every
        /// text of every section the details mode shows as a line of its own (the summary, then each
        /// keyword definition it uses: what the game shows while Shift is held, without the hint to hold
        /// Shift), in the game's own order; the summary alone when <paramref name="details"/> is false.
        /// An item's tooltip goes on with one definition line per stat it modifies ("Attack Speed:
        /// Increases how often a character auto attacks."), the game's own stat text, which its item
        /// tooltip leaves out. Empty when the control has no tooltip.</summary>
        public static List<string> Lines(TooltipRaycastTarget target, bool details = true)
            => Lines(target != null ? target.TooltipSource : null, target, details);

        /// <summary>The same lines from a tooltip source alone: what the game would show for it, had
        /// it a target on screen (the compendium builds ability and class-upgrade sources itself).</summary>
        public static List<string> Lines(ITooltipSource source, bool details = true)
            => Lines(source, null, details);

        private static List<string> Lines(ITooltipSource source, TooltipRaycastTarget target, bool details)
        {
            var lines = new List<string>();
            var view = Populate(source);
            if (view == null) return lines;
            var head = new StringBuilder();
            Append(head, view._titleText != null ? view._titleText.text : null);
            Append(head, view._subtitleText != null ? view._subtitleText.text : null);
            if (head.Length > 0) lines.Add(head.ToString());

            // The view itself activates the sections its mode shows (summary or details) and leaves the
            // others inactive; that flag is the filter. (The section tuple's mode field does not read
            // back reliably through the interop value tuple.)
            view.SetDetailsMode(details);
            foreach (var section in Sections(view))
            {
                // A quest's sections are one line: its label with its count and its state ("Tank or
                // Vanguard, 0 / 3"). Read apart, the counts are bare numbers, and equal ones fold
                // into one in a buffer (the Rift Seal's three "0 / 3").
                if (section.Kind == SectionKind.Separator) continue;
                if (section.Kind == SectionKind.Bonus) lines.Add(QuestLabel(section));
                else AddLines(lines, section.Text);
            }
            view.Clear();
            if (details) AddStatDefinitions(lines, target);
            return lines;
        }

        // ---- quests ----

        /// <summary>A quest a tooltip shows: an item's or a relic's ("Quest: Inflict 100 Poison.", its
        /// reward, "0 / 100"), or one of the Rift Seal's three charges ("Tank or Vanguard", "0 / 3").
        /// The game draws each as a conditional-bonus section (a text with a locked or an unlocked
        /// icon) and a progress bar after it; an ordinary quest's requirement is the description
        /// section just before.</summary>
        public sealed class Quest
        {
            /// <summary>What must be done ("Quest: Inflict 100 Poison."), when the game says it apart
            /// from the label; null for a charge of the Rift Seal, whose label says it.</summary>
            public string Requirement;
            /// <summary>The conditional bonus' text: the reward of an ordinary quest, the classes of a
            /// Rift Seal charge.</summary>
            public string Label;
            /// <summary>The progress bar's text ("0 / 100"), or null when the game draws none.</summary>
            public string Progress;
            /// <summary>Whether the game shows the bonus unlocked.</summary>
            public bool Complete;

            /// <summary>The quest in a line: what to do (else the label), the count, the state. A
            /// requirement's own full stop goes, so the count does not follow ".,".</summary>
            public string Line => Strings.QuestLine((Requirement ?? Label ?? "").TrimEnd('.', ' '), Progress, Complete);
        }

        /// <summary>The quests the control's tooltip shows, in its order; empty when it has none.</summary>
        public static List<Quest> Quests(TooltipRaycastTarget target)
        {
            var quests = new List<Quest>();
            var view = Populate(target);
            if (view == null) return quests;
            view.SetDetailsMode(true);
            var sections = Sections(view);
            view.Clear();
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].Kind != SectionKind.Bonus) continue;
                var quest = new Quest { Label = sections[i].Text, Progress = sections[i].Progress, Complete = sections[i].Complete };
                // The requirement stands right before the bonus, as a description of its own.
                if (i > 0 && sections[i - 1].Kind == SectionKind.Text) quest.Requirement = sections[i - 1].Text;
                quests.Add(quest);
            }
            return quests;
        }

        private static string QuestLabel(Section bonus) => Strings.QuestLine(bonus.Text, bonus.Progress, bonus.Complete);

        private enum SectionKind { Text, Bonus, Progress, Separator }

        private sealed class Section
        {
            public SectionKind Kind;
            public string Text;
            public string Progress;
            public bool Complete;
        }

        // The sections the view's mode shows, in order, by what the game built each from. The view
        // itself activates the ones its mode shows (summary or details) and leaves the others
        // inactive; that flag is the filter. (The section tuple's mode field does not read back
        // reliably through the interop value tuple.) A progress bar is folded into the bonus before it.
        private static List<Section> Sections(TooltipView view)
        {
            var list = new List<Section>();
            var sections = view._sections;
            if (sections == null) return list;
            for (int i = 0; i < sections.Count; i++)
            {
                var go = sections[i].Item1;
                if (go == null || !go.activeSelf) continue;
                var progress = go.GetComponent<QuestProgressView>();
                if (progress != null)
                {
                    string count = progress._progressBarText != null ? progress._progressBarText.text : null;
                    if (string.IsNullOrWhiteSpace(count)) continue;
                    var last = list.Count > 0 ? list[list.Count - 1] : null;
                    if (last != null && last.Kind == SectionKind.Bonus && last.Progress == null) last.Progress = count.Trim();
                    else list.Add(new Section { Kind = SectionKind.Progress, Text = count.Trim() });
                    continue;
                }
                var bonus = go.GetComponent<ConditionalBonusView>();
                if (bonus != null)
                {
                    string text = bonus._bonusText != null ? bonus._bonusText.text : null;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var unlocked = bonus._unlockedIcon;
                    list.Add(new Section { Kind = SectionKind.Bonus, Text = text.Trim(), Complete = unlocked != null && unlocked.gameObject.activeSelf });
                    continue;
                }
                var sb = new StringBuilder();
                foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(tmp.text);
                }
                // A section without text is a separator: kept, since it parts a description from a
                // quest that is not its requirement (the Rift Seal's charges stand under one).
                list.Add(sb.Length > 0 ? new Section { Kind = SectionKind.Text, Text = sb.ToString() } : new Section { Kind = SectionKind.Separator });
            }
            // A progress bar with no bonus before it is a line of its own.
            for (int i = 0; i < list.Count; i++)
                if (list[i].Kind == SectionKind.Progress) list[i] = new Section { Kind = SectionKind.Text, Text = Strings.QuestProgressAlone(list[i].Text) };
            return list;
        }

        // ---- flexible tooltip objects ----

        // A few widgets carry no tooltip source but the tooltip asset's own object, filled by the game
        // with a title and a description (TooltipHelper.PopulateTitleDescriptionTooltipObject: a hero
        // card's class and archetype tags). Its information is a list of texts by identifier.
        private const string FlexTitle = "tooltip_title";
        private const string FlexDescription = "tooltip_description";
        private const string FlexDetails = "tooltip_details";
        private const string FlexExtra = "tooltip_extra_information";

        /// <summary>A flexible tooltip object's title ("Shard", an archetype tag's name), or null.</summary>
        public static string Title(TRavljen.Tooltip.TooltipObject tooltip)
        {
            string title = FlexText(tooltip, FlexTitle);
            return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }

        /// <summary>A flexible tooltip object as buffer lines, what the game shows with Shift held: the
        /// title, the details (the description when it has none), then the keyword definitions, each
        /// paragraph a line, a repeat dropped ("Shards: The currency..." is an archetype's description
        /// and its own keyword definition at once). The title is left out when the text opens with it
        /// ("Crit" over "Crit: The percent chance..."). Empty when the object holds nothing.</summary>
        public static List<string> Lines(TRavljen.Tooltip.TooltipObject tooltip)
        {
            var lines = new List<string>();
            string details = FlexText(tooltip, FlexDetails);
            AddLines(lines, string.IsNullOrWhiteSpace(details) ? FlexText(tooltip, FlexDescription) : details);
            AddLines(lines, FlexText(tooltip, FlexExtra));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            lines.RemoveAll(line => !seen.Add(AnyTag.Replace(line, "").Trim()));
            string title = Title(tooltip);
            if (title != null && (lines.Count == 0 || !AnyTag.Replace(lines[0], "").TrimStart().StartsWith(title, StringComparison.OrdinalIgnoreCase)))
                lines.Insert(0, title);
            return lines;
        }

        private static string FlexText(TRavljen.Tooltip.TooltipObject tooltip, string identifier)
        {
            try
            {
                var info = tooltip != null ? tooltip.information : null;
                var flexible = info != null ? info.TryCast<TRavljen.Tooltip.Flexible.FlexibleTooltipInformation>() : null;
                var data = flexible != null ? flexible.data : null;
                if (data == null) return null;
                for (int i = 0; i < data.Count; i++)
                {
                    var value = data[i];
                    if (value == null || value.identifier != identifier) continue;
                    var text = value.TryCast<TRavljen.Tooltip.Flexible.TextDataValue>();
                    return text != null ? text.text : null;
                }
            }
            catch (Exception e)
            {
                CoreLog.Warning("TooltipReader: flexible tooltip: " + e.Message);
            }
            return null;
        }

        /// <summary>The definitions of the keywords a raw game text uses ("Omnivamp: Restores Health...",
        /// one per line), through the game's own keyword parser: what a tooltip's keyword section would
        /// hold for it. The text must be the raw localized string with its keyword tags
        /// ("[Omnivamp]&lt;omnivamp&gt;"); a formatted text yields nothing. Empty when there are none.</summary>
        public static List<string> KeywordDefinitions(string raw)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return lines;
            try
            {
                var parser = ApplicationKeywordParser.Instance;
                if (parser == null) return lines;
                var result = parser.Parse(raw, null);
                AddLines(lines, result.ExtraInfo);
            }
            catch (Exception e)
            {
                CoreLog.Warning("TooltipReader: keyword definitions: " + e.Message);
            }
            return lines;
        }

        /// <summary>The raw localized description of a balancing entry (with its keyword tags), or null.</summary>
        public static string RawDescription(Il2CppObjectBase entry)
        {
            try
            {
                var named = entry != null ? entry.TryCast<gg.leyline.balancing.Data.INamedDescriptionBalancingEntry>() : null;
                var key = named != null ? named.DescriptionLocaKey : null;
                var localized = key != null ? key.LocalizedString : null;
                return localized != null ? localized.GetLocalizedString() : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("TooltipReader: raw description: " + e.Message);
                return null;
            }
        }

        // An item's stat lines ("+10 Attack Speed") define nothing: the game keeps a stat's definition
        // in its stat text helper, what a hero card's stat tooltip shows. One line per stat the item
        // modifies, after the sections; stats the game has no text for are skipped.
        private static void AddStatDefinitions(List<string> lines, TooltipRaycastTarget target)
        {
            try
            {
                var entry = ItemEntry(target != null ? target.TooltipSource : null);
                var mods = entry != null ? entry.StatModifications : null;
                if (mods == null) return;
                for (int i = 0; i < mods.Length; i++)
                {
                    var type = mods[i].TargetStat;
                    if (type == TargetStatType.None) continue;
                    string name = StatTextHelper.GetStatName(type);
                    string description = StatTextHelper.GetStatDescription(type);
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description)) continue;
                    lines.Add(Strings.StatDefinition(name.Trim(), description.Trim()));
                }
            }
            catch (Exception e)
            {
                CoreLog.Warning("TooltipReader: stat definitions failed: " + e.Message);
            }
        }

        // The item entry behind an item's tooltip (a run's item instance or a catalogue entry), else null.
        private static IItemEntry ItemEntry(ITooltipSource source)
        {
            var obj = source as Il2CppObjectBase;
            if (obj == null) return null;
            var instance = obj.TryCast<ItemInstanceTooltipSource>();
            if (instance != null) return instance._itemEntry;
            var entry = obj.TryCast<ItemEntryTooltipSource>();
            return entry != null ? entry.ItemEntry : null;
        }

        // A section text holds one paragraph per line break (the keyword definitions come as one text,
        // "Rank: ...\nCrit: ..."): each is a line of its own. A line opening with an icon sprite whose
        // spoken name comes up again in the line ("<sprite name=Rank> Rank:", "<sprite name=ManaRegen>
        // +2 Mana Regen") drops the icon, or speech would say the name twice.
        private static readonly Regex LeadingIcon = new Regex(
            @"^\s*<sprite\s+name=""?(?<name>[^""\s>]+)""?[^>]*>\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AnyTag = new Regex("<[^>]+>", RegexOptions.Compiled);

        private static void AddLines(List<string> lines, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // TextMesh Pro draws a written-out "\n" as a line break (the Rift Seal's description has
            // two): it is one here too, not a backslash and an n for speech to stumble on.
            text = text.Replace("\\n", "\n");
            foreach (var raw in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                var m = LeadingIcon.Match(line);
                if (m.Success)
                {
                    string spoken = Speech.SpriteName(m.Groups["name"].Value);
                    string rest = AnyTag.Replace(line.Substring(m.Length), "");
                    if (spoken.Length > 0 && rest.IndexOf(spoken, StringComparison.OrdinalIgnoreCase) >= 0)
                        line = line.Substring(m.Length).TrimStart();
                }
                lines.Add(line);
            }
        }

        private static void Append(StringBuilder sb, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (sb.Length > 0) sb.Append(". ");
            sb.Append(text.Trim());
        }

        // Fill the shared view from the target's source; null when there is no source or no controller.
        private static TooltipView Populate(TooltipRaycastTarget target)
            => Populate(target != null ? target.TooltipSource : null);

        private static TooltipView Populate(ITooltipSource source)
        {
            try
            {
                if (source == null) return null;
                var controller = Controller();
                if (controller == null) return null;
                var view = controller._tooltipView;
                var config = controller._tooltipConfig;
                var parser = controller._keywordParser;
                if (view == null || config == null || parser == null) return null;

                GiveContext(source, controller);
                view.Clear();
                source.PopulateView(view, config, parser);
                return view;
            }
            catch (Exception e)
            {
                CoreLog.Warning("TooltipReader: populate failed: " + e.Message);
                return null;
            }
        }

        // Game-run sources compose against the run (player stats, relics held); the run controller
        // hands them its context before populating, so do the same.
        private static void GiveContext(ITooltipSource source, AppTooltipController controller)
        {
            var run = controller.TryCast<GameRunTooltipController>();
            if (run == null) return;
            var obj = source as Il2CppObjectBase;
            if (obj == null) return;
            var ability = obj.TryCast<AbilityInstanceTooltipSource>();
            if (ability != null) { ability.Context = run.Context; return; }
            var relic = obj.TryCast<RelicInstanceTooltipSource>();
            if (relic != null) { relic.Context = run.Context; return; }
            var item = obj.TryCast<ItemInstanceTooltipSource>();
            if (item != null) item.Context = run.Context;
        }

        // The area's tooltip controller (the run's in a run, the application's in the menu): a listed
        // controller of its scope, or, for the application scope (which lists none), a child of it.
        private static AppTooltipController Controller()
            => GameScopes.Controller<AppTooltipController>() ?? GameScopes.Component<AppTooltipController>();
    }
}
