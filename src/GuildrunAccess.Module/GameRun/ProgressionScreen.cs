using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Application.UI.Tooltips;
using Ember.Scopes.Application.Unlock;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The meta-progression panel after a run (<see cref="ProgressionUnlockUIController"/>): the
    /// player's XP and level, every milestone on the timeline with its state (locked, unlocked, new) and
    /// the rewards it holds (named through their own tooltips; the buffer reads the milestone's tooltip and
    /// each reward's description), then New Run and Quit to Menu, all one Tab-stop read top to bottom,
    /// the landing on New Run. The screen appears once the panel's fill animation has enabled the
    /// buttons. Escape presses Quit to Menu.
    /// </summary>
    public sealed class ProgressionScreen : Screen
    {
        public override string Key => "app.progression";
        public override int Layer => 25;
        public override bool Exclusive => true;

        private static ProgressionUnlockUIController Progression => GameScopes.Controller<ProgressionUnlockUIController>();

        private ProgressionUnlockUIController Panel()
        {
            var panel = Progression;
            return panel != null && panel.gameObject.activeInHierarchy ? panel : null;
        }

        // The panel disables its buttons while the XP bar fills (_buttonsToDisableDuringAnimation,
        // SetButtonsInteractable after AnimateFillAsync, two seconds a level), so the screen waits
        // for them: a landing on New Run then never reads "disabled" for a button that comes alive
        // a moment later unannounced.
        public override bool IsActive()
        {
            var panel = Panel();
            if (panel == null) return false;
            var newRun = panel._newRunButton;
            var quit = panel._quitToMenuButton;
            return (newRun != null && newRun.interactable) || (quit != null && quit.interactable);
        }

        private static readonly ControlId NewRunId = ControlId.Structural("progression:newrun");
        private bool _landed;

        // One Tab-stop, top to bottom: the XP, the hint, the milestones, then the buttons; the first
        // landing is New Run, as it was when the buttons had a stop of their own.
        public override void Build(GraphBuilder b)
        {
            var panel = Panel();
            if (panel == null) return;

            b.PushContext(Title(panel), null, positions: false);
            b.BeginStop("progression");
            var reader = panel._unlockReader;
            if (reader != null)
                b.AddItem(ControlId.Structural("progression:xp"), GameNodes.Text(() => Strings.ProgressionXp(reader.CurrentXP, reader.CurrentLevel)));
            var hint = Hint(panel);
            if (hint != null)
                b.AddItem(ControlId.Structural("progression:hint"), GameNodes.Text(() => hint.text));

            var thresholds = panel._thresholds;
            if (thresholds != null && thresholds.Count > 0)
            {
                b.PushContext(Strings.ProgressionMilestones, Strings.RoleList);
                for (int i = 0; i < thresholds.Count; i++)
                {
                    var threshold = thresholds[i];
                    if (threshold == null || !threshold.gameObject.activeInHierarchy) continue;
                    b.AddItem(ControlId.Structural("progression:milestone:" + i), Milestone(threshold));
                }
                b.PopContext();
            }

            if (GameNodes.IsShown(panel._newRunButton))
                b.AddItem(NewRunId, GameNodes.Button(panel._newRunButton));
            if (GameNodes.IsShown(panel._quitToMenuButton))
                b.AddItem(ControlId.Structural("progression:quit"), GameNodes.Button(panel._quitToMenuButton));

            b.PopContext();
        }

        public override void OnUpdate()
        {
            if (_landed) return;
            _landed = true;
            Navigation.FocusNode(NewRunId);
        }

        public override void OnPop() => _landed = false;

        private static string Title(ProgressionUnlockUIController panel)
        {
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "Title" && !string.IsNullOrWhiteSpace(tmp.text)) return tmp.text;
            return Strings.ScreenProgression;
        }

        // The panel's bottom hint line ("Play the game to earn XP...").
        private static TMP_Text Hint(ProgressionUnlockUIController panel)
        {
            var bonus = panel._bonusTokenView;
            var holder = bonus != null ? bonus.transform.parent : null;
            return holder != null ? holder.GetComponent<TMP_Text>() : null;
        }

        // "50 XP, Tilly Bundle, locked: Shard Power Extractor, Legendary Relic; The Golden Standard, Epic Item".
        private static NodeVtable Milestone(ProgressionUnlockThresholdView threshold)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => MilestoneLabel(threshold)),
                    new NodeAnnouncement(() => StateText(threshold), live: true, kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => Rewards(threshold, false), kind: AnnouncementKinds.Tooltip),
                },
                SearchText = () => TooltipReader.Title(threshold._tooltipRaycastTarget),
                Details = () => DetailLines(threshold),
            };
        }

        private static string MilestoneLabel(ProgressionUnlockThresholdView threshold)
        {
            string xp = threshold._thresholdText != null ? threshold._thresholdText.text : null;
            string name = TooltipReader.Title(threshold._tooltipRaycastTarget);
            string head = string.IsNullOrWhiteSpace(xp) ? null : Strings.ProgressionThreshold(xp);
            if (string.IsNullOrEmpty(head)) return name;
            return string.IsNullOrEmpty(name) ? head : head + ", " + name;
        }

        private static string StateText(ProgressionUnlockThresholdView threshold)
        {
            try
            {
                switch ((int)threshold._currentState)
                {
                    case 1: return Strings.ProgressionUnlocked;
                    case 2: return Strings.ProgressionNew;
                    default: return Strings.ProgressionLocked;
                }
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        // The rewards under the milestone, named through their own tooltips ("Tilly Bundle; The Golden
        // Standard"). A hero or token-slot reward is an icon without a tooltip; the milestone's title
        // and description name those ("Tilly Bundle", "2x Boss Tokens").
        private static string Rewards(ProgressionUnlockThresholdView threshold, bool full)
        {
            var sb = new StringBuilder();
            foreach (var target in RewardTargets(threshold))
            {
                string text = TooltipReader.Heading(target);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(text);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // The buffer lines: the milestone's own tooltip, then every reward's, each its heading, summary
        // and keyword lines.
        private static IEnumerable<string> DetailLines(ProgressionUnlockThresholdView threshold)
        {
            var lines = TooltipReader.Lines(threshold._tooltipRaycastTarget);
            foreach (var target in RewardTargets(threshold)) lines.AddRange(TooltipReader.Lines(target));
            return lines;
        }

        private static IEnumerable<TooltipRaycastTarget> RewardTargets(ProgressionUnlockThresholdView threshold)
        {
            foreach (var target in threshold.GetComponentsInChildren<TooltipRaycastTarget>(false))
                if (target != null && target != threshold._tooltipRaycastTarget) yield return target;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var panel = Panel();
                var quit = panel != null ? panel._quitToMenuButton : null;
                if (GameNodes.IsShown(quit) && quit.interactable) quit.onClick.Invoke();
            });
        }
    }
}
