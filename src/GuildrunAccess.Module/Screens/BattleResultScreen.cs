using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Battle.UI.BattleFlow;
using Ember.Scopes.Battle.UI.BattleResult;
using Ember.Scopes.Battle.UI.BattleStats;
using Ember.Scopes.Battle.UI.Tracking.Views;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.MainMenu.UI;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Run;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
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

        private readonly Finder<BattleFlowUIStateController> _flow = new Finder<BattleFlowUIStateController>();

        private BattleResultPanelView Panel()
        {
            var flow = _flow.Get();
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

            BuildRewards(b, panel);
            BuildStats(b, panel);
            BuildLeaderboard(b, panel);
            BuildActions(b, panel);

            b.PopContext();
        }

        // ---- rewards: the shard total and every reward line ----

        private static void BuildRewards(GraphBuilder b, BattleResultPanelView panel)
        {
            var shards = panel._totalShardsText;
            var rewards = panel._rewardParent;
            bool shardsShown = shards != null && shards.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(shards.text);
            bool rewardsShown = rewards != null && rewards.gameObject.activeInHierarchy;
            if (!shardsShown && !rewardsShown) return;

            b.BeginStop("rewards");
            if (shardsShown)
                b.AddItem(ControlId.Structural("result:shards"), GameNodes.Text(() => Strings.RunShards + " " + shards.text));
            if (rewardsShown)
            {
                b.PushContext(Strings.RunRewards, Strings.RoleList);
                int i = 0;
                foreach (var tmp in rewards.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                    var t = tmp;
                    b.AddItem(ControlId.Structural("result:reward" + i++), GameNodes.Text(() => t.text));
                }
                b.PopContext();
            }
        }

        // ---- stats: durations, the combat being shown, each hero's tracker ----

        private static void BuildStats(GraphBuilder b, BattleResultPanelView panel)
        {
            var stats = panel.GetComponentInChildren<BattleStatsPanel>(false);
            if (stats == null || !stats.gameObject.activeInHierarchy) return;

            b.BeginStop("stats");
            b.PushContext(Strings.ResultStats, null, positions: false);
            AddLine(b, "result:duration", stats._totalDurationText);
            AddLine(b, "result:combat", stats._battleIndexText);
            AddLine(b, "result:combatduration", stats._battleDurationText);
            if (GameNodes.IsShown(stats._previousBattleButton))
                b.AddItem(ControlId.Structural("result:prevbattle"), GameNodes.Button(stats._previousBattleButton, () => Strings.ResultPreviousCombat));
            if (GameNodes.IsShown(stats._nextBattleButton))
                b.AddItem(ControlId.Structural("result:nextbattle"), GameNodes.Button(stats._nextBattleButton, () => Strings.ResultNextCombat));

            var summary = stats._damagerTrackerSummaryView;
            var heroes = summary != null ? summary._heroViews : null;
            if (heroes != null)
            {
                b.PushContext(Strings.ResultTracker, Strings.RoleList);
                foreach (var hero in heroes)
                {
                    if (hero == null || !hero.gameObject.activeInHierarchy) continue;
                    var h = hero;
                    b.AddItem(ControlId.Structural("result:tracker:" + h.GetInstanceID()), new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => TrackerLine(h)) },
                        SearchText = () => HeroName(h),
                        OnTooltip = () => Core.Speech.Say(StatusTooltips(h) ?? Strings.NoTooltip, interrupt: true),
                    });
                }
                b.PopContext();
            }
            b.PopContext();
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

        private static string StatusTooltips(HeroBattleStatsView hero)
        {
            var sb = new StringBuilder();
            foreach (var stat in hero.GetComponentsInChildren<StatView>(false))
            {
                var text = stat != null ? TooltipReader.Describe(stat._tooltipRaycastTarget) : null;
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
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
            b.BeginStop("leaderboard");
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
            // container; the stats and leaderboard buttons are declared in their own stops above.
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
