using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Battle.EndScreen;
using Ember.Scopes.Battle.EndScreen.UI;
using Ember.Scopes.GameRun.UI.Relics;
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
    /// The run's end screen (<see cref="EndScreenController"/>, after the final result and the comic):
    /// the outcome as the context, the run info (difficulty, floor, leaderboard standing), each hero of
    /// the final team as a grid column (name and title, the stat highlights, abilities, items; Space for
    /// tooltips), the backup team, the relics held, then the buttons (Quit to Menu, Summary, Continue).
    /// Sits above the result panel that stays behind it. Escape presses Continue.
    /// </summary>
    public sealed class RunEndScreen : Screen
    {
        public override string Key => "gamerun.end";
        public override int Layer => 20;
        public override bool Exclusive => true;

        private readonly Finder<EndScreenController> _end = new Finder<EndScreenController>();

        private EndScreenController Controller()
        {
            var end = _end.Get();
            return end != null && end.gameObject.activeInHierarchy ? end : null;
        }

        public override bool IsActive() => Controller() != null;

        public override void Build(GraphBuilder b)
        {
            var end = Controller();
            if (end == null) return;

            var info = end._runInfoView;
            b.PushContext(Outcome(info), null, positions: false);

            BuildInfo(b, info);
            BuildHeroes(b, end);
            BuildBackup(b, end);
            BuildRelics(b, end);
            BuildActions(b, end);

            b.PopContext();
        }

        // The outcome, in the game's own words when its title object carries text.
        private static string Outcome(EndScreenRunInfoView info)
        {
            if (info != null)
            {
                bool victory = info._victoryTitleObject != null && info._victoryTitleObject.activeInHierarchy;
                var holder = victory ? info._victoryTitleObject : info._defeatTextObject;
                var tmp = holder != null ? holder.GetComponentInChildren<TMP_Text>(false) : null;
                if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text)) return tmp.text;
                return victory ? Strings.EndVictory : Strings.EndDefeat;
            }
            return Strings.ScreenEnd;
        }

        // ---- run info ----

        private static void BuildInfo(GraphBuilder b, EndScreenRunInfoView info)
        {
            if (info == null || !info.gameObject.activeInHierarchy) return;
            b.BeginStop("info");
            b.PushContext(Strings.EndInfo, null, positions: false);
            AddLabelled(b, "end:difficulty", Strings.RunDifficulty, info._difficultyText);
            AddLabelled(b, "end:floor", Strings.EndFloor, info._floorReachedText);
            AddLabelled(b, "end:endless", null, info._endlessCycleText);
            var stats = info._leaderboardStatsParent;
            if (stats != null && stats.activeInHierarchy)
            {
                AddLabelled(b, "end:rank", Strings.EndRank, info._leaderboardRankText);
                AddLabelled(b, "end:change", Strings.EndRankChange, info._leaderboardChangeText);
                AddLabelled(b, "end:top", Strings.EndTopPercent, info._topPercentText);
            }
            b.PopContext();
        }

        private static void AddLabelled(GraphBuilder b, string key, string label, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return;
            b.AddItem(ControlId.Structural(key), GameNodes.Text(() => string.IsNullOrEmpty(label) ? text.text : label + " " + text.text));
        }

        // ---- the final team: a grid of cards ----

        private static void BuildHeroes(GraphBuilder b, EndScreenController end)
        {
            var cards = new List<EndScreenHeroCardView>();
            foreach (var card in end.GetComponentsInChildren<EndScreenHeroCardView>(false))
                if (card != null && card.gameObject.activeInHierarchy) cards.Add(card);
            if (cards.Count == 0) return;

            b.BeginStop("heroes");
            b.PushContext(Strings.EndHeroes, null, positions: true);
            const string rowKey = "end:heroes";

            // One row per detail, every card a column (the loop variable is copied per cell: the
            // cell closures read the card at speak time).
            var rows = new (string Key, string Caption, System.Func<EndScreenHeroCardView, string> Text, System.Func<EndScreenHeroCardView, string> Tooltip)[]
            {
                ("name", null, NameAndTitle, c => HeroCardNodes.AbilitiesTooltips(c._abilitiesView)),
                ("stats", Strings.HeroStats, c => Highlights(c) ?? Strings.EndNoHighlights, null),
                ("abilities", Strings.HeroAbilities, c => HeroCardNodes.AbilitiesLine(c._abilitiesView), c => HeroCardNodes.AbilitiesTooltips(c._abilitiesView)),
                ("items", Strings.RunItems, c => Items(c) ?? Strings.EndNoItems, ItemTooltips),
            };
            foreach (var row in rows)
            {
                b.StartRow(rowKey);
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    var r = row;
                    b.AddItem(ControlId.Structural("end:hero:" + i + ":" + r.Key), Cell(card, () => r.Text(card), r.Caption, r.Tooltip));
                }
                b.EndRow();
            }

            b.PopContext();
        }

        private static NodeVtable Cell(EndScreenHeroCardView card, System.Func<string> text, string caption, System.Func<EndScreenHeroCardView, string> tooltip)
        {
            var parts = new List<NodeAnnouncement>();
            if (caption != null) parts.Add(new NodeAnnouncement(() => caption));
            parts.Add(GameNodes.LabelPart(text));
            return new NodeVtable
            {
                Announcements = parts,
                SearchText = () => card._heroNameText != null ? card._heroNameText.text : null,
                OnTooltip = () =>
                {
                    string t = tooltip != null ? tooltip(card) : null;
                    Core.Speech.Say(string.IsNullOrWhiteSpace(t) ? Strings.NoTooltip : t, interrupt: true);
                },
            };
        }

        private static string NameAndTitle(EndScreenHeroCardView card)
        {
            string name = card._heroNameText != null ? card._heroNameText.text : null;
            string title = card._heroTitleText != null && card._heroTitleText.gameObject.activeInHierarchy ? card._heroTitleText.text : null;
            return string.IsNullOrWhiteSpace(title) ? name : name + ", " + title;
        }

        // The card's shown stat highlights ("Best Auto Hit 226"); hidden pairs keep prefab placeholders.
        private static string Highlights(EndScreenHeroCardView card)
        {
            var labels = card._statHighlightLabelTexts;
            var values = card._statHighlightValueTexts;
            if (labels == null || values == null) return null;
            var sb = new StringBuilder();
            for (int i = 0; i < labels.Length && i < values.Length; i++)
            {
                var label = labels[i];
                var value = values[i];
                if (label == null || value == null || !label.gameObject.activeInHierarchy || !value.gameObject.activeInHierarchy) continue;
                if (string.IsNullOrWhiteSpace(label.text) && string.IsNullOrWhiteSpace(value.text)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(label.text).Append(' ').Append(value.text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static string Items(EndScreenHeroCardView card) => ItemNodes.ItemNames(Slots(card));

        private static string ItemTooltips(EndScreenHeroCardView card) => ItemNodes.ItemTooltips(Slots(card));

        // The card's equipment slots (an IL2CPP list) as a .NET list.
        private static List<Ember.Scopes.GameRun.UI.Slots.PlaceholderSlotView> Slots(EndScreenHeroCardView card)
        {
            var list = new List<Ember.Scopes.GameRun.UI.Slots.PlaceholderSlotView>();
            var equipment = card != null ? card._endScreenEquipmentView : null;
            var slots = equipment != null ? equipment._slots : null;
            if (slots == null) return list;
            for (int i = 0; i < slots.Count; i++) list.Add(slots[i]);
            return list;
        }

        // ---- the backup team ----

        private static void BuildBackup(GraphBuilder b, EndScreenController end)
        {
            var container = end._backupTeamContainer;
            if (container == null || !container.gameObject.activeInHierarchy) return;
            var views = container.GetComponentsInChildren<EndScreenBackupHeroIconView>(false);
            int n = 0;
            foreach (var view in views)
            {
                if (view == null || !view.gameObject.activeInHierarchy) continue;
                if (n++ == 0)
                {
                    b.BeginStop("backup");
                    b.PushContext(Strings.EndBackup, Strings.RoleList);
                }
                var hero = view._bottomHeroView;
                var v = view;
                b.AddItem(ControlId.Structural("end:backup:" + view.GetInstanceID()), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => BackupName(v)) },
                    SearchText = () => BackupName(v),
                    OnTooltip = () => Core.Speech.Say((hero != null ? HeroCardNodes.AbilitiesTooltips(hero._abilitiesView) : null) ?? Strings.NoTooltip, interrupt: true),
                });
            }
            if (n > 0) b.PopContext();
        }

        private static string BackupName(EndScreenBackupHeroIconView view)
        {
            var hero = view._bottomHeroView;
            string name = hero != null ? RunData.HeroName(hero) : null;
            string abilities = hero != null ? HeroCardNodes.AbilitiesLine(hero._abilitiesView) : null;
            if (string.IsNullOrEmpty(name)) return abilities ?? view.gameObject.name;
            return string.IsNullOrEmpty(abilities) ? name : name + ": " + abilities;
        }

        // ---- relics ----

        private static void BuildRelics(GraphBuilder b, EndScreenController end)
        {
            var container = end._relicsContainer;
            if (container == null || !container.activeInHierarchy) return;
            int n = 0;
            foreach (var relic in container.GetComponentsInChildren<RelicView>(false))
            {
                if (relic == null || !relic.gameObject.activeInHierarchy) continue;
                if (n++ == 0)
                {
                    b.BeginStop("relics");
                    b.PushContext(Strings.RunRelics, Strings.RoleList);
                }
                b.AddItem(ControlId.Structural("end:relic:" + relic.GetInstanceID()), ItemNodes.Relic(relic));
            }
            if (n > 0) b.PopContext();
        }

        // ---- actions ----

        private static void BuildActions(GraphBuilder b, EndScreenController end)
        {
            var nav = end._navigationView;
            if (nav == null || !nav.gameObject.activeInHierarchy) return;
            b.BeginStop("actions");
            foreach (var button in new[] { nav._newRunButton, nav._continueButton, nav._summaryButton, nav._quitToMenuButton })
            {
                if (!GameNodes.IsShown(button) || !button.interactable) continue;
                b.AddItem(ControlId.Structural("end:btn:" + button.GetInstanceID()), GameNodes.Button(button));
            }
            var wishlist = end._wishlistWidgetView;
            var wish = wishlist != null ? wishlist._wishListButton : null;
            if (GameNodes.IsShown(wish) && wish.interactable)
                b.AddItem(ControlId.Structural("end:btn:wishlist"), GameNodes.Button(wish));
        }

        public override object InitialFocusStop => "actions";

        public override IEnumerable<ElementAction> GetActions()
        {
            // Escape: the main way on (Continue), else Proceed.
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var end = Controller();
                var nav = end != null ? end._navigationView : null;
                if (nav == null) return;
                var button = GameNodes.IsShown(nav._newRunButton) && nav._newRunButton.interactable ? nav._newRunButton
                    : GameNodes.IsShown(nav._continueButton) && nav._continueButton.interactable ? nav._continueButton : null;
                if (button != null) button.onClick.Invoke();
            });
        }
    }
}
