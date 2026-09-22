using System;
using System.Collections.Generic;
using Ember.Balancing.SimulationBridge;
using Ember.Scopes.Battle.Board.Controllers;
using Ember.Scopes.Battle.Board.Data;
using Ember.Scopes.Battle.Board.Services;
using Ember.Scopes.Battle.UI.BattleFlow;
using Ember.Scopes.GameRun.GameRegistry.Data;
using Ember.Scopes.GameRun.GameRegistry.Data.Characters;
using Ember.Scopes.GameRun.GameRegistry.Data.Items;
using Ember.Scopes.GameRun.GameRegistry.Services;
using Ember.Scopes.GameRun.InputHandling;
using Ember.Scopes.GameRun.UI.Navigation;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using gg.leyline.balancing.Data;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using UnityEngine;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The run's registry, read and driven the way the game's own views do it: hero and item data by
    /// id, localized names, and the registry service's equip / unequip / move calls (what the mouse
    /// drags resolve to). Everything is found live from the scene's controllers; nothing is cached
    /// across runs.
    /// </summary>
    internal static class RunData
    {
        private static NavigationUIController Nav => GameScopes.Controller<NavigationUIController>();

        /// <summary>The bottom hero panels (party and reserve), or null outside a run.</summary>
        public static BottomHeroPanelUIController Party => GameScopes.Controller<BottomHeroPanelUIController>();

        /// <summary>The battle board's controller (its character registries), or null outside a battle scene.</summary>
        public static BoardController BoardController => GameScopes.Controller<BoardController>();

        /// <summary>The battle flow's state as the game's own session data has it (InitialSelection, Intro,
        /// Placement, Resolution, Result, Shop, Crossroads, ...), or null outside a run or when the
        /// reader is not reachable.</summary>
        public static Ember.Scopes.GameRun.RunSession.Data.BattleFlowState? FlowState()
        {
            try
            {
                var flow = GameScopes.Controller<BattleFlowUIStateController>();
                var reader = flow != null ? flow._runSessionReader : null;
                var state = reader != null ? reader.BattleFlowState : null;
                return state != null ? state.CurrentValue : (Ember.Scopes.GameRun.RunSession.Data.BattleFlowState?)null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("FlowState: unreadable: " + e.Message);
                return null;
            }
        }

        /// <summary>The run session's reader (the act structure, the floor indices), from whichever of
        /// the game's controllers that hold one is live: the battle flow's in a battle scene, the map
        /// strip's, the hero picker's at a run's start. Null outside a run.</summary>
        public static Ember.Scopes.GameRun.RunSession.Data.RunSessionDataReader SessionReader()
        {
            var flow = GameScopes.Controller<BattleFlowUIStateController>();
            if (flow != null && flow._runSessionReader != null) return flow._runSessionReader;
            var chunk = GameScopes.Controller<Ember.Scopes.GameRun.UI.ChunkUI.ChunkUIController>();
            if (chunk != null && chunk._runSessionReader != null) return chunk._runSessionReader;
            var picker = GameScopes.Controller<Ember.Scopes.GameRun.UI.HeroPicker.HeroPickerController>();
            return picker != null ? picker._runSessionReader : null;
        }

        /// <summary>The name of the boss the current act (chunk) ends on, as the game names it for its
        /// map strip (<c>FloorNodeData.TryGetBossName</c> on the chunk's last floor), or null.</summary>
        public static string ActBossName()
        {
            try
            {
                var reader = SessionReader();
                var chunk = reader != null ? reader.CurrentChunk : null;
                var floors = chunk != null ? chunk.Floors : null;
                if (floors == null || floors.Length == 0) return null;
                string name;
                return floors[floors.Length - 1].TryGetBossName(out name) && !string.IsNullOrWhiteSpace(name) ? name : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("ActBossName: unreadable: " + e.Message);
                return null;
            }
        }

        /// <summary>Whether the run is in the placement phase (the board editable): the battle flow shows
        /// its placement UI and a board exists.</summary>
        public static bool Placing()
        {
            var flow = GameScopes.Controller<BattleFlowUIStateController>();
            var placement = flow != null ? flow._placementParent : null;
            return placement != null && placement.activeInHierarchy && Board() != null;
        }

        /// <summary>The registry reader (hero/item data), or null outside a run.</summary>
        public static GameRegistryDataReader Reader()
        {
            var party = Party;
            return party != null ? party._gameRegistryReader : null;
        }

        /// <summary>The registry service (equip, move, discard), or null outside a run.</summary>
        public static IGameRegistryService Service()
        {
            var nav = Nav;
            return nav != null ? nav._gameRegistryService : null;
        }

        // ---- ids (nullable reads go through Nullables: the proxy misreads them) ----

        public static bool TryHeroId(BottomHeroView view, out HeroId id)
        {
            id = default;
            return view != null && !view.IsEmpty && Nullables.TryGet(() => view.HeroId, out id);
        }

        public static bool TryItemId(PlaceholderSlotView slot, out ItemId id)
        {
            id = default;
            return slot != null && ItemNodes.HasItem(slot) && Nullables.TryGet(() => slot.ItemId, out id);
        }

        public static bool TryOwnerHeroId(PlaceholderSlotView slot, out HeroId id)
        {
            id = default;
            return slot != null && Nullables.TryGet(() => slot.OwnerHeroId, out id);
        }

        // ---- data ----

        public static HeroData Hero(HeroId id)
        {
            try
            {
                var reader = Reader();
                var data = reader != null ? reader.GetHeroData(id) : null;
                return data != null ? data.TryCast<HeroData>() : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: hero lookup failed: " + e.Message);
                return null;
            }
        }

        public static ItemData Item(ItemId id)
        {
            try
            {
                var reader = Reader();
                var data = reader != null ? reader.GetItemData(id) : null;
                return data != null ? data.TryCast<ItemData>() : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: item lookup failed: " + e.Message);
                return null;
            }
        }

        // ---- names ----

        /// <summary>The hero's localized name, or null.</summary>
        public static string HeroName(HeroData hero)
        {
            try
            {
                var named = hero != null && hero.HeroEntry != null ? hero.HeroEntry.TryCast<INamedBalancingEntry>() : null;
                return LocalizedName(named);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: hero name failed: " + e.Message);
                return null;
            }
        }

        /// <summary>A character entry's (hero's or enemy's) localized name, or null.</summary>
        public static string EntryName(Ember.Balancing.Sheets.Characters.ICharacterEntry entry)
        {
            try
            {
                var named = entry != null ? entry.TryCast<INamedBalancingEntry>() : null;
                return LocalizedName(named);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: entry name failed: " + e.Message);
                return null;
            }
        }

        /// <summary>Any balancing entry's (hero, class, item, relic) localized name, or null.</summary>
        public static string NameOf(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase entry)
        {
            try
            {
                var named = entry != null ? entry.TryCast<INamedBalancingEntry>() : null;
                return LocalizedName(named);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: entry name failed: " + e.Message);
                return null;
            }
        }

        // A balancing entry's name through its localization key (the game's own localized string;
        // the English text when the key has none).
        internal static string LocalizedName(INamedBalancingEntry named)
        {
            var key = named != null ? named.NameLocaKey : null;
            if (key == null) return null;
            string text = null;
            try
            {
                var localized = key.LocalizedString;
                text = localized != null ? localized.GetLocalizedString() : null;
            }
            catch (Exception)
            {
                // A key without a table (some enemies): the English text is all there is.
            }
            return !string.IsNullOrWhiteSpace(text) ? text : key.EnglishText;
        }

        public static string HeroName(HeroId id) => HeroName(Hero(id));

        /// <summary>The name of the hero in a party/reserve slot, or null when empty or unknown.</summary>
        public static string HeroName(BottomHeroView view)
            => TryHeroId(view, out var id) ? HeroName(id) : null;

        // ---- classes and rank ----

        // The rank letters the game's hero card shows (its Rank_C .. Rank_S sprites, rank 1 first); a
        // rank past them reads as its number.
        private static readonly string[] RankLetters = { "C", "B", "A", "S" };

        /// <summary>The hero's rank as the game shows it ("C" at rank 1), or null without data.</summary>
        public static string HeroRank(HeroData hero)
        {
            if (hero == null) return null;
            try
            {
                int rank = hero.Rank;
                return rank >= 1 && rank <= RankLetters.Length ? RankLetters[rank - 1] : rank.ToString();
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: hero rank failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The hero's classes, localized, in the game's order (a specialization's extra class
        /// included); empty without data.</summary>
        public static List<string> HeroClasses(HeroData hero)
        {
            var names = new List<string>();
            try
            {
                var refs = hero != null ? hero._allClasses : null;
                if (refs == null) return names;
                for (int i = 0; i < refs.Count; i++) AddClassName(names, refs[i]);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: hero classes failed: " + e.Message);
            }
            return names;
        }

        /// <summary>A hero entry's classes (what the hero starts with), for a hero shown without its run
        /// data; empty without any.</summary>
        public static List<string> HeroClasses(Ember.Balancing.Sheets.Characters.Heroes.IHeroEntry entry)
        {
            var names = new List<string>();
            try
            {
                // The classes live on the sheet entry (the interface exposes none).
                var sheet = entry != null ? entry.TryCast<Ember.Balancing.Sheets.Characters.Heroes.HeroEntry>() : null;
                var refs = sheet != null ? sheet.GetClasses() : null;
                if (refs == null) return names;
                foreach (var reference in refs) AddClassName(names, reference);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: entry classes failed: " + e.Message);
            }
            return names;
        }

        // A class reference resolved through the game's balancing to its localized name.
        private static void AddClassName(List<string> names, BalancingRef<Ember.Balancing.Sheets.Characters.Classes.IHeroClassEntry> reference)
        {
            var balancing = Ember.Balancing.EmberBalancing.Instance;
            var entry = balancing != null ? balancing.Get<Ember.Balancing.Sheets.Characters.Classes.IHeroClassEntry>(reference) : null;
            string name = NameOf(entry);
            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }

        /// <summary>"Skorn, Warrior, rank C": the hero's name with its classes and rank, the line every
        /// control standing for a hero opens with; null without a name. Feedback about a hero ("Skorn
        /// picked up") keeps the plain <see cref="HeroName(HeroData)"/>.</summary>
        public static string HeroLabel(HeroData hero)
        {
            string name = HeroName(hero);
            return name != null ? Strings.HeroTitle(name, HeroClasses(hero), HeroRank(hero)) : null;
        }

        public static string HeroLabel(HeroId id) => HeroLabel(Hero(id));

        /// <summary>The label of the hero in a party/reserve slot, or null when empty or unknown.</summary>
        public static string HeroLabel(BottomHeroView view)
            => TryHeroId(view, out var id) ? HeroLabel(id) : null;

        /// <summary>The label of the hero a character entry stands for: the owned hero of that entry (its
        /// classes and rank as they are now), else the entry's name and starting classes.</summary>
        public static string HeroLabel(Ember.Balancing.Sheets.Characters.ICharacterEntry entry)
        {
            var owned = OwnedHero(entry);
            if (owned != null) return HeroLabel(owned);
            string name = EntryName(entry);
            if (name == null) return null;
            var hero = entry != null ? entry.TryCast<Ember.Balancing.Sheets.Characters.Heroes.IHeroEntry>() : null;
            return Strings.HeroTitle(name, HeroClasses(hero), null);
        }

        /// <summary>The party's or reserve's hero whose entry this is (the same balancing object), or
        /// null: what a portrait, which holds an entry and no id, stands for.</summary>
        public static HeroData OwnedHero(Ember.Balancing.Sheets.Characters.ICharacterEntry entry)
        {
            var party = Party;
            if (entry == null || party == null) return null;
            return OwnedHero(party._activeHeroPanel, entry) ?? OwnedHero(party._reserveHeroPanel, entry);
        }

        private static HeroData OwnedHero(BottomHeroPanelView panel, Ember.Balancing.Sheets.Characters.ICharacterEntry entry)
        {
            try
            {
                if (panel == null || panel.HeroViews == null) return null;
                foreach (var view in panel.HeroViews)
                {
                    if (view == null || view.IsEmpty) continue;
                    if (!TryHeroId(view, out var id)) continue;
                    var hero = Hero(id);
                    var owned = hero != null ? hero.HeroEntry : null;
                    if (owned != null && owned.Pointer == entry.Pointer) return hero;
                }
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: owned hero lookup failed: " + e.Message);
            }
            return null;
        }

        /// <summary>The item's localized name, or null.</summary>
        public static string ItemName(ItemData item)
        {
            try
            {
                var named = item != null && item.ItemEntry != null ? item.ItemEntry.TryCast<INamedBalancingEntry>() : null;
                return LocalizedName(named);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: item name failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The active ability the unit's party card shows (the one drawn in the active-ability
        /// frame; the first otherwise), or null.</summary>
        public static string ActiveAbilityName(Ember.Scopes.Battle.Characters.CharacterViewController unit)
        {
            try
            {
                if (unit == null || !Nullables.TryGet(() => unit.HeroId, out HeroId id)) return null;
                var view = ViewOf(id);
                var abilities = view != null ? HeroCardNodes.Abilities(view._abilitiesView) : null;
                if (abilities == null || abilities.Count == 0) return null;
                var pick = abilities[0];
                foreach (var ability in abilities)
                {
                    var frame = ability._frameImage;
                    if (frame != null && ability._activeAbilityFrame != null && frame.sprite == ability._activeAbilityFrame) { pick = ability; break; }
                }
                return UI.TooltipReader.Title(pick._tooltipRaycastTarget);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: active ability name failed: " + e.Message);
                return null;
            }
        }

        // ---- actions (the registry's own operations; the drag controllers call these) ----

        /// <summary>Equip a reserve item to a hero's next free slot. False when the call could not be
        /// made, or when the hero has no free slot: the registry's EquipItem checks none itself (the
        /// game's mouse drag checks before calling it), takes the item out of the reserve and then
        /// finds no slot to put it in, and the item is gone from every view for the rest of the run.</summary>
        public static bool Equip(HeroId hero, ItemId item)
        {
            try
            {
                var service = Service();
                if (service == null) return false;
                if (!HasFreeSlot(Hero(hero)))
                {
                    CoreLog.Warning("RunData: equip refused: the hero's item slots are full");
                    return false;
                }
                service.EquipItem(hero, item, NoSlot());
                return true;
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: equip failed: " + e);
                return false;
            }
        }

        /// <summary>Move an equipped item back to the reserve.</summary>
        public static bool Unequip(HeroId hero, ItemId item)
        {
            try
            {
                var service = Service();
                if (service == null) return false;
                service.UnequipItem(hero, item, NoSlot());
                return true;
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: unequip failed: " + e);
                return false;
            }
        }

        // "No slot index" for the registry calls: a proper empty nullable (the interop proxy rejects
        // a plain null for a nullable-typed parameter).
        private static Il2CppSystem.Nullable<int> NoSlot() => new Il2CppSystem.Nullable<int>();

        // ---- the board ----

        /// <summary>The battle board's tile reader (size, occupants), or null outside a battle scene.</summary>
        public static BoardDataReader Board()
        {
            var controller = BoardController;
            return controller != null ? controller._boardDataReader : null;
        }

        /// <summary>The board service (the drag operations: swaps between cells and reserve slots).</summary>
        public static BoardService BoardService() => Services.Resolve<BoardService>();

        public static bool TryHeroAt(Vector2Int cell, out HeroId id)
        {
            id = default;
            var board = Board();
            return board != null && Nullables.TryGet(() => board.GetHeroIdAtPosition(cell), out id);
        }

        public static bool TryEnemyAt(Vector2Int cell, out EnemyId id)
        {
            id = default;
            var board = Board();
            return board != null && Nullables.TryGet(() => board.GetEnemyIdAtPosition(cell), out id);
        }

        /// <summary>The cell a hero stands on, by the board's own tiles (the player's rows scanned for
        /// its id: about half a millisecond, a keypress's cost, never a render's); false for a hero
        /// in the reserve. The tiles, not the hero's view: they are what a move acts on, so a second
        /// move starts from where the first one put the hero, whatever its view is doing.</summary>
        public static bool TryCellOf(HeroId hero, out Vector2Int cell)
        {
            cell = default;
            var board = Board();
            if (board == null) return false;
            try
            {
                int w = board.BoardWidth, h = board.BoardHeight;
                for (int y = 0; y < h; y++)
                {
                    if (!board.IsInPlayableRange(new Vector2Int(0, y))) continue;
                    for (int x = 0; x < w; x++)
                    {
                        var at = new Vector2Int(x, y);
                        if (TryHeroAt(at, out var other) && other.Guid == hero.Guid) { cell = at; return true; }
                    }
                }
            }
            catch (Exception e) { CoreLog.Warning("RunData: hero cell lookup failed: " + e.Message); }
            return false;
        }

        /// <summary>Whether the cell is on the player's side (where heroes may be placed).</summary>
        public static bool IsPlayerCell(Vector2Int cell)
        {
            try { var board = Board(); return board != null && board.IsInPlayableRange(cell); }
            catch (Exception) { return false; }
        }

        public static EnemyData Enemy(EnemyId id)
        {
            try
            {
                var reader = Reader();
                var data = reader != null ? reader.GetEnemyData(id) : null;
                return data != null ? data.TryCast<EnemyData>() : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: enemy lookup failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The enemy's localized name; an enemy the game's data leaves nameless is named after
        /// its named sibling (<see cref="EnemyNames"/>); null when nothing names it.</summary>
        public static string EnemyName(EnemyId id)
        {
            try
            {
                var enemy = Enemy(id);
                return CharacterName(enemy != null ? enemy.CharacterEntry : null);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: enemy name failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The enemy as every control and line standing for it names it: its name, with its
        /// number when another enemy on the board shares the name ("Slime 2", <see cref="EnemyNumbers"/>).
        /// A caller naming several enemies reads the numbers once and passes them.</summary>
        public static string EnemyLabel(EnemyId id, Dictionary<string, int> numbers = null)
            => EnemyNumbers.Label(id, numbers);

        /// <summary>A character entry's (a hero's or an enemy's) localized name, an enemy entry without
        /// one named after its named sibling; null when nothing names it.</summary>
        public static string CharacterName(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase entry)
        {
            if (entry == null) return null;
            string name = NameOf(entry);
            return !string.IsNullOrWhiteSpace(name) ? name : EnemyNames.Fallback(entry);
        }

        /// <summary>The name of the unit a character view stands for, through the registry (a hero's
        /// name, an enemy's with the nameless fallback and its number among its namesakes), else its
        /// object's name; null for no view.</summary>
        public static string UnitName(Ember.Scopes.Battle.Characters.CharacterViewController unit, Dictionary<string, int> numbers = null)
        {
            if (unit == null) return null;
            try
            {
                if (Nullables.TryGet(() => unit.HeroId, out HeroId hero))
                {
                    string name = HeroName(hero);
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
                if (Nullables.TryGet(() => unit.EnemyId, out EnemyId enemy))
                {
                    string name = EnemyLabel(enemy, numbers);
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: unit name failed: " + e.Message);
            }
            return unit.gameObject.name.Replace("(Clone)", "");
        }

        /// <summary>Swap two board cells (a hero moves to an empty cell, or two heroes trade places).</summary>
        public static bool SwapBoard(Vector2Int from, Vector2Int to)
        {
            try
            {
                var service = BoardService();
                if (service == null) return false;
                service.SwapBoardPositions(from, to);
                return true;
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: board swap failed: " + e);
                return false;
            }
        }

        /// <summary>Swap a reserve slot with a board cell: places a reserve hero on the board, sends a
        /// board hero to an empty reserve slot, or trades the two.</summary>
        public static bool SwapReserveAndBoard(int reserveIndex, Vector2Int cell)
        {
            try
            {
                var service = BoardService();
                if (service == null) return false;
                service.SwapReserveAndBoardPositions(reserveIndex, cell);
                return true;
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: reserve/board swap failed: " + e);
                return false;
            }
        }

        // ---- the drag's rules ----
        // The services the keyboard's moves and sales go through check none of what follows: the game
        // asks it in its drag controllers (BattleHeroDragSubController.DragReserveHeroToBoard,
        // TryMovingHeroFromBoardToReserve, DragToDiscard; ItemDragSubController.SellItem) and answers
        // a refused drag with a dialog. The keyboard asks the same and shows the same dialog, in the
        // game's words, which the dialog screen reads. They are no courtesies: the board's limit tops
        // out at five (IHeroesSingleton.MaxTotalHeroes less one) and the catwalk lays out five
        // waypoints, so with a sixth hero on the board the walk to the next floor throws
        // (CatwalkWaypoint.GetRelatedPoints) and the run stands still behind the game's error dialog.
        // Each answers true when the move is refused, the reason already on its way to the player.

        /// <summary>A reserve hero for an EMPTY cell of a full board ("No Space Left"). A cell with a
        /// hero on it is a trade, which keeps the count.</summary>
        public static bool RefuseFullBoard(Vector2Int cell) => Refuse(
            () => TryHeroAt(cell, out _) || Reader().HasSpaceOnBoard(),
            () => GameRunInputLocalization.NoSpaceLeftTitle, () => GameRunInputLocalization.NoSpaceLeftDescription);

        /// <summary>The board's only hero for an empty reserve slot ("Cannot Move Hero").</summary>
        public static bool RefuseOnlyHeroOnBoard() => Refuse(
            () => Reader().HeroesOnBoardCount() != 1,
            () => GameRunInputLocalization.CantMoveOnlyHeroTitle, () => GameRunInputLocalization.CantMoveOnlyHeroDescription);

        /// <summary>The sale of the only hero the run owns ("Cannot Sell Hero").</summary>
        public static bool RefuseOnlyHeroSale() => Refuse(
            () => Reader().OwnedHeroCount != 1,
            () => GameRunInputLocalization.CantSellOnlyHeroTitle, () => GameRunInputLocalization.CantSellOnlyHeroDescription);

        /// <summary>The sale of the Rift Seal on a Red Rift run ("Cannot Sell Rift Seal").</summary>
        public static bool RefuseRiftSealSale(ItemId item) => Refuse(
            () => !IsRiftSeal(item),
            () => GameRunInputLocalization.CantSellRiftAnchorTitle, () => GameRunInputLocalization.CantSellRiftAnchorDescription);

        // The Red Rift's relic item, as the game's sale asks it: a challenge run, and the item the
        // difficulties singleton names. The entries are compared, not the refs (a generic struct the
        // proxy misreads: its id comes back short a letter).
        private static bool IsRiftSeal(ItemId item)
        {
            if (MissionNodes.Controller() == null) return false; // not a Red Rift run
            var balancing = Ember.Balancing.EmberBalancing.Instance;
            var difficulties = balancing.GetSingleton<Ember.Balancing.Difficulty.IDifficultiesSingleton>();
            var seal = balancing.Get<Ember.Balancing.Sheets.Items.IItemEntry>(difficulties.AnchorItemRef);
            var data = Item(item);
            var entry = data != null ? data.ItemEntry : null;
            return seal != null && entry != null && seal.Pointer == entry.Pointer;
        }

        // A rule that cannot be asked refuses too: the move stays undone, where letting it through
        // is what breaks a run.
        private static bool Refuse(Func<bool> allowed, Func<UnityEngine.Localization.LocalizedString> title, Func<UnityEngine.Localization.LocalizedString> message)
        {
            try
            {
                if (allowed()) return false;
                Ember.System.UI.DialogPanel.ShowSimpleDialog(title(), message(), null);
            }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: drag rule failed: " + e);
                Speech.Say(Strings.RunMoveFailed, interrupt: true);
            }
            return true;
        }

        /// <summary>The first empty reserve slot's index, or -1.</summary>
        public static int FreeReserveIndex()
        {
            var party = Party;
            var panel = party != null ? party._reserveHeroPanel : null;
            var views = panel != null && panel.gameObject.activeInHierarchy ? panel.HeroViews : null;
            if (views == null) return -1;
            foreach (var view in views)
                if (view != null && view.gameObject.activeInHierarchy && view.IsEmpty) return view.Index;
            return -1;
        }

        /// <summary>The party or reserve slot view showing the hero, or null.</summary>
        public static BottomHeroView ViewOf(HeroId id)
        {
            var party = Party;
            if (party == null) return null;
            foreach (var panel in new[] { party._activeHeroPanel, party._reserveHeroPanel })
            {
                var views = panel != null ? panel.HeroViews : null;
                if (views == null) continue;
                foreach (var view in views)
                    if (TryHeroId(view, out var other) && other.Guid == id.Guid) return view;
            }
            return null;
        }

        /// <summary>Whether the hero has a free item slot. Counted the game's way: its EquippedItemCount
        /// is the slot list's length, empties included, so it never says anything about room.</summary>
        public static bool HasFreeSlot(HeroData hero)
        {
            try { return hero != null && hero.GetFreeItemSlotCount() > 0; }
            catch (Exception e)
            {
                CoreLog.Warning("RunData: free slot count failed: " + e.Message);
                return false;
            }
        }
    }
}
