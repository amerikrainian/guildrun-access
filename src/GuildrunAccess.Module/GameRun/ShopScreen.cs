using System.Collections.Generic;
using Ember.Scopes.GameRun.Shop.UI;
using Ember.Scopes.GameRun.Shop.UI.Views;
using Ember.Scopes.GameRun.UI.HeroCard;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Buffers;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The shop between fights (<see cref="ShopUIController"/>): one stop of offers, the heroes, items
    /// and relics for sale side by side as columns (up/down within one, Right and Left across, Alt+arrows
    /// too), each a container only while it has something left to buy: a sold-out container is gone,
    /// and with all three gone the stop is. A hero is the shared hero line with its price; an item or
    /// relic carries name, cost, sale tag and description (the full tooltip in the control buffer).
    /// Then the actions (reroll, freeze, the key fragment offer, proceed) and the run HUD the shop
    /// leaves on screen (the party, the inventory, info, map, sidebar, menu), where a hero's or item's
    /// menu offers Sell.
    /// Buying goes through the view's own click handler, so the game's purchase flow runs as for a
    /// mouse click. Escape presses Proceed.
    /// </summary>
    internal sealed class ShopScreen : RunPanelScreen
    {
        public ShopScreen()
        {
            Add(new ShopSection());
            AddHud();
        }

        public override string Key => "gamerun.shop";
        protected override string ContextLabel => Strings.ScreenShop;

        private static ShopUIController Shop => GameScopes.Controller<ShopUIController>();

        /// <summary>The shop controller while its panel is up and shown, else null: the shop's actions
        /// (selling) are offered only then.</summary>
        public static ShopUIController Open()
        {
            var shop = Shop;
            if (shop == null || !shop.gameObject.activeInHierarchy) return null;
            var group = shop._canvasGroup;
            return group == null || group.alpha > 0.5f ? shop : null;
        }

        public override bool IsActive() => Open() != null;

        // Alt+B: the offers (the stop is gone once everything is sold; the actions stay a Tab away).
        protected override object PanelStop => "shop:offers";
        protected override string PanelStopLabel => Strings.ShopOffers;

        protected override string PanelBackLabel
        {
            get { var shop = Shop; return shop != null ? GameNodes.LabelOf(shop._proceedButton) ?? base.PanelBackLabel : base.PanelBackLabel; }
        }

        protected override void PanelBack()
        {
            var shop = Shop;
            if (shop != null && GameNodes.IsShown(shop._proceedButton) && shop._proceedButton.interactable)
                shop._proceedButton.onClick.Invoke();
        }

        // The shop's own stops: the offers (heroes, items and relics as columns), then the actions.
        private sealed class ShopSection : ScreenSection
        {
            public override void Build(GraphBuilder b)
            {
                var shop = Shop;
                if (shop == null) return;

                // Heroes for sale: the shared list, price on the line.
                var cards = new List<HeroCardView>();
                var views = new List<HeroCardShopItemView>();
                var heroes = shop._heroChoicesPanel;
                if (heroes != null && heroes.gameObject.activeInHierarchy && heroes._heroCardShopItemViews != null)
                    foreach (var v in heroes._heroCardShopItemViews)
                    {
                        if (v == null || !v.gameObject.activeInHierarchy || v._heroCardView == null || Sold(v)) continue;
                        views.Add(v);
                        cards.Add(v._heroCardView);
                    }
                b.BeginStop("shop:offers");
                if (cards.Count > 0)
                {
                    b.SetRegion("shop:heroes");
                    b.PushContext(Strings.ShopHeroes, null, positions: true);
                    b.StartColumn();
                    HeroCardNodes.AddGrid(b, "shop:hero", cards,
                        i => () => Buy(views[i]),
                        i => { var price = HeroCardNodes.Price(cards[i]); return price != null ? Strings.ShopCost(price) : null; });
                    b.EndColumn();
                    b.PopContext();
                }

                // Items, then relics (the regular panels, or the super-shop ones when those are up).
                AddOffers(b, "items", Strings.ShopItems, shop._itemToBuyPanel, shop._superShopItemToBuyPanel);
                AddOffers(b, "relics", Strings.ShopRelics, shop._relicsToBuyPanel, shop._superShopRelicsToBuyPanel, shop._superShopTeamSizeRelicsToBuyPanel);
                b.SetRegion(null);

                // Actions.
                b.BeginStop("shop:actions");
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
                        Details = () => TooltipReader.Lines(key.TooltipRaycastTarget),
                    });
                }
                if (shop._threatLevelText != null && shop._threatLevelText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(shop._threatLevelText.text))
                    b.AddItem(ControlId.Structural("shop:threat"), GameNodes.Text(() => Strings.ShopThreat + " " + shop._threatLevelText.text));
                if (GameNodes.IsShown(shop._proceedButton))
                    b.AddItem(ControlId.Structural("shop:proceed"), GameNodes.Button(shop._proceedButton));
                b.PopContext();
            }

            // Ctrl+R and Ctrl+F: the reroll and freeze buttons pressed from anywhere on the shop,
            // focus unmoved (the keys are registered without a handler of their own, so they reach
            // the shop screen here and mean nothing elsewhere).
            public override IEnumerable<ElementAction> GetActions()
            {
                if (Open() == null) yield break;
                yield return new ElementAction("shop.reroll", Strings.Get("bind.shop.reroll"), _ => Reroll());
                yield return new ElementAction("shop.freeze", Strings.Get("bind.shop.freeze"), _ => Freeze());
            }

            // The game redraws the offers over the frames after a reroll (its template rows linger
            // for about six), so the feedback waits: the button's caption, which carries the new
            // cost, then the focused control's line, since what stood under focus changed. Two speech
            // events, the second queued behind the first: they are two facts about two controls, and
            // joined into one line the new offer read as if it were part of the reroll button.
            private static void Reroll()
            {
                var shop = Open();
                if (shop == null || !Press(shop._rerollShopButton, Strings.ShopReroll)) return;
                Later.Frames(12, () =>
                {
                    var s = Open();
                    Speech.Say(s != null ? Caption(s._rerollShopButton, Strings.ShopReroll) : Strings.ShopReroll, interrupt: true);
                    var node = Navigation.FocusedNode;
                    if (node == null) return;
                    foreach (var line in NodeLines.Lines(node)) { Speech.Say(line); break; }
                }, "shop reroll feedback");
            }

            // The freeze toggle's caption a couple of frames on; the state from the shop's reader when
            // the caption reads the same either way.
            private static void Freeze()
            {
                var shop = Open();
                if (shop == null) return;
                string before = Caption(shop._freezeShopButton, Strings.ShopFreeze);
                if (!Press(shop._freezeShopButton, Strings.ShopFreeze)) return;
                Later.Frames(2, () =>
                {
                    var s = Open();
                    if (s == null) return;
                    string after = Caption(s._freezeShopButton, Strings.ShopFreeze);
                    if (after != before) { Speech.Say(after, interrupt: true); return; }
                    var reader = s._shopReader;
                    bool frozen = reader != null && reader.IsShopFrozen != null && reader.IsShopFrozen.CurrentValue;
                    Speech.Say(after + ", " + (frozen ? Strings.ShopFrozen : Strings.ShopUnfrozen), interrupt: true);
                }, "shop freeze feedback");
            }

            // The button clicked as the game would. A hidden one is silent (the key means nothing
            // then); a disabled one, a reroll the shards cannot pay, speaks its caption with the
            // disabled state and stays.
            private static bool Press(Button button, string fallback)
            {
                if (!GameNodes.IsShown(button)) return false;
                if (!button.interactable)
                {
                    Speech.Say(Caption(button, fallback) + ", " + Strings.StateDisabled, interrupt: true);
                    return false;
                }
                button.onClick.Invoke();
                return true;
            }
        }

        // A bought card stays in the row with its card view hidden (HideHeroCard), so its price is
        // gone too. Its canvas group is NOT a sold signal: the game fades every card in from alpha 0
        // on the shop's first frames, priced and clickable all along.
        private static bool Sold(HeroCardShopItemView view)
        {
            var group = view._heroCardCanvasGroup;
            if (group != null && !group.interactable) return true;
            var card = view._heroCardView;
            if (card == null || !card.gameObject.activeInHierarchy) return true;
            return HeroCardNodes.Price(card) == null;
        }

        private static string Caption(Button button, string fallback)
        {
            string text = GameNodes.LabelOf(button);
            return string.IsNullOrWhiteSpace(text) || text == button.gameObject.name ? fallback : text;
        }

        // The offers in whichever of the given panels is showing, as one column of the offers stop (none:
        // no container). An offer is a row the game bound an
        // item or relic entry to (SetShopItem / SetShopRelic): the panel prefab also ships template
        // rows ("Very Long Item Name", cost 666) that stay active next to the real ones for the shop's
        // first frames until ClearItems destroys them, and those carry no entry.
        private static void AddOffers(GraphBuilder b, string key, string label, params ItemShopChoicesPanelView[] panels)
        {
            var items = new List<ShopItemView>();
            foreach (var panel in panels)
            {
                if (panel == null || !panel.gameObject.activeInHierarchy) continue;
                foreach (var item in panel.GetComponentsInChildren<ShopItemView>(false))
                    if (item != null && item.gameObject.activeInHierarchy && IsOffer(item)) items.Add(item);
            }
            if (items.Count == 0) return;
            b.SetRegion("shop:" + key);
            b.PushContext(label, Strings.RoleList);
            b.StartColumn();
            foreach (var item in items)
                b.AddItem(ControlId.Structural("shop:" + key + ":" + item.GetInstanceID()), Offer(item));
            b.EndColumn();
            b.PopContext();
        }

        private static bool IsOffer(ShopItemView item) => item._shopItemEntry != null || item._relicEntry != null;

        // An item or relic for sale: "name, cost X, Sale, description"; Enter buys, the tooltip is its
        // buffer line. The sale tag is the discount view's own text (the game's "Sale", or its
        // mark-up word), shown only on a discounted offer.
        private static NodeVtable Offer(ShopItemView item)
        {
            var discount = item.GetComponentInChildren<Ember.Scopes.GameRun.UI.Common.DiscountView>(true);
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => item._itemNameText != null && !string.IsNullOrWhiteSpace(item._itemNameText.text)
                        ? item._itemNameText.text : TooltipReader.Title(item._tooltipRaycastTarget)),
                    new NodeAnnouncement(() => item._itemCostText != null && !string.IsNullOrWhiteSpace(item._itemCostText.text)
                        ? Strings.ShopCost(item._itemCostText.text) : null, live: true, kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => SaleTag(discount), live: true, kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => item._itemDescriptionText != null && item._itemDescriptionText.gameObject.activeInHierarchy
                        ? item._itemDescriptionText.text : null, kind: AnnouncementKinds.Tooltip),
                },
                SearchText = () => item._itemNameText != null ? item._itemNameText.text : null,
                OnActivate = () => Click(item),
                Details = () => TooltipReader.Lines(item._tooltipRaycastTarget),
            };
        }

        private static string SaleTag(Ember.Scopes.GameRun.UI.Common.DiscountView discount)
        {
            if (discount == null || !discount.gameObject.activeInHierarchy) return null;
            var text = discount._discountText;
            return text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) ? text.text : null;
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
    }
}
