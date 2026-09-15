using System.Collections.Generic;
using gg.leyline.tutorialsystem.UI;
using GuildrunAccess.Core;
using Il2CppInterop.Runtime;
using UnityEngine;
using GuildrunAccess.Module.Interop;

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
        private readonly Dictionary<int, string> _lastSpoken = new Dictionary<int, string>();

        public void Tick()
        {
            // The displays are permanent children of the run scope's tutorial controller.
            var displays = GameScopes.Components<TutorialTextDisplay>();
            foreach (var d in displays)
            {
                if (d == null) continue;
                // The timed texts (a step's modal phase) are the tutorial screen's: it reads each one
                // as the control Enter skips (see Screens.TutorialPromptScreen).
                if (d.TryCast<TimedTutorialTextDisplay>() != null) continue;
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

    }
}
