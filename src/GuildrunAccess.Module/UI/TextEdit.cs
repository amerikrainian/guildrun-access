using System;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// Editing one of the game's text fields with the game's own editor: the field is selected and
    /// activated as a click does, so the caret, the selection, Ctrl+A, the clipboard, Delete and any
    /// character the OS types are TextMesh Pro's, which reads its keys from Unity's legacy event
    /// queue (untouched by focus mode, which disables the Input System's keyboard alone). While it
    /// lasts the mod's keys stand down (<see cref="OwnsKeyboard"/>, <c>InputManager.TextFieldFocused</c>)
    /// and each step is echoed from the field's state before and after it (<see cref="EditEcho"/>).
    /// Enter, Escape and Tab end it: the first two are the field's own way out, and Escape keeps the
    /// text (the field would put the old one back, and with it undo a filter the player just typed).
    /// </summary>
    public static class TextEdit
    {
        private const int FocusWaitFrames = 60;

        private static TMP_InputField _field;
        private static bool _pending;      // begun, waiting for the Enter that began it to come up
        private static bool _seenFocus;
        private static int _deadline;
        private static bool _activated;    // the field's Escape setting is ours to give back
        private static bool _restoreOnEscape;
        private static bool _holdKeys;     // ended, waiting for the key that ended it to come up
        // The field as the last step left it: what the next echo is measured against, not a cache.
        private static EditState _last;

        public static bool Active => _field != null;

        /// <summary>Whether the keyboard is the field's: while editing, and until the key that ended it
        /// is released (the Escape that leaves the field must not also close the screen).</summary>
        public static bool OwnsKeyboard => Active || _holdKeys;

        public static void Begin(TMP_InputField field)
        {
            if (field == null || Active) return;
            _field = field;
            _pending = true;
            _seenFocus = false;
            _activated = false;
            Speech.Say(Strings.EditBegin, interrupt: true);
        }

        public static void Tick()
        {
            if (_holdKeys && !EndKeyHeld()) _holdKeys = false;
            if (_field == null) return;
            try
            {
                if (!_field.gameObject.activeInHierarchy) { End(announce: false); return; }
                if (_pending)
                {
                    // The field would read the Enter that began the edit as its submit.
                    if (EndKeyHeld()) return;
                    _pending = false;
                    _restoreOnEscape = _field.restoreOriginalTextOnEscape;
                    _activated = true;
                    _field.restoreOriginalTextOnEscape = false;
                    var es = EventSystem.current;
                    if (es != null) es.SetSelectedGameObject(_field.gameObject);
                    _field.ActivateInputField();
                    _deadline = Time.frameCount + FocusWaitFrames;
                    _last = new EditState(_field.text, (_field.text ?? "").Length, (_field.text ?? "").Length);
                    return;
                }
                if (!_field.isFocused)
                {
                    if (_seenFocus) End(announce: true);
                    else if (Time.frameCount > _deadline)
                    {
                        CoreLog.Warning("TextEdit: the field never took focus");
                        End(announce: true);
                    }
                    return;
                }
                _seenFocus = true;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                {
                    _field.DeactivateInputField();
                    End(announce: true);
                    return;
                }
                var now = Read(_field);
                bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
                string say = EditEcho.Describe(_last, now, ctrl);
                _last = now;
                if (say != null) Speech.Say(say, interrupt: true);
            }
            catch (Exception e)
            {
                CoreLog.Warning("TextEdit: " + e.Message);
                End(announce: false);
            }
        }

        /// <summary>Module teardown: the field goes back to the game as it was.</summary>
        public static void Shutdown()
        {
            if (Active) End(announce: false);
            _holdKeys = false;
        }

        private static EditState Read(TMP_InputField field)
            => new EditState(field.text, field.selectionStringFocusPosition, field.selectionStringAnchorPosition);

        private static bool EndKeyHeld()
            => UnityEngine.Input.GetKey(KeyCode.Return) || UnityEngine.Input.GetKey(KeyCode.KeypadEnter)
            || UnityEngine.Input.GetKey(KeyCode.Escape) || UnityEngine.Input.GetKey(KeyCode.Tab);

        private static void End(bool announce)
        {
            var field = _field;
            _field = null;
            _pending = false;
            _holdKeys = true;
            try
            {
                if (field != null && _activated)
                {
                    field.restoreOriginalTextOnEscape = _restoreOnEscape;
                    if (field.isFocused) field.DeactivateInputField();
                }
                // Focus mode keeps the game's own selection empty.
                var es = EventSystem.current;
                if (es != null && field != null && es.currentSelectedGameObject == field.gameObject) es.SetSelectedGameObject(null);
            }
            catch (Exception e) { CoreLog.Warning("TextEdit: giving the field back failed: " + e.Message); }
            if (announce) Navigation.AnnounceCurrent();
        }
    }
}
