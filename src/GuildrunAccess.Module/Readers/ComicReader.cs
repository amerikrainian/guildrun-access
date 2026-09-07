using System.Collections.Generic;
using Ember.Scopes.Application.Comics;
using GuildrunAccess.Core;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using GuildrunAccess.Module.Interop;

namespace GuildrunAccess.Module.Readers
{
    /// <summary>
    /// Speaks the intro comics as they play: every speech bubble's text is queued the frame it becomes
    /// visible (its bubble active with a non-zero CanvasGroup alpha), in the order the comic reveals
    /// them, so the player hears the story at the pace sighted players read it. The comic advances by
    /// itself and needs no input. Polled from the module tick; owns no native handle.
    /// </summary>
    internal sealed class ComicReader
    {
        private readonly HashSet<int> _spoken = new HashSet<int>();
        private bool _wasPlaying;

        public void Tick()
        {
            var c = Controller();
            bool playing = c != null && c.gameObject.activeInHierarchy && c._panelContainer != null
                && c._panelContainer.childCount > 0;
            if (!playing)
            {
                if (_wasPlaying) { _spoken.Clear(); _wasPlaying = false; }
                return;
            }
            _wasPlaying = true;

            foreach (var tmp in c._panelContainer.GetComponentsInChildren<TMP_Text>(false))
            {
                if (tmp == null || string.IsNullOrWhiteSpace(tmp.text)) continue;
                int id = tmp.GetInstanceID();
                if (_spoken.Contains(id)) continue;
                if (!Visible(tmp)) continue;
                _spoken.Add(id);
                Speech.Say(tmp.text, interrupt: false);
            }
        }

        // A bubble fades in through a CanvasGroup: count it once it is actually showing.
        private static bool Visible(TMP_Text tmp)
        {
            var group = tmp.GetComponentInParent<CanvasGroup>();
            return group == null || group.alpha > 0.05f;
        }

        private static ComicController Controller() => GameScopes.Controller<ComicController>();
    }
}
