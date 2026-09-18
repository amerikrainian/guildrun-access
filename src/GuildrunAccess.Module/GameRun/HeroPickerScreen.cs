using System;
using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.HeroPicker;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The starting-hero choice at the top of a run (<see cref="HeroPickerController"/>): the offered
    /// cards as the shared hero grid (<see cref="HeroCardNodes.AddGrid"/>), each hero's line naming the
    /// relic bundled with it before the stats (the offer is the pair), with an extra relic row in the
    /// buffers for its description and tooltip, Enter recruiting through the card's own selection
    /// button, and the reroll button (when offered) as the last stop. The game shows nothing else
    /// here, no HUD and no Settings button, but its Escape still opens the pause menu: so does ours.
    /// </summary>
    public sealed class HeroPickerScreen : Screen
    {
        public override string Key => "gamerun.heropicker";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private static HeroPickerController Picker => GameScopes.Controller<HeroPickerController>();

        // The offered cards, in on-screen order: the active choice views under the holder.
        private static List<InitialHeroChoiceView> Choices(HeroPickerController c)
        {
            var list = new List<InitialHeroChoiceView>();
            var holder = c._heroChoiceHolder;
            if (holder == null) return list;
            foreach (var v in holder.GetComponentsInChildren<InitialHeroChoiceView>(false))
                if (v != null && v.gameObject.activeInHierarchy) list.Add(v);
            return list;
        }

        public override bool IsActive()
        {
            var c = Picker;
            if (c == null) return false;
            var panel = c._panelParent;
            return panel != null && panel.activeInHierarchy && Choices(c).Count > 0;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Picker;
            if (c == null) return;
            var choices = Choices(c);
            if (choices.Count == 0) return;

            var cards = new List<HeroCardView>(choices.Count);
            foreach (var v in choices) cards.Add(v._heroCardView);

            b.PushContext(PickerTitle(c) ?? Strings.ScreenChooseHero, Strings.RoleList, positions: false);
            b.BeginStop("heroes");

            // The relic row only when the offer bundles relics (the tutorial run has none).
            bool anyRelic = false;
            foreach (var v in choices) if (HasRelic(v)) { anyRelic = true; break; }
            var rows = new List<HeroCardNodes.GridRow>();
            if (anyRelic)
                rows.Add(new HeroCardNodes.GridRow("relic", () => Strings.HeroRelic,
                    card => RelicLine(ChoiceOf(choices, card)), card => RelicTooltip(ChoiceOf(choices, card))));

            HeroCardNodes.AddGrid(b, "hero", cards, i => () => Select(choices[i]), i => RelicSuffix(choices[i]), rows.ToArray());

            // The reroll offer, when the game shows one: only past the tutorial's save point and
            // while the profile holds a Boss Token (ReRollPanelView.Initialize shows the panel off
            // ProgressionReader.BonusTokens). Its cost is a number beside an icon, its tooltip says
            // what the number is of.
            var reroll = c._reRollPanelView;
            if (reroll != null && GameNodes.IsShown(reroll._reRollButton))
            {
                var button = reroll._reRollButton;
                b.BeginStop("actions");
                var vt = GameNodes.Button(button, () => RerollCaption(button));
                vt.Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => RerollCaption(button)),
                    new NodeAnnouncement(() => RerollCost(reroll), kind: AnnouncementKinds.Value),
                    GameNodes.DisabledPart(() => button.interactable),
                };
                vt.Details = () => TooltipReader.Lines(reroll._tooltipRaycastTarget);
                // The game swaps the offer within the click. The button outlives it while tokens
                // remain, and focus with it, so nothing would say the heroes changed: land on the
                // first new one either way (by id: once the last token hides the button, the stop
                // remembers wherever the vanished focus fell).
                vt.OnActivate = () =>
                {
                    if (!button.interactable) return;
                    button.onClick.Invoke();
                    Navigation.FocusNode(HeroCardNodes.GridId("hero", 0));
                };
                b.AddItem(ControlId.Structural("heropicker:reroll"), vt);
            }

            b.PopContext();
        }

        public override IEnumerable<GuildrunAccess.Core.UI.ElementAction> GetActions()
        {
            // Escape: the pause menu (Continue, Settings, Compendium, Abandon Run, Quit to Menu), the
            // only way out of a run that has just begun.
            yield return new GuildrunAccess.Core.UI.ElementAction(GuildrunAccess.Core.UI.ActionIds.Back, Strings.HelpPause, _ => RunSettingsScreen.Open());
        }

        private static InitialHeroChoiceView ChoiceOf(List<InitialHeroChoiceView> choices, HeroCardView card)
        {
            foreach (var v in choices) if (ReferenceEquals(v._heroCardView, card) || v._heroCardView == card) return v;
            return null;
        }

        private static void Select(InitialHeroChoiceView v)
        {
            var buttons = v._selectionButtons;
            if (buttons == null) return;
            foreach (var button in buttons)
                if (GameNodes.IsShown(button)) { button.onClick.Invoke(); return; }
        }

        private static string RerollCaption(UnityEngine.UI.Button button)
        {
            string text = GameNodes.LabelOf(button);
            return string.IsNullOrWhiteSpace(text) || text == button.gameObject.name ? Strings.HeroReroll : text;
        }

        // "cost 1 Boss Token": the panel's number, and the name of the icon beside it, which the game
        // writes nowhere but as the one bold term of the reroll's tooltip ("Consume one <b>Boss
        // Token</b> to reroll..."), its own word for it in the player's language. The number alone
        // when the description has no such term.
        private static string RerollCost(ReRollPanelView reroll)
        {
            string cost = null;
            foreach (var tmp in reroll.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "CostText" && !string.IsNullOrWhiteSpace(tmp.text)) { cost = tmp.text.Trim(); break; }
            if (cost == null) return null;
            string token = null;
            try
            {
                var description = reroll._tooltipDescription;
                var m = description != null ? BoldTerm.Match(description.GetLocalizedString() ?? "") : null;
                if (m != null && m.Success) token = m.Groups[1].Value.Trim();
            }
            catch (Exception e) { GuildrunAccess.Core.CoreLog.Warning("hero picker: the reroll's token name: " + e.Message); }
            return Strings.ShopCost(string.IsNullOrEmpty(token) ? cost : cost + " " + token);
        }

        private static readonly System.Text.RegularExpressions.Regex BoldTerm =
            new System.Text.RegularExpressions.Regex("<b>(.+?)</b>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static string PickerTitle(HeroPickerController c)
        {
            var panel = c._panelParent;
            if (panel == null) return null;
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "TitleText" && !string.IsNullOrWhiteSpace(tmp.text))
                    return tmp.text;
            return null;
        }

        // The relic panel is hidden when the offer bundles no relic; its views then still carry the
        // prefab's placeholder text, so the panel's visibility gates the row.
        private static bool HasRelic(InitialHeroChoiceView v)
            => v != null && v._relicPanel != null && v._relicPanel.gameObject.activeInHierarchy;

        // The hero line's relic part, "relic Starter Kit: Sustained Shard Boost", or nothing when the offer
        // bundles none: the pair is the choice, so its other half is not left to the buffers.
        private static string RelicSuffix(InitialHeroChoiceView v)
        {
            if (!HasRelic(v)) return null;
            string name = ItemNodes.RelicName(v._relicView);
            return string.IsNullOrWhiteSpace(name) ? null : Strings.HeroRelicNamed(name);
        }

        private static string RelicLine(InitialHeroChoiceView v)
        {
            if (!HasRelic(v)) return Strings.HeroNoRelic;
            var relic = v._relicView;
            string name = ItemNodes.RelicName(relic);
            string desc = v._relicDescriptionText != null ? v._relicDescriptionText.text : null;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(desc)) return Strings.HeroNoRelic;
            return string.IsNullOrWhiteSpace(desc) ? name : name + ", " + desc;
        }

        private static List<string> RelicTooltip(InitialHeroChoiceView v)
        {
            if (!HasRelic(v)) return null;
            var relic = v._relicView;
            return relic != null ? TooltipReader.Lines(relic._tooltipRaycastTarget) : null;
        }
    }
}
