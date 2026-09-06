using System.Collections.Generic;
using Ember.Scopes.GameRun.Shop.UI;
using Ember.Scopes.GameRun.Shop.UI.Views;
using Ember.Scopes.GameRun.UI.HeroCard;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The shop between fights (<see cref="ShopUIController"/>): the hero offers as the shared hero grid
    /// with each card's price on its name, the items and relics for sale as controls carrying name,
    /// cost and description (Space for the full tooltip), and the actions (reroll, freeze, the key
    /// fragment offer, proceed). Buying goes through the view's own click handler, so the game's
    /// purchase flow runs as for a mouse click. Escape presses Proceed.
    /// </summary>
    public sealed class ShopScreen : Screen
    {
        public override string Key => "gamerun.shop";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private readonly Finder<ShopUIController> _shop = new Finder<ShopUIController>();

        public override bool IsActive()
        {
            var shop = _shop.Get();
            if (shop == null || !shop.gameObject.activeInHierarchy) return false;
            var group = shop._canvasGroup;
            return group == null || group.alpha > 0.5f;
        }

        public override void Build(GraphBuilder b)
        {
            var shop = _shop.Get();
            if (shop == null) return;

            b.PushContext(Strings.ScreenShop, null, positions: false);

            // Heroes for sale: the shared grid, price on the name.
            var cards = new List<HeroCardView>();
            var views = new List<HeroCardShopItemView>();
            var heroes = shop._heroChoicesPanel;
            if (heroes != null && heroes.gameObject.activeInHierarchy && heroes._heroCardShopItemViews != null)
                foreach (var v in heroes._heroCardShopItemViews)
                {
                    if (v == null || !v.gameObject.activeInHierarchy || v._heroCardView == null) continue;
                    views.Add(v);
                    cards.Add(v._heroCardView);
                }
            if (cards.Count > 0)
            {
                b.BeginStop("heroes");
                b.PushContext(Strings.ShopHeroes, null, positions: false);
                HeroCardNodes.AddGrid(b, "shop:hero", cards,
                    i => () => Buy(views[i]),
                    i => { var price = HeroCardNodes.Price(cards[i]); return price != null ? Strings.ShopCost(price) : null; },
                    HeroCardNodes.StatsRow, HeroCardNodes.AbilitiesRow);
                b.PopContext();
            }

            // Items, then relics (the regular panels, or the super-shop ones when those are up).
            AddOffers(b, "items", Strings.ShopItems, shop._itemToBuyPanel, shop._superShopItemToBuyPanel);
            AddOffers(b, "relics", Strings.ShopRelics, shop._relicsToBuyPanel, shop._superShopRelicsToBuyPanel, shop._superShopTeamSizeRelicsToBuyPanel);

            // Actions.
            b.BeginStop("actions");
            b.PushContext(Strings.ShopActions, Strings.RoleList);
            if (GameNodes.IsShown(shop._rerollShopButton))
                b.AddItem(ControlId.Structural("shop:reroll"), GameNodes.Button(shop._rerollShopButton, () => Caption(shop._rerollShopButton, Strings.ShopReroll)));
            if (GameNodes.IsShown(shop._freezeShopButton))
                b.AddItem(ControlId.Structural("shop:freeze"), GameNodes.Button(shop._freezeShopButton, () => Caption(shop._freezeShopButton, Strings.ShopFreeze)));
            var key = shop._keyFragmentButton;
            if (key != null && GameNodes.IsShown(key.PayButton))
            {
                var pay = key.PayButton;
                b.AddItem(ControlId.Structural("shop:keyfragment"), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => key.ButtonText != null && !string.IsNullOrWhiteSpace(key.ButtonText.text) ? key.ButtonText.text : GameNodes.LabelOf(pay)),
                        new NodeAnnouncement(() => key.RemainingShopsText != null && key.RemainingShopsText.gameObject.activeInHierarchy ? key.RemainingShopsText.text : null, kind: AnnouncementKinds.Value),
                        GameNodes.DisabledPart(() => pay.interactable),
                    },
                    OnActivate = () => { if (pay.interactable) pay.onClick.Invoke(); },
                    OnTooltip = () => Core.Speech.Say(TooltipReader.Describe(key.TooltipRaycastTarget) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            if (shop._threatLevelText != null && shop._threatLevelText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(shop._threatLevelText.text))
                b.AddItem(ControlId.Structural("shop:threat"), GameNodes.Text(() => Strings.ShopThreat + " " + shop._threatLevelText.text));
            if (GameNodes.IsShown(shop._proceedButton))
                b.AddItem(ControlId.Structural("shop:proceed"), GameNodes.Button(shop._proceedButton));
            b.PopContext();

            b.PopContext();
        }

        private static string Caption(Button button, string fallback)
        {
            string text = GameNodes.LabelOf(button);
            return string.IsNullOrWhiteSpace(text) || text == button.gameObject.name ? fallback : text;
        }

        // The offers in whichever of the given panels is showing.
        private static void AddOffers(GraphBuilder b, string key, string label, params ItemShopChoicesPanelView[] panels)
        {
            var items = new List<ShopItemView>();
            foreach (var panel in panels)
            {
                if (panel == null || !panel.gameObject.activeInHierarchy) continue;
                foreach (var item in panel.GetComponentsInChildren<ShopItemView>(false))
                    if (item != null && item.gameObject.activeInHierarchy) items.Add(item);
            }
            b.BeginStop(key);
            b.PushContext(label, Strings.RoleList);
            if (items.Count == 0)
                b.AddItem(ControlId.Structural("shop:" + key + ":none"), GameNodes.Text(() => Strings.ShopNothing));
            foreach (var item in items)
                b.AddItem(ControlId.Structural("shop:" + key + ":" + item.GetInstanceID()), Offer(item));
            b.PopContext();
        }

        // An item or relic for sale: "name, cost X, item, description"; Enter buys, Space reads the tooltip.
        private static NodeVtable Offer(ShopItemView item)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Item,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => item._itemNameText != null && !string.IsNullOrWhiteSpace(item._itemNameText.text)
                        ? item._itemNameText.text : TooltipReader.Title(item._tooltipRaycastTarget)),
                    new NodeAnnouncement(() => item._itemCostText != null && !string.IsNullOrWhiteSpace(item._itemCostText.text)
                        ? Strings.ShopCost(item._itemCostText.text) : null, live: true, kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => item._itemDescriptionText != null && item._itemDescriptionText.gameObject.activeInHierarchy
                        ? item._itemDescriptionText.text : null, kind: AnnouncementKinds.Tooltip),
                },
                SearchText = () => item._itemNameText != null ? item._itemNameText.text : null,
                OnActivate = () => Click(item),
                OnTooltip = () => Core.Speech.Say(TooltipReader.Describe(item._tooltipRaycastTarget) ?? Strings.NoTooltip, interrupt: true),
            };
        }

        // The view handles its own click (the game's purchase flow, confirmation and all).
        private static void Click(ShopItemView item)
        {
            var es = EventSystem.current;
            item.OnPointerClick(new PointerEventData(es));
        }

        // A hero card for sale sells through the card's own click handler (the shop subscribes to it;
        // its only Button is the compendium opener).
        private static void Buy(HeroCardShopItemView view)
        {
            var card = view._heroCardView;
            if (card == null) return;
            card.OnPointerClick(new PointerEventData(EventSystem.current));
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var shop = _shop.Get();
                if (shop != null && GameNodes.IsShown(shop._proceedButton) && shop._proceedButton.interactable)
                    shop._proceedButton.onClick.Invoke();
            });
        }
    }
}
