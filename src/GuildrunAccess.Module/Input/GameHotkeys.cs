using System;
using System.Collections.Generic;
using GuildrunAccess.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuildrunAccess.Module.Input
{
    /// <summary>
    /// While focus mode owns the keyboard, the game's own keyboard hotkeys must not fire on the same
    /// keys (its Navigation map binds Tab to the Heroes panel, Space to the reserve and the shop, Enter
    /// to the feedback window, Escape to back). Every enabled game action's keyboard bindings get an
    /// empty override (the Input System's way to disable one binding, leaving mouse and gamepad
    /// bindings alive); the overrides are removed when focus mode turns off or the module unloads.
    /// Re-scanned every few frames: the game enables fresh action maps on scene changes, and a hot
    /// reload's old generation restores bindings the new one already blanked.
    /// </summary>
    internal static class GameHotkeys
    {
        private sealed class Entry
        {
            public InputAction Action;
            public List<int> Indices = new List<int>();
        }

        private static readonly Dictionary<string, Entry> _seen = new Dictionary<string, Entry>();
        private const int ScanEvery = 30;
        private static int _lastScan = -ScanEvery;
        private static bool _wasOn;

        public static void Tick(bool focusOn)
        {
            if (!focusOn)
            {
                if (_wasOn) Restore();
                _wasOn = false;
                return;
            }
            _wasOn = true;
            if (Time.frameCount - _lastScan < ScanEvery) return;
            _lastScan = Time.frameCount;
            try { Scan(); }
            catch (Exception e) { CoreLog.Warning("GameHotkeys: scan failed: " + e.Message); }
        }

        private static void Scan()
        {
            var list = new Il2CppSystem.Collections.Generic.List<InputAction>();
            InputSystem.ListEnabledActions(list);
            for (int i = 0; i < list.Count; i++)
            {
                var action = list[i];
                if (action == null) continue;
                string key = action.id.ToString();
                if (_seen.TryGetValue(key, out var entry))
                {
                    Reassert(entry);
                    continue;
                }
                entry = new Entry { Action = action };
                var bindings = action.bindings;
                for (int j = 0; j < bindings.Count; j++)
                {
                    var binding = bindings[j];
                    if (IsKeyboard(binding.path)) entry.Indices.Add(j);
                }
                foreach (int index in entry.Indices)
                    InputActionRebindingExtensions.ApplyBindingOverride(action, index, "");
                _seen[key] = entry;
                if (entry.Indices.Count > 0)
                    CoreLog.Info("GameHotkeys: blanked " + entry.Indices.Count + " keyboard binding(s) of " + Name(action));
            }
        }

        // A binding whose override was removed under us (the old generation's Restore) gets it back.
        private static void Reassert(Entry entry)
        {
            if (entry.Indices.Count == 0) return;
            var bindings = entry.Action.bindings;
            foreach (int index in entry.Indices)
            {
                if (index >= bindings.Count) continue;
                if (bindings[index].overridePath != null) continue;
                InputActionRebindingExtensions.ApplyBindingOverride(entry.Action, index, "");
            }
        }

        private static bool IsKeyboard(string path)
            => path != null && (path.StartsWith("<Keyboard>/", StringComparison.Ordinal) || path.Contains("{Submit}") || path.Contains("{Cancel}"));

        /// <summary>Give the game its keyboard bindings back.</summary>
        public static void Restore()
        {
            foreach (var entry in _seen.Values)
            {
                if (entry.Action == null) continue;
                foreach (int index in entry.Indices)
                {
                    try { InputActionRebindingExtensions.RemoveBindingOverride(entry.Action, index); }
                    catch (Exception e) { CoreLog.Warning("GameHotkeys: restore failed for " + Name(entry.Action) + ": " + e.Message); }
                }
            }
            _seen.Clear();
            _lastScan = -ScanEvery;
        }

        private static string Name(InputAction action)
        {
            try { return (action.actionMap != null ? action.actionMap.name + "/" : "") + action.name; }
            catch (Exception) { return "?"; }
        }
    }
}
