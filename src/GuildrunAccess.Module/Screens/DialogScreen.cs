using System.Collections.Generic;
using GuildrunAccess.Core;
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

            // The error box's stack trace: its first line (the exception and its message) on the
            // control, every line in the control buffer, and Enter copies the whole text to the
            // clipboard (the game's own Ctrl+A, Ctrl+C needs the field clicked into, which focus mode
            // keeps from the game's input).
            int k = 0;
            foreach (var field in p.GetComponentsInChildren<TMP_InputField>(false))
            {
                if (field == null || string.IsNullOrWhiteSpace(field.text)) continue;
                var f = field;
                b.AddItem(ControlId.Structural(_key + ":field" + k++), new NodeVtable
                {
                    ControlType = ControlTypes.Button,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => Strings.DialogStackTrace),
                        new NodeAnnouncement(() => FirstLine(f.text), kind: AnnouncementKinds.Value),
                    },
                    SearchText = () => Strings.DialogStackTrace,
                    OnActivate = () => Copy(f.text),
                    Details = () => GameNodes.Lines(Lines(f.text)),
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

        private static string[] Lines(string text)
            => (text ?? "").Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        private static string FirstLine(string text)
        {
            var lines = Lines(text);
            return lines.Length > 0 ? lines[0].Trim() : null;
        }

        private static void Copy(string text)
        {
            try
            {
                GUIUtility.systemCopyBuffer = text ?? "";
                Speech.Say(Strings.DialogCopied, interrupt: true);
            }
            catch (System.Exception e)
            {
                CoreLog.Warning("Dialog: clipboard copy failed: " + e.Message);
                Speech.Say(Strings.DialogCopyFailed, interrupt: true);
            }
        }

        // The stack trace the game shows is otherwise only in its own player log: put it in ours too,
        // once per showing, so a bug report can quote it.
        public override void OnPush()
        {
            var p = Panel();
            if (p == null) return;
            foreach (var field in p.GetComponentsInChildren<TMP_InputField>(false))
                if (field != null && !string.IsNullOrWhiteSpace(field.text))
                    CoreLog.Info("Game error dialog: " + field.text.Trim());
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
