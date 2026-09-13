using System;
using System.Collections.Generic;
using Ember.Balancing.SimulationBridge;
using Ember.Scopes.Battle.UI.Sidebar;
using Ember.Scopes.GameRun.GameRegistry.Data.Characters;
using Ember.Scopes.GameRun.GameRegistry.Data.Items;
using Ember.Scopes.GameRun.Shop.UI;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.EnemyCard;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Scopes.GameRun.UI.Slots.HeroPanel;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Interop;
using UnityEngine;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// What Enter does to a hero or an item in the run HUD, the keyboard's version of the mouse's drags
    /// and clicks: a hero's menu (inspect its card, pick it up to move, send it to the reserve, unequip
    /// an item, sell it or an item while the shop is up), an item's "equip to which hero" list (and
    /// Sell in the shop), and the inspect itself with its landing on the sidebar card. Declares no nodes of its own: the board, party and items sections call it, and the
    /// screen's Escape consults <see cref="Moves"/>. A section so that its per-frame landing and its
    /// pop cleanup ride the composite screen's lifecycle.
    /// </summary>
    internal sealed class HeroActions : ScreenSection
    {
        /// <summary>The pending keyboard drag, shared with the board (drop) and the screen (Escape).</summary>
        public HeroMoves Moves { get; } = new HeroMoves();

        private static InformationSidebarController Sidebar => GameScopes.Controller<InformationSidebarController>();

        // ---- the hero menu ----

        /// <summary>Enter on a party/reserve slot: the hero's menu (its board cell looked up when it
        /// stands on the board).</summary>
        public void OpenHeroMenu(BottomHeroView view, bool reserve)
        {
            if (view == null || view.IsEmpty) return; // an empty slot has nothing to do
            if (!RunData.TryHeroId(view, out var heroId)) return;
            Vector2Int? cell = null;
            if (!reserve)
            {
                var hero = RunData.Hero(heroId);
                try { if (hero != null) cell = hero.CellPosition; } catch (Exception) { }
            }
            OpenHeroMenu(view, heroId, cell, reserve);
        }

        /// <summary>Enter on a hero: a menu of what the mouse would do with it: inspect its card, move it
        /// (pick up, then Enter on a board cell), send it to the reserve, unequip one of its items.</summary>
        public void OpenHeroMenu(BottomHeroView view, HeroId heroId, Vector2Int? cell, bool reserve)
        {
            string heroName = RunData.HeroName(heroId) ?? Strings.RunParty;
            var options = new List<ChoiceOption>();
            options.Add(new ChoiceOption(Strings.RunInspect, () => Inspect(heroId)));
            if (RunData.Placing())
            {
                if (reserve && view != null)
                {
                    int index = view.Index;
                    options.Add(new ChoiceOption(Strings.RunToBoard, () => Moves.PickUpFromReserve(heroName, index)));
                }
                else if (cell.HasValue)
                {
                    var from = cell.Value;
                    options.Add(new ChoiceOption(Strings.RunMove, () => Moves.PickUpFromBoard(heroName, from)));
                    int free = RunData.FreeReserveIndex();
                    options.Add(new ChoiceOption(Strings.RunToReserve, () =>
                    {
                        Speech.Say(RunData.SwapReserveAndBoard(free, from) ? Strings.RunMoved(heroName, Strings.RunReserve) : Strings.RunMoveFailed, interrupt: true);
                    }, enabled: free >= 0));
                }
            }
            var shop = ShopScreen.Open();
            if (view != null && view._itemSlotViews != null)
            {
                foreach (var slot in view._itemSlotViews)
                {
                    if (!RunData.TryItemId(slot, out var itemId)) continue;
                    string itemName = ItemNodes.ItemName(slot) ?? Strings.RunItems;
                    var id = itemId;
                    options.Add(new ChoiceOption(Strings.RunUnequip(itemName), () =>
                    {
                        if (RunData.Unequip(heroId, id)) Speech.Say(Strings.RunUnequipped(itemName), interrupt: true);
                    }));
                    if (shop != null) options.Add(SellItemOption(shop, id, itemName));
                }
            }
            // In the shop, the hero itself sells (what dragging it onto the selling panel does).
            if (shop != null && shop._shopService != null)
            {
                var service = shop._shopService;
                options.Add(new ChoiceOption(Strings.RunSell(heroName), () =>
                {
                    try { service.SellHero(heroId); Speech.Say(Strings.RunSold(heroName), interrupt: true); }
                    catch (Exception e) { CoreLog.Warning("Sell hero failed: " + e.Message); Speech.Say(Strings.RunMoveFailed, interrupt: true); }
                }));
            }
            ChoiceSubmenuScreen.Open(Strings.RunHeroActions(heroName), options);
        }

        // ---- equip ----

        /// <summary>Enter on a reserve item: pick the hero to equip it to (what dragging it onto a hero does).</summary>
        public void OpenEquipMenu(PlaceholderSlotView slot)
        {
            if (!RunData.TryItemId(slot, out var itemId)) return;
            string itemName = ItemNodes.ItemName(slot) ?? Strings.RunItems;
            var options = new List<ChoiceOption>();
            var party = RunData.Party;
            if (party != null)
            {
                AddEquipTargets(options, party._activeHeroPanel, itemId, itemName);
                AddEquipTargets(options, party._reserveHeroPanel, itemId, itemName);
            }
            var shop = ShopScreen.Open();
            if (shop != null) options.Add(SellItemOption(shop, itemId, itemName));
            if (options.Count == 0) { Speech.Say(Strings.RunNoHeroes, interrupt: true); return; }
            ChoiceSubmenuScreen.Open(Strings.RunEquipTo(itemName), options);
        }

        // "Sell X for N shards": the shop service sells by item id, the price by the item's rarity (what
        // the selling panel shows while an item is dragged over it).
        private static ChoiceOption SellItemOption(ShopUIController shop, ItemId itemId, string itemName)
        {
            int price = -1;
            try
            {
                var data = RunData.Item(itemId);
                var entry = data != null ? data.ItemEntry : null;
                if (entry != null && shop._shopReader != null) price = shop._shopReader.GetItemSellPrice(entry.Rarity);
            }
            catch (Exception e) { CoreLog.Warning("Sell price: " + e.Message); }
            string label = price >= 0 ? Strings.RunSellFor(itemName, price) : Strings.RunSell(itemName);
            var service = shop._shopService;
            return new ChoiceOption(label, () =>
            {
                if (service == null) return;
                try { service.SellItem(itemId); Speech.Say(Strings.RunSold(itemName), interrupt: true); }
                catch (Exception e) { CoreLog.Warning("Sell item failed: " + e.Message); Speech.Say(Strings.RunMoveFailed, interrupt: true); }
            }, enabled: service != null);
        }

        private static void AddEquipTargets(List<ChoiceOption> options, BottomHeroPanelView panel, ItemId itemId, string itemName)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy || panel.HeroViews == null) return;
            foreach (var view in panel.HeroViews)
            {
                if (view == null || !view.gameObject.activeInHierarchy || view.IsEmpty) continue;
                if (!RunData.TryHeroId(view, out var heroId)) continue;
                string heroName = RunData.HeroName(view) ?? Strings.RunParty;
                var id = heroId;
                options.Add(new ChoiceOption(heroName, () =>
                {
                    Speech.Say(RunData.Equip(id, itemId) ? Strings.RunEquipped(itemName, heroName) : Strings.RunEquipFailed, interrupt: true);
                }, RunLabels.Wearing(view._itemSlotViews)));
            }
        }

        // ---- inspect: the hero's card in the sidebar ----

        // The game inspects on a mouse click over the slot: its CharacterCardController polls the pointer
        // (no widget event to invoke) and publishes ShowHeroCardNotification, whose only subscriber is
        // the sidebar. Asking the sidebar directly is that same path without a pointer. Focus follows
        // the card, but only once we are back on the run screen: the menu's close re-attaches the
        // navigator and drops any focus request made before that, so the landing is applied from OnUpdate.
        public void Inspect(HeroId heroId)
        {
            var sidebar = Sidebar;
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy)
            {
                CoreLog.Warning("Inspect: no information sidebar in the scene");
                Speech.Say(Strings.RunInspectFailed, interrupt: true);
                return;
            }
            try { sidebar.ShowHeroCard(heroId); }
            catch (Exception e)
            {
                CoreLog.Warning("Inspect: ShowHeroCard threw: " + e.Message);
                Speech.Say(Strings.RunInspectFailed, interrupt: true);
                return;
            }
            _inspectPending = true;
            _inspectEnemy = false;
            _inspectDeadline = NavInput.Current.FrameCount + InspectLandingFrames;
        }

        /// <summary>The same for an enemy on the board: the sidebar shows its card (abilities, stats),
        /// and focus lands on it.</summary>
        public void InspectEnemy(EnemyId enemyId)
        {
            var sidebar = Sidebar;
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy)
            {
                CoreLog.Warning("Inspect: no information sidebar in the scene");
                Speech.Say(Strings.RunInspectFailed, interrupt: true);
                return;
            }
            try { sidebar.ShowEnemyCard(enemyId); }
            catch (Exception e)
            {
                CoreLog.Warning("Inspect: ShowEnemyCard threw: " + e.Message);
                Speech.Say(Strings.RunInspectFailed, interrupt: true);
                return;
            }
            _inspectPending = true;
            _inspectEnemy = true;
            _inspectDeadline = NavInput.Current.FrameCount + InspectLandingFrames;
        }

        /// <summary>Show a hero's card in the sidebar without moving focus: what the game does when the
        /// mouse hovers the hero; a board cell does it on landing so the buffers can read the card.</summary>
        public static void PeekHero(HeroId heroId)
        {
            var sidebar = Sidebar;
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy) return;
            try { sidebar.ShowHeroCard(heroId); }
            catch (Exception e) { CoreLog.Warning("Peek: ShowHeroCard threw: " + e.Message); }
        }

        /// <summary>The same for an enemy on the board.</summary>
        public static void PeekEnemy(EnemyId enemyId)
        {
            var sidebar = Sidebar;
            if (sidebar == null || !sidebar.gameObject.activeInHierarchy) return;
            try { sidebar.ShowEnemyCard(enemyId); }
            catch (Exception e) { CoreLog.Warning("Peek: ShowEnemyCard threw: " + e.Message); }
        }

        /// <summary>The sidebar's enemy card when it is showing the named enemy, else null.</summary>
        public static EnemyCardView ShownEnemyCard(string enemyName)
        {
            var sidebar = Sidebar;
            var card = sidebar != null ? sidebar._enemyCardView : null;
            if (card == null || !card.gameObject.activeInHierarchy || card._nameText == null) return null;
            return string.Equals(card._nameText.text, enemyName, StringComparison.Ordinal) ? card : null;
        }

        /// <summary>The sidebar's hero card when it is showing the named hero, else null.</summary>
        public static HeroCardView ShownHeroCard(string heroName)
        {
            var sidebar = Sidebar;
            var card = sidebar != null ? sidebar._heroCardView : null;
            if (card == null || !card.gameObject.activeInHierarchy) return null;
            string shown = HeroCardNodes.NameAndClass(card);
            return shown != null && heroName != null && shown.StartsWith(heroName, StringComparison.Ordinal) ? card : null;
        }

        // The card's name row is the landing; the card may take a frame or two to show.
        private const int InspectLandingFrames = 60;
        private bool _inspectPending;
        private bool _inspectEnemy;
        private int _inspectDeadline;

        public override void OnUpdate()
        {
            if (!_inspectPending) return;
            var sidebar = Sidebar;
            UnityEngine.Component card = sidebar == null ? null
                : _inspectEnemy ? (UnityEngine.Component)sidebar._enemyCardView : sidebar._heroCardView;
            if (card != null && card.gameObject.activeInHierarchy)
            {
                _inspectPending = false;
                Navigation.FocusNode(_inspectEnemy ? SidebarNodes.EnemyCardId(SidebarSection.KeyPrefix)
                    : SidebarNodes.HeroCardId(SidebarSection.KeyPrefix));
            }
            else if (NavInput.Current.FrameCount >= _inspectDeadline)
            {
                _inspectPending = false;
                CoreLog.Warning("Inspect: the hero card did not show within " + InspectLandingFrames + " frames");
                Speech.Say(Strings.RunInspectFailed, interrupt: true);
            }
        }

        /// <summary>Leaving the run: a picked-up hero is put down silently.</summary>
        public override void OnPop()
        {
            Moves.Cancel(silent: true);
            _inspectPending = false;
        }
    }
}
