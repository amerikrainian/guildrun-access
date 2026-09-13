using System;
using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Relics;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Buffers;
using Buffer = GuildrunAccess.Core.Buffers.Buffer;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.GameRun;
using GuildrunAccess.Module.Interop;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// The mod's buffer roster and its review keys (Ctrl plus arrows), the Harkest Dungeon pattern:
    /// review lists for the information a focus announcement leaves out. In cycling order: the
    /// focused control's own lines (its head, then one line per tooltip), the hero it concerns, the
    /// items it carries, the run's relics, the party and the enemies of a fight (one line per unit),
    /// and the battle events log, which follows its latest line. Every buffer reads live on each
    /// keypress; a focus change re-homes review to the control's buffer and rewinds the control-fed
    /// ones, since a new control is new content. An empty buffer is skipped by the review keys.
    /// </summary>
    internal static class Buffers
    {
        public static BufferManager Manager { get; private set; }
        public static BufferControls Controls { get; private set; }

        private static Buffer _ui, _hero, _item;
        private static ControlId _homed;

        public static void Init()
        {
            Manager = new BufferManager();
            Controls = new BufferControls(Manager, (text, interrupt) => Speech.Say(text, interrupt: interrupt));

            _ui = Manager.Add(new Buffer(BufferKeys.Ui, () => Strings.BufferUi));
            _ui.SetSource(() => NodeLines.Lines(Navigation.FocusedNode));
            _hero = Manager.Add(new Buffer(BufferKeys.Hero, () => Strings.BufferHero));
            _hero.SetSource(() => NodeLines.SideLines(Navigation.FocusedNode, BufferKeys.Hero));
            _item = Manager.Add(new Buffer(BufferKeys.Item, () => Strings.BufferItem));
            _item.SetSource(() => NodeLines.SideLines(Navigation.FocusedNode, BufferKeys.Item));

            Manager.Add(new Buffer(BufferKeys.Relic, () => Strings.BufferRelic)).SetSource(RelicLines);
            Manager.Add(new Buffer(BufferKeys.Party, () => Strings.BufferParty)).SetSource(() => BoardSection.UnitLines(heroes: true));
            Manager.Add(new Buffer(BufferKeys.Enemies, () => Strings.BufferEnemies)).SetSource(() => BoardSection.UnitLines(heroes: false));
            var combat = Manager.Add(new Buffer(BufferKeys.Combat, () => Strings.BufferCombat));
            combat.FollowLatest = true;
            combat.SetSource(CombatLines);
        }

        /// <summary>Per frame: a focus change (by key or by the game moving things) re-homes review to
        /// the control's own buffer and rewinds the control-fed buffers.</summary>
        public static void Tick()
        {
            if (Manager == null) return;
            var id = Navigation.FocusedNode?.Id;
            if (Equals(id, _homed)) return;
            _homed = id;
            _ui.Reset();
            _hero.Reset();
            _item.Reset();
            Manager.SetCurrent(BufferKeys.Ui);
        }

        // The run's relics: each its tooltip lines (heading, summary, keywords), or its name alone.
        private static IEnumerable<string> RelicLines()
        {
            var relics = GameScopes.Controller<RelicUIController>();
            if (relics == null || !relics.gameObject.activeInHierarchy) yield break;
            foreach (var relic in relics.GetComponentsInChildren<RelicView>(false))
            {
                if (relic == null || !relic.gameObject.activeInHierarchy) continue;
                var lines = TooltipReader.Lines(relic._tooltipRaycastTarget);
                if (lines.Count == 0) { yield return ItemNodes.RelicName(relic); continue; }
                foreach (var line in lines) yield return line;
            }
        }

        // The battle events log, oldest first (the buffer follows the latest line).
        private static IEnumerable<string> CombatLines()
        {
            var lines = BattleEvents.Lines;
            for (int i = 0; i < lines.Count; i++) yield return lines[i].Text;
        }
    }
}
