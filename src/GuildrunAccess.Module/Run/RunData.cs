using System;
using Ember.Balancing.SimulationBridge;
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
using GuildrunAccess.Module.Screens;

namespace GuildrunAccess.Module.Run
{
    /// <summary>
    /// The run's registry, read and driven the way the game's own views do it: hero and item data by
    /// id, localized names, and the registry service's equip / unequip / move calls (what the mouse
    /// drags resolve to). Everything is found live from the scene's controllers; nothing is cached
    /// across runs.
    /// </summary>
    internal static class RunData
    {
        private static readonly Finder<NavigationUIController> _nav = new Finder<NavigationUIController>();
        private static readonly Finder<BottomHeroPanelUIController> _party = new Finder<BottomHeroPanelUIController>();

        /// <summary>The registry reader (hero/item data), or null outside a run.</summary>
        public static GameRegistryDataReader Reader()
        {
            var party = _party.Get();
            return party != null ? party._gameRegistryReader : null;
        }

        /// <summary>The registry service (equip, move, discard), or null outside a run.</summary>
        public static IGameRegistryService Service()
        {
            var nav = _nav.Get();
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
            return slot != null && UI.ItemNodes.HasItem(slot) && Nullables.TryGet(() => slot.ItemId, out id);
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

        // A balancing entry's name through its localization key (the game's own localized string;
        // the English text when the key has none).
        private static string LocalizedName(INamedBalancingEntry named)
        {
            var key = named != null ? named.NameLocaKey : null;
            if (key == null) return null;
            var localized = key.LocalizedString;
            string text = localized != null ? localized.GetLocalizedString() : null;
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

        /// <summary>Whether the hero has a free item slot.</summary>
        public static bool HasFreeSlot(HeroData hero)
        {
            try { return hero != null && hero.EquippedItemCount < hero.ItemSlotCount; }
            catch (Exception) { return false; }
        }
    }
}
