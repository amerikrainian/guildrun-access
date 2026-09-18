using System.Collections.Generic;
using Ember.Scopes.Event.UI;
using Ember.Scopes.Event.UI.ChoiceButtons;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.Text;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using GuildrunAccess.Module.Interop;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// A random event (<see cref="EventUIController"/>; the campfire is one too): the event's name as the
    /// context, its story text as a line (the whole text at once, not the typewriter's trickle), the
    /// choices as buttons (an item or relic on offer named on its button, its tooltip in the control
    /// buffer), then the outcome text, the outcome summary ("Logan permanently gained 35 Magic.") and
    /// the Proceed button once a choice was made, all one Tab-stop read top to bottom with the arrows;
    /// the run HUD's sections follow. Escape presses Proceed when it is up.
    /// </summary>
    internal sealed class EventScreen : RunPanelScreen
    {
        public EventScreen()
        {
            Add(new StorySection());
            AddHud();
        }

        public override string Key => "gamerun.event";

        private static EventUIController EventUI => GameScopes.Controller<EventUIController>();

        protected override string ContextLabel
        {
            get
            {
                var e = EventUI;
                return e != null && e._eventNameText != null && !string.IsNullOrWhiteSpace(e._eventNameText.text)
                    ? e._eventNameText.text : Strings.ScreenEvent;
            }
        }

        public override bool IsActive()
        {
            var e = EventUI;
            return e != null && e.gameObject.activeInHierarchy && !e._isCovered;
        }

        protected override string PanelBackLabel
        {
            get { var e = EventUI; return e != null ? GameNodes.LabelOf(e._proceedButton) ?? base.PanelBackLabel : base.PanelBackLabel; }
        }

        protected override void PanelBack()
        {
            var e = EventUI;
            if (e != null && GameNodes.IsShown(e._proceedButton) && e._proceedButton.interactable)
                e._proceedButton.onClick.Invoke();
        }

        private static readonly ControlId OutcomeId = ControlId.Structural("event:outcome");
        private static ControlId ChoiceId(ChoiceButtonView choice) => ControlId.Structural("event:choice:" + choice.GetInstanceID());

        // The story: text, choices, outcome, summary, Proceed, one stop.
        private sealed class StorySection : ScreenSection
        {
            private string _lastOutcome;
            private string _lastChoices;

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

            public override void Build(GraphBuilder b)
            {
                var e = EventUI;
                if (e == null) return;

                // One Tab-stop: the story reads top to bottom with the arrows alone.
                b.BeginStop("story");
                b.AddItem(ControlId.Structural("event:description"), GameNodes.Text(() => FullText(e._eventDescriptionTypewriterEffect, e._eventDescriptionText)));

                // The choices the controller spawned for this event (the holder also keeps inactive templates),
                // bare: no list context and no counts, each choice is its own caption.
                var choices = Choices(e);
                for (int i = 0; i < choices.Count; i++)
                    b.AddItem(ChoiceId(choices[i]), Choice(choices[i]));

                // After choosing: the outcome text, the summary line(s), then Proceed. The summaries the
                // game shows are clones it spawns into the choices holder (the controller's own summary
                // view is the template, never shown), one per outcome.
                var outcomeText = e._eventOutcomeTypewriterEffect;
                bool outcomeShown = outcomeText != null && outcomeText.gameObject.activeInHierarchy
                    && !string.IsNullOrWhiteSpace(FullText(outcomeText, null));
                var summaries = Summaries(e);
                if (outcomeShown || summaries.Count > 0)
                {
                    b.PushContext(Strings.EventOutcome, null, positions: false);
                    if (outcomeShown)
                        b.AddItem(OutcomeId, GameNodes.Text(() => FullText(outcomeText, null)));
                    foreach (var summary in summaries)
                        b.AddItem(ControlId.Structural("event:summary:" + summary.GetInstanceID()), Summary(summary));
                    b.PopContext();
                }

                if (GameNodes.IsShown(e._proceedButton))
                    b.AddItem(ControlId.Structural("event:proceed"), GameNodes.Button(e._proceedButton, () => Strings.RunProceed));
            }
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

        // The outcome summaries on show, in the holder's order: the spawned clones with text, never the
        // controller's template (which stays inactive).
        private static List<ChoiceOutcomeSummaryView> Summaries(EventUIController e)
        {
            var list = new List<ChoiceOutcomeSummaryView>();
            var holder = e._eventChoicesHolder;
            if (holder == null) return list;
            for (int i = 0; i < holder.childCount; i++)
            {
                var view = holder.GetChild(i).GetComponent<ChoiceOutcomeSummaryView>();
                if (view == null || !view.gameObject.activeInHierarchy) continue;
                if (view._text == null || string.IsNullOrWhiteSpace(view._text.text)) continue;
                list.Add(view);
            }
            var own = e._outcomeSummaryView;
            if (own != null && own.gameObject.activeInHierarchy && !list.Contains(own)
                && own._text != null && !string.IsNullOrWhiteSpace(own._text.text))
                list.Add(own);
            return list;
        }

        // The heroes a choice shows as portraits, icons alone: the hero a rank-up, a stat bonus or a
        // reroll would go to (sixteen of the game's event builders attach one). The portrait holds the
        // hero's entry, the party has the hero.
        private static List<Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroData> Heroes(ChoiceButtonView view)
        {
            var list = new List<Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroData>();
            var heroes = view != null ? view.TryCast<HeroListChoiceButtonView>() : null;
            var portraits = heroes != null ? heroes._heroPortraitViews : null;
            if (portraits == null) return list;
            for (int i = 0; i < portraits.Count; i++)
            {
                var portrait = portraits[i];
                if (portrait == null || !portrait.gameObject.activeInHierarchy || portrait.IsEmpty) continue;
                var hero = RunData.OwnedHero(portrait._characterEntry);
                if (hero != null) list.Add(hero);
            }
            return list;
        }

        // "Duelist, rank C" after a caption that names the hero ("Rank up, Nyx"), the whole "Nyx,
        // Duelist, rank C" after one that does not: which hero it is, and the class and rank a choice
        // between heroes is made on.
        private static string HeroesPart(ChoiceButtonView view)
        {
            var heroes = Heroes(view);
            if (heroes.Count == 0) return null;
            string caption = view._buttonText != null ? TextUtil.StripRichText(view._buttonText.text) : null;
            var parts = new List<string>();
            foreach (var hero in heroes)
            {
                string label = RunData.HeroLabel(hero);
                string name = RunData.HeroName(hero);
                if (label == null) continue;
                bool named = !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(caption)
                    && caption.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (named && label.StartsWith(name + ", ", System.StringComparison.Ordinal)) label = label.Substring(name.Length + 2);
                else if (named && label == name) continue;
                parts.Add(label);
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        // The party or reserve slot of the (first) hero a choice shows: the hero and items buffers and
        // the glance keys read it as they would on the slot itself.
        private static Ember.Scopes.GameRun.UI.Slots.HeroPanel.BottomHeroView HeroSlot(ChoiceButtonView view)
        {
            var heroes = Heroes(view);
            return heroes.Count > 0 ? RunData.ViewOf(heroes[0].HeroId) : null;
        }

        // A choice: its caption, the offered item or relic (named, its tooltip a buffer line), the hero
        // it concerns, disabled state.
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
                    new NodeAnnouncement(() => Unnamed(view._buttonText, item != null ? ItemNodes.ItemName(item) : relic != null ? ItemNodes.RelicName(relic) : null), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => HeroesPart(view), kind: AnnouncementKinds.Value),
                    GameNodes.DisabledPart(() => button == null || button.interactable),
                },
                SearchText = () => view._buttonText != null ? view._buttonText.text : null,
                OnActivate = () => { if (button != null && button.interactable) button.onClick.Invoke(); },
                Details = () => item != null ? TooltipReader.Lines(item.TooltipRaycastTarget)
                    : relic != null ? TooltipReader.Lines(relic._tooltipRaycastTarget) : null,
                Subject = () => HeroSlot(view),
                SideLines = HeroLines.SideOfSlots(
                    () => { var slot = HeroSlot(view); return slot != null ? HeroLines.ForSlot(slot, BoardSection.VitalsOf(slot)) : null; },
                    () => { var slot = HeroSlot(view); return slot != null ? slot._itemSlotViews : null; }),
            };
        }

        // An offered item's or relic's name when the caption does not already say it ("Gain Hasty Vest"
        // names its vest, "Take the armor" would not): the tooltip's heading names it in the buffer either
        // way, so the name is never lost, only not said twice.
        private static string Unnamed(TMPro.TMP_Text caption, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string text = caption != null ? TextUtil.StripRichText(caption.text) : null;
            string plain = TextUtil.StripRichText(name);
            return !string.IsNullOrEmpty(text) && text.IndexOf(plain, System.StringComparison.OrdinalIgnoreCase) >= 0 ? null : name;
        }

        // The hero an outcome summary shows as a portrait ("Nyx ranked up."), or null.
        private static Ember.Scopes.GameRun.GameRegistry.Data.Characters.HeroData SummaryHero(ChoiceOutcomeSummaryView summary)
        {
            var portrait = summary._heroPortraitView;
            if (portrait == null || !portrait.gameObject.activeInHierarchy || portrait.IsEmpty) return null;
            return RunData.OwnedHero(portrait._characterEntry);
        }

        private static Ember.Scopes.GameRun.UI.Slots.HeroPanel.BottomHeroView SummaryHeroSlot(ChoiceOutcomeSummaryView summary)
        {
            var hero = SummaryHero(summary);
            return hero != null ? RunData.ViewOf(hero.HeroId) : null;
        }

        // The outcome summary: its text plus the item, relic or hero it shows. The hero is named only
        // when the text does not name it; its buffers and the glance keys read it either way, the
        // hero as the outcome left it.
        private static NodeVtable Summary(ChoiceOutcomeSummaryView summary)
        {
            var item = summary._itemView != null && summary._itemView.gameObject.activeInHierarchy ? summary._itemView : null;
            var relic = summary._relicView != null && summary._relicView.gameObject.activeInHierarchy ? summary._relicView : null;
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(() => summary._text != null ? summary._text.text : null),
                    new NodeAnnouncement(() => Unnamed(summary._text, item != null ? ItemNodes.ItemName(item) : relic != null ? ItemNodes.RelicName(relic) : null), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => Unnamed(summary._text, RunData.HeroName(SummaryHero(summary))), kind: AnnouncementKinds.Value),
                },
                SearchText = () => summary._text != null ? summary._text.text : null,
                Details = () => item != null ? TooltipReader.Lines(item.TooltipRaycastTarget)
                    : relic != null ? TooltipReader.Lines(relic._tooltipRaycastTarget) : null,
                Subject = () => SummaryHeroSlot(summary),
                SideLines = HeroLines.SideOfSlots(
                    () => { var slot = SummaryHeroSlot(summary); return slot != null ? HeroLines.ForSlot(slot, BoardSection.VitalsOf(slot)) : null; },
                    () => { var slot = SummaryHeroSlot(summary); return slot != null ? slot._itemSlotViews : null; }),
            };
        }
    }
}
