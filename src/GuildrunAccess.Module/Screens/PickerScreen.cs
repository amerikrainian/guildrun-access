using System.Collections.Generic;
using Ember.Scopes.GameRun.UI.Common;
using Ember.Scopes.GameRun.UI.HeroCard;
using Ember.Scopes.GameRun.UI.HeroSpecializations;
using Ember.Scopes.GameRun.UI.Navigation;
using Ember.Scopes.GameRun.UI.RankModifiers;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using GuildrunAccess.Module.Interop;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// A hero's rank-up choice (<see cref="BasePickerView"/>: the specialization picker and the rank
    /// modifier picker, which the run's navigation controller shows when a hero ranks up): the picker's
    /// title as the context, the hero's card as the shared hero grid, the choices as buttons carrying
    /// name, kind and description (Space for the full tooltip), and Continue once it appears. Choosing
    /// goes through the choice view's own click handler.
    /// </summary>
    public sealed class PickerScreen : Screen
    {
        public override string Key => "gamerun.picker";
        public override int Layer => 20;
        public override bool Exclusive => true;

        private static NavigationUIController Nav => GameScopes.Controller<NavigationUIController>();

        // Whichever picker the navigation controller currently shows, or null.
        private BasePickerView Picker()
        {
            var nav = Nav;
            if (nav == null) return null;
            BasePickerView spec = nav._specializationPickerView;
            if (spec != null && spec.gameObject.activeInHierarchy) return spec;
            BasePickerView rank = nav._rankModifierPickerView;
            if (rank != null && rank.gameObject.activeInHierarchy) return rank;
            return null;
        }

        public override bool IsActive() => Picker() != null;

        public override void Build(GraphBuilder b)
        {
            var picker = Picker();
            if (picker == null) return;

            b.PushContext(Title(picker), null, positions: false);

            // The hero ranking up: its card, through the shared grid (one column).
            var card = picker.GetComponentInChildren<HeroCardView>(false);
            if (card != null)
            {
                b.BeginStop("hero");
                b.PushContext(Strings.PickerHero, null, positions: false);
                HeroCardNodes.AddGrid(b, "picker:hero", new List<HeroCardView> { card }, i => null, null,
                    HeroCardNodes.StatsRow, HeroCardNodes.AbilitiesRow);
                b.PopContext();
            }

            // The choices: specializations or rank modifiers, whichever this picker offers.
            b.BeginStop("choices");
            b.PushContext(Strings.PickerChoices, Strings.RoleList);
            int n = 0;
            foreach (var choice in picker.GetComponentsInChildren<SpecializationChoiceView>(false))
            {
                if (choice == null || !choice.gameObject.activeInHierarchy) continue;
                n++;
                b.AddItem(ControlId.Structural("picker:spec:" + choice.GetInstanceID()), Specialization(choice));
            }
            foreach (var choice in picker.GetComponentsInChildren<RankModifierChoiceView>(false))
            {
                if (choice == null || !choice.gameObject.activeInHierarchy) continue;
                n++;
                b.AddItem(ControlId.Structural("picker:mod:" + choice.GetInstanceID()), Modifier(choice));
            }
            if (n == 0)
                b.AddItem(ControlId.Structural("picker:none"), GameNodes.Text(() => Strings.PickerNoChoices));
            b.PopContext();

            var cont = picker._continueButton;
            if (GameNodes.IsShown(cont))
            {
                b.BeginStop("actions");
                b.AddItem(ControlId.Structural("picker:continue"), GameNodes.Button(cont));
            }

            b.PopContext();
        }

        private static string Title(BasePickerView picker)
        {
            var title = picker._titleText;
            var tmp = title != null ? title.GetComponentInChildren<TMP_Text>(true) : null;
            string text = tmp != null ? tmp.text : null;
            return string.IsNullOrWhiteSpace(text) ? Strings.ScreenPicker : text;
        }

        // A specialization: "The Unstoppable, Passive Ability, <description>", Enter picks it.
        private static NodeVtable Specialization(SpecializationChoiceView choice)
        {
            var subtitle = choice._subtitleText != null ? choice._subtitleText.GetComponent<TMP_Text>() : null;
            return new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => choice._nameText != null ? choice._nameText.text : null),
                    new NodeAnnouncement(() => subtitle != null ? subtitle.text : null, kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => choice._descriptionText != null ? choice._descriptionText.text : null),
                },
                SearchText = () => choice._nameText != null ? choice._nameText.text : null,
                OnActivate = () => choice.OnPointerClick(new PointerEventData(EventSystem.current)),
                OnTooltip = () => GameNodes.SayTooltip(TooltipReader.Describe(choice._tooltipRaycastTarget)
                    ?? (choice._descriptionText != null ? choice._descriptionText.text : null)),
            };
        }

        // A rank modifier: "<name>, <description>", Enter picks it.
        private static NodeVtable Modifier(RankModifierChoiceView choice)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => choice._itemNameText != null && !string.IsNullOrWhiteSpace(choice._itemNameText.text)
                        ? choice._itemNameText.text : TooltipReader.Title(choice._tooltipRaycastTarget)),
                    GameNodes.TooltipPart(() => choice._modifierDescriptionText != null ? choice._modifierDescriptionText.text : null),
                },
                SearchText = () => choice._itemNameText != null ? choice._itemNameText.text : null,
                OnActivate = () => choice.OnPointerClick(new PointerEventData(EventSystem.current)),
                OnTooltip = () => GameNodes.SayTooltip(TooltipReader.Describe(choice._tooltipRaycastTarget)
                    ?? (choice._modifierDescriptionText != null ? choice._modifierDescriptionText.text : null)),
            };
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            // Escape presses Continue once the choice is made.
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var picker = Picker();
                var cont = picker != null ? picker._continueButton : null;
                if (GameNodes.IsShown(cont) && cont.interactable) cont.onClick.Invoke();
            });
        }
    }
}
