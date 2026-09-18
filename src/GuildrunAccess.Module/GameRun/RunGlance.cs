using System;
using System.Collections.Generic;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.GameRun.UI;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
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
    /// crossroads and the events, so the last fight's time is spoken there too. Ctrl+N the units
    /// near the focused cell or hero while placing, Ctrl+H the hostile ones (see
    /// <see cref="Nearby"/>). Ctrl+Q the quests of the focused hero, item or relic, Ctrl+M a Red Rift
    /// run's missions. Each is silent where its fact is not on screen: outside a run, off the
    /// board, the timer's text hidden.</summary>
    internal static class RunGlance
    {
        // Each glance is a LINE, read at the keypress: its key speaks the line (Say), and the key help
        // lists the key only where there is a line to speak (Has). Null is the silence.

        /// <summary>Speak a glance's line in place, interrupting. A failure is logged.</summary>
        public static void Say(string name, Func<string> line)
        {
            try
            {
                string text = line();
                if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt: true);
            }
            catch (Exception e) { CoreLog.Warning("RunGlance: " + name + " failed: " + e.Message); }
        }

        /// <summary>Whether the glance has anything to say right now.</summary>
        public static bool Has(string name, Func<string> line)
        {
            try { return !string.IsNullOrEmpty(line()); }
            catch (Exception e) { CoreLog.Warning("RunGlance: " + name + " failed: " + e.Message); return false; }
        }

        public static string ShardsLine()
        {
            var info = GameScopes.Controller<BasicInfoUIPanelController>();
            if (info == null || !info.gameObject.activeInHierarchy) return null;
            var parts = new List<string>();
            Add(parts, TooltipReader.Title(info._currentGoldTooltip) ?? Strings.RunGold, info._currentGoldText);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        public static string PositionLine()
            => BoardSection.TryFocusedCell(out var cell) ? RunLabels.CellName(cell) : UnitGlance.LineFor(UnitGlance.Group.Position);

        /// <summary>Ctrl+N / Ctrl+H: the units round the focused cell, or round the cell the focused
        /// control's hero stands on, each with its distance in hex steps, nearest first and by name
        /// within a distance ("Mushroom Tank 2, Slime 1 3": the last number is the distance); every unit, or the hostile ones alone.
        /// Hostile is the origin's unit's word: the enemies from a hero, the heroes from an enemy,
        /// and from an empty cell, where nobody stands to have any, silence (Ctrl+N answers there).
        /// The origin's own occupant is left out. Placement only: a fight moves its units off the
        /// registry's cells, and there the keys are silent, as they are off the board.</summary>
        public static string NearbyLine(bool hostilesOnly)
        {
            if (!RunData.Placing()) return null;
            if (!BoardSection.TryFocusedCell(out var from) && !UnitGlance.TryFocusedUnitCell(out from)) return null;
            var board = RunData.Board();
            if (board == null) return null;
            int w = board.BoardWidth, h = board.BoardHeight;
            bool heroes = !hostilesOnly || RunData.TryEnemyAt(from, out _);
            bool enemies = !hostilesOnly || RunData.TryHeroAt(from, out _);
            if (!heroes && !enemies) return null;
            var numbers = enemies ? EnemyNumbers.Read() : null;
            var units = new List<KeyValuePair<int, string>>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x == from.x && y == from.y) continue;
                    var cell = new UnityEngine.Vector2Int(x, y);
                    string name;
                    if (enemies && RunData.TryEnemyAt(cell, out var enemy)) name = RunData.EnemyLabel(enemy, numbers) ?? Strings.RunBoard;
                    else if (heroes && RunData.TryHeroAt(cell, out var hero)) name = RunData.HeroName(hero) ?? Strings.RunParty;
                    else continue;
                    units.Add(new KeyValuePair<int, string>(HexGrid.Distance(from.x, from.y, x, y), name));
                }
            if (units.Count == 0) return null;
            units.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : string.Compare(a.Value, b.Value, StringComparison.CurrentCultureIgnoreCase));
            var parts = new List<string>();
            foreach (var unit in units) parts.Add(Strings.GlanceNearby(unit.Value, unit.Key));
            return string.Join(", ", parts);
        }

        /// <summary>Ctrl+Q: the quests of what is focused, in place: a hero's (the quests of the items
        /// it wears, a slot's, a card's, a cell's or a fighting unit's alike: "Rift Seal: Tank or
        /// Vanguard, 1 / 3; ..."), or the focused item's or relic's own. The count is the game's
        /// progress bar text, read at the keypress. The rewards stay in the quests buffer. Silent
        /// when nothing focused has a quest.</summary>
        public static string QuestsLine()
        {
            var lines = new List<string>(Core.Buffers.NodeLines.SideLines(Core.UI.Navigation.FocusedNode, Core.Buffers.BufferKeys.QuestBrief));
            return lines.Count > 0 ? string.Join(". ", lines) : null;
        }

        /// <summary>Ctrl+M: a Red Rift run's missions from anywhere in the run, the sidebar's panel
        /// shown or not: "Red Rift Missions - 2/6", then each mission with its state. Silent outside
        /// a Red Rift run.</summary>
        public static string MissionsLine()
        {
            var lines = MissionNodes.Lines();
            return lines.Count > 0 ? string.Join(". ", lines) : null;
        }

        public static string TimerLine()
        {
            var timer = GameScopes.Controller<BattleTimerController>();
            var text = timer != null ? timer._timerText : null;
            return Drawn(text) && !string.IsNullOrWhiteSpace(text.text) ? text.text : null;
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
