using System;
using System.Collections.Generic;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.Battle.UI.Hud;
using Ember.Scopes.GameRun.UI.EnemyCard;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The "board" stop, in one of two shapes. While placing: the grid, every cell as a node (enemy rows
    /// first, then the player's, each side a region), Enter dropping a picked-up hero or opening the
    /// occupant's menu, Shift+arrows stepping the hero under focus one cell. During a fight: every unit
    /// with a health bar, heroes first, with health and mana.
    /// </summary>
    internal sealed class BoardSection : ScreenSection
    {
        /// <summary>The action keys of the Shift+arrow moves (bound in the module's input registration).</summary>
        public const string MoveUp = "run.move.up";
        public const string MoveDown = "run.move.down";
        public const string MoveLeft = "run.move.left";
        public const string MoveRight = "run.move.right";

        private const string CellPrefix = "run:cell:";

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
                b.AddItem(CellId(new Vector2Int(x, y)), CellNode(new Vector2Int(x, y)));
            b.EndRow();
        }

        private static ControlId CellId(Vector2Int cell) => ControlId.Structural(CellPrefix + cell.x + ":" + cell.y);

        // The cell under focus, from the focused node's key; false off the grid.
        private static bool TryFocusedCell(out Vector2Int cell)
        {
            cell = default;
            var node = Navigation.FocusedNode;
            var key = node != null ? node.Id.StructuralKey as string : null;
            if (key == null || !key.StartsWith(CellPrefix, StringComparison.Ordinal)) return false;
            var parts = key.Substring(CellPrefix.Length).Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return false;
            cell = new Vector2Int(x, y);
            return true;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            // Shift+arrows: the hero under focus steps one cell that way (trading places with a hero
            // standing there, as a drag would), focus following it, the new cell's coordinates the
            // whole announcement. Off the grid, into the enemy rows, from an empty or enemy cell, or
            // outside placement: nothing happens and nothing is said.
            yield return new ElementAction(MoveUp, Strings.Get("bind.run.move.up"), _ => Nudge(0, 1));
            yield return new ElementAction(MoveDown, Strings.Get("bind.run.move.down"), _ => Nudge(0, -1));
            yield return new ElementAction(MoveLeft, Strings.Get("bind.run.move.left"), _ => Nudge(-1, 0));
            yield return new ElementAction(MoveRight, Strings.Get("bind.run.move.right"), _ => Nudge(1, 0));
        }

        private static void Nudge(int dx, int dy)
        {
            if (!RunData.Placing() || !TryFocusedCell(out var from) || !RunData.TryHeroAt(from, out _)) return;
            var to = new Vector2Int(from.x + dx, from.y + dy);
            if (!RunData.IsPlayerCell(to)) return;
            if (!RunData.SwapBoard(from, to)) { Speech.Say(Strings.RunMoveFailed, interrupt: true); return; }
            Navigation.FocusNode(CellId(to), announce: false);
            Speech.Say(RunLabels.CellName(to), interrupt: true);
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
                    // "health 675, mana 40 of 75": the occupant's vitals from its bar, which stands during
                    // placement too; nothing on an empty cell.
                    new NodeAnnouncement(() => VitalsAt(cell), kind: AnnouncementKinds.Value),
                    // The card's other non-zero stats, once the landing has shown the card.
                    new NodeAnnouncement(() => BriefStatsAt(cell), kind: AnnouncementKinds.Value),
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

        private static void Peek(CharacterViewController view)
        {
            if (view == null) return;
            if (Nullables.TryGet(() => view.HeroId, out Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroId hero)) HeroActions.PeekHero(hero);
            else if (Nullables.TryGet(() => view.EnemyId, out Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId enemy)) HeroActions.PeekEnemy(enemy);
        }

        // A fighting unit's rows and tooltips come from its card when the sidebar shows it, else its
        // own line and items.
        private static IEnumerable<string> UnitHeroLines(Unit u)
        {
            if (u.IsHero)
            {
                var card = HeroActions.ShownHeroCard(u.Name);
                if (card != null) return HeroLines.ForCard(card);
            }
            else
            {
                var card = ShownEnemyCard(u);
                if (card != null) return SidebarNodes.EnemyRows(card);
            }
            return new[] { UnitLine(u) };
        }

        private static IEnumerable<string> UnitDetails(Unit u)
        {
            var lines = new List<string>();
            if (u.IsHero)
            {
                var card = HeroActions.ShownHeroCard(u.Name);
                if (card != null) { lines.AddRange(HeroCardNodes.AbilitiesTooltips(card)); lines.AddRange(HeroCardNodes.StatsTooltips(card)); }
            }
            else
            {
                var card = ShownEnemyCard(u);
                if (card != null) lines.AddRange(SidebarNodes.EnemyDetails(card));
            }
            lines.AddRange(ItemNodes.ItemTooltips(u.Bar._itemSlotViews));
            return lines;
        }

        // The sidebar's enemy card when it shows this unit (matched by enemy id: a nameless enemy has no
        // name to match by).
        private static EnemyCardView ShownEnemyCard(Unit u)
        {
            if (u.View == null) return null;
            return Nullables.TryGet(() => u.View.EnemyId, out Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId id)
                ? HeroActions.ShownEnemyCard(id) : null;
        }

        // The control buffer: the full stats line from the card the landing showed, then a hero's
        // abilities and items (its slot) and the card's stat tooltips; an enemy's abilities and stats.
        private static IEnumerable<string> CellDetails(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var id))
            {
                var lines = new List<string>();
                var card = HeroActions.ShownHeroCard(RunData.HeroName(id));
                string stats = card != null ? HeroCardNodes.StatsLine(card) : VitalsOf(id);
                if (!string.IsNullOrEmpty(stats)) lines.Add(stats);
                var view = RunData.ViewOf(id);
                if (view != null) lines.AddRange(RunLabels.SlotTooltips(view));
                if (card != null) lines.AddRange(HeroCardNodes.StatsTooltips(card));
                return lines;
            }
            if (RunData.TryEnemyAt(cell, out var enemy))
            {
                var card = HeroActions.ShownEnemyCard(enemy);
                if (card != null) return SidebarNodes.EnemyDetails(card);
                string line = VitalsOf(enemy);
                return line != null ? new[] { line } : null;
            }
            return null;
        }

        // The non-zero stats of the card the landing showed for this cell, past health and mana (the bar
        // already gave those, live).
        private static string BriefStatsAt(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var id))
            {
                var card = HeroActions.ShownHeroCard(RunData.HeroName(id));
                return card != null ? HeroCardNodes.StatsBrief(card, vitals: false) : null;
            }
            if (RunData.TryEnemyAt(cell, out var enemy))
            {
                var card = HeroActions.ShownEnemyCard(enemy);
                return card != null ? SidebarNodes.StatsBrief(card) : null;
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
                return view != null ? HeroLines.ForSlot(view, VitalsOf(id)) : null;
            }
            if (RunData.TryEnemyAt(cell, out var enemy))
            {
                var card = HeroActions.ShownEnemyCard(enemy);
                if (card != null)
                    return GameNodes.Lines(SidebarNodes.EnemyLine(card), SidebarNodes.Stats(card), HeroCardNodes.AbilitiesLine(card));
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

        private struct Unit { public string Name; public HealthBarView Bar; public bool IsHero; public CharacterViewController View; }

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
                        // "Pimenta, wearing Freezing Tome, hero, health 650": the hero named as on the board.
                        new NodeAnnouncement(() => Strings.RunUnit(u.IsHero, RunLabels.WithItems(u.Name, u.Bar._itemSlotViews), Strings.HeroStat(Strings.HeroHealth, Health(u.Bar))), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => Shield(u.Bar), kind: AnnouncementKinds.Value),
                        // Mana regenerates all fight long: it rides along in a re-read but never causes one.
                        new NodeAnnouncement(() => Mana(u.Bar), kind: AnnouncementKinds.Value) { LiveReadoutIgnore = true },
                    },
                    SearchText = () => u.Name,
                    // Landing shows the unit's card in the sidebar, as hovering it does; the buffers read
                    // it: the hero buffer as name, stats, abilities, the control buffer as the tooltips.
                    OnFocus = () => Peek(u.View),
                    Details = () => UnitDetails(u),
                    SideLines = HeroLines.Side(() => UnitHeroLines(u), () => ItemNodes.ItemTooltips(u.Bar._itemSlotViews)),
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
                units.Add(new Unit { Name = UnitName(bar, c), Bar = bar, IsHero = isHero, View = c });
            }
        }

        // A unit's name as its bar shows it; a bar the game leaves blank (an enemy entry without a name)
        // names the unit through the registry instead.
        internal static string UnitName(HealthBarView bar, CharacterViewController unit)
        {
            string name = bar._characterNameText != null ? bar._characterNameText.text : null;
            return string.IsNullOrWhiteSpace(name) ? RunData.UnitName(unit) : name;
        }

        /// <summary>The unit whose bar this is (the board registries' views, matched by entity id), or
        /// null when no unit on the board owns it.</summary>
        internal static CharacterViewController UnitOf(HealthBarView bar)
        {
            var battle = GameScopes.Controller<BattleUIController>();
            var board = RunData.BoardController;
            if (bar == null || battle == null || board == null || battle._healthBars == null) return null;
            return OwnerOf(bar, battle, ViewsOf(board.CharacterViewControllers)) ?? OwnerOf(bar, battle, ViewsOf(board._enemyViewControllers));
        }

        private static CharacterViewController OwnerOf(HealthBarView bar, BattleUIController battle, Il2CppReferenceArray<CharacterViewController> views)
        {
            if (views == null) return null;
            foreach (var c in views)
            {
                if (c == null) continue;
                HealthBarView owned;
                if (battle._healthBars.TryGetValue(c.EntityId, out owned) && owned != null && owned.Pointer == bar.Pointer) return c;
            }
            return null;
        }

        // The bar's own label is an inactive text set once at spawn (the starting health, "<b>520</b>"),
        // never the value of the moment; the bar keeps the live current health and shield as fields,
        // updated with every hit and heal it draws.
        // "Karsu, hero, health 650, shield 40, mana 45 of 85": the unit's whole line, for the party
        // and enemies buffers (one line per unit, read live on every buffer key).
        private static string UnitLine(Unit u)
        {
            var parts = new List<string> { Strings.RunUnit(u.IsHero, RunLabels.WithItems(u.Name, u.Bar._itemSlotViews), Strings.HeroStat(Strings.HeroHealth, Health(u.Bar))) };
            string shield = Shield(u.Bar);
            if (shield != null) parts.Add(shield);
            string mana = Mana(u.Bar);
            if (mana != null) parts.Add(mana);
            return string.Join(", ", parts);
        }

        // A unit's live bar by its registry id (heroes and enemies keep separate registries), or null
        // when it stands nowhere or its bar is not up.
        private static HealthBarView BarOf(Il2CppSystem.Collections.Generic.Dictionary<Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroId, CharacterViewController> views, Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroId id)
        {
            CharacterViewController c;
            return views != null && views.TryGetValue(id, out c) ? BarOf(c) : null;
        }

        private static HealthBarView BarOf(Il2CppSystem.Collections.Generic.Dictionary<Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId, CharacterViewController> views, Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId id)
        {
            CharacterViewController c;
            return views != null && views.TryGetValue(id, out c) ? BarOf(c) : null;
        }

        private static HealthBarView BarOf(CharacterViewController c)
        {
            var battle = GameScopes.Controller<BattleUIController>();
            if (c == null || !c.gameObject.activeInHierarchy || battle == null || battle._healthBars == null) return null;
            HealthBarView bar;
            return battle._healthBars.TryGetValue(c.EntityId, out bar) && bar != null && bar.gameObject.activeInHierarchy ? bar : null;
        }

        /// <summary>"health 675, shield 40, mana 40 of 75": what a unit's bar shows, read live; null
        /// without a bar.</summary>
        internal static string Vitals(HealthBarView bar)
        {
            if (bar == null) return null;
            var parts = new List<string> { Strings.HeroStat(Strings.HeroHealth, Health(bar)) };
            string shield = Shield(bar);
            if (shield != null) parts.Add(shield);
            string mana = Mana(bar);
            if (mana != null) parts.Add(mana);
            return string.Join(", ", parts);
        }

        internal static string VitalsOf(Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroId id)
        {
            var board = RunData.BoardController;
            return Vitals(BarOf(board != null ? board.CharacterViewControllers : null, id));
        }

        internal static string VitalsOf(Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId id)
        {
            var board = RunData.BoardController;
            return Vitals(BarOf(board != null ? board._enemyViewControllers : null, id));
        }

        /// <summary>A slot's hero's vitals, or null when it stands on no board.</summary>
        internal static string VitalsOf(BottomHeroView view)
            => RunData.TryHeroId(view, out var id) ? VitalsOf(id) : null;

        private static string VitalsAt(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var hero)) return VitalsOf(hero);
            if (RunData.TryEnemyAt(cell, out var enemy)) return VitalsOf(enemy);
            return null;
        }

        // The line of one enemy on the board, by its id; null when it has no live bar.
        private static string UnitLineFor(Ember.Scopes.GameRun.GameRegistry.Data.Characters.EnemyId enemy)
        {
            var board = RunData.BoardController;
            var views = board != null ? board._enemyViewControllers : null;
            CharacterViewController c;
            if (views == null || !views.TryGetValue(enemy, out c) || c == null) return null;
            var bar = BarOf(c);
            if (bar == null) return null;
            return UnitLine(new Unit { Name = UnitName(bar, c), Bar = bar, IsHero = false, View = c });
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
