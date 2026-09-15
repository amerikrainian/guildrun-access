using System;
using System.Collections.Generic;
using gg.leyline.tutorialsystem.Flow;
using gg.leyline.tutorialsystem.Flow.Elements;
using gg.leyline.tutorialsystem.UI;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Interop;
using GuildrunAccess.Module.UI;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The tutorial's blocking phase as a modal. A tutorial step is a flow of elements: a modal goes up
    /// over the game's UI, timed texts show one after another (15 seconds each, a mouse click skipping
    /// one), the modal comes down, and an action prompt waits for the player (pick a hero, buy one,
    /// start the fight). The prompt only listens from then on: a player who acts during the texts is
    /// not heard, the step never completes, the tutorial never reaches its save point, and the game
    /// resets it at the main menu every time (the same first run forever, and no run ever saved).
    /// The mouse cannot reach the game through the modal; this screen is the keyboard's modal: active
    /// while a running step stands at a timed text or a delay, Exclusive so nothing beneath acts,
    /// each text a control read as it shows, Enter or Escape skipping it exactly as the click does
    /// (its timer run out). The action prompts are not modal here either: the player acts through
    /// the run's screens, and the prompt's own text reads through <see cref="Readers.TutorialReader"/>.
    /// </summary>
    internal sealed class TutorialPromptScreen : Screen
    {
        public override string Key => "tutorial";
        public override int Layer => 29; // above every run panel, under the game's dialogs
        public override bool Exclusive => true;
        // No ScreenName: the name is the text's context, read with the first text (a step may open
        // with a delay, and a name spoken over nothing is noise).
        public override bool AllowsTypeahead => false;

        // A frame or two can pass between one element's end and the next one's start (the flow's
        // continuations): the screen holds through them, so one step's texts read as one modal.
        private const int GraceFrames = 12;
        private int _lastBlockedFrame = int.MinValue / 2;

        public override bool IsActive()
        {
            int frame = NavInput.Current.FrameCount;
            if (Waiting() != null) { _lastBlockedFrame = frame; return true; }
            return frame - _lastBlockedFrame < GraceFrames;
        }

        /// <summary>The element a running step is waiting out right now (a timed text or a delay), or
        /// null when no step runs or the running one waits for the player instead. The first element
        /// of an executing flow not yet complete is the one running or about to.</summary>
        private static TutorialFlowElement Waiting()
        {
            foreach (var flow in GameScopes.Components<TutorialStepFlow>())
            {
                if (flow == null || !flow._isExecuting) continue;
                var elements = flow._flowElements;
                if (elements == null) continue;
                foreach (var element in elements)
                {
                    if (element == null || element.IsComplete) continue;
                    if (IsWait(element)) return element;
                    break;
                }
            }
            return null;
        }

        private static bool IsWait(TutorialFlowElement element)
            => element.TryCast<TimedInfoFlowElement>() != null || element.TryCast<DelayElement>() != null;

        // The timed text running right now (its per-frame timer subscribed), or null during a delay.
        private static TimedInfoFlowElement Running()
        {
            var timed = Waiting()?.TryCast<TimedInfoFlowElement>();
            return timed != null && timed._updateDisposable != null ? timed : null;
        }

        public override void Build(GraphBuilder b)
        {
            var timed = Running();
            if (timed == null) return; // a delay, or the text not shown yet: nothing to read
            string text = TextOf(timed);
            if (string.IsNullOrWhiteSpace(text)) return;
            var element = timed;
            // "Tutorial, dialog, <text>, button" on entry; one node per text, so the next text is a
            // new node under the same context and its arrival reads as a move.
            b.PushContext(Strings.ScreenTutorial, Strings.RoleDialog, positions: false);
            b.AddItem(ControlId.Structural("tutorial:" + element.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => TextOf(element)) },
                SearchText = () => TextOf(element),
                OnActivate = () => Skip(element),
            });
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            // Escape skips the text as Enter does (the click the mouse would give the modal).
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var timed = Running();
                if (timed != null) Skip(timed);
            });
        }

        // What the click does in the element's own update: its timer runs out, and the flow moves on.
        private static void Skip(TimedInfoFlowElement element)
        {
            try { element._elapsedTime = element._waitDuration; }
            catch (Exception e) { CoreLog.Warning("Tutorial: skipping the text failed: " + e.Message); }
        }

        // The element's display text; the timed display shown under the scope when the element's own
        // reference is unreadable.
        private static string TextOf(TimedInfoFlowElement element)
        {
            try
            {
                var display = element.TextDisplay;
                var component = display != null ? display._textComponent : null;
                if (component != null) return component.text;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Tutorial: text display unreadable: " + e.Message);
            }
            foreach (var display in GameScopes.Components<TimedTutorialTextDisplay>())
            {
                if (display == null || display.Container == null || !display.Container.activeInHierarchy) continue;
                return display._textComponent != null ? display._textComponent.text : null;
            }
            return null;
        }
    }
}
