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
        private static AppTooltipController _controller;
        private const int SearchEvery = 60;
        private static int _lastSearchFrame = -SearchEvery;

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

        /// <summary>The full readout: heading, then every section the details mode shows (what the game
        /// shows while Shift is held: the summary plus the definitions of the keywords it uses, without
        /// the hint to hold Shift), in the game's own order; the summary alone when
        /// <paramref name="details"/> is false. Null when the control has no tooltip.</summary>
        public static string Describe(TooltipRaycastTarget target, bool details = true)
        {
            var view = Populate(target);
            if (view == null) return null;
            var sb = new StringBuilder();
            Append(sb, view._titleText != null ? view._titleText.text : null);
            Append(sb, view._subtitleText != null ? view._subtitleText.text : null);

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
                        if (tmp != null) Append(sb, tmp.text);
                }
            }
            view.Clear();
            return sb.Length > 0 ? sb.ToString() : null;
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

        // The scene's tooltip controller (the run one in a run, the application one in the menu).
        private static AppTooltipController Controller()
        {
            if (_controller != null) return _controller;
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return null;
            _lastSearchFrame = Time.frameCount;
            var found = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<AppTooltipController>());
            _controller = found != null ? found.TryCast<AppTooltipController>() : null;
            return _controller;
        }
    }
}
