using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Application.Comics;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.Input;
using GuildrunAccess.Module.UI;
using TMPro;
using UnityEngine;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// A comic while it plays (<see cref="ComicController"/>: the intro, the defeat): the game advances
    /// its panels on a mouse click anywhere, which no widget event reproduces, so Enter sends the game a
    /// synthetic click. The bubbles themselves are spoken as they appear by the comic reader; Space
    /// re-reads the panel's visible text. Owns the keys while the comic is up.
    /// </summary>
    public sealed class ComicScreen : Screen
    {
        public override string Key => "app.comic";
        public override int Layer => 40;
        public override bool Exclusive => true;

        private readonly Finder<ComicController> _comic = new Finder<ComicController>();

        // A comic is playing when one of its panels is actually active (the container keeps inactive
        // panel prefabs around between comics).
        private ComicController Playing()
        {
            var c = _comic.Get();
            var container = c != null && c.gameObject.activeInHierarchy ? c._panelContainer : null;
            if (container == null) return null;
            for (int i = 0; i < container.childCount; i++)
                if (container.GetChild(i).gameObject.activeInHierarchy) return c;
            return null;
        }

        public override bool IsActive() => Playing() != null;

        public override void Build(GraphBuilder b)
        {
            var c = Playing();
            if (c == null) return;
            b.PushContext(Strings.ScreenComic, null, positions: false);
            b.AddItem(ControlId.Structural("comic:continue"), new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Strings.ComicContinue) },
                OnActivate = () => SyntheticMouse.ClickCenter(),
                OnTooltip = () => Core.Speech.Say(VisibleText(c) ?? Strings.NoTooltip, interrupt: true),
            });
            b.PopContext();
        }

        // Every bubble currently showing, in order.
        private static string VisibleText(ComicController c)
        {
            var sb = new StringBuilder();
            foreach (var tmp in c._panelContainer.GetComponentsInChildren<TMP_Text>(false))
            {
                if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                var group = tmp.GetComponentInParent<CanvasGroup>();
                if (group != null && group.alpha <= 0.05f) continue;
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(tmp.text.Trim());
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
