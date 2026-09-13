using System.Collections.Generic;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GuildrunAccess.Module.Interop;
using Navigation = GuildrunAccess.Core.UI.Navigation;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// A modal dialog panel (the privacy/GDPR consent, the generic confirmation, the error box, the exit
    /// and survey prompts), read generically: every text under the panel becomes a read-only line and
    /// every button a control, in hierarchy order, so entering the dialog speaks its title and message
    /// before landing on the first line; an input field (the error box's stack trace) is a control
    /// whose buffer holds its text line by line. Escape presses the panel's cancel/close button when
    /// it has one.
    /// Active whenever a panel of the type is active in the scene; Exclusive, so only dialog keys live.
    /// </summary>
    /// <typeparam name="TPanel">The panel MonoBehaviour type (a permanent child of its scope).</typeparam>
    public sealed class DialogScreen<TPanel> : Screen where TPanel : MonoBehaviour
    {
        private readonly string _key;
        private readonly System.Func<string> _name;
        public DialogScreen(string key, System.Func<string> name)
        {
            _key = key;
            _name = name;
        }

        public override string Key => _key;
        public override int Layer => 30;
        public override bool Exclusive => true;
        // No ScreenName: the dialog context announces the name via the path diff on entry.

        // The panel is a permanent, usually inactive child of its area's scope: found once per scope.
        private static TPanel Panel() => GameScopes.Component<TPanel>();

        public override bool IsActive()
        {
            var p = Panel();
            return p != null && p.gameObject.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            var p = Panel();
            if (p == null) return;

            // The panel's texts that are not button captions: the first is the dialog's title (already
            // localized by the game), which names the context; the rest (the message) become lines.
            var texts = new List<TMP_Text>();
            foreach (var tmp in p.GetComponentsInChildren<TMP_Text>(false))
            {
                if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                if (tmp.GetComponentInParent<Button>() != null) continue; // a caption reads with its button
                if (tmp.GetComponentInParent<TMP_InputField>() != null) continue; // the error box's stack trace
                texts.Add(tmp);
            }

            string title = texts.Count > 1 ? texts[0].text : _name();
            b.PushContext(title, Strings.RoleDialog, positions: false);

            for (int i = texts.Count > 1 ? 1 : 0; i < texts.Count; i++)
            {
                var t = texts[i];
                b.AddItem(ControlId.Structural(_key + ":text" + i), GameNodes.Text(() => t.text));
            }

            int k = 0;
            foreach (var field in p.GetComponentsInChildren<TMP_InputField>(false))
            {
                if (field == null || string.IsNullOrWhiteSpace(field.text)) continue;
                var f = field;
                b.AddItem(ControlId.Structural(_key + ":field" + k++), new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Strings.DialogStackTrace) },
                    Details = () => GameNodes.Lines(f.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)),
                });
            }

            int j = 0;
            foreach (var button in p.GetComponentsInChildren<Button>(false))
            {
                if (!IsChoice(button)) continue;
                b.AddItem(ControlId.Structural(_key + ":button" + j++), GameNodes.Button(button));
            }

            b.PopContext();
        }

        // A real choice has a caption; the full-screen modal backdrop button (which cancels on click)
        // has none and is reached through Escape instead.
        private static bool IsChoice(Button button)
        {
            if (!GameNodes.IsShown(button)) return false;
            return button.GetComponentInChildren<TMP_Text>(true) != null
                || button.GetComponentInChildren<Text>(true) != null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var p = Panel();
                if (p == null) return;
                Button cancel = null;
                foreach (var button in p.GetComponentsInChildren<Button>(false))
                {
                    if (!GameNodes.IsShown(button)) continue;
                    string n = button.gameObject.name.ToLowerInvariant();
                    if (n.Contains("cancel") || n.Contains("close") || n.Contains("modal")) { cancel = button; break; }
                }
                if (cancel != null) cancel.onClick.Invoke();
                else Navigation.AnnounceCurrent(); // no way out but a choice: re-read where we are
            });
        }
    }
}
