using System;
using System.Collections.Generic;
using Ember.Balancing.Sheets.Characters;
using Ember.Balancing.Sheets.Characters.Attacks;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.Battle.UI.Hud;
using Ember.Scopes.GameRun.SimulationRelay.Notifications;
using Ember.Scopes.GameRun.Tutorial;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Audio;
using GuildrunAccess.Module.GameRun;
using GuildrunAccess.Module.Interop;
using HarmonyLib;
using UnityEngine;

namespace GuildrunAccess.Module.Audio
{
    /// <summary>
    /// The fight's sound cues: what the battle HUD draws, taken at the moment the game draws it (the
    /// same Harmony postfixes the battle events log hangs on, plus the mana bar and the tutorial's
    /// rush and stall handlers), turned into one <see cref="AudioCue"/> each through the
    /// <see cref="Player"/>, whose per-cue interval thins a rattle to a pulse. A bar's
    /// <c>_isPlayer</c> tells a hero from an enemy. Only a fighting unit's bar counts (the game
    /// drives the same views while it sets a board up), and only while the flow state is a fight.
    /// Nothing is cached about the game: the two edge-triggers (low health, mana full) remember
    /// only which bars have already sounded, cleared at each new placement.
    /// </summary>
    internal static class CombatCues
    {
        /// <summary>The player the hooks play through; null before the module wires one.</summary>
        public static CuePlayer Player;

        private static readonly HashSet<int> _lowBars = new HashSet<int>();
        private static readonly HashSet<int> _fullBars = new HashSet<int>();

        /// <summary>A new fight is being set up: the edge-triggers and the interval clocks start over.</summary>
        public static void Reset()
        {
            _lowBars.Clear();
            _fullBars.Clear();
            Player?.Reset();
        }

        private static void Play(AudioCue cue)
        {
            try { Player?.Play(cue); }
            catch (Exception e) { CoreLog.Warning("CombatCues: " + cue + ": " + e.Message); }
        }

        // A bar that belongs to a fighting unit (named, or a nameless enemy in the Resolution state).
        private static bool Fighting(HealthBarView bar) => BattleEvents.UnitName(bar) != null;

        [HarmonyPatch(typeof(HealthBarView), nameof(HealthBarView.UpdateHealth))]
        private static class HealthPatch
        {
            private static void Postfix(HealthBarView __instance, HealthBarView.HealthChangeState changeState)
            {
                try
                {
                    if (Player == null || !Fighting(__instance)) return;
                    bool hero = __instance._isPlayer;
                    if (changeState.Heal)
                    {
                        if (hero && Math.Max(changeState.TotalDamage, changeState.HealthDamage) > 0) Play(AudioCue.HeroHealed);
                    }
                    else if (changeState.HasDamage && changeState.TotalDamage > 0)
                    {
                        Play(hero ? AudioCue.HeroDamaged : AudioCue.EnemyDamaged);
                        if (changeState.IsCrit) Play(AudioCue.Crit);
                    }
                    bool dead = changeState.CurrentHealth <= 0 && changeState.CurrentShield <= 0 && changeState.HasDamage;
                    if (dead)
                    {
                        Play(hero ? AudioCue.HeroDied : AudioCue.EnemyDied);
                        return;
                    }
                    // Low health: once as the hero crosses below a quarter, again only after it recovers.
                    if (!hero) return;
                    int id = __instance.GetInstanceID();
                    int max = __instance._maxHealth;
                    bool low = max > 0 && changeState.CurrentHealth > 0 && changeState.CurrentHealth * 4 < max;
                    if (low && _lowBars.Add(id)) Play(AudioCue.HeroLowHealth);
                    else if (!low) _lowBars.Remove(id);
                }
                catch (Exception e) { CoreLog.Warning("CombatCues: health hook failed: " + e.Message); }
            }
        }

        [HarmonyPatch(typeof(HealthBarView), nameof(HealthBarView.SetStatusStack))]
        private static class StatusPatch
        {
            private static void Postfix(HealthBarView __instance, StatusType type, int stackCount)
            {
                try
                {
                    if (Player == null || stackCount <= 0 || !__instance._isPlayer || !Fighting(__instance)) return;
                    Play(AudioCue.StatusOnHero);
                }
                catch (Exception e) { CoreLog.Warning("CombatCues: status hook failed: " + e.Message); }
            }
        }

        // The mana bar: the ability is ready when the bar fills; sounded as it crosses full, again
        // only after it drops (a cast empties it).
        [HarmonyPatch(typeof(HealthBarView), nameof(HealthBarView.UpdateMana))]
        private static class ManaPatch
        {
            private static void Postfix(HealthBarView __instance, int currentMana)
            {
                try
                {
                    if (Player == null || !__instance._isPlayer || !Fighting(__instance)) return;
                    var slider = __instance._manaSlider;
                    if (slider == null || slider.maxValue <= 0f) return;
                    int id = __instance.GetInstanceID();
                    bool full = currentMana >= slider.maxValue;
                    if (full && _fullBars.Add(id)) Play(AudioCue.ManaFull);
                    else if (!full) _fullBars.Remove(id);
                }
                catch (Exception e) { CoreLog.Warning("CombatCues: mana hook failed: " + e.Message); }
            }
        }

        // An enemy's ability icon over its bar is its cast (a hero's cast is its skill animation below).
        [HarmonyPatch(typeof(HealthBarView), nameof(HealthBarView.ShowAbilityIcon))]
        private static class EnemyCastPatch
        {
            private static void Postfix(HealthBarView __instance, Sprite icon, string tooltipTitle, string tooltipDescription)
            {
                try
                {
                    if (Player == null || __instance._isPlayer || !Fighting(__instance)) return;
                    Play(AudioCue.EnemyCast);
                }
                catch (Exception e) { CoreLog.Warning("CombatCues: ability icon hook failed: " + e.Message); }
            }
        }

        [HarmonyPatch(typeof(CharacterViewController), "HandleAnimationAudio")]
        private static class HeroCastPatch
        {
            private static void Postfix(CharacterViewController __instance, AnimationType animationType, float timeScale)
            {
                try
                {
                    if (Player == null || animationType != AnimationType.SkillAttack) return;
                    if (!Nullables.TryGet(() => __instance.HeroId, out _)) return; // an enemy: its icon sounds it
                    if (BattleEvents.UnitName(__instance) == null) return;
                    Play(AudioCue.HeroCast);
                }
                catch (Exception e) { CoreLog.Warning("CombatCues: animation hook failed: " + e.Message); }
            }
        }

        // Rush and stall activations reach the tutorial's subscriber whether or not a tutorial runs
        // (the notification is published by generic relay methods no patch can name): its handlers
        // are the one non-generic site that sees every one.
        [HarmonyPatch(typeof(TutorialServiceInitializer), "_HandleNotifications_b__23_7")]
        private static class RushPatch
        {
            private static void Postfix(RushEffectActivatedNotification notification)
            {
                try { if (Player != null) Play(AudioCue.RushStarted); }
                catch (Exception e) { CoreLog.Warning("CombatCues: rush hook failed: " + e.Message); }
            }
        }

        [HarmonyPatch(typeof(TutorialServiceInitializer), "_HandleNotifications_b__23_6")]
        private static class StallPatch
        {
            private static void Postfix(StallEffectActivatedNotification notification)
            {
                try { if (Player != null) Play(AudioCue.StallStarted); }
                catch (Exception e) { CoreLog.Warning("CombatCues: stall hook failed: " + e.Message); }
            }
        }
    }
}
