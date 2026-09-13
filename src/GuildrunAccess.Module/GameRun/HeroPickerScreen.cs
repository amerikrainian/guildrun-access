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

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The starting-hero choice at the top of a run (<see cref="HeroPickerController"/>): the offered
    /// cards as the shared hero grid (<see cref="HeroCardNodes.AddGrid"/>) with an extra relic row for
    /// the bundled relic, Enter recruiting through the card's own selection button, and the reroll
    /// button (when offered) as the last stop.
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
            var rows = new List<HeroCardNodes.GridRow> { HeroCardNodes.StatsRow, HeroCardNodes.AbilitiesRow };
            if (anyRelic)
                rows.Add(new HeroCardNodes.GridRow("relic", () => Strings.HeroRelic,
                    card => RelicLine(ChoiceOf(choices, card)), card => RelicTooltip(ChoiceOf(choices, card))));

            HeroCardNodes.AddGrid(b, "hero", cards, i => () => Select(choices[i]), null, rows.ToArray());

            // The reroll offer, when the game shows one.
            var reroll = c._reRollPanelView;
            if (reroll != null && GameNodes.IsShown(reroll._reRollButton))
            {
                b.BeginStop("actions");
                b.AddItem(ControlId.Structural("heropicker:reroll"),
                    GameNodes.Button(reroll._reRollButton, () => Strings.HeroReroll));
            }

            b.PopContext();
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
