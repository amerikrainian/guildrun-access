using Ember.Scopes.Application.UI.Tooltips;
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
using Navigation = GuildrunAccess.Core.UI.Navigation;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// A hero's rank-up choice (<see cref="BasePickerView"/>: the specialization picker and the rank
    /// modifier picker, which the run's navigation controller shows when a hero ranks up): the picker's
    /// title as the context, then one Tab-stop read top to bottom with the arrows, as an event is: the
    /// hero's line (the shared hero grid, its card in the buffers), the choices as bare buttons
    /// carrying name, kind and description (the full tooltip in the control buffer), and Continue
    /// once the choice is made, landed on as it appears. Choosing goes through the choice view's own
    /// click handler.
    /// </summary>
    public sealed class PickerScreen : Screen
    {
        public override string Key => "gamerun.picker";
        public override int Layer => 20;
        public override bool Exclusive => true;

        private static readonly ControlId ContinueId = ControlId.Structural("picker:continue");
        private bool _continueShown;

        // Continue appears once the choice is made (the chosen button gone by then): land on it, so the
        // result is a keypress away.
        public override void OnUpdate()
        {
            var picker = Picker();
            bool shown = picker != null && GameNodes.IsShown(picker._continueButton);
            if (shown && !_continueShown) Navigation.FocusNode(ContinueId);
            _continueShown = shown;
        }

        public override void OnPop() => _continueShown = false;

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
            // One Tab-stop: the hero, the choices, Continue, read top to bottom with the arrows alone.
            b.BeginStop("picker");

            // The hero ranking up: its line, through the shared grid (one column); Down is the first choice.
            var card = picker.GetComponentInChildren<HeroCardView>(false);
            if (card != null)
                HeroCardNodes.AddGrid(b, "picker:hero", new List<HeroCardView> { card }, i => null, null);

            // The choices, specializations or rank modifiers, whichever this picker offers, bare: no
            // list context and no counts, each choice its own caption.
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

            // Continue once the choice is made; before the choices arrive (the picker animating in),
            // a line saying so.
            var cont = picker._continueButton;
            if (GameNodes.IsShown(cont))
                b.AddItem(ContinueId, GameNodes.Button(cont));
            else if (n == 0)
                b.AddItem(ControlId.Structural("picker:none"), GameNodes.Text(() => Strings.PickerNoChoices));

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
                Details = () => DetailsOf(choice._tooltipRaycastTarget, choice._descriptionText),
            };
        }

        // The tooltip as lines, else the shown description as the one line.
        private static IEnumerable<string> DetailsOf(TooltipRaycastTarget target, TMPro.TMP_Text description)
        {
            var lines = TooltipReader.Lines(target);
            return lines.Count > 0 ? lines : GameNodes.Lines(description != null ? description.text : null);
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
                Details = () => DetailsOf(choice._tooltipRaycastTarget, choice._modifierDescriptionText),
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
