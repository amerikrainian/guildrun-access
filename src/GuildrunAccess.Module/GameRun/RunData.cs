using System;
using Ember.Balancing.SimulationBridge;
using Ember.Scopes.Battle.Board.Controllers;
using Ember.Scopes.Battle.Board.Data;
using Ember.Scopes.Battle.Board.Services;
using Ember.Scopes.Battle.UI.BattleFlow;
using Ember.Scopes.GameRun.GameRegistry.Data;
using Ember.Scopes.GameRun.GameRegistry.Data.Characters;
using Ember.Scopes.GameRun.GameRegistry.Data.Items;
using Ember.Scopes.GameRun.GameRegistry.Services;
using Ember.Scopes.GameRun.UI.Navigation;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using gg.leyline.balancing.Data;
using GuildrunAccess.Core;
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

        /// <summary>Equip a reserve item to a hero's next free slot. False when the call could not be made.</summary>
        public static bool Equip(HeroId hero, ItemId item)
        {
            try
            {
                var service = Service();
                if (service == null) return false;
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

        /// <summary>A character entry's (a hero's or an enemy's) localized name, an enemy entry without
        /// one named after its named sibling; null when nothing names it.</summary>
        public static string CharacterName(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase entry)
        {
            if (entry == null) return null;
            string name = NameOf(entry);
            return !string.IsNullOrWhiteSpace(name) ? name : EnemyNames.Fallback(entry);
        }

        /// <summary>The name of the unit a character view stands for, through the registry (a hero's
        /// name, an enemy's with the nameless fallback), else its object's name; null for no view.</summary>
        public static string UnitName(Ember.Scopes.Battle.Characters.CharacterViewController unit)
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
                    string name = EnemyName(enemy);
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

        /// <summary>Whether the hero has a free item slot.</summary>
        public static bool HasFreeSlot(HeroData hero)
        {
            try { return hero != null && hero.EquippedItemCount < hero.ItemSlotCount; }
            catch (Exception) { return false; }
        }
    }
}
