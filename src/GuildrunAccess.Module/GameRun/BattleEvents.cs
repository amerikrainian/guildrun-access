using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ember.Balancing.Sheets.Characters;
using Ember.Balancing.Sheets.Characters.Attacks;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.Battle.UI.Hud;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using HarmonyLib;
using UnityEngine;
using GuildrunAccess.Module.Interop;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// What the battle HUD shows as it happens, taken at the moment the game draws it (Harmony postfixes
    /// on the HUD views): a health bar's floating damage or heal number, a status icon's stack count,
    /// an enemy's ability icon, a unit's death animation, a hero's cast animation. Each becomes a line
    /// for the run HUD's events stop, the battle events log, and NOTHING is spoken as it happens: a
    /// fight throws off several events a second, and the log is where the player reads them at their
    /// own pace. Nothing the game does not display is reported. Lines are queued from the hooks and
    /// drained on the module tick, so the graph stays on the main thread.
    /// </summary>
    internal static class BattleEvents
    {
        /// <summary>One displayed event.</summary>
        public sealed class Line
        {
            public int Sequence;
            public string Text;
        }

        private const int Capacity = 80;
        private static readonly ConcurrentQueue<Line> _pending = new ConcurrentQueue<Line>();
        private static readonly List<Line> _lines = new List<Line>();
        private static int _sequence;

        /// <summary>The kept lines, oldest first.</summary>
        public static IReadOnlyList<Line> Lines => _lines;

        /// <summary>Forget the lines (a new fight is being set up).</summary>
        public static void Clear() => _lines.Clear();

        internal static void Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _pending.Enqueue(new Line { Text = text });
        }

        /// <summary>Drain the queue on the main thread into the kept lines.</summary>
        public static void Tick()
        {
            while (_pending.TryDequeue(out var line))
            {
                line.Sequence = ++_sequence;
                _lines.Add(line);
                if (_lines.Count > Capacity) _lines.RemoveAt(0);
            }
        }

        // ---- the hooks' readers ----

        // The unit a bar belongs to, by the name the bar shows. The game drives the same bar views
        // while it sets a board up (an ability icon shown on a bar whose name is not filled in yet, as
        // placement opens), and those are not fight events: a hook drops its line rather than narrate
        // "battlefield casts ...". An enemy the game's data leaves nameless keeps a blank bar all fight
        // long, so a blank bar is named through the registry instead, while the fight itself runs.
        internal static string UnitName(HealthBarView bar) => UnitName(bar, null);

        internal static string UnitName(HealthBarView bar, CharacterViewController unit)
        {
            var text = bar != null ? bar._characterNameText : null;
            string name = text != null ? text.text : null;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            if (bar == null || RunData.FlowState() != Ember.Scopes.GameRun.RunSession.Data.BattleFlowState.Resolution) return null;
            if (unit == null) unit = BoardSection.UnitOf(bar);
            return unit != null ? RunData.UnitName(unit) : null;
        }

        // A unit is fighting when it has a named bar; a character view animating without one (placement,
        // a rank-up flourish) is not narrated.
        internal static string UnitName(CharacterViewController unit)
        {
            if (unit == null) return null;
            var bar = HealthBar(unit);
            return bar != null && bar.gameObject.activeInHierarchy ? UnitName(bar, unit) : null;
        }

        private static HealthBarView HealthBar(CharacterViewController unit)
        {
            try
            {
                var battle = GameScopes.Controller<Ember.Scopes.Battle.UI.BattleUIController>();
                var bars = battle != null ? battle._healthBars : null;
                HealthBarView bar;
                return bars != null && bars.TryGetValue(unit.EntityId, out bar) ? bar : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>A health bar's floating number: damage (crit or not), shield damage, or a heal.</summary>
    [HarmonyPatch(typeof(HealthBarView), nameof(HealthBarView.UpdateHealth))]
    internal static class HealthBarUpdatePatch
    {
        private static void Postfix(HealthBarView __instance, HealthBarView.HealthChangeState changeState)
        {
            try
            {
                string unit = BattleEvents.UnitName(__instance);
                if (unit == null) return; // not a fighting unit: no named bar
                if (changeState.Heal)
                {
                    int healed = Math.Max(changeState.TotalDamage, changeState.HealthDamage);
                    if (healed > 0) BattleEvents.Add(Strings.BattleHealed(unit, healed));
                    return;
                }
                if (changeState.HasDamage && changeState.TotalDamage > 0)
                    BattleEvents.Add(changeState.IsCrit ? Strings.BattleCrit(unit, changeState.TotalDamage) : Strings.BattleDamage(unit, changeState.TotalDamage));
                // The bar emptying is how the HUD shows a unit falling.
                if (changeState.CurrentHealth <= 0 && changeState.CurrentShield <= 0 && changeState.HasDamage)
                    BattleEvents.Add(Strings.BattleDefeated(unit));
            }
            catch (Exception e) { CoreLog.Warning("BattleEvents: health hook failed: " + e.Message); }
        }
    }

    /// <summary>A status icon's stack count on a health bar (shown or cleared).</summary>
    [HarmonyPatch(typeof(HealthBarView), nameof(HealthBarView.SetStatusStack))]
    internal static class HealthBarStatusPatch
    {
        private static void Postfix(HealthBarView __instance, StatusType type, int stackCount)
        {
            try
            {
                string unit = BattleEvents.UnitName(__instance);
                if (unit == null) return; // not a fighting unit: no named bar
                string status = Strings.Status(type.ToString());
                BattleEvents.Add(stackCount > 0 ? Strings.BattleStatus(unit, status, stackCount) : Strings.BattleStatusGone(unit, status));
            }
            catch (Exception e) { CoreLog.Warning("BattleEvents: status hook failed: " + e.Message); }
        }
    }

    /// <summary>An enemy's ability icon appearing over its bar, with the game's own title for it.</summary>
    [HarmonyPatch(typeof(HealthBarView), nameof(HealthBarView.ShowAbilityIcon))]
    internal static class HealthBarAbilityPatch
    {
        private static void Postfix(HealthBarView __instance, Sprite icon, string tooltipTitle, string tooltipDescription)
        {
            try
            {
                string unit = BattleEvents.UnitName(__instance);
                if (unit == null) return; // not a fighting unit: no named bar
                BattleEvents.Add(Strings.BattleCast(unit, string.IsNullOrWhiteSpace(tooltipTitle) ? Strings.BattleAbility : tooltipTitle));
            }
            catch (Exception e) { CoreLog.Warning("BattleEvents: ability icon hook failed: " + e.Message); }
        }
    }

    /// <summary>A unit's skill animation (its active ability), by the animation the game plays.</summary>
    [HarmonyPatch(typeof(CharacterViewController), "HandleAnimationAudio")]
    internal static class CharacterAnimationPatch
    {
        private static void Postfix(CharacterViewController __instance, AnimationType animationType, float timeScale)
        {
            try
            {
                if (animationType != AnimationType.SkillAttack) return;
                string unit = BattleEvents.UnitName(__instance);
                if (unit == null) return; // not a fighting unit: no named bar
                string ability = RunData.ActiveAbilityName(__instance);
                BattleEvents.Add(Strings.BattleCast(unit, ability ?? Strings.BattleAbility));
            }
            catch (Exception e) { CoreLog.Warning("BattleEvents: animation hook failed: " + e.Message); }
        }
    }
}
