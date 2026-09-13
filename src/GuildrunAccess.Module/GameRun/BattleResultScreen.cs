using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Battle.UI.BattleFlow;
using Ember.Scopes.Battle.UI.BattleResult;
using Ember.Scopes.Battle.UI.BattleStats;
using Ember.Scopes.Battle.UI.Tracking.Views;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.Slots;
using Ember.Utilities.UI;
using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Input;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The post-fight result panel (victory, defeat, or the run's end): its title as the context, the
    /// total shards and reward lines, the battle stats (run and combat duration, each hero's combat
    /// tracker: damage dealt / taken / healing, statuses applied), the endless-mode leaderboard when the
    /// panel shows it, then the buttons (Proceed / Summary / feedback, or whatever button the panel's
    /// current form shows). Active while the battle flow shows its result parent; sits above the run HUD
    /// and owns the keys. Escape presses Proceed.
    /// </summary>
    public sealed class BattleResultScreen : Screen
    {
        public override string Key => "gamerun.result";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private static BattleFlowUIStateController Flow => GameScopes.Controller<BattleFlowUIStateController>();

        private BattleResultPanelView Panel()
        {
            var flow = Flow;
            var result = flow != null ? flow._resultParent : null;
            var panel = flow != null ? flow._battleResultPanelView : null;
            return result != null && result.activeInHierarchy && panel != null && panel.gameObject.activeInHierarchy ? panel : null;
        }

        public override bool IsActive() => Panel() != null;

        public override void Build(GraphBuilder b)
        {
            var panel = Panel();
            if (panel == null) return;

            string title = panel._panelTitleText != null && !string.IsNullOrWhiteSpace(panel._panelTitleText.text)
                ? panel._panelTitleText.text : Strings.ScreenBattleResult;
            b.PushContext(title, null, positions: false);

            // One stop for the numbers: the rewards and the stat lines; then the tabs, then their content.
            b.BeginStop("stats");
            BuildRewards(b, panel);
            BuildStats(b, panel);
            BuildLeaderboard(b, panel);
            BuildActions(b, panel);

            b.PopContext();
        }

        // ---- rewards: every reward line as the game draws it, the total when there are several ----

        private static void BuildRewards(GraphBuilder b, BattleResultPanelView panel)
        {
            var rewards = panel._rewardParent;
            var views = new List<ResultTextRewardView>();
            if (rewards != null && rewards.gameObject.activeInHierarchy)
                foreach (var view in rewards.GetComponentsInChildren<ResultTextRewardView>(false))
                    if (view != null && view.gameObject.activeInHierarchy) views.Add(view);

            // "Battle Won, Shard 15": the reward line is one control, its title and value together.
            if (views.Count > 0)
            {
                b.PushContext(Strings.RunRewards, Strings.RoleList);
                for (int i = 0; i < views.Count; i++)
                {
                    var v = views[i];
                    b.AddItem(ControlId.Structural("result:reward:" + v.GetInstanceID()), GameNodes.Text(() => RewardLine(v)));
                }
                b.PopContext();
            }

            // The total only says something new when it sums several lines (with one reward it is
            // that reward over again); captioned as the game captions it ("Total Earned").
            var total = panel._totalShardsText;
            if (views.Count != 1 && total != null && total.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(total.text))
                b.AddItem(ControlId.Structural("result:shards"), GameNodes.Text(() => TotalLine(total)));
        }

        private static string RewardLine(ResultTextRewardView view)
        {
            string title = view._titleText != null ? view._titleText.text : null;
            string value = view._valueText != null ? view._valueText.text : null;
            if (string.IsNullOrWhiteSpace(title)) return value;
            return string.IsNullOrWhiteSpace(value) ? title : title + ", " + value;
        }

        // The total row is the value text beside a title text under one parent.
        private static string TotalLine(TMP_Text total)
        {
            string title = null;
            var parent = total.transform.parent;
            if (parent != null)
                foreach (var tmp in parent.GetComponentsInChildren<TMP_Text>(false))
                    if (tmp != null && tmp != total && !string.IsNullOrWhiteSpace(tmp.text)) { title = tmp.text; break; }
            return (string.IsNullOrWhiteSpace(title) ? Strings.RunShards : title) + ", " + total.text;
        }

        // ---- stats: durations, the combat being shown, each hero's tracker ----

        private static void BuildStats(GraphBuilder b, BattleResultPanelView panel)
        {
            var stats = panel.GetComponentInChildren<BattleStatsPanel>(false);
            if (stats == null || !stats.gameObject.activeInHierarchy) return;

            b.PushContext(Strings.ResultStats, null, positions: false);
            AddLine(b, "result:duration", stats._totalDurationText);
            AddLine(b, "result:combat", stats._battleIndexText);
            AddLine(b, "result:combatduration", stats._battleDurationText);
            if (GameNodes.IsShown(stats._previousBattleButton))
                b.AddItem(ControlId.Structural("result:prevbattle"), GameNodes.Button(stats._previousBattleButton, () => Strings.ResultPreviousCombat));
            if (GameNodes.IsShown(stats._nextBattleButton))
                b.AddItem(ControlId.Structural("result:nextbattle"), GameNodes.Button(stats._nextBattleButton, () => Strings.ResultNextCombat));

            // The Tracker / Hero Stats switch, a stop of its own between the stat lines and what the
            // selected tab shows.
            var tabs = stats.GetComponentInChildren<TabView>(false);
            if (tabs != null)
            {
                int shown = 0;
                foreach (var toggle in tabs.GetComponentsInChildren<Toggle>(false))
                {
                    if (!GameNodes.IsShown(toggle)) continue;
                    if (shown++ == 0) b.BeginStop("stats:tabs");
                    var t = toggle;
                    b.AddItem(ControlId.Structural("result:tab:" + t.GetInstanceID()), GameNodes.Tab(t));
                }
            }

            // What the selected tab shows, a stop of its own: a Tab reaches the rows (the hero cards,
            // or the tracker) without arrowing past the lines and the tabs.
            b.BeginStop("stats:entries");

            // Hero Stats: one card per hero (name, stats, items).
            var cards = stats._heroCards;
            if (cards != null)
            {
                int shown = 0;
                foreach (var card in cards)
                {
                    if (card == null || !card.gameObject.activeInHierarchy) continue;
                    if (shown++ == 0) b.PushContext(Strings.EndHeroes, Strings.RoleList);
                    var c = card;
                    b.AddItem(ControlId.Structural("result:card:" + c.GetInstanceID()), new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => MiniCardLine(c)) },
                        SearchText = () => c._name != null ? c._name.text : null,
                        Details = () => MiniCardTooltips(c),
                        SideLines = HeroLines.Side(() => MiniCardRows(c), () => MiniCardTooltips(c)),
                    });
                }
                if (shown > 0) b.PopContext();
            }

            var summary = stats._damagerTrackerSummaryView;
            var heroes = summary != null ? summary._heroViews : null;
            if (heroes != null && summary.gameObject.activeInHierarchy)
            {
                b.PushContext(Strings.ResultTracker, Strings.RoleList);
                foreach (var hero in heroes)
                {
                    if (hero == null || !hero.gameObject.activeInHierarchy) continue;
                    var h = hero;
                    var tip = summary._tooltipView;
                    b.AddItem(ControlId.Structural("result:tracker:" + h.GetInstanceID()), new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => TrackerLine(h)) },
                        SearchText = () => HeroName(h),
                        // Each tracker's hover breakdown by source, then the status descriptions.
                        Details = () => TrackerTooltips(tip, h, StatusTooltips(h)),
                    });
                }
                b.PopContext();
            }
            b.PopContext();
        }

        // "damage dealt: Kai: Basic attack 2,000, Shuriken 906", "damage taken: Kai: ...", then one line
        // per status description: the row's buffer lines.
        private static List<string> TrackerTooltips(Ember.Scopes.Battle.UI.Tracking.Views.TrackerTooltipView tip, HeroBattleStatsView hero, List<string> statuses)
        {
            var parts = new List<string>();
            var trackers = hero.Trackers;
            if (trackers != null)
                foreach (var tracker in trackers)
                {
                    if (tracker == null || !tracker.gameObject.activeInHierarchy || tracker.IsEmpty) continue;
                    string breakdown = SidebarNodes.TrackerTooltip(tip, tracker.CurrentMode, tracker);
                    if (breakdown != null) parts.Add(Strings.ResultTrackerMode((int)tracker.CurrentMode) + ": " + breakdown);
                }
            parts.AddRange(statuses);
            return parts;
        }

        // "Kai: damage dealt 2,906, damage taken 1,589, healing done 1,820, Poison applied 40".
        private static string TrackerLine(HeroBattleStatsView hero)
        {
            var parts = new List<string>();
            var trackers = hero.Trackers;
            if (trackers != null)
                foreach (var tracker in trackers)
                {
                    if (tracker == null || !tracker.gameObject.activeInHierarchy || tracker.IsEmpty) continue;
                    string value = tracker._damageDealtText != null ? tracker._damageDealtText.text : null;
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    parts.Add(Strings.ResultTrackerMode((int)tracker.CurrentMode) + " " + value);
                }
            foreach (var stat in hero.GetComponentsInChildren<StatView>(false))
            {
                if (stat == null || stat._statText == null || string.IsNullOrWhiteSpace(stat._statText.text)) continue;
                string name = TooltipReader.Title(stat._tooltipRaycastTarget);
                parts.Add((string.IsNullOrEmpty(name) ? Strings.ResultStatusApplied : name) + " " + stat._statText.text);
            }
            string heroName = HeroName(hero) ?? Strings.RunParty;
            return parts.Count == 0 ? heroName : heroName + ": " + string.Join(", ", parts);
        }

        private static List<string> StatusTooltips(HeroBattleStatsView hero)
        {
            var lines = new List<string>();
            foreach (var stat in hero.GetComponentsInChildren<StatView>(false))
            {
                if (stat != null) lines.AddRange(TooltipReader.Lines(stat._tooltipRaycastTarget));
            }
            return lines;
        }

        // "Kai, health 875, mana 105, Attack 25, ..., wearing Hammer": name, stats, items.
        private static string MiniCardLine(MiniHeroCard card)
        {
            var parts = new List<string> { MiniCardName(card) };
            string stats = MiniCardStats(card);
            if (!string.IsNullOrEmpty(stats)) parts.Add(stats);
            string items = ItemNodes.ItemNames(EquipmentSlots(card));
            if (!string.IsNullOrEmpty(items)) parts.Add(Strings.RunWearing(items));
            return string.Join(", ", parts);
        }

        private static string MiniCardName(MiniHeroCard card)
        {
            string name = card._name != null ? card._name.text : null;
            return string.IsNullOrEmpty(name) ? Strings.RunParty : name;
        }

        private static string MiniCardStats(MiniHeroCard card)
        {
            var statsView = card._heroStatsView;
            return statsView != null ? HeroCardNodes.StatsLine(statsView, statsView._healthText, statsView._manaText) : null;
        }

        // The hero buffer's rows: name, stats (the mini card shows no abilities).
        private static IEnumerable<string> MiniCardRows(MiniHeroCard card) => GameNodes.Lines(MiniCardName(card), MiniCardStats(card));

        private static List<string> MiniCardTooltips(MiniHeroCard card) => ItemNodes.ItemTooltips(EquipmentSlots(card));

        private static List<PlaceholderSlotView> EquipmentSlots(MiniHeroCard card)
        {
            var list = new List<PlaceholderSlotView>();
            var equipment = card != null ? card._equipmentView : null;
            var slots = equipment != null ? equipment._equipmentSlots : null;
            if (slots == null) return list;
            for (int i = 0; i < slots.Count; i++) list.Add(slots[i]);
            return list;
        }

        private static string HeroName(HeroBattleStatsView hero)
        {
            var portrait = hero != null ? hero._heroPortrait : null;
            return portrait != null ? RunData.EntryName(portrait._characterEntry) : null;
        }

        // ---- the leaderboard (the run's end) ----

        private static void BuildLeaderboard(GraphBuilder b, BattleResultPanelView panel)
        {
            var lb = panel.GetComponentInChildren<LeaderboardController>(false);
            if (lb == null || !lb.gameObject.activeInHierarchy) return;
            LeaderboardNodes.Add(b, lb, "result:lb");
        }

        // ---- actions ----

        private static void BuildActions(GraphBuilder b, BattleResultPanelView panel)
        {
            b.BeginStop("actions");
            var added = new HashSet<int>();
            foreach (var button in new[] { panel._proceedButton, panel._summaryButton, panel._surveyButton })
            {
                if (!GameNodes.IsShown(button) || !button.interactable) continue;
                added.Add(button.GetInstanceID());
                b.AddItem(ControlId.Structural("result:btn:" + button.GetInstanceID()), GameNodes.Button(button));
            }
            // The panel's other forms (the run's end) show their own captioned buttons in its button
            // container (the flow controller's QuitButton reads "Proceed" there); the stats and
            // leaderboard buttons are declared in their own stops above. Those buttons are wired by
            // the game at runtime (no inspector listener, one AddListener), so the widget's own click
            // event is what the game hears, as for any Button; a synthetic mouse click reached them
            // only when the pointer happened to be over them in time.
            foreach (var button in OtherButtons(panel))
            {
                if (added.Contains(button.GetInstanceID())) continue;
                b.AddItem(ControlId.Structural("result:btn:" + button.GetInstanceID()), GameNodes.Button(button));
            }
        }

        private static List<Button> OtherButtons(BattleResultPanelView panel)
        {
            var list = new List<Button>();
            foreach (var button in panel.GetComponentsInChildren<Button>(false))
            {
                if (!GameNodes.IsShown(button) || !button.interactable) continue;
                if (button.GetComponentInParent<BattleStatsPanel>() != null || button.GetComponentInParent<LeaderboardController>() != null) continue;
                var caption = button.GetComponentInChildren<TMP_Text>(false);
                if (caption == null || string.IsNullOrWhiteSpace(caption.text)) continue;
                list.Add(button);
            }
            return list;
        }

        // The button Escape presses: Proceed when it shows, else the form's first captioned button.
        private static Button ProceedLike(BattleResultPanelView panel)
        {
            if (GameNodes.IsShown(panel._proceedButton) && panel._proceedButton.interactable) return panel._proceedButton;
            var others = OtherButtons(panel);
            return others.Count > 0 ? others[0] : null;
        }

        private static void AddLine(GraphBuilder b, string key, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Text(() => OneLine(text.text)));
        }

        private static string OneLine(string text) => text == null ? null : text.Replace("\r", "").Replace("\n", ", ").Trim();

        public override object InitialFocusStop => "actions";

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var panel = Panel();
                var button = panel != null ? ProceedLike(panel) : null;
                if (button != null) button.onClick.Invoke();
            });
        }
    }
}
