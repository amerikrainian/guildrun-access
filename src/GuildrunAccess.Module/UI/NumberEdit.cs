using System;
using System.Text;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Screens;
using GuildrunAccess.Core.Strings;
using UnityEngine;
using Navigation = GuildrunAccess.Core.UI.Navigation;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// Mod-owned entry of a number, for fields the game has no widget behind (the sounds screen's
    /// intervals): the dd2a11y synthetic text field, cut down to what a number needs. Focus mode
    /// disables the Input System's keyboard, so the layout-aware text-input event is not there to
    /// listen to; instead the digits, the decimal point (a period or a comma, on the row or the
    /// keypad), Backspace, Enter and Escape are polled through Unity's legacy input, which focus mode
    /// leaves alone, each echoed. Enter commits (an empty buffer included: the host decides what blank
    /// means), Escape cancels, and the mod's own keys stand down meanwhile
    /// (<see cref="OwnsKeyboard"/>, until the key that ended the edit comes up, so the Enter that
    /// commits is not also an activation). The edit dies with its screen: a screen change ends it
    /// without a word.
    /// </summary>
    internal static class NumberEdit
    {
        private static bool _active;
        private static bool _pending;   // begun, waiting for the Enter that began it to come up
        private static bool _holdKeys;  // ended, waiting for the key that ended it to come up
        private static readonly StringBuilder _buffer = new StringBuilder();
        private static Action<string> _commit;
        private static Action _cancel;
        private static Screen _owner;

        public static bool Active => _active;

        /// <summary>Whether the keyboard is the field's: while editing, and until the key that ended
        /// it is released.</summary>
        public static bool OwnsKeyboard => _active || _holdKeys;

        /// <summary>Start an edit with an empty buffer. <paramref name="hint"/> is spoken after
        /// "editing": what a bare Enter will produce, or what stands there now.</summary>
        public static void Begin(Action<string> commit, Action cancel, string hint = null)
        {
            if (_active) return;
            _commit = commit;
            _cancel = cancel;
            _buffer.Clear();
            _owner = ScreenManager.Current;
            _active = true;
            _pending = true;
            Speech.Say(Strings.EditBegin, interrupt: true);
            if (!string.IsNullOrEmpty(hint)) Speech.Say(hint);
        }

        public static void Tick()
        {
            if (_holdKeys && !EndKeyHeld()) _holdKeys = false;
            if (!_active) return;
            try
            {
                // The screen the field is on went away: the field is gone, end without a word.
                if (!ReferenceEquals(ScreenManager.Current, _owner)) { End(); return; }
                if (_pending)
                {
                    if (EndKeyHeld()) return;
                    _pending = false;
                }
                if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    string text = _buffer.ToString();
                    var commit = _commit;
                    End();
                    commit(text);
                    return;
                }
                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    var cancel = _cancel;
                    End();
                    cancel();
                    return;
                }
                if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
                {
                    if (_buffer.Length > 0)
                    {
                        char erased = _buffer[_buffer.Length - 1];
                        _buffer.Length--;
                        Speech.Say(Strings.EditDeleted(Spoken(erased)), interrupt: true);
                    }
                    else Speech.Say(Strings.ValueBlank, interrupt: true);
                    return;
                }
                for (int d = 0; d <= 9; d++)
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0 + d) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad0 + d))
                    {
                        Type((char)('0' + d));
                        return;
                    }
                }
                if (UnityEngine.Input.GetKeyDown(KeyCode.Period) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadPeriod) || UnityEngine.Input.GetKeyDown(KeyCode.Comma))
                    Type('.');
            }
            catch (Exception e)
            {
                CoreLog.Warning("NumberEdit: " + e.Message);
                End();
            }
        }

        private static void Type(char c)
        {
            _buffer.Append(c);
            Speech.Say(Spoken(c), interrupt: true);
        }

        /// <summary>The dev driver's keys, since it presses no OS key: digits and points typed, or
        /// "enter", "escape", "backspace". Ignored with no edit under way.</summary>
        internal static string Inject(string keys)
        {
            if (!_active) return "no number edit is active";
            _pending = false;
            switch (keys)
            {
                case "enter": { string text = _buffer.ToString(); var commit = _commit; End(); commit(text); return "committed " + text; }
                case "escape": { var cancel = _cancel; End(); cancel(); return "cancelled"; }
                case "backspace": if (_buffer.Length > 0) _buffer.Length--; return "buffer " + _buffer;
                default:
                    foreach (char c in keys) if (char.IsDigit(c) || c == '.' || c == ',') Type(c == ',' ? '.' : c);
                    return "buffer " + _buffer;
            }
        }

        private static string Spoken(char c) => c == '.' ? Strings.EditPoint : c.ToString();

        /// <summary>Module teardown: drop the edit without a word.</summary>
        public static void Shutdown()
        {
            if (_active) End();
            _holdKeys = false;
        }

        private static bool EndKeyHeld()
            => UnityEngine.Input.GetKey(KeyCode.Return) || UnityEngine.Input.GetKey(KeyCode.KeypadEnter) || UnityEngine.Input.GetKey(KeyCode.Escape);

        private static void End()
        {
            _active = false;
            _pending = false;
            _holdKeys = true;
            _commit = null;
            _cancel = null;
            _owner = null;
        }
    }
}
