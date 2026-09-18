using System.Collections.Generic;
using Ember.Scopes.GameRun.Challenge.UI;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using TMPro;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The missions of a Red Rift run (the game's challenge mode, the difficulty screen's last tier):
    /// "Red Rift Missions - 2/6", then each mission by the game's own text ("Skip a Campfire reward",
    /// "Hero death limit: 10", "Charge the Rift Seal (1/3)"). The game marks a mission done or failed
    /// with an icon alone (<see cref="ChallengeItemView"/>'s feedback objects), so that state is the
    /// one word added. Read off the run's <see cref="ChallengeModeController"/>, whose texts the game
    /// keeps current whether or not the sidebar shows the panel: the sidebar's rows and the missions
    /// glance key read the same lines. Nothing outside a Red Rift run.
    /// </summary>
    internal static class MissionNodes
    {
        /// <summary>The run's missions controller while the run is a Red Rift one, else null.</summary>
        public static ChallengeModeController Controller()
        {
            var controller = GameScopes.Controller<ChallengeModeController>();
            var reader = controller != null ? controller._challengeReader : null;
            return reader != null && reader.IsChallengeRun ? controller : null;
        }

        /// <summary>"Red Rift Missions - 0/6": the panel's title, which counts the missions done.</summary>
        public static string Title(ChallengeModeController controller) => Text(controller != null ? controller._titleText : null);

        /// <summary>The missions the run has, in the panel's order.</summary>
        public static List<ChallengeItemView> Views(ChallengeModeController controller)
        {
            var list = new List<ChallengeItemView>();
            var views = controller != null ? controller._challengeViews : null;
            if (views == null) return list;
            foreach (var view in views)
                if (view != null && view.gameObject.activeSelf && Text(view._titleText) != null) list.Add(view);
            return list;
        }

        /// <summary>A mission's text with its state: bare while pending, else "..., complete" or
        /// "..., failed".</summary>
        public static string Line(ChallengeItemView view)
        {
            string text = Text(view != null ? view._titleText : null);
            if (text == null) return null;
            if (Shown(view._failedFeedbackObject)) return Strings.MissionFailed(text);
            if (Shown(view._completedFeedbackObject)) return Strings.MissionComplete(text);
            return text;
        }

        /// <summary>What the panel says once the run's missions are lost, or null while they stand.</summary>
        public static string Failed(ChallengeModeController controller)
        {
            var failed = controller != null ? controller._runFailedObject : null;
            if (!Shown(failed)) return null;
            foreach (var tmp in failed.GetComponentsInChildren<TMP_Text>(false))
                if (Text(tmp) != null) return tmp.text;
            return null;
        }

        /// <summary>The whole panel as lines: the title, each mission, the failure text; empty outside
        /// a Red Rift run.</summary>
        public static List<string> Lines()
        {
            var lines = new List<string>();
            var controller = Controller();
            if (controller == null) return lines;
            string title = Title(controller);
            if (title != null) lines.Add(title);
            foreach (var view in Views(controller)) lines.Add(Line(view));
            string failed = Failed(controller);
            if (failed != null) lines.Add(failed);
            return lines;
        }

        private static bool Shown(UnityEngine.GameObject go) => go != null && go.activeSelf;

        private static string Text(TMP_Text text) => text != null && !string.IsNullOrWhiteSpace(text.text) ? text.text : null;
    }
}
