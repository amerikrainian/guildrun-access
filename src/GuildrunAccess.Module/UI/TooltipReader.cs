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
        {
            var lines = new List<string>();
            var view = Populate(target);
            if (view == null) return lines;
            var head = new StringBuilder();
            Append(head, view._titleText != null ? view._titleText.text : null);
            Append(head, view._subtitleText != null ? view._subtitleText.text : null);
            if (head.Length > 0) lines.Add(head.ToString());

            // The view itself activates the sections its mode shows (summary or details) and leaves the
            // others inactive; that flag is the filter. (The section tuple's mode field does not read
            // back reliably through the interop value tuple.)
            view.SetDetailsMode(details);
            var sections = view._sections;
            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    var go = sections[i].Item1;
                    if (go == null || !go.activeSelf) continue;
                    foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(true))
                        if (tmp != null) AddLines(lines, tmp.text);
                }
            }
            view.Clear();
            if (details) AddStatDefinitions(lines, target);
            return lines;
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
        {
            try
            {
                if (target == null) return null;
                var source = target.TooltipSource;
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
