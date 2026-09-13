using System.Collections.Generic;
using System;
using System.Text;
using Ember.Scopes.Application.UI.Tooltips;
using Ember.Scopes.GameRun.Utilities.Tooltips;
using Ember.Scopes.GameRun.Utilities.Tooltips.Sources;
using GuildrunAccess.Core;
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
        /// Empty when the control has no tooltip.</summary>
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
                        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text)) lines.Add(tmp.text.Trim());
                }
            }
            view.Clear();
            return lines;
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
