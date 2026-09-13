using System;
using System.Collections.Generic;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.Battle.UI.Hud;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The "board" stop, in one of two shapes. While placing: the grid, every cell as a node (enemy rows
    /// first, then the player's, each side a region), Enter dropping a picked-up hero or opening the
    /// occupant's menu. During a fight: every unit with a health bar, heroes first, with health and mana.
    /// </summary>
    internal sealed class BoardSection : ScreenSection
    {
        private readonly HeroActions _actions;

        public BoardSection(HeroActions actions) { _actions = actions; }

        public override void Build(GraphBuilder b)
        {
            if (RunData.Placing()) BuildGrid(b);
            else BuildBattlefield(b);
        }

        // ---- the placement grid ----

        private void BuildGrid(GraphBuilder b)
        {
            var board = RunData.Board();
            if (board == null) return;
            int w, h;
            try { w = board.BoardWidth; h = board.BoardHeight; }
            catch (Exception) { return; }
            int playerRows = PlayerRows(h);
            b.BeginStop("board");
            b.PushContext(Strings.RunGrid, null, positions: false);
            b.SetRegion("run:board:enemies");
            b.PushContext(Strings.RunBoardEnemies, null, positions: false);
            for (int y = h - 1; y >= playerRows; y--) AddGridRow(b, w, y);
            b.PopContext();
            b.SetRegion("run:board:heroes");
            b.PushContext(Strings.RunBoardHeroes, null, positions: false);
            for (int y = playerRows - 1; y >= 0; y--) AddGridRow(b, w, y);
            b.PopContext();
            b.SetRegion(null);
            b.PopContext();
        }

        private void AddGridRow(GraphBuilder b, int width, int y)
        {
            b.StartRow("grid");
            for (int x = 0; x < width; x++)
                b.AddItem(ControlId.Structural("run:cell:" + x + ":" + y), CellNode(new Vector2Int(x, y)));
            b.EndRow();
        }

        // How many rows from the bottom belong to the player (the placeable range).
        private static int PlayerRows(int height)
        {
            int rows = 0;
            for (int y = 0; y < height; y++)
                if (RunData.IsPlayerCell(new Vector2Int(0, y))) rows++;
                else break;
            return rows;
        }

        // "Pimenta, wearing Freezing Tome, 5, 1" / "Snake, 3, 6" / "empty, 1, 3": column then row on
        // the game's one grid, the container saying whose side it is.
        private NodeVtable CellNode(Vector2Int cell)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => Occupant(cell) ?? Strings.RunCellEmpty, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => RunLabels.CellName(cell), kind: AnnouncementKinds.Value),
                },
                SearchText = () => Occupant(cell),
                OnActivate = () => ActivateCell(cell),
                // Landing on an occupied cell shows its card in the sidebar, as the mouse hovering it
                // does; the buffers below read that card, so what the inspect panel shows is what
                // review reads, without leaving the grid.
                OnFocus = () => Peek(cell),
                Details = () => CellDetails(cell),
                SideLines = HeroLines.Side(() => CellHeroLines(cell),
                    () => { var view = RunData.TryHeroAt(cell, out var id) ? RunData.ViewOf(id) : null; return view != null ? ItemNodes.ItemTooltips(view._itemSlotViews) : null; }),
            };
        }

        private static void Peek(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var hero)) HeroActions.PeekHero(hero);
            else if (RunData.TryEnemyAt(cell, out var enemy)) HeroActions.PeekEnemy(enemy);
        }

        // The control buffer: a hero's abilities and items (its slot), plus its card's stat tooltips
        // when the sidebar shows it; an enemy's abilities and stats from its card.
        private static IEnumerable<string> CellDetails(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var id))
            {
                var view = RunData.ViewOf(id);
                var lines = view != null ? RunLabels.SlotTooltips(view) : new List<string>();
                var card = HeroActions.ShownHeroCard(RunData.HeroName(id));
                if (card != null) lines.AddRange(HeroCardNodes.StatsTooltips(card));
                return lines;
            }
            if (RunData.TryEnemyAt(cell, out var enemy))
            {
                var card = HeroActions.ShownEnemyCard(RunData.EnemyName(enemy));
                if (card == null) return null;
                var lines = HeroCardNodes.AbilitiesTooltips(card);
                lines.AddRange(SidebarNodes.StatTooltips(card));
                return lines;
            }
            return null;
        }

        // The hero buffer: the card's rows as the sidebar reads them (name with health and mana, the
        // abilities line, the stats line); a hero without its card shown falls back to its slot, an
        // enemy to its unit line.
        private static IEnumerable<string> CellHeroLines(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var id))
            {
                var card = HeroActions.ShownHeroCard(RunData.HeroName(id));
                if (card != null)
                    return GameNodes.Lines(HeroCardNodes.NameAndClass(card), HeroCardNodes.StatsLine(card), HeroCardNodes.AbilitiesLine(card));
                var view = RunData.ViewOf(id);
                return view != null ? HeroLines.ForSlot(view) : null;
            }
            if (RunData.TryEnemyAt(cell, out var enemy))
            {
                var card = HeroActions.ShownEnemyCard(RunData.EnemyName(enemy));
                if (card != null)
                    return GameNodes.Lines(SidebarNodes.EnemyLine(card), HeroCardNodes.AbilitiesLine(card), SidebarNodes.Stats(card));
                string line = UnitLineFor(enemy);
                return line != null ? new[] { line } : null;
            }
            return null;
        }

        private static string Occupant(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var hero))
            {
                string name = RunData.HeroName(hero) ?? Strings.RunParty;
                var view = RunData.ViewOf(hero);
                return view != null ? RunLabels.WithItems(name, view._itemSlotViews) : name;
            }
            if (RunData.TryEnemyAt(cell, out var enemy)) return RunData.EnemyName(enemy) ?? Strings.RunBoard;
            return null;
        }

        // Enter on a cell: drop a picked-up hero here, else open the hero's menu, else inspect the enemy
        // there (its card in the sidebar), else nothing to do.
        private void ActivateCell(Vector2Int cell)
        {
            if (_actions.Moves.Pending) { _actions.Moves.Drop(cell); return; }
            if (RunData.TryHeroAt(cell, out var id))
            {
                _actions.OpenHeroMenu(RunData.ViewOf(id), id, cell, reserve: false);
                return;
            }
            if (RunData.TryEnemyAt(cell, out var enemy))
            {
                _actions.InspectEnemy(enemy);
                return;
            }
            Speech.Say(Strings.RunCellEmpty, interrupt: true);
        }

        // ---- the battlefield ----

        private struct Unit { public string Name; public HealthBarView Bar; public bool IsHero; }

        /// <summary>Whether any unit stands on the battlefield: a character view with a live health bar,
        /// which is the fight itself. The run screen's activity hangs on it (see
        /// <see cref="GameRunScreen.IsActive"/>), so it is polled every frame: first match wins.</summary>
        internal static bool HasUnits()
        {
            var battle = GameScopes.Controller<BattleUIController>();
            var board = RunData.BoardController;
            if (battle == null || board == null || battle._healthBars == null || battle._healthBars.Count == 0) return false;
            return AnyUnit(battle, ViewsOf(board.CharacterViewControllers)) || AnyUnit(battle, ViewsOf(board._enemyViewControllers));
        }

        private static bool AnyUnit(BattleUIController battle, Il2CppReferenceArray<CharacterViewController> views)
        {
            if (views == null) return false;
            var bars = battle._healthBars;
            foreach (var c in views)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                HealthBarView bar;
                if (bars.TryGetValue(c.EntityId, out bar) && bar != null && bar.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        // An empty battlefield declares nothing: the run screen is not active then (a fight has just
        // ended, or the HUD is between panels), so there is no landing to announce.
        private void BuildBattlefield(GraphBuilder b)
        {
            var units = Units();
            if (units.Count == 0) return;
            b.BeginStop("board");
            b.PushContext(Strings.RunBoard, Strings.RoleList);
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                b.AddItem(ControlId.Structural("run:unit:" + u.Bar.GetInstanceID()), new NodeVtable
                {
                    // Units fall all through a fight: when the focused one does, focus slides to a
                    // neighbour without reading it out (the death is in the battle events log).
                    QuietVanish = true,
                    // Resting on a unit follows its fight: the line is re-read as its health, shield or
                    // mana change (coalesced, interrupting, so the latest state is what is heard).
                    LiveReadout = true,
                    Announcements = new List<NodeAnnouncement>
                    {
                        // "Pimenta, wearing Freezing Tome, hero, 650 health": the hero named as on the board.
                        new NodeAnnouncement(() => Strings.RunUnit(u.IsHero, RunLabels.WithItems(u.Name, u.Bar._itemSlotViews), Health(u.Bar)), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => Shield(u.Bar), kind: AnnouncementKinds.Value),
                        // Mana regenerates all fight long: it rides along in a re-read but never causes one.
                        new NodeAnnouncement(() => Mana(u.Bar), kind: AnnouncementKinds.Value) { LiveReadoutIgnore = true },
                    },
                    SearchText = () => u.Name,
                    Details = () => ItemNodes.ItemTooltips(u.Bar._itemSlotViews),
                    SideLines = HeroLines.Side(() => new[] { UnitLine(u) }, () => ItemNodes.ItemTooltips(u.Bar._itemSlotViews)),
                });
            }
            b.PopContext();
        }

        // The board controller's own registries of character views (heroes by hero id, enemies by
        // enemy id), matched to the HUD's health bars by entity id.
        private static List<Unit> Units()
        {
            var units = new List<Unit>();
            var battle = GameScopes.Controller<BattleUIController>();
            var board = RunData.BoardController;
            if (battle == null || board == null || battle._healthBars == null) return units;
            AddUnits(units, battle, ViewsOf(board.CharacterViewControllers), isHero: true);
            AddUnits(units, battle, ViewsOf(board._enemyViewControllers), isHero: false);
            units.Sort((x, y) => x.IsHero == y.IsHero ? string.CompareOrdinal(x.Name, y.Name) : (x.IsHero ? -1 : 1));
            return units;
        }

        // A registry's views as an array (the value collection copied out: no interop enumerator).
        private static Il2CppReferenceArray<CharacterViewController> ViewsOf<TKey>(
            Il2CppSystem.Collections.Generic.Dictionary<TKey, CharacterViewController> views)
        {
            try
            {
                if (views == null || views.Count == 0) return null;
                var array = new Il2CppReferenceArray<CharacterViewController>(views.Count);
                views.Values.CopyTo(array, 0);
                return array;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Units: character views unreadable: " + e.Message);
                return null;
            }
        }

        private static void AddUnits(List<Unit> units, BattleUIController battle, Il2CppReferenceArray<CharacterViewController> views, bool isHero)
        {
            if (views == null) return;
            var bars = battle._healthBars;
            foreach (var c in views)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                HealthBarView bar;
                if (!bars.TryGetValue(c.EntityId, out bar) || bar == null || !bar.gameObject.activeInHierarchy) continue;
                string name = bar._characterNameText != null ? bar._characterNameText.text : null;
                if (string.IsNullOrWhiteSpace(name)) name = c.gameObject.name.Replace("(Clone)", "");
                units.Add(new Unit { Name = name, Bar = bar, IsHero = isHero });
            }
        }

        // The bar's own label is an inactive text set once at spawn (the starting health, "<b>520</b>"),
        // never the value of the moment; the bar keeps the live current health and shield as fields,
        // updated with every hit and heal it draws.
        // "Karsu, hero, 650 health, shield 40, mana 45 of 85": the unit's whole line, for the party
        // and enemies buffers (one line per unit, read live on every buffer key).
        private static string UnitLine(Unit u)
        {
            var parts = new List<string> { Strings.RunUnit(u.IsHero, RunLabels.WithItems(u.Name, u.Bar._itemSlotViews), Health(u.Bar)) };
            string shield = Shield(u.Bar);
            if (shield != null) parts.Add(shield);
            string mana = Mana(u.Bar);
            if (mana != null) parts.Add(mana);
            return string.Join(", ", parts);
        }

        // The line of one enemy on the board, by its id; null when it has no live bar.
        private static string UnitLineFor(Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId enemy)
        {
            var battle = GameScopes.Controller<BattleUIController>();
            var board = RunData.BoardController;
            var views = board != null ? board._enemyViewControllers : null;
            if (battle == null || views == null || battle._healthBars == null) return null;
            CharacterViewController c;
            HealthBarView bar;
            if (!views.TryGetValue(enemy, out c) || c == null || !c.gameObject.activeInHierarchy) return null;
            if (!battle._healthBars.TryGetValue(c.EntityId, out bar) || bar == null || !bar.gameObject.activeInHierarchy) return null;
            string name = bar._characterNameText != null ? bar._characterNameText.text : null;
            if (string.IsNullOrWhiteSpace(name)) name = c.gameObject.name.Replace("(Clone)", "");
            return UnitLine(new Unit { Name = name, Bar = bar, IsHero = false });
        }

        /// <summary>One line per unit of a side, heroes or enemies, in the battlefield's order; empty
        /// outside a fight.</summary>
        internal static IEnumerable<string> UnitLines(bool heroes)
        {
            foreach (var u in Units())
                if (u.IsHero == heroes) yield return UnitLine(u);
        }

        private static string Health(HealthBarView bar) => Number(bar._currentHealth);

        private static string Shield(HealthBarView bar)
            => bar._currentShield > 0 ? Strings.RunShield(Number(bar._currentShield)) : null;

        private static string Number(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        private static string Mana(HealthBarView bar)
        {
            var slider = bar._manaSlider;
            if (slider == null || !slider.gameObject.activeInHierarchy || slider.maxValue <= 0) return null;
            return Strings.RunMana(((int)slider.value).ToString(), ((int)slider.maxValue).ToString());
        }
    }
}
