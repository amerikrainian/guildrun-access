using System;
using System.Collections.Generic;
using System.Globalization;
using Ember.Balancing.Sheets.Items;
using Ember.Balancing.SimulationBridge;
using Ember.Scopes.Application.Utilities;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.Battle.UI.Hud;
using Ember.Scopes.GameRun.GameRegistry.Data.Characters;
using Ember.Scopes.GameRun.UI.EnemyCard;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using Ember.Simulation.Core.State.Components.Stats;
using Ember.Simulation.Core.State.Entities;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Photon.Deterministic;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The glance keys: a digit speaks one fact group of the unit the focused control concerns (its
    /// <see cref="Core.Graph.NodeVtable.Subject"/>: a card, a slot, a board unit, a cell's occupant),
    /// in place, focus unmoved, everything read at the keypress. While the unit stands on the board
    /// with its bar up the numbers are the fight's: health, shield and mana from the bar, the stats
    /// from the simulation entity with its temporary bonuses, the statuses from the bar's icons, the
    /// target from the entity. Anywhere else the registry's sheet answers (a hero's or an enemy's
    /// data, a picker card's own), and the fight-only groups are silent. A control that concerns no
    /// unit is silent too: the absence is the answer.
    /// <para>A stat is read as the <see cref="CharacterStat"/> struct off the unit's stats component,
    /// never through the <c>IReadOnlyCharacterStat</c> interface proxy <c>GetStat</c> returns: that
    /// proxy misreads the boxed struct (the value carries the stat type in its low bits, the base
    /// reads zero, the integer flag false).</para>
    /// </summary>
    internal static class UnitGlance
    {
        public enum Group { Vitals, Attack, Tempo, Sustain, Statuses, Target }

        // The groups in the card's own panel order.
        private static readonly TargetStatType[] AttackStats = { TargetStatType.BaseAttackDamage, TargetStatType.Attack, TargetStatType.Magic, TargetStatType.Defense };
        private static readonly TargetStatType[] TempoStats = { TargetStatType.AttackSpeed, TargetStatType.Crit, TargetStatType.AttackRange, TargetStatType.MoveSpeed };
        private static readonly TargetStatType[] SustainStats =
        {
            TargetStatType.ManaRegen, TargetStatType.HPPerSecond, TargetStatType.OmniVamp,
            TargetStatType.StartingMana, TargetStatType.FrostResistance, TargetStatType.StunResistance,
        };

        // The unit a subject stands for: its board view when it has one, its registry id, a card's
        // own data where the registry has no id for it, or just the card, whose drawn panels are the
        // last resort (a shop's or the run-start picker's offers hold no data object at all).
        private struct Target
        {
            public CharacterViewController View;
            public bool HasHero; public HeroId Hero;
            public bool HasEnemy; public EnemyId Enemy;
            public HeroData HeroSheet;
            public HeroCardView Card;
        }

        // The sheet's answers outside a fight: a hero's or an enemy's registry data, which share the
        // stats component and the current health and mana but no interop base type.
        private sealed class Sheet
        {
            public CharacterStatsComponent Stats;
            public int Health;
            public int Mana;
        }

        public static void Speak(Group group, bool detail = false)
        {
            try
            {
                var subject = Navigation.FocusedNode?.Vtable?.Subject;
                if (subject == null) return;
                if (!Resolve(subject(), out var target)) return;
                string line = Line(target, group, detail);
                if (!string.IsNullOrEmpty(line)) Speech.Say(line, interrupt: true);
            }
            catch (Exception e) { CoreLog.Warning("UnitGlance: " + group + " failed: " + e.Message); }
        }

        private static bool Resolve(object subject, out Target t)
        {
            t = default;
            switch (subject)
            {
                case CharacterViewController c:
                    t.View = c;
                    t.HasHero = Nullables.TryGet(() => c.HeroId, out t.Hero);
                    if (!t.HasHero) t.HasEnemy = Nullables.TryGet(() => c.EnemyId, out t.Enemy);
                    break;
                case HeroCardView card:
                    t.Card = card;
                    t.HasHero = Nullables.TryGet(() => card.HeroId, out t.Hero);
                    if (!t.HasHero) t.HasEnemy = Nullables.TryGet(() => card.EnemyId, out t.Enemy);
                    if (!t.HasHero && !t.HasEnemy && card._heroData != null) t.HeroSheet = card._heroData.TryCast<HeroData>();
                    break;
                case BottomHeroView view:
                    t.HasHero = RunData.TryHeroId(view, out t.Hero);
                    break;
                case EnemyCardView enemy:
                    t.HasEnemy = Nullables.TryGet(() => enemy.EnemyId, out t.Enemy);
                    break;
                case HeroId hero:
                    t.HasHero = true; t.Hero = hero;
                    break;
                case EnemyId enemy:
                    t.HasEnemy = true; t.Enemy = enemy;
                    break;
                default:
                    return false;
            }
            if (t.View == null) t.View = ViewOf(t);
            return t.HasHero || t.HasEnemy || t.HeroSheet != null || t.Card != null;
        }

        // The board's character view for a registry id, when the unit stands on the board.
        private static CharacterViewController ViewOf(Target t)
        {
            var board = RunData.BoardController;
            if (board == null) return null;
            CharacterViewController c;
            if (t.HasHero && board.CharacterViewControllers != null && board.CharacterViewControllers.TryGetValue(t.Hero, out c)) return c;
            if (t.HasEnemy && board._enemyViewControllers != null && board._enemyViewControllers.TryGetValue(t.Enemy, out c)) return c;
            return null;
        }

        // The unit's simulation entity while it stands on the board with its bar up, else null: the
        // frame exists only once the fight runs (none during placement), and between fights the
        // battle scope keeps a stale one, while the bar is what makes a unit a fighting one
        // everywhere else in the mod.
        private static Entity Live(Target t, out HealthBarView bar)
        {
            bar = BoardSection.BarOf(t.View);
            if (bar == null) return null;
            var battle = GameScopes.Controller<BattleUIController>();
            var reader = battle != null ? battle._simulationDataReader : null;
            var frame = reader != null ? reader.CurrentFrame : null;
            Entity entity;
            return frame != null && frame.TryGetEntity(t.View.EntityId, out entity) ? entity : null;
        }

        private static Sheet SheetOf(Target t)
        {
            var hero = t.HeroSheet ?? (t.HasHero ? RunData.Hero(t.Hero) : null);
            if (hero != null) return new Sheet { Stats = hero.Stats, Health = hero.CurrentHealth, Mana = hero.CurrentMana };
            var enemy = t.HasEnemy ? RunData.Enemy(t.Enemy) : null;
            if (enemy != null) return new Sheet { Stats = enemy.Stats, Health = enemy.CurrentHealth, Mana = enemy.CurrentMana };
            return null;
        }

        private static string Line(Target t, Group group, bool detail)
        {
            var entity = Live(t, out var bar);
            switch (group)
            {
                case Group.Vitals: return Vitals(t, bar);
                case Group.Attack: return Stats(t, entity, AttackStats, detail, all: true);
                case Group.Tempo: return Stats(t, entity, TempoStats, detail, all: true);
                case Group.Sustain: return Stats(t, entity, SustainStats, detail, all: false);
                case Group.Statuses: return bar != null ? Join(BoardSection.Statuses(bar)) : null;
                case Group.Target: return entity != null ? TargetLine(entity) : null;
            }
            return null;
        }

        // "health 450/650, shield 40, mana 30/85": the bar's numbers in a fight (what the game draws),
        // the sheet's current health and mana against its max stats otherwise.
        private static string Vitals(Target t, HealthBarView bar)
        {
            var parts = new List<string>();
            if (bar != null)
            {
                parts.Add(Strings.GlancePair(Strings.HeroHealth, N(bar._currentHealth), N(bar._maxHealth)));
                if (bar._currentShield > 0) parts.Add(Strings.RunShield(N(bar._currentShield)));
                string mana = BoardSection.Mana(bar);
                if (mana != null) parts.Add(mana);
                return Join(parts);
            }
            var sheet = SheetOf(t);
            if (sheet == null) return t.Card != null ? HeroCardNodes.Vitals(t.Card) : null;
            parts.Add(Strings.GlancePair(Strings.HeroHealth, N(sheet.Health), Total(StatOf(sheet.Stats, TargetStatType.MaxHealth))));
            parts.Add(Strings.GlancePair(Strings.HeroMana, N(sheet.Mana), Total(StatOf(sheet.Stats, TargetStatType.MaxMana))));
            return Join(parts);
        }

        // "Base Attack Damage 41, Attack 0, Magic 0, Defense 42": a group's stats by the game's names,
        // the fight's values from the entity (temporary bonuses included), the sheet's otherwise.
        // Zeros are dropped from an optional group, as the card hides those panels. With detail, each
        // stat's breakdown follows it when it has a bonus: base, the rank bonus and the other
        // bonuses, the nonzero ones.
        private static string Stats(Target t, Entity entity, TargetStatType[] types, bool detail, bool all)
        {
            CharacterStatsComponent stats;
            if (entity != null) stats = entity.Stats;
            else
            {
                var sheet = SheetOf(t);
                if (sheet == null) return t.Card != null ? PanelStats(t.Card, types, all) : null;
                stats = sheet.Stats;
            }
            var parts = new List<string>();
            foreach (var type in types)
            {
                var s = StatOf(stats, type);
                bool isInt = s.IsIntValue;
                string total = Fmt(s.Value, isInt);
                if (!all && total == "0") continue;
                string name = StatTextHelper.GetStatName(type);
                if (string.IsNullOrWhiteSpace(name)) name = type.ToString();
                string head = Strings.HeroStat(name, total);
                if (!detail || !s.HasBonus) { parts.Add(head); continue; }
                var sub = new List<string> { head, Strings.GlanceBase(Fmt(s.BaseValue, isInt)) };
                string rank = Fmt(s.RankBonus, isInt);
                if (rank != "0") sub.Add(Strings.GlanceRank(rank));
                string bonus = Fmt(s.TotalBonusWithoutRankBonus, isInt);
                if (bonus != "0") sub.Add(Strings.GlanceBonus(bonus));
                parts.Add(string.Join(", ", sub));
            }
            return parts.Count > 0 ? string.Join(detail ? "; " : ", ", parts) : null;
        }

        // A data-less card's group: the card's own stat panels by type, each the text the game draws
        // on it, the inactive ones (the card hides a panel it has no value for) and, in an optional
        // group, the zeros left out. No breakdown here: nothing stands behind the panels but their
        // tooltips, which the control buffer already holds.
        private static string PanelStats(HeroCardView card, TargetStatType[] types, bool all)
        {
            var views = card._statViews;
            if (views == null) return null;
            var parts = new List<string>();
            foreach (var type in types)
            {
                StatView view;
                if (!views.TryGetValue(type, out view) || view == null || !view.gameObject.activeInHierarchy) continue;
                var text = view._statText;
                if (text == null || string.IsNullOrWhiteSpace(text.text)) continue;
                if (!all && HeroCardNodes.IsZero(text.text)) continue;
                string name = StatTextHelper.GetStatName(type);
                if (string.IsNullOrWhiteSpace(name)) name = type.ToString();
                parts.Add(Strings.HeroStat(name, text.text));
            }
            return Join(parts);
        }

        // The stat struct of a type off a stats component (the component's own properties, one per
        // type; the None type reads as an empty stat).
        private static CharacterStat StatOf(CharacterStatsComponent stats, TargetStatType type)
        {
            switch (type)
            {
                case TargetStatType.MaxHealth: return stats.MaxHealth;
                case TargetStatType.MaxMana: return stats.MaxMana;
                case TargetStatType.MoveSpeed: return stats.MoveSpeed;
                case TargetStatType.Defense: return stats.Defense;
                case TargetStatType.BaseAttackDamage: return stats.BaseAttackDamage;
                case TargetStatType.Attack: return stats.Attack;
                case TargetStatType.Magic: return stats.Magic;
                case TargetStatType.BaseAttackSpeed: return stats.BaseAttackSpeed;
                case TargetStatType.AttackSpeed: return stats.AttackSpeed;
                case TargetStatType.AttackRange: return stats.AttackRange;
                case TargetStatType.Crit: return stats.Crit;
                case TargetStatType.ManaRegen: return stats.ManaRegen;
                case TargetStatType.OmniVamp: return stats.OmniVamp;
                case TargetStatType.DamageAmp: return stats.DamageAmp;
                case TargetStatType.HPPerSecond: return stats.HPPerSecond;
                case TargetStatType.BaseTrueDamage: return stats.BaseTrueDamage;
                case TargetStatType.BonusTrueDamage: return stats.BonusTrueDamage;
                case TargetStatType.StartingMana: return stats.StartingMana;
                case TargetStatType.FrostResistance: return stats.FrostResistance;
                case TargetStatType.StunResistance: return stats.StunResistance;
                default: return default;
            }
        }

        // "attacking Goblin": the entity's target of the moment, by the name of its board view.
        private static string TargetLine(Entity entity)
        {
            if (!Nullables.TryGet(() => entity.CurrentTargetId, out EntityId id)) return Strings.GlanceNoTarget;
            var board = RunData.BoardController;
            if (board == null) return Strings.GlanceNoTarget;
            var view = Find(BoardSection.ViewsOf(board.CharacterViewControllers), id) ?? Find(BoardSection.ViewsOf(board._enemyViewControllers), id);
            string name = view != null ? RunData.UnitName(view) : null;
            return name != null ? Strings.GlanceTarget(name) : Strings.GlanceNoTarget;
        }

        private static CharacterViewController Find(Il2CppReferenceArray<CharacterViewController> views, EntityId id)
        {
            if (views == null) return null;
            for (int i = 0; i < views.Length; i++)
            {
                var c = views[i];
                if (c != null && c.EntityId.Equals(id)) return c;
            }
            return null;
        }

        private static string Total(CharacterStat stat) => Fmt(stat.Value, stat.IsIntValue);

        // A fixed-point value as the card shows it: whole for an integer stat, else up to two decimals.
        private static string Fmt(FP value, bool isInt)
        {
            double d = value.RawValue / 65536.0;
            return isInt ? Math.Round(d).ToString(CultureInfo.InvariantCulture) : d.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static string Join(List<string> parts) => parts != null && parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
