using System;
using System.Collections.Generic;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.GameRun.UI;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using TMPro;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>The run's glance keys, each spoken in place with focus unmoved and read at the keypress.
    /// Ctrl+S the shards, by the game's own tooltip title, from the same live text the info stop reads
    /// (the panel's second counter is the Emergency Rewind, not a currency, and stays the info stop's).
    /// Ctrl+C the board position of what is focused, bare coordinates, column then row ("4, 1"): a
    /// grid cell's while placing, empty or not, else the cell the focused control's unit stands on (a
    /// fighting unit's as it moves; a slot's or card's hero while it is on the board). Ctrl+T the
    /// battle timer as the top panel draws it ("00:42"): the top panel stays up over the shop, the
    /// crossroads and the events, so the last fight's time is spoken there too. Each is silent where
    /// its fact is not on screen: outside a run, off the board, the timer's text hidden.</summary>
    internal static class RunGlance
    {
        public static void Shards()
        {
            try
            {
                var info = GameScopes.Controller<BasicInfoUIPanelController>();
                if (info == null || !info.gameObject.activeInHierarchy) return;
                var parts = new List<string>();
                Add(parts, TooltipReader.Title(info._currentGoldTooltip) ?? Strings.RunGold, info._currentGoldText);
                if (parts.Count > 0) Speech.Say(string.Join(", ", parts), interrupt: true);
            }
            catch (Exception e) { CoreLog.Warning("RunGlance: shards failed: " + e.Message); }
        }

        public static void Position()
        {
            try
            {
                if (BoardSection.TryFocusedCell(out var cell)) { Speech.Say(RunLabels.CellName(cell), interrupt: true); return; }
                UnitGlance.Speak(UnitGlance.Group.Position);
            }
            catch (Exception e) { CoreLog.Warning("RunGlance: position failed: " + e.Message); }
        }

        public static void Timer()
        {
            try
            {
                var timer = GameScopes.Controller<BattleTimerController>();
                var text = timer != null ? timer._timerText : null;
                if (!Drawn(text) || string.IsNullOrWhiteSpace(text.text)) return;
                Speech.Say(text.text, interrupt: true);
            }
            catch (Exception e) { CoreLog.Warning("RunGlance: timer failed: " + e.Message); }
        }

        // Whether the game draws a text right now: active, not transparent, and under no faded-out
        // CanvasGroup (a group that ignores its parents ends the walk, as it does for Unity).
        private static bool Drawn(TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || text.alpha <= 0f) return false;
            for (var t = text.transform; t != null; t = t.parent)
            {
                var group = t.GetComponent<UnityEngine.CanvasGroup>();
                if (group == null) continue;
                if (group.alpha <= 0f) return false;
                if (group.ignoreParentGroups) break;
            }
            return true;
        }

        private static void Add(List<string> parts, string title, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            parts.Add(Strings.GlanceValue(title, text.text));
        }
    }
}
