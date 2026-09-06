using System;
using System.Collections.Generic;
using System.Text;
using Ember.Balancing.SimulationBridge;
using Ember.Scopes.Battle.Characters;
using Ember.Scopes.Battle.UI;
using Ember.Scopes.Battle.UI.BattleFlow;
using Ember.Scopes.Battle.UI.Hud;
using Ember.Scopes.Battle.UI.Sidebar;
using Ember.Scopes.GameRun.GameRegistry.Data.Characters;
using Ember.Scopes.GameRun.UI;
using Ember.Scopes.GameRun.UI.BattleSpeed;
using Ember.Scopes.GameRun.UI.ChunkUI;
using Ember.Scopes.GameRun.UI.HeroCard.Elements;
using Ember.Scopes.GameRun.UI.Navigation;
using Ember.Scopes.GameRun.UI.Relics;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.Equipment;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Module.Run;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The run HUD: the base context while a run is loaded, between and during fights. Tab-stops, in
    /// order: actions (Fight), the battlefield (every unit with a health bar: heroes then enemies, with
    /// health and mana), the party (active and reserve slots with their abilities and item slots), the
    /// item reserve, relics, info (gold, shards, difficulty, timer, the act map), battle speed, and the
    /// menu (heroes, settings, feedback). Everything is read live from the game's own views; Space reads
    /// a control's tooltip through the game's tooltip pipeline.
    /// </summary>
    public sealed class GameRunScreen : Screen
    {
        public override string Key => "gamerun";
        public override int Layer => 0;
        public override bool AllowsTypeahead => true;

        // The HUD has many stops and no natural end: Tab past the last one comes round to the first.
        public GameRunScreen() { Wrap = true; }

        // The controllers, each re-found by scene scan (throttled) when absent.
        private readonly Finder<BottomHeroPanelUIController> _party = new Finder<BottomHeroPanelUIController>();
        private readonly Finder<BattleFlowUIStateController> _flow = new Finder<BattleFlowUIStateController>();
        private readonly Finder<BattleUIController> _battle = new Finder<BattleUIController>();
        private readonly Finder<ItemReserveUIController> _reserve = new Finder<ItemReserveUIController>();
        private readonly Finder<RelicUIController> _relics = new Finder<RelicUIController>();
        private readonly Finder<BasicInfoUIPanelController> _info = new Finder<BasicInfoUIPanelController>();
        private readonly Finder<ChunkUIController> _chunk = new Finder<ChunkUIController>();
        private readonly Finder<BattleTimerController> _timer = new Finder<BattleTimerController>();
        private readonly Finder<BattleSpeedController> _speed = new Finder<BattleSpeedController>();
        private readonly Finder<NavigationUIController> _nav = new Finder<NavigationUIController>();
        private readonly Finder<InformationSidebarController> _sidebar = new Finder<InformationSidebarController>();

        public override bool IsActive()
        {
            var party = _party.Get();
            return party != null && party.gameObject.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            b.PushContext(Strings.ScreenRun, null, positions: false);
            BuildActions(b);
            if (Placing()) BuildGrid(b);
            else BuildBoard(b);
            BuildParty(b);
            BuildItems(b);
            BuildRelics(b);
            BuildInfo(b);
            BuildMap(b);
            BuildSidebar(b);
            BuildEvents(b);
            BuildSpeed(b);
            BuildMenu(b);
            b.PopContext();
        }

        // ---- actions: the Fight button while placing ----

        private void BuildActions(GraphBuilder b)
        {
            var flow = _flow.Get();
            var placement = flow != null ? flow._placementParent : null;
            if (placement == null || !placement.activeInHierarchy) return;
            b.BeginStop("actions");
            foreach (var button in placement.GetComponentsInChildren<Button>(false))
            {
                if (!GameNodes.IsShown(button) || !button.interactable) continue;
                var btn = button;
                b.AddItem(ControlId.Structural("run:action:" + button.gameObject.name + button.GetInstanceID()),
                    GameNodes.Button(btn));
            }
        }

        // ---- the placement grid (before a fight): every cell, enemy rows first ----

        // A pending keyboard "drag": the hero picked up from a board cell or a reserve slot, dropped by
        // Enter on a board cell.
        private enum MoveSource { None, Board, Reserve }
        private MoveSource _moveSource = MoveSource.None;
        private Vector2Int _moveFrom;
        private int _moveReserveIndex = -1;
        private string _moveHero;

        private bool Placing()
        {
            var flow = _flow.Get();
            var placement = flow != null ? flow._placementParent : null;
            return placement != null && placement.activeInHierarchy && RunData.Board() != null;
        }

        private bool _wasPlacing;

        public override void OnUpdate()
        {
            // The events belong to one fight: placement returning means the next one is being set up.
            bool placing = Placing();
            if (placing && !_wasPlacing && BattleEvents.Lines.Count > 0) BattleEvents.Clear();
            _wasPlacing = placing;
        }

        private void BuildGrid(GraphBuilder b)
        {
            var board = RunData.Board();
            if (board == null) return;
            int w, h;
            try { w = board.BoardWidth; h = board.BoardHeight; }
            catch (Exception) { return; }
            b.BeginStop("board");
            b.PushContext(Strings.RunGrid, null, positions: false);
            for (int y = h - 1; y >= 0; y--)
            {
                b.StartRow("grid");
                for (int x = 0; x < w; x++)
                    b.AddItem(ControlId.Structural("run:cell:" + x + ":" + y), CellNode(new Vector2Int(x, y)));
                b.EndRow();
            }
            b.PopContext();
        }

        // "Kai, column 4, row 1" / "Mushroom Tank, column 3, enemy row 2" / "empty, column 1, row 3".
        private NodeVtable CellNode(Vector2Int cell)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => Occupant(cell) ?? Strings.RunCellEmpty, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(() => CellName(cell), kind: AnnouncementKinds.Value),
                },
                SearchText = () => Occupant(cell),
                OnActivate = () => ActivateCell(cell),
                OnTooltip = () =>
                {
                    var view = RunData.TryHeroAt(cell, out var id) ? RunData.ViewOf(id) : null;
                    Core.Speech.Say((view != null ? SlotTooltips(view) : null) ?? Strings.NoTooltip, interrupt: true);
                },
            };
        }

        private static string Occupant(Vector2Int cell)
        {
            if (RunData.TryHeroAt(cell, out var hero)) return RunData.HeroName(hero) ?? Strings.RunParty;
            if (RunData.TryEnemyAt(cell, out var enemy)) return RunData.EnemyName(enemy) ?? Strings.RunBoard;
            return null;
        }

        // Rows count from the player's back line; the enemy side counts its own rows from the front.
        private static string CellName(Vector2Int cell)
        {
            if (RunData.IsPlayerCell(cell)) return Strings.RunCellPos(cell.x + 1, cell.y + 1);
            int playerRows = 0;
            for (int y = 0; y < cell.y; y++) if (RunData.IsPlayerCell(new Vector2Int(cell.x, y))) playerRows++;
            return Strings.RunCellPosEnemy(cell.x + 1, cell.y - playerRows + 1);
        }

        // Enter on a cell: drop a picked-up hero here, else open the hero's menu, else nothing to do.
        private void ActivateCell(Vector2Int cell)
        {
            if (_moveSource != MoveSource.None) { Drop(cell); return; }
            if (RunData.TryHeroAt(cell, out var id))
            {
                var view = RunData.ViewOf(id);
                OpenHeroMenu(view, id, cell, reserve: false);
                return;
            }
            Core.Speech.Say(Strings.RunCellEmpty, interrupt: true);
        }

        private void PickUpFromBoard(string hero, Vector2Int cell)
        {
            _moveSource = MoveSource.Board;
            _moveFrom = cell;
            _moveHero = hero;
            Core.Speech.Say(Strings.RunPickedUp(hero), interrupt: true);
        }

        private void PickUpFromReserve(string hero, int reserveIndex)
        {
            _moveSource = MoveSource.Reserve;
            _moveReserveIndex = reserveIndex;
            _moveHero = hero;
            Core.Speech.Say(Strings.RunPickedUp(hero), interrupt: true);
        }

        private void Drop(Vector2Int cell)
        {
            if (!RunData.IsPlayerCell(cell)) { Core.Speech.Say(Strings.RunMoveInvalid, interrupt: true); return; }
            bool ok = _moveSource == MoveSource.Board
                ? RunData.SwapBoard(_moveFrom, cell)
                : RunData.SwapReserveAndBoard(_moveReserveIndex, cell);
            string hero = _moveHero;
            CancelMove(silent: true);
            Core.Speech.Say(ok ? Strings.RunMoved(hero, CellName(cell)) : Strings.RunMoveFailed, interrupt: true);
        }

        private void CancelMove(bool silent = false)
        {
            bool had = _moveSource != MoveSource.None;
            _moveSource = MoveSource.None;
            _moveReserveIndex = -1;
            _moveHero = null;
            if (had && !silent) Core.Speech.Say(Strings.RunMoveCancelled, interrupt: true);
        }

        public override void OnPop() => CancelMove(silent: true);

        public override IEnumerable<ElementAction> GetActions()
        {
            // Escape: cancel a pending move; otherwise the game's own Escape (its settings panel).
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                if (_moveSource != MoveSource.None) { CancelMove(); return; }
                var nav = _nav.Get();
                if (nav != null && GameNodes.IsShown(nav._settingsButton) && nav._settingsButton.interactable)
                    nav._settingsButton.onClick.Invoke();
            });
        }

        // ---- the battlefield: units with health bars, heroes first ----

        private struct Unit { public string Name; public HealthBarView Bar; public bool IsHero; }

        private List<Unit> Units()
        {
            var units = new List<Unit>();
            var battle = _battle.Get();
            var bars = battle != null ? battle._healthBars : null;
            if (bars == null) return units;
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<CharacterViewController>()))
            {
                var c = o != null ? o.TryCast<CharacterViewController>() : null;
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                HealthBarView bar;
                if (!bars.TryGetValue(c.EntityId, out bar) || bar == null || !bar.gameObject.activeInHierarchy) continue;
                string name = bar._characterNameText != null ? bar._characterNameText.text : null;
                if (string.IsNullOrWhiteSpace(name)) name = c.gameObject.name.Replace("(Clone)", "");
                units.Add(new Unit { Name = name, Bar = bar, IsHero = IsHero(c) });
            }
            units.Sort((x, y) => x.IsHero == y.IsHero ? string.CompareOrdinal(x.Name, y.Name) : (x.IsHero ? -1 : 1));
            return units;
        }

        // A hero character carries a hero id; the interop nullable throws when there is none (an enemy).
        private static bool IsHero(CharacterViewController c)
        {
            try { return c.HeroId.HasValue; }
            catch (NullReferenceException) { return false; }
        }

        private void BuildBoard(GraphBuilder b)
        {
            var units = Units();
            b.BeginStop("board");
            b.PushContext(Strings.RunBoard, Strings.RoleList);
            if (units.Count == 0)
                b.AddItem(ControlId.Structural("run:board:none"), GameNodes.Text(() => Strings.RunNoUnits));
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                b.AddItem(ControlId.Structural("run:unit:" + u.Bar.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => Strings.RunUnit(u.IsHero, u.Name, Health(u.Bar)), kind: AnnouncementKinds.Label),
                        // Not live: mana and health change every tick of a fight; re-read on demand (Ctrl+Space).
                        new NodeAnnouncement(() => Mana(u.Bar), kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => u.Name,
                    OnTooltip = () => Core.Speech.Say(ItemsOn(u.Bar) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            b.PopContext();
        }

        private static string Health(HealthBarView bar)
            => bar._healthText != null ? bar._healthText.text : "";

        private static string Mana(HealthBarView bar)
        {
            var slider = bar._manaSlider;
            if (slider == null || !slider.gameObject.activeInHierarchy || slider.maxValue <= 0) return null;
            return Strings.RunMana(((int)slider.value).ToString(), ((int)slider.maxValue).ToString());
        }

        // The unit's equipped items, by name.
        private static string ItemsOn(HealthBarView bar) => ItemNodes.ItemNames(bar._itemSlotViews);

        // ---- the party: active and reserve slots ----

        private void BuildParty(GraphBuilder b)
        {
            var party = _party.Get();
            if (party == null) return;
            b.BeginStop("party");
            AddHeroPanel(b, party._activeHeroPanel, Strings.RunParty, "party", false);
            AddHeroPanel(b, party._reserveHeroPanel, Strings.RunReserve, "reserve", true);
        }

        private void AddHeroPanel(GraphBuilder b, BottomHeroPanelView panel, string label, string key, bool reserve)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            var views = panel.HeroViews;
            if (views == null) return;
            b.PushContext(label, Strings.RoleList);
            int n = 0;
            foreach (var v in views)
            {
                if (v == null || !v.gameObject.activeInHierarchy) continue;
                n++;
                int index = n;
                var view = v;
                b.AddItem(ControlId.Structural("run:" + key + ":" + view.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => reserve ? Strings.RunReserveSlot(index) : Strings.RunPartySlot(index), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => view.IsEmpty ? Strings.RunSlotEmpty : SlotSummary(view), live: true, kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => SlotSummary(view),
                    OnActivate = () => OpenHeroMenu(view, reserve),
                    OnTooltip = () => Core.Speech.Say(SlotTooltips(view) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            b.PopContext();
        }

        // The hero occupying a slot: its name, then its ability names and equipped items.
        private static string SlotSummary(BottomHeroView view)
        {
            string abilities = HeroCardNodes.AbilitiesLine(view._abilitiesView);
            string items = ItemNodes.ItemNames(view._itemSlotViews);
            string body = items == null ? abilities : abilities + "; " + items;
            string name = RunData.HeroName(view);
            return string.IsNullOrEmpty(name) ? body : name + ": " + body;
        }

        // Enter on a party/reserve slot: the hero's menu (its board cell looked up when it stands on the board).
        private void OpenHeroMenu(BottomHeroView view, bool reserve)
        {
            if (view == null || view.IsEmpty) { ClickPortrait(view); return; }
            if (!RunData.TryHeroId(view, out var heroId)) { ClickPortrait(view); return; }
            Vector2Int? cell = null;
            if (!reserve)
            {
                var hero = RunData.Hero(heroId);
                try { if (hero != null) cell = hero.CellPosition; } catch (Exception) { }
            }
            OpenHeroMenu(view, heroId, cell, reserve);
        }

        // Enter on a hero: a menu of what the mouse would do with it: inspect its card, move it (pick
        // up, then Enter on a board cell), send it to the reserve, unequip one of its items.
        private void OpenHeroMenu(BottomHeroView view, HeroId heroId, Vector2Int? cell, bool reserve)
        {
            string heroName = RunData.HeroName(heroId) ?? Strings.RunParty;
            var options = new List<ChoiceOption>();
            if (view != null) options.Add(new ChoiceOption(Strings.RunInspect, () => ClickPortrait(view)));
            if (Placing())
            {
                if (reserve && view != null)
                {
                    int index = view.Index;
                    options.Add(new ChoiceOption(Strings.RunToBoard, () => PickUpFromReserve(heroName, index)));
                }
                else if (cell.HasValue)
                {
                    var from = cell.Value;
                    options.Add(new ChoiceOption(Strings.RunMove, () => PickUpFromBoard(heroName, from)));
                    int free = RunData.FreeReserveIndex();
                    options.Add(new ChoiceOption(Strings.RunToReserve, () =>
                    {
                        Core.Speech.Say(RunData.SwapReserveAndBoard(free, from) ? Strings.RunMoved(heroName, Strings.RunReserve) : Strings.RunMoveFailed, interrupt: true);
                    }, enabled: free >= 0));
                }
            }
            if (view != null && view._itemSlotViews != null)
            {
                foreach (var slot in view._itemSlotViews)
                {
                    if (!RunData.TryItemId(slot, out var itemId)) continue;
                    string itemName = ItemNodes.ItemName(slot) ?? Strings.RunItems;
                    var id = itemId;
                    options.Add(new ChoiceOption(Strings.RunUnequip(itemName), () =>
                    {
                        if (RunData.Unequip(heroId, id)) Core.Speech.Say(Strings.RunUnequipped(itemName), interrupt: true);
                    }));
                }
            }
            ChoiceSubmenuScreen.Open(Strings.RunHeroActions(heroName), options);
        }

        // Enter on a reserve item: pick the hero to equip it to (what dragging it onto a hero does).
        private void OpenEquipMenu(PlaceholderSlotView slot)
        {
            if (!RunData.TryItemId(slot, out var itemId)) return;
            string itemName = ItemNodes.ItemName(slot) ?? Strings.RunItems;
            var options = new List<ChoiceOption>();
            var party = _party.Get();
            if (party != null)
            {
                AddEquipTargets(options, party._activeHeroPanel, itemId, itemName);
                AddEquipTargets(options, party._reserveHeroPanel, itemId, itemName);
            }
            if (options.Count == 0) { Core.Speech.Say(Strings.RunNoHeroes, interrupt: true); return; }
            ChoiceSubmenuScreen.Open(Strings.RunEquipTo(itemName), options);
        }

        private static void AddEquipTargets(List<ChoiceOption> options, BottomHeroPanelView panel, ItemId itemId, string itemName)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy || panel.HeroViews == null) return;
            foreach (var view in panel.HeroViews)
            {
                if (view == null || !view.gameObject.activeInHierarchy || view.IsEmpty) continue;
                if (!RunData.TryHeroId(view, out var heroId)) continue;
                string heroName = RunData.HeroName(view) ?? Strings.RunParty;
                string items = ItemNodes.ItemNames(view._itemSlotViews);
                var id = heroId;
                options.Add(new ChoiceOption(heroName, () =>
                {
                    Core.Speech.Say(RunData.Equip(id, itemId) ? Strings.RunEquipped(itemName, heroName) : Strings.RunEquipFailed, interrupt: true);
                }, items));
            }
        }

        private static string SlotTooltips(BottomHeroView view)
        {
            string abilities = HeroCardNodes.AbilitiesTooltips(view._abilitiesView);
            string items = ItemNodes.ItemTooltips(view._itemSlotViews);
            if (abilities == null) return items;
            return items == null ? abilities : abilities + ". " + items;
        }

        // The portrait button opens the hero's card in the sidebar (the game's own inspect action).
        private static void ClickPortrait(BottomHeroView view)
        {
            if (view == null) return;
            foreach (var button in view.GetComponentsInChildren<Button>(false))
                if (GameNodes.IsShown(button)) { button.onClick.Invoke(); return; }
        }

        // ---- items: the reserve column ----

        private void BuildItems(GraphBuilder b)
        {
            var reserve = _reserve.Get();
            if (reserve == null || !reserve.gameObject.activeInHierarchy) return;
            b.BeginStop("items");
            b.PushContext(Strings.RunItems, Strings.RoleList);
            int n = 0;
            foreach (var slot in reserve.GetComponentsInChildren<PlaceholderSlotView>(false))
            {
                if (!ItemNodes.HasItem(slot)) continue;
                n++;
                var s = slot;
                b.AddItem(ControlId.Structural("run:item:" + slot.GetInstanceID()), ItemNodes.Slot(slot, () => OpenEquipMenu(s)));
            }
            if (n == 0) b.AddItem(ControlId.Structural("run:item:none"), GameNodes.Text(() => Strings.RunNoItems));
            b.PopContext();
        }

        // ---- relics ----

        private void BuildRelics(GraphBuilder b)
        {
            var relics = _relics.Get();
            if (relics == null || !relics.gameObject.activeInHierarchy) return;
            b.BeginStop("relics");
            b.PushContext(Strings.RunRelics, Strings.RoleList);
            int n = 0;
            foreach (var relic in relics.GetComponentsInChildren<RelicView>(false))
            {
                if (relic == null || !relic.gameObject.activeInHierarchy) continue;
                n++;
                b.AddItem(ControlId.Structural("run:relic:" + relic.GetInstanceID()), ItemNodes.Relic(relic));
            }
            if (n == 0) b.AddItem(ControlId.Structural("run:relic:none"), GameNodes.Text(() => Strings.RunNoRelics));
            b.PopContext();
        }

        // ---- info: gold, shards, difficulty, timer, the act map ----

        private void BuildInfo(GraphBuilder b)
        {
            b.BeginStop("info");
            b.PushContext(Strings.RunInfo, Strings.RoleList);
            var info = _info.Get();
            if (info != null && info.gameObject.activeInHierarchy)
            {
                AddValue(b, "gold", () => TooltipReader.Title(info._currentGoldTooltip) ?? Strings.RunGold,
                    () => info._currentGoldText != null ? info._currentGoldText.text : null, info._currentGoldTooltip);
                AddValue(b, "shards", () => TooltipReader.Title(info._stabilizerTooltip) ?? Strings.RunShards,
                    () => info._accumulatedShardsText != null ? info._accumulatedShardsText.text : null, info._stabilizerTooltip);
                var difficulty = info._difficultyPanel;
                if (difficulty != null && difficulty.activeInHierarchy)
                    AddValue(b, "difficulty", () => TooltipReader.Title(info._difficultyTooltip) ?? Strings.RunDifficulty,
                        () => { var tmp = difficulty.GetComponentInChildren<TMP_Text>(false); return tmp != null ? tmp.text : null; },
                        info._difficultyTooltip);
            }
            var timer = _timer.Get();
            if (timer != null && timer._timerText != null && timer._timerText.gameObject.activeInHierarchy)
                AddValue(b, "timer", () => Strings.RunTimer, () => timer._timerText.text, null);
            b.PopContext();
        }

        private static void AddValue(GraphBuilder b, string key, Func<string> label, Func<string> value, Ember.Scopes.Application.UI.Tooltips.TooltipRaycastTarget tooltip)
        {
            b.AddItem(ControlId.Structural("run:info:" + key), new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(label, kind: AnnouncementKinds.Label),
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                OnTooltip = tooltip == null ? (Action)null
                    : () => Core.Speech.Say(TooltipReader.Describe(tooltip) ?? Strings.NoTooltip, interrupt: true),
            });
        }

        private static List<ActNodeView> MapNodes(ChunkUIController chunk)
        {
            var list = new List<ActNodeView>();
            var parent = chunk._nodeParent;
            if (parent == null) return list;
            foreach (var node in parent.GetComponentsInChildren<ActNodeView>(false))
                if (node != null && node.gameObject.activeInHierarchy) list.Add(node);
            return list;
        }

        // ---- the act map: one node per stage, the current one marked ----

        private void BuildMap(GraphBuilder b)
        {
            var chunk = _chunk.Get();
            if (chunk == null || !chunk.gameObject.activeInHierarchy) return;
            var nodes = MapNodes(chunk);
            if (nodes.Count == 0) return;
            b.BeginStop("map");
            b.PushContext(MapTitle(chunk), Strings.RoleList);
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                b.AddItem(ControlId.Structural("run:map:" + node.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        new NodeAnnouncement(() => NodeTitle(node), kind: AnnouncementKinds.Label),
                        new NodeAnnouncement(() => IsCurrent(node) ? Strings.RunMapCurrent : null, live: true, kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => NodeTitle(node),
                    OnTooltip = () => Core.Speech.Say(TooltipReader.Describe(node.TooltipRaycastTarget) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            b.PopContext();
        }

        // "map" plus the act indicator's own text ("Act 1") when it shows one.
        private static string MapTitle(ChunkUIController chunk)
        {
            var indicator = chunk._actIndicatorView;
            var text = indicator != null ? indicator._actIndicatorText : null;
            string act = text != null && text.gameObject.activeInHierarchy ? text.text : null;
            return string.IsNullOrWhiteSpace(act) ? Strings.RunMap : Strings.RunMap + ", " + act.Trim();
        }

        private static string NodeTitle(ActNodeView node)
        {
            string title = node.Title;
            if (string.IsNullOrWhiteSpace(title)) title = TooltipReader.Title(node.TooltipRaycastTarget);
            return string.IsNullOrWhiteSpace(title) ? node.gameObject.name : title;
        }

        private static bool IsCurrent(ActNodeView node)
        {
            var marker = node.CurrentMarker;
            return marker != null && marker.gameObject.activeInHierarchy && marker.enabled;
        }

        // ---- battle speed ----

        private void BuildSpeed(GraphBuilder b)
        {
            var speed = _speed.Get();
            if (speed == null || !speed.gameObject.activeInHierarchy) return;
            b.BeginStop("speed");
            b.PushContext(Strings.RunSpeed, null, positions: false);
            b.StartRow();
            int any = 0;
            if (speed._autoView != null && GameNodes.IsShown(speed._autoView.Toggle))
            {
                b.AddItem(ControlId.Structural("run:speed:auto"), GameNodes.Tab(speed._autoView.Toggle, () => Strings.RunSpeedAuto));
                any++;
            }
            var views = speed._speedViews;
            if (views != null)
                for (int i = 0; i < views.Length; i++)
                {
                    var v = views[i];
                    if (v == null || !GameNodes.IsShown(v.Toggle)) continue;
                    int n = i + 1;
                    b.AddItem(ControlId.Structural("run:speed:" + n), GameNodes.Tab(v.Toggle, () => Strings.RunSpeedN(n)));
                    any++;
                }
            if (any > 0) b.EndRow();
            else
            {
                b.EndRow();
            }
            b.PopContext();
        }

        // ---- the information sidebar: inspect cards, damage tracker, challenge ----

        private void BuildSidebar(GraphBuilder b)
        {
            var sidebar = _sidebar.Get();
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy) return;
            b.BeginStop("sidebar");
            SidebarNodes.Add(b, sidebar, "run:sidebar");
        }

        // ---- battle events: what the HUD showed, newest first ----

        private void BuildEvents(GraphBuilder b)
        {
            var lines = BattleEvents.Lines;
            b.BeginStop("events");
            b.PushContext(Strings.RunEvents, Strings.RoleList);
            if (lines.Count == 0)
                b.AddItem(ControlId.Structural("run:event:none"), GameNodes.Text(() => Strings.RunEventsEmpty));
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                b.AddItem(ControlId.Structural("run:event:" + line.Sequence), GameNodes.Text(() => line.Text));
            }
            b.PopContext();
        }

        // ---- menu: heroes panel, settings, feedback ----

        private void BuildMenu(GraphBuilder b)
        {
            var nav = _nav.Get();
            if (nav == null || !nav.gameObject.activeInHierarchy) return;
            b.BeginStop("menu");
            b.PushContext(Strings.RunMenu, Strings.RoleList);
            if (GameNodes.IsShown(nav._heroPanelButton))
                b.AddItem(ControlId.Structural("run:menu:heroes"), GameNodes.Button(nav._heroPanelButton, () => Strings.RunHeroPanel));
            if (GameNodes.IsShown(nav._choiceNavigationButton))
                b.AddItem(ControlId.Structural("run:menu:choice"), GameNodes.Button(nav._choiceNavigationButton,
                    () => nav._choiceNavigationLabel != null && !string.IsNullOrWhiteSpace(nav._choiceNavigationLabel.text)
                        ? nav._choiceNavigationLabel.text : GameNodes.LabelOf(nav._choiceNavigationButton)));
            if (GameNodes.IsShown(nav._showFeedbackPanelButton))
                b.AddItem(ControlId.Structural("run:menu:feedback"), GameNodes.Button(nav._showFeedbackPanelButton, () => Strings.RunFeedback));
            if (GameNodes.IsShown(nav._settingsButton))
                b.AddItem(ControlId.Structural("run:menu:settings"), GameNodes.Button(nav._settingsButton, () => Strings.RunSettings));
            b.PopContext();
        }
    }

    /// <summary>A throttled scene-scan cache for one controller type: re-finds it only after it was
    /// destroyed, at most every few frames, so screens can poll it every frame cheaply.</summary>
    internal sealed class Finder<T> where T : MonoBehaviour
    {
        private T _value;
        private const int SearchEvery = 30;
        private int _lastSearchFrame = -SearchEvery;

        public T Get()
        {
            if (_value != null) return _value;
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return null;
            _lastSearchFrame = Time.frameCount;
            var found = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<T>());
            _value = found != null ? found.TryCast<T>() : null;
            return _value;
        }
    }
}
