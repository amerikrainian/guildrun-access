using System.Collections.Generic;
using Ember.Scopes.Event.UI;
using Ember.Scopes.Event.UI.ChoiceButtons;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using GuildrunAccess.Module.Interop;
using Navigation = GuildrunAccess.Core.UI.Navigation;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// A random event (<see cref="EventUIController"/>): the event's name as the context, its story
    /// text as a line (the whole text at once, not the typewriter's trickle), the choices as buttons
    /// (an item or relic on offer named on its button, its tooltip in the control buffer), then the outcome text and
    /// the Proceed button once a choice was made, all one Tab-stop read top to bottom with the arrows.
    /// Escape presses Proceed when it is up.
    /// </summary>
    public sealed class EventScreen : Screen
    {
        public override string Key => "gamerun.event";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private static EventUIController EventUI => GameScopes.Controller<EventUIController>();
        private string _lastOutcome;
        private string _lastChoices;

        private static readonly ControlId OutcomeId = ControlId.Structural("event:outcome");
        private static ControlId ChoiceId(ChoiceButtonView choice) => ControlId.Structural("event:choice:" + choice.GetInstanceID());

        // When the outcome text appears (the chosen button is gone by then), land on it so the result
        // is spoken and Proceed is a step away; when a new set of choices replaces the old one (an
        // event that branches, like the campfire), land on the first new choice.
        public override void OnUpdate()
        {
            var e = EventUI;
            if (e == null) return;
            var typewriter = e._eventOutcomeTypewriterEffect;
            string outcome = typewriter != null && typewriter.gameObject.activeInHierarchy ? FullText(typewriter, null) : null;
            if (outcome != _lastOutcome)
            {
                _lastOutcome = outcome;
                if (!string.IsNullOrWhiteSpace(outcome)) { Navigation.FocusNode(OutcomeId); return; }
            }
            var live = Choices(e);
            var sb = new System.Text.StringBuilder();
            foreach (var choice in live) sb.Append(choice.GetInstanceID()).Append(',');
            string choices = sb.ToString();
            if (choices == _lastChoices) return;
            bool hadChoices = !string.IsNullOrEmpty(_lastChoices);
            _lastChoices = choices;
            if (hadChoices && live.Count > 0) Navigation.FocusNode(ChoiceId(live[0]));
        }

        public override void OnPop()
        {
            _lastOutcome = null;
            _lastChoices = null;
        }

        public override bool IsActive()
        {
            var e = EventUI;
            return e != null && e.gameObject.activeInHierarchy && !e._isCovered;
        }

        public override void Build(GraphBuilder b)
        {
            var e = EventUI;
            if (e == null) return;

            string title = e._eventNameText != null && !string.IsNullOrWhiteSpace(e._eventNameText.text)
                ? e._eventNameText.text : Strings.ScreenEvent;
            b.PushContext(title, null, positions: false);

            // One Tab-stop: the story reads top to bottom with the arrows alone.
            b.BeginStop("story");
            b.AddItem(ControlId.Structural("event:description"), GameNodes.Text(() => FullText(e._eventDescriptionTypewriterEffect, e._eventDescriptionText)));

            // The choices the controller spawned for this event (the holder also keeps inactive templates),
            // bare: no list context and no counts, each choice is its own caption.
            var choices = Choices(e);
            for (int i = 0; i < choices.Count; i++)
                b.AddItem(ChoiceId(choices[i]), Choice(choices[i]));

            // After choosing: the outcome text, the summary line(s), then Proceed.
            var outcomeText = e._eventOutcomeTypewriterEffect;
            bool outcomeShown = outcomeText != null && outcomeText.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(FullText(outcomeText, null));
            var summary = e._outcomeSummaryView;
            bool summaryShown = summary != null && summary.gameObject.activeInHierarchy;
            if (outcomeShown || summaryShown)
            {
                b.PushContext(Strings.EventOutcome, null, positions: false);
                if (outcomeShown)
                    b.AddItem(OutcomeId, GameNodes.Text(() => FullText(outcomeText, null)));
                if (summaryShown)
                    b.AddItem(ControlId.Structural("event:summary"), Summary(summary));
                b.PopContext();
            }

            if (GameNodes.IsShown(e._proceedButton))
                b.AddItem(ControlId.Structural("event:proceed"), GameNodes.Button(e._proceedButton, () => Strings.RunProceed));

            b.PopContext();
        }

        // The whole text the typewriter is (or was) revealing, else the label's current text.
        private static string FullText(gg.leyline.utilities.UI.TypewriterEffectComponent typewriter, TMPro.TMP_Text label)
        {
            string full = typewriter != null ? typewriter._dialogText : null;
            if (!string.IsNullOrWhiteSpace(full)) return full.Trim();
            var text = label != null ? label : (typewriter != null ? typewriter._text : null);
            return text != null && text.text != null ? text.text.Trim() : null;
        }

        private static List<ChoiceButtonView> Choices(EventUIController e)
        {
            var list = new List<ChoiceButtonView>();
            var holder = e._eventChoicesHolder;
            if (holder == null) return list;
            for (int i = 0; i < holder.childCount; i++)
            {
                var view = holder.GetChild(i).GetComponent<ChoiceButtonView>();
                if (view != null && view.gameObject.activeInHierarchy) list.Add(view);
            }
            return list;
        }

        // A choice: its caption, the offered item or relic (named, its tooltip a buffer line), disabled state.
        private static NodeVtable Choice(ChoiceButtonView view)
        {
            var artifact = view.TryCast<ArtifactChoiceButtonView>();
            var item = artifact != null && artifact._itemView != null && artifact._itemView.gameObject.activeInHierarchy ? artifact._itemView : null;
            var relic = artifact != null && artifact._relicView != null && artifact._relicView.gameObject.activeInHierarchy ? artifact._relicView : null;
            var button = view._button;
            return new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => view._buttonText != null ? view._buttonText.text : null),
                    new NodeAnnouncement(() => item != null ? ItemNodes.ItemName(item) : relic != null ? ItemNodes.RelicName(relic) : null, kind: AnnouncementKinds.Value),
                    GameNodes.DisabledPart(() => button == null || button.interactable),
                },
                SearchText = () => view._buttonText != null ? view._buttonText.text : null,
                OnActivate = () => { if (button != null && button.interactable) button.onClick.Invoke(); },
                Details = () => GameNodes.Lines(item != null ? TooltipReader.Describe(item.TooltipRaycastTarget)
                    : relic != null ? TooltipReader.Describe(relic._tooltipRaycastTarget) : null),
            };
        }

        // The outcome summary: its text plus the item, relic or hero it shows.
        private static NodeVtable Summary(ChoiceOutcomeSummaryView summary)
        {
            var item = summary._itemView != null && summary._itemView.gameObject.activeInHierarchy ? summary._itemView : null;
            var relic = summary._relicView != null && summary._relicView.gameObject.activeInHierarchy ? summary._relicView : null;
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => summary._text != null ? summary._text.text : null),
                    new NodeAnnouncement(() => item != null ? ItemNodes.ItemName(item) : relic != null ? ItemNodes.RelicName(relic) : null, kind: AnnouncementKinds.Value),
                },
                SearchText = () => summary._text != null ? summary._text.text : null,
                Details = () => GameNodes.Lines(item != null ? TooltipReader.Describe(item.TooltipRaycastTarget)
                    : relic != null ? TooltipReader.Describe(relic._tooltipRaycastTarget) : null),
            };
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var e = EventUI;
                if (e != null && GameNodes.IsShown(e._proceedButton) && e._proceedButton.interactable)
                    e._proceedButton.onClick.Invoke();
            });
        }
    }
}
