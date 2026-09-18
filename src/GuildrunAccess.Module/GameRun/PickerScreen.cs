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
    /// click handler: the view type the shown picker drives, since the rank modifier prefab carries a
    /// <see cref="SpecializationChoiceView"/> on the same object as its
    /// <see cref="RankModifierChoiceView"/> (the same texts wired to both, only the latter's click
    /// subscribed), so listing every view of both types read each modifier twice, the first copy dead.
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
            // list context and no counts, each choice its own caption. Only the view type this picker
            // drives (each picker instantiates its own prefab and subscribes its own view's click): the
            // modifier prefab's twin SpecializationChoiceView is inert.
            int n = 0;
            if (picker.TryCast<SpecializationPickerView>() != null)
            {
                foreach (var choice in picker.GetComponentsInChildren<SpecializationChoiceView>(false))
                {
                    if (choice == null || !choice.gameObject.activeInHierarchy) continue;
                    n++;
                    b.AddItem(ControlId.Structural("picker:spec:" + choice.GetInstanceID()), Specialization(choice));
                }
            }
            else
            {
                foreach (var choice in picker.GetComponentsInChildren<RankModifierChoiceView>(false))
                {
                    if (choice == null || !choice.gameObject.activeInHierarchy) continue;
                    n++;
                    b.AddItem(ControlId.Structural("picker:mod:" + choice.GetInstanceID()), Modifier(choice));
                }
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

        // A specialization: "The Conduit, Passive Ability, Mystic, Backup, Stealth, Gain the Mystic
        // class and 6 Mana Regen. <description>", Enter picks it. The tags are the card's banners, icons
        // alone (the class the path adds, its archetypes); the sentence before the description is the
        // card's additional-class panel, which only a path that adds a class shows, and which carries
        // that path's stat gains too. A path whose ability is an active one shows the mana it brings
        // in the same place ("20/50 Mana": nine of the demo's paths), and a path that comes with an
        // item shows it under the description (Irini's The Olympic, the demo's one: an icon, the
        // description naming it), the item's own tooltip following the ability's in the control buffer.
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
                    new NodeAnnouncement(() => Tags(choice), kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => AdditionalClass(choice)),
                    GameNodes.TooltipPart(() => ActiveMana(choice)),
                    GameNodes.TooltipPart(() => choice._descriptionText != null ? choice._descriptionText.text : null),
                    GameNodes.TooltipPart(() => ItemName(choice)),
                },
                SearchText = () => choice._nameText != null ? choice._nameText.text : null,
                OnActivate = () => choice.OnPointerClick(new PointerEventData(EventSystem.current)),
                Details = () =>
                {
                    var lines = new List<string>(DetailsOf(choice, choice._tooltipRaycastTarget, choice._descriptionText));
                    var item = ShownItem(choice);
                    if (item != null) lines.AddRange(TooltipReader.Lines(item.TooltipRaycastTarget));
                    return lines;
                },
            };
        }

        // The active-ability panel's one text while the card shows it, "[20/50 Mana]" (the mana the
        // hero starts a fight with and the ability's cost), without the brackets it is drawn in.
        private static string ActiveMana(SpecializationChoiceView choice)
        {
            var container = choice._activeAbilityContainer;
            if (container == null || !container.activeInHierarchy) return null;
            var view = choice._activeAbilityView;
            var text = view != null ? view._mana : null;
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) return null;
            return text.text.Trim().TrimStart('[').TrimEnd(']');
        }

        // The item a path comes with, while the card shows one (a hidden view keeps the prefab's).
        private static Ember.Scopes.Application.UI.Common.ItemView ShownItem(SpecializationChoiceView choice)
        {
            var item = choice._itemView;
            return item != null && item.gameObject.activeInHierarchy ? item : null;
        }

        // The item's name, unless the focus line has it already: the picker's card draws the icon alone
        // and leaves the naming to the description ("Gain the unique quest item Zeus's Thunder."), so
        // the name is said once. A card that writes the name, or a description that does not, gets
        // "Zeus's Thunder, item" after the description.
        private static string ItemName(SpecializationChoiceView choice)
        {
            var item = ShownItem(choice);
            var text = item != null ? item._itemNameText : null;
            string name = text != null ? text.text : null;
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (!text.gameObject.activeInHierarchy)
            {
                string description = choice._descriptionText != null ? GuildrunAccess.Contracts.TextFilter.Clean(choice._descriptionText.text) : null;
                string plain = GuildrunAccess.Contracts.TextFilter.Clean(name);
                if (description != null && !string.IsNullOrWhiteSpace(plain)
                    && description.IndexOf(plain.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0) return null;
            }
            return name.Trim() + ", " + Strings.Get("role.item");
        }

        // "Mystic, Backup, Stealth", or null for a card without banners.
        private static string Tags(Component choice)
        {
            var names = HeroCardNodes.TagNames(choice);
            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        // The additional-class panel's sentence while the card shows it (a hidden one keeps the
        // prefab's "Become a Duelist").
        private static string AdditionalClass(SpecializationChoiceView choice)
        {
            var container = choice._additionalClassContainer;
            if (container == null || !container.activeInHierarchy) return null;
            var view = choice._additionalClassView;
            var text = view != null ? view._additionalClassText : null;
            return text != null && text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) ? text.text : null;
        }

        // The tooltip as lines, else the shown description as the one line; then the banners' tooltips
        // (what the added class plays like, what an archetype stands for).
        private static IEnumerable<string> DetailsOf(Component choice, TooltipRaycastTarget target, TMPro.TMP_Text description)
        {
            var lines = TooltipReader.Lines(target);
            if (lines.Count == 0) lines.AddRange(GameNodes.Lines(description != null ? description.text : null));
            lines.AddRange(HeroCardNodes.TagsTooltips(choice));
            return lines;
        }

        // A rank modifier: "<name>, Class Upgrade, <description>", Enter picks it. The kind is the card's
        // subtitle, which the twin SpecializationChoiceView on the same object holds (the modifier view
        // has no field for it); a card without one says nothing there.
        private static NodeVtable Modifier(RankModifierChoiceView choice)
        {
            var twin = choice.GetComponent<SpecializationChoiceView>();
            var subtitle = twin != null && twin._subtitleText != null ? twin._subtitleText.GetComponent<TMP_Text>() : null;
            return new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => choice._itemNameText != null && !string.IsNullOrWhiteSpace(choice._itemNameText.text)
                        ? choice._itemNameText.text : TooltipReader.Title(choice._tooltipRaycastTarget)),
                    new NodeAnnouncement(() => subtitle != null && subtitle.isActiveAndEnabled ? subtitle.text : null, kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => Tags(choice), kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => choice._modifierDescriptionText != null ? choice._modifierDescriptionText.text : null),
                },
                SearchText = () => choice._itemNameText != null ? choice._itemNameText.text : null,
                OnActivate = () => choice.OnPointerClick(new PointerEventData(EventSystem.current)),
                Details = () => DetailsOf(choice, choice._tooltipRaycastTarget, choice._modifierDescriptionText),
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
