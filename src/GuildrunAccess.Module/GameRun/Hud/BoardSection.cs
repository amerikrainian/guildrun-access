using System;
using System.Collections.Generic;
using Ember.Balancing.Sheets.Characters.Attacks;
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
    /// occupant's menu. During a fight: every unit with a health bar, heroes first, with health and
    /// mana. The game's board is a pointy-top hex grid (Unity's hexagon layout, odd rows half a cell to
    /// the right), so a cell has no neighbour straight up or down: Q E A D Z C step focus to its six
    /// neighbours and Shift+the same letter moves the hero under focus there, while the arrows jump
    /// between units (Up toward the enemies, Down toward the heroes, Left and Right round a side) and
    /// Home and End to a side's first and last. The board's nodes carry no edges of their own: the
    /// navigator hands an unwired arrow to the screen, and this section answers it.
    /// </summary>
    internal sealed class BoardSection : ScreenSection
    {
        /// <summary>The action keys of the hex steps and of the Shift+letter moves (bound in the module's
        /// input registration to Q E A D Z C, the hexagon's own layout on the keyboard).</summary>
        public const string StepUpLeft = "run.hex.upleft";
        public const string StepUpRight = "run.hex.upright";
        public const string StepLeft = "run.hex.left";
        public const string StepRight = "run.hex.right";
        public const string StepDownLeft = "run.hex.downleft";
        public const string StepDownRight = "run.hex.downright";
        public const string MoveUpLeft = "run.move.upleft";
        public const string MoveUpRight = "run.move.upright";
        public const string MoveLeft = "run.move.left";
        public const string MoveRight = "run.move.right";
        public const string MoveDownLeft = "run.move.downleft";
        public const string MoveDownRight = "run.move.downright";

        private const string CellPrefix = "run:cell:";
        private const string UnitPrefix = "run:unit:";

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

        // Raw nodes, no row wiring: on a hex grid the arrows are not a cell's neighbours (the hex keys
        // are) but jumps between units, answered in GetActions.
        private void AddGridRow(GraphBuilder b, int width, int y)
        {
            for (int x = 0; x < width; x++)
                b.AddNode(CellId(new Vector2Int(x, y)), CellNode(new Vector2Int(x, y)));
        }

        private static ControlId CellId(Vector2Int cell) => ControlId.Structural(CellPrefix + cell.x + ":" + cell.y);

        // The glance keys' subject on a cell: its occupant's registry id, or nothing on an empty cell.
        private static object CellSubject(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var hero)) return hero;
            if (RunData.TryEnemyAt(cell, out var enemy)) return enemy;
            return null;
        }

        /// <summary>The grid cell under focus, from the focused node's key; false off the grid (a
        /// fight's unit nodes included).</summary>
        internal static bool TryFocusedCell(out Vector2Int cell)
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

        /// <summary>Whether the letters are the board's keys right now: a grid cell is focused during
        /// placement (the run screen's type-ahead search stands down then).</summary>
        internal static bool OwnsLetters() => RunData.Placing() && TryFocusedCell(out _);

        public override IEnumerable<ElementAction> GetActions()
        {
            // The board's keys answer only while focus rests on one of its nodes: anywhere else on the
            // HUD they are not offered, so the arrows keep their meaning there and the letters go to the
            // type-ahead search.
            if (RunData.Placing())
            {
                if (!TryFocusedCell(out var cell)) yield break;
                // Q E A D Z C: focus steps to the cell's neighbour that way, announced as any move; off
                // the grid nothing happens and nothing is said, as at a list's edge.
                yield return new ElementAction(StepUpLeft, Strings.Get("bind.run.hex.upleft"), _ => Step(cell, HexDir.UpLeft));
                yield return new ElementAction(StepUpRight, Strings.Get("bind.run.hex.upright"), _ => Step(cell, HexDir.UpRight));
                yield return new ElementAction(StepLeft, Strings.Get("bind.run.hex.left"), _ => Step(cell, HexDir.Left));
                yield return new ElementAction(StepRight, Strings.Get("bind.run.hex.right"), _ => Step(cell, HexDir.Right));
                yield return new ElementAction(StepDownLeft, Strings.Get("bind.run.hex.downleft"), _ => Step(cell, HexDir.DownLeft));
                yield return new ElementAction(StepDownRight, Strings.Get("bind.run.hex.downright"), _ => Step(cell, HexDir.DownRight));
                // Arrows: jumps between units. Up from the heroes' side lands on the enemy nearest
                // straight ahead (the closest column, then the closest row), Down from the enemies' side
                // on the hero; Left and Right cycle the focused side's units in reading order (from an
                // empty cell, the next one along), Home and End its first and last. Nothing that way
                // (the far edge of a side, a side without units): silent, an edge.
                yield return new ElementAction(UiActions.Up, Strings.Get("bind.ui.up"), _ => SwitchSide(cell, toEnemies: true));
                yield return new ElementAction(UiActions.Down, Strings.Get("bind.ui.down"), _ => SwitchSide(cell, toEnemies: false));
                yield return new ElementAction(UiActions.Left, Strings.Get("bind.ui.left"), _ => CycleUnit(cell, -1));
                yield return new ElementAction(UiActions.Right, Strings.Get("bind.ui.right"), _ => CycleUnit(cell, 1));
                yield return new ElementAction(UiActions.Home, Strings.Get("bind.ui.home"), _ => JumpUnit(cell, first: true));
                yield return new ElementAction(UiActions.End, Strings.Get("bind.ui.end"), _ => JumpUnit(cell, first: false));
                // Shift+Q E A D Z C: the hero under focus steps one cell that way (trading places with a
                // hero standing there, as a drag would), focus following it, the new cell's coordinates
                // the whole announcement. Off the grid, into the enemy rows, or from an empty or enemy
                // cell: nothing happens and nothing is said.
                yield return new ElementAction(MoveUpLeft, Strings.Get("bind.run.move.upleft"), _ => Nudge(cell, HexDir.UpLeft));
                yield return new ElementAction(MoveUpRight, Strings.Get("bind.run.move.upright"), _ => Nudge(cell, HexDir.UpRight));
                yield return new ElementAction(MoveLeft, Strings.Get("bind.run.move.left"), _ => Nudge(cell, HexDir.Left));
                yield return new ElementAction(MoveRight, Strings.Get("bind.run.move.right"), _ => Nudge(cell, HexDir.Right));
                yield return new ElementAction(MoveDownLeft, Strings.Get("bind.run.move.downleft"), _ => Nudge(cell, HexDir.DownLeft));
                yield return new ElementAction(MoveDownRight, Strings.Get("bind.run.move.downright"), _ => Nudge(cell, HexDir.DownRight));
                yield break;
            }
            // A fight: the same arrows over the units, a side at a time (heroes then enemies, each in
            // the battlefield's order). Up from a hero is the enemy at the same place in its row (the
            // last when there are fewer), Down from an enemy the hero.
            if (!FocusedUnitKey(out _)) yield break;
            var units = Units();
            int at = FocusedUnitIndex(units);
            if (at < 0) yield break;
            yield return new ElementAction(UiActions.Up, Strings.Get("bind.ui.up"), _ => SwitchFightSide(units, at, toEnemies: true));
            yield return new ElementAction(UiActions.Down, Strings.Get("bind.ui.down"), _ => SwitchFightSide(units, at, toEnemies: false));
            yield return new ElementAction(UiActions.Left, Strings.Get("bind.ui.left"), _ => CycleFightUnit(units, at, -1));
            yield return new ElementAction(UiActions.Right, Strings.Get("bind.ui.right"), _ => CycleFightUnit(units, at, 1));
            yield return new ElementAction(UiActions.Home, Strings.Get("bind.ui.home"), _ => JumpFightUnit(units, at, first: true));
            yield return new ElementAction(UiActions.End, Strings.Get("bind.ui.end"), _ => JumpFightUnit(units, at, first: false));
        }

        // ---- the grid's keys ----

        private static void Step(Vector2Int from, HexDir dir)
        {
            HexGrid.Neighbor(from.x, from.y, dir, out int x, out int y);
            var to = new Vector2Int(x, y);
            if (OnGrid(to)) Navigation.MoveTo(CellId(to));
        }

        private static void Nudge(Vector2Int from, HexDir dir)
        {
            if (!RunData.TryHeroAt(from, out _)) return;
            HexGrid.Neighbor(from.x, from.y, dir, out int x, out int y);
            var to = new Vector2Int(x, y);
            if (!RunData.IsPlayerCell(to)) return;
            if (!RunData.SwapBoard(from, to)) { Speech.Say(Strings.RunMoveFailed, interrupt: true); return; }
            Navigation.FocusNode(CellId(to), announce: false);
            Speech.Say(RunLabels.CellName(to), interrupt: true);
        }

        // Up from the heroes' side, Down from the enemies': the unit of the other side nearest straight
        // ahead, by column (the odd rows' half-cell shift counted) and then by row.
        private static void SwitchSide(Vector2Int from, bool toEnemies)
        {
            if (RunData.IsPlayerCell(from) != toEnemies) return;
            bool heroes = !toEnemies;
            float column = HexGrid.Column(from.x, from.y);
            bool found = false;
            Vector2Int best = default;
            float bestDx = 0f;
            int bestDy = 0;
            foreach (var cell in SideCells(heroes))
            {
                if (!Occupied(cell, heroes)) continue;
                float dx = Math.Abs(HexGrid.Column(cell.x, cell.y) - column);
                int dy = Math.Abs(cell.y - from.y);
                if (found && (dx > bestDx || (dx == bestDx && dy >= bestDy))) continue;
                best = cell;
                bestDx = dx;
                bestDy = dy;
                found = true;
            }
            if (found) Navigation.MoveTo(CellId(best));
        }

        // Left and Right: the next unit of the focused cell's side that way in reading order, round the
        // ends; the focused cell itself is never the answer, so a side's lone unit stays put, silent.
        private static void CycleUnit(Vector2Int from, int dir)
        {
            bool heroes = RunData.IsPlayerCell(from);
            var cells = SideCells(heroes);
            int at = IndexOf(cells, from);
            int n = cells.Count;
            if (at < 0) return;
            for (int i = 1; i < n; i++)
            {
                var cell = cells[((at + dir * i) % n + n) % n];
                if (Occupied(cell, heroes)) { Navigation.MoveTo(CellId(cell)); return; }
            }
        }

        // Home and End: the first and last unit of the focused cell's side in reading order.
        private static void JumpUnit(Vector2Int from, bool first)
        {
            bool heroes = RunData.IsPlayerCell(from);
            var cells = SideCells(heroes);
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[first ? i : cells.Count - 1 - i];
                if (Occupied(cell, heroes)) { Navigation.MoveTo(CellId(cell)); return; }
            }
        }

        // A side's cells in the grid's reading order (the row farthest from the player first, left to
        // right): the order Left and Right cycle its units in.
        private static List<Vector2Int> SideCells(bool heroes)
        {
            var cells = new List<Vector2Int>();
            var board = RunData.Board();
            if (board == null) return cells;
            int w, h;
            try { w = board.BoardWidth; h = board.BoardHeight; }
            catch (Exception) { return cells; }
            int playerRows = PlayerRows(h);
            int top = heroes ? playerRows - 1 : h - 1, bottom = heroes ? 0 : playerRows;
            for (int y = top; y >= bottom; y--)
                for (int x = 0; x < w; x++) cells.Add(new Vector2Int(x, y));
            return cells;
        }

        // Only heroes stand on the player's side and only enemies on theirs: one registry read per cell.
        private static bool Occupied(Vector2Int cell, bool heroes)
            => heroes ? RunData.TryHeroAt(cell, out _) : RunData.TryEnemyAt(cell, out _);

        private static int IndexOf(List<Vector2Int> cells, Vector2Int cell)
        {
            for (int i = 0; i < cells.Count; i++)
                if (cells[i].x == cell.x && cells[i].y == cell.y) return i;
            return -1;
        }

        private static bool OnGrid(Vector2Int cell)
        {
            var board = RunData.Board();
            if (board == null || cell.x < 0 || cell.y < 0) return false;
            try { return cell.x < board.BoardWidth && cell.y < board.BoardHeight; }
            catch (Exception) { return false; }
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

        // ---- the battlefield's keys ----

        // The focused node's unit, by the bar instance id in its key; false off the battlefield.
        private static bool FocusedUnitKey(out int instance)
        {
            instance = 0;
            var key = Navigation.FocusedNode?.Id.StructuralKey as string;
            return key != null && key.StartsWith(UnitPrefix, StringComparison.Ordinal)
                && int.TryParse(key.Substring(UnitPrefix.Length), out instance);
        }

        private static int FocusedUnitIndex(List<Unit> units)
        {
            if (!FocusedUnitKey(out int instance)) return -1;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Bar.GetInstanceID() == instance) return i;
            return -1;
        }

        // The units of one side are contiguous in the battlefield's order (heroes first).
        private static void SideRange(List<Unit> units, bool heroes, out int start, out int count)
        {
            start = 0;
            count = 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i].IsHero == heroes) { if (count == 0) start = i; count++; }
        }

        private static void SwitchFightSide(List<Unit> units, int at, bool toEnemies)
        {
            if (units[at].IsHero != toEnemies) return;
            SideRange(units, units[at].IsHero, out int from, out _);
            SideRange(units, !units[at].IsHero, out int start, out int count);
            if (count == 0) return;
            Navigation.MoveTo(UnitId(units[start + Math.Min(at - from, count - 1)]));
        }

        private static void CycleFightUnit(List<Unit> units, int at, int dir)
        {
            SideRange(units, units[at].IsHero, out int start, out int count);
            if (count < 2) return;
            Navigation.MoveTo(UnitId(units[start + ((at - start + dir) % count + count) % count]));
        }

        private static void JumpFightUnit(List<Unit> units, int at, bool first)
        {
            SideRange(units, units[at].IsHero, out int start, out int count);
            if (count == 0) return;
            Navigation.MoveTo(UnitId(units[first ? start : start + count - 1]));
        }

        private static ControlId UnitId(Unit u) => ControlId.Structural(UnitPrefix + u.Bar.GetInstanceID());

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
                Subject = () => CellSubject(cell),
                SideLines = HeroLines.SideOfSlots(() => CellHeroLines(cell),
                    () => { var view = RunData.TryHeroAt(cell, out var id) ? RunData.ViewOf(id) : null; return view != null ? view._itemSlotViews : null; }),
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
                if (card != null) lines.AddRange(HeroCardNodes.Tooltips(card));
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
        // abilities and items (its slot) and the card's tag and stat tooltips; an enemy's abilities
        // and stats.
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
                if (card != null) { lines.AddRange(HeroCardNodes.TagsTooltips(card)); lines.AddRange(HeroCardNodes.StatsTooltips(card)); }
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

        // The hero buffer: the card's rows as every hero card reads (name, stats, abilities, then its
        // tooltips: HeroLines.ForCard); a hero without its card shown falls back to its slot, an enemy
        // to its unit line.
        private static IEnumerable<string> CellHeroLines(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var id))
            {
                var card = HeroActions.ShownHeroCard(RunData.HeroName(id));
                if (card != null) return HeroLines.ForCard(card);
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
                string name = RunData.HeroLabel(hero) ?? Strings.RunParty;
                var view = RunData.ViewOf(hero);
                return view != null ? RunLabels.WithItems(name, view._itemSlotViews) : name;
            }
            if (RunData.TryEnemyAt(cell, out var enemy)) return RunData.EnemyLabel(enemy) ?? Strings.RunBoard;
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

        // Name is the plain name (a hero's matches the sidebar's card; an enemy's carries its number
        // among its namesakes, "Slime 2", which also orders them within their side); Label opens the
        // unit's readout (a hero's name with its classes and rank).
        private struct Unit { public string Name; public string Label; public HealthBarView Bar; public bool IsHero; public CharacterViewController View; }

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
        // ended, or the HUD is between panels), so there is no landing to announce. Raw nodes, heroes
        // then enemies, each side a region: the arrows are answered in GetActions, and a unit speaks its
        // place within its side itself (a raw node gets no stamped position).
        private void BuildBattlefield(GraphBuilder b)
        {
            var units = Units();
            if (units.Count == 0) return;
            SideRange(units, heroes: true, out _, out int heroCount);
            b.BeginStop("board");
            b.PushContext(Strings.RunBoard, Strings.RoleList);
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (i == 0 || units[i - 1].IsHero != u.IsHero) b.SetRegion(u.IsHero ? "run:board:heroes" : "run:board:enemies");
                int index = u.IsHero ? i + 1 : i - heroCount + 1;
                int count = u.IsHero ? heroCount : units.Count - heroCount;
                b.AddNode(UnitId(u), new NodeVtable
                {
                    // Units fall all through a fight: when the focused one does, focus slides to a
                    // neighbour without reading it out (the death is in the battle events log).
                    QuietVanish = true,
                    // A unit's line is read when focus lands on it and holds the vitals of that moment;
                    // parked on a unit, nothing is re-read as they change (a fight changes them every
                    // tick, and following that is spam): stepping back onto the unit reads them afresh,
                    // and the buffers read them live.
                    Announcements = new List<NodeAnnouncement>
                    {
                        // "Pimenta, wearing Freezing Tome, hero, health 650": the hero named as on the board.
                        new NodeAnnouncement(() => Strings.RunUnit(u.IsHero, RunLabels.WithItems(u.Label, u.Bar._itemSlotViews), Strings.HeroStat(Strings.HeroHealth, Health(u.Bar))), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => Shield(u.Bar), kind: AnnouncementKinds.Value),
                        new NodeAnnouncement(() => Mana(u.Bar), kind: AnnouncementKinds.Value),
                        // "2 of 3" within its side, under the same setting as a stamped position.
                        new NodeAnnouncement(() => GraphAnnouncer.PositionText != null ? GraphAnnouncer.PositionText(index, count) : null, kind: AnnouncementKinds.Position),
                    },
                    SearchText = () => u.Label,
                    // Landing shows the unit's card in the sidebar, as hovering it does; the buffers read
                    // it: the hero buffer as name, stats, abilities, the control buffer as the tooltips.
                    OnFocus = () => Peek(u.View),
                    Details = () => UnitDetails(u),
                    Subject = () => u.View,
                    SideLines = HeroLines.SideOfSlots(() => UnitHeroLines(u), () => u.Bar._itemSlotViews),
                });
            }
            b.SetRegion(null);
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
        internal static Il2CppReferenceArray<CharacterViewController> ViewsOf<TKey>(
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
            // The enemies' numbers, read once for the side ("Slime 2": EnemyNumbers).
            var numbers = isHero ? null : EnemyNumbers.Read();
            foreach (var c in views)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                HealthBarView bar;
                if (!bars.TryGetValue(c.EntityId, out bar) || bar == null || !bar.gameObject.activeInHierarchy) continue;
                units.Add(new Unit { Name = UnitName(bar, c, numbers), Label = UnitLabel(bar, c, numbers), Bar = bar, IsHero = isHero, View = c });
            }
        }

        // A unit's name as its bar shows it; a bar the game leaves blank (an enemy entry without a name)
        // names the unit through the registry instead. An enemy that shares its name reads with its
        // number either way ("Slime 2"), the same one its placement cell read with.
        internal static string UnitName(HealthBarView bar, CharacterViewController unit, Dictionary<string, int> numbers = null)
        {
            string name = bar._characterNameText != null ? bar._characterNameText.text : null;
            return string.IsNullOrWhiteSpace(name) ? RunData.UnitName(unit, numbers) : EnemyNumbers.Numbered(name, unit, numbers);
        }

        // A hero's unit opens with its classes and rank ("Skorn, Warrior, rank C"); an enemy's with its name.
        private static string UnitLabel(HealthBarView bar, CharacterViewController unit, Dictionary<string, int> numbers = null)
        {
            if (unit != null && Nullables.TryGet(() => unit.HeroId, out Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroId hero))
            {
                string label = RunData.HeroLabel(hero);
                if (!string.IsNullOrWhiteSpace(label)) return label;
            }
            return UnitName(bar, unit, numbers);
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
        // "Karsu, hero, health 650, shield 40, mana 45 of 85, Poison 3, Stun 2 seconds": the unit's
        // whole line, for the party and enemies buffers (one line per unit, read live on every
        // buffer key), its status icons last.
        private static string UnitLine(Unit u)
        {
            var parts = new List<string> { Strings.RunUnit(u.IsHero, RunLabels.WithItems(u.Label, u.Bar._itemSlotViews), Strings.HeroStat(Strings.HeroHealth, Health(u.Bar))) };
            string shield = Shield(u.Bar);
            if (shield != null) parts.Add(shield);
            string mana = Mana(u.Bar);
            if (mana != null) parts.Add(mana);
            parts.AddRange(Statuses(u.Bar));
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

        internal static HealthBarView BarOf(CharacterViewController c)
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
            string name = UnitName(bar, c);
            return UnitLine(new Unit { Name = name, Label = name, Bar = bar, IsHero = false, View = c });
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

        internal static string Mana(HealthBarView bar)
        {
            var slider = bar._manaSlider;
            if (slider == null || !slider.gameObject.activeInHierarchy || slider.maxValue <= 0) return null;
            return Strings.RunMana(((int)slider.value).ToString(), ((int)slider.maxValue).ToString());
        }

        // "Poison 3", "Stun 2 seconds": the bar's status icons with the number each shows. The bar
        // keeps its live icons by status type; a stacking status's icon shows its stacks, a timed
        // one's (the bar's TimerStatusTypes: stun, stealth, the can't-act statuses, immunity...)
        // the whole seconds left, which the game rewrites on the icon every tick since build
        // 25323618. The keys are copied out (no interop enumerator), so the order is the
        // dictionary's, not the drawn one.
        internal static List<string> Statuses(HealthBarView bar)
        {
            var lines = new List<string>();
            try
            {
                var icons = bar._activeStatusIcons;
                if (icons == null || icons.Count == 0) return lines;
                var types = new Il2CppStructArray<StatusType>(icons.Count);
                icons.Keys.CopyTo(types, 0);
                for (int i = 0; i < types.Length; i++)
                {
                    StatusIconView icon;
                    if (!icons.TryGetValue(types[i], out icon) || icon == null || !icon.gameObject.activeInHierarchy) continue;
                    string name = Strings.Status(types[i].ToString());
                    int count = icon.CurrentStackCount;
                    lines.Add(IsTimerStatus(types[i]) ? Strings.RunStatusTimed(name, count) : Strings.RunStatus(name, count));
                }
            }
            catch (Exception e) { CoreLog.Warning("Units: status icons unreadable: " + e.Message); }
            return lines;
        }

        private static bool IsTimerStatus(StatusType type)
        {
            var timers = HealthBarView.TimerStatusTypes;
            if (timers == null) return false;
            for (int i = 0; i < timers.Length; i++)
                if (timers[i] == type) return true;
            return false;
        }
    }
}
