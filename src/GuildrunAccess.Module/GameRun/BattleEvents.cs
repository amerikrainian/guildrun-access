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
    /// for the run HUD's events stop; deaths and casts are spoken as they happen, the numbers too when
    /// the player asks for them. Nothing the game does not display is reported. Lines are queued from
    /// the hooks and drained on the module tick, so speech and the graph stay on the main thread.
    /// </summary>
    internal static class BattleEvents
    {
        /// <summary>One displayed event.</summary>
        public sealed class Line
        {
            public int Sequence;
            public string Text;
            public bool Key; // a death or a cast: spoken by default
        }

        private const int Capacity = 80;
        private static readonly ConcurrentQueue<Line> _pending = new ConcurrentQueue<Line>();
        private static readonly List<Line> _lines = new List<Line>();
        private static int _sequence;

        /// <summary>The kept lines, oldest first.</summary>
        public static IReadOnlyList<Line> Lines => _lines;

        /// <summary>Forget the lines (a new fight is being set up).</summary>
        public static void Clear() => _lines.Clear();

        // The settings that decide what is spoken; read through the host store each time (edits apply live).
        public static bool NarrateKeyEvents => Setting("narrate_battle", true);
        public static bool NarrateNumbers => Setting("narrate_battle_numbers", false);
        public static void SetNarrateKeyEvents(bool value) => SetSetting("narrate_battle", value);
        public static void SetNarrateNumbers(bool value) => SetSetting("narrate_battle_numbers", value);

        private static bool Setting(string key, bool fallback)
        {
            var store = ModuleMain.Host != null ? ModuleMain.Host.Settings.Store : null;
            return store != null ? store.GetBool(key, fallback) : fallback;
        }

        private static void SetSetting(string key, bool value)
        {
            var store = ModuleMain.Host != null ? ModuleMain.Host.Settings.Store : null;
            if (store != null) store.SetBool(key, value);
        }

        internal static void Add(string text, bool key)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _pending.Enqueue(new Line { Text = text, Key = key });
        }

        /// <summary>Drain the queue on the main thread: keep the lines, speak the chosen ones.</summary>
        public static void Tick()
        {
            while (_pending.TryDequeue(out var line))
            {
                line.Sequence = ++_sequence;
                _lines.Add(line);
                if (_lines.Count > Capacity) _lines.RemoveAt(0);
                bool speak = line.Key ? NarrateKeyEvents : NarrateNumbers;
                if (speak) Speech.Say(line.Text, interrupt: false);
            }
        }

        // ---- the hooks' readers ----

        internal static string UnitName(HealthBarView bar)
        {
            var text = bar != null ? bar._characterNameText : null;
            string name = text != null ? text.text : null;
            return string.IsNullOrWhiteSpace(name) ? Strings.RunBoard : name;
        }

        internal static string UnitName(CharacterViewController unit)
        {
            if (unit == null) return Strings.RunBoard;
            var bar = HealthBar(unit);
            if (bar != null) return UnitName(bar);
            return unit.gameObject.name.Replace("(Clone)", "");
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
                if (changeState.Heal)
                {
                    int healed = Math.Max(changeState.TotalDamage, changeState.HealthDamage);
                    if (healed > 0) BattleEvents.Add(Strings.BattleHealed(unit, healed), key: false);
                    return;
                }
                if (changeState.HasDamage && changeState.TotalDamage > 0)
                    BattleEvents.Add(changeState.IsCrit ? Strings.BattleCrit(unit, changeState.TotalDamage) : Strings.BattleDamage(unit, changeState.TotalDamage), key: false);
                // The bar emptying is how the HUD shows a unit falling.
                if (changeState.CurrentHealth <= 0 && changeState.CurrentShield <= 0 && changeState.HasDamage)
                    BattleEvents.Add(Strings.BattleDefeated(unit), key: true);
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
                string status = Strings.Status(type.ToString());
                BattleEvents.Add(stackCount > 0 ? Strings.BattleStatus(unit, status, stackCount) : Strings.BattleStatusGone(unit, status), key: false);
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
                BattleEvents.Add(Strings.BattleCast(unit, string.IsNullOrWhiteSpace(tooltipTitle) ? Strings.BattleAbility : tooltipTitle), key: true);
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
                string ability = RunData.ActiveAbilityName(__instance);
                BattleEvents.Add(Strings.BattleCast(unit, ability ?? Strings.BattleAbility), key: true);
            }
            catch (Exception e) { CoreLog.Warning("BattleEvents: animation hook failed: " + e.Message); }
        }
    }
}
