using System.Collections.Generic;
using gg.leyline.tutorialsystem.UI;
using GuildrunAccess.Core;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace GuildrunAccess.Module.Readers
{
    /// <summary>
    /// Speaks the tutorial system's text displays (the main, secondary, and timed displays on the
    /// utilities canvas) whenever one shows or changes its text, queued so it never cuts off the
    /// player. Polled from the module tick; each display is re-read live, nothing is cached but the
    /// last line spoken per display (to speak each line once).
    /// </summary>
    internal sealed class TutorialReader
    {
        private TutorialTextDisplay[] _displays;
        private const int SearchEvery = 60;
        private int _lastSearchFrame = -SearchEvery;
        private readonly Dictionary<int, string> _lastSpoken = new Dictionary<int, string>();

        public void Tick()
        {
            var displays = Displays();
            if (displays == null) return;
            foreach (var d in displays)
            {
                if (d == null) continue;
                int id = d.GetInstanceID();
                var container = d.Container;
                bool shown = container != null && container.activeInHierarchy && d.gameObject.activeInHierarchy;
                if (!shown)
                {
                    _lastSpoken.Remove(id); // hidden: the next show of the same line speaks again
                    continue;
                }
                var text = d._textComponent != null ? d._textComponent.text : null;
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (_lastSpoken.TryGetValue(id, out var last) && last == text) continue;
                _lastSpoken[id] = text;
                Speech.Say(text, interrupt: false);
            }
        }

        // The displays live on a persistent canvas; re-scan when none are known or one was destroyed.
        private TutorialTextDisplay[] Displays()
        {
            if (_displays != null)
            {
                bool alive = true;
                foreach (var d in _displays) if (d == null) { alive = false; break; }
                if (alive) return _displays;
            }
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return null;
            _lastSearchFrame = Time.frameCount;
            var found = Object.FindObjectsOfType(Il2CppType.Of<TutorialTextDisplay>());
            var list = new List<TutorialTextDisplay>();
            if (found != null)
                foreach (var o in found)
                {
                    var d = o != null ? o.TryCast<TutorialTextDisplay>() : null;
                    if (d != null) list.Add(d);
                }
            _displays = list.Count > 0 ? list.ToArray() : null;
            return _displays;
        }
    }
}
