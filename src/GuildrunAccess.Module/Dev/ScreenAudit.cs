using System;
using System.Collections.Generic;
using System.Text;
using Ember.Scopes.Application.UI.Tooltips;
using GuildrunAccess.Contracts;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Buffers;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.Dev
{
    /// <summary>
    /// A dev-only coverage report for the screen on show (<c>POST /input</c> with <c>dev.audit</c>):
    /// every node of the current render with its buffer lines, then what the game draws that none of
    /// that carries: visible texts no readout or buffer line contains, tooltips whose heading no buffer
    /// line holds, and interactable widgets whose caption no readout speaks. It scans the scene
    /// (<c>FindObjectsOfType</c>, the walk the mod itself never does), so it is for the dev driver only.
    /// Matches are substring, case- and tag-insensitive, so the report over-reports rather than under:
    /// a listed text is a lead to check, not a verdict.
    /// </summary>
    internal static class ScreenAudit
    {
        public static string Run()
        {
            var sb = new StringBuilder();
            try
            {
                var nav = Navigation.Active as GraphNavigator;
                var render = nav?.CurrentRender;
                var focused = nav?.FocusedNode;
                var cur = GuildrunAccess.Core.Screens.ScreenManager.Current;
                sb.Append("screen: ").Append(cur != null ? cur.Key : "(none)").Append('\n');

                // Everything the mod can speak on this screen: the readouts and every buffer line.
                var spoken = new List<string>();
                if (render != null)
                {
                    sb.Append("nodes (").Append(render.Order.Count).Append("):\n");
                    foreach (var n in render.Order)
                    {
                        string readout = GraphAnnouncer.ComposeFull(n);
                        spoken.Add(readout);
                        sb.Append(ReferenceEquals(n, focused) ? "  > " : "    ").Append(n.Id.StructuralKey).Append(": ").Append(readout).Append('\n');
                        AppendLines(sb, spoken, "control", NodeLines.Lines(n));
                        AppendLines(sb, spoken, "hero", NodeLines.SideLines(n, BufferKeys.Hero));
                        AppendLines(sb, spoken, "items", NodeLines.SideLines(n, BufferKeys.Item));
                    }
                }
                var manager = Buffers.Manager;
                if (manager != null)
                {
                    foreach (var buffer in manager.Buffers)
                    {
                        if (buffer.Key == BufferKeys.Ui || buffer.Key == BufferKeys.Hero || buffer.Key == BufferKeys.Item) continue;
                        var lines = buffer.Lines;
                        if (lines.Count == 0) continue;
                        sb.Append("buffer ").Append(buffer.Key).Append(" (").Append(lines.Count).Append("):\n");
                        foreach (var line in lines)
                        {
                            spoken.Add(line);
                            sb.Append("      ").Append(line).Append('\n');
                        }
                    }
                }
                var haystack = new List<string>(spoken.Count);
                foreach (var s in spoken) haystack.Add(Norm(s));

                UnreadTexts(sb, haystack);
                UnreadTooltips(sb, haystack);
                UnreadWidgets(sb, haystack);
            }
            catch (Exception e)
            {
                sb.Append("[audit failed] ").Append(e).Append('\n');
            }
            return sb.ToString();
        }

        private static void AppendLines(StringBuilder sb, List<string> spoken, string name, IEnumerable<string> lines)
        {
            if (lines == null) return;
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                spoken.Add(line);
                sb.Append("      [").Append(name).Append("] ").Append(line).Append('\n');
            }
        }

        // Visible texts (active TMP with content) that no readout or buffer line contains.
        private static void UnreadTexts(StringBuilder sb, List<string> haystack)
        {
            var missing = new List<string>();
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<TMP_Text>()))
            {
                var tmp = o != null ? o.TryCast<TMP_Text>() : null;
                if (tmp == null || !tmp.gameObject.activeInHierarchy || !tmp.enabled) continue;
                if (Faded(tmp.transform)) continue;
                string raw = tmp.text;
                if (string.IsNullOrWhiteSpace(raw)) continue;
                foreach (var piece in raw.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string norm = Norm(piece);
                    if (norm.Length == 0 || Covered(norm, haystack)) continue;
                    missing.Add(Path(tmp.transform) + " = " + norm);
                }
            }
            Section(sb, "texts not read", missing);
        }

        // Tooltips on show whose heading appears in no buffer line.
        private static void UnreadTooltips(StringBuilder sb, List<string> haystack)
        {
            var missing = new List<string>();
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<TooltipRaycastTarget>()))
            {
                var target = o != null ? o.TryCast<TooltipRaycastTarget>() : null;
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                if (Faded(target.transform)) continue;
                if (target.TooltipSource == null) { missing.Add(Path(target.transform) + " = (no source)"); continue; }
                string heading;
                try { heading = TooltipReader.Heading(target); }
                catch (Exception e) { missing.Add(Path(target.transform) + " = (heading threw: " + e.Message + ")"); continue; }
                if (heading == null) { missing.Add(Path(target.transform) + " = (empty tooltip)"); continue; }
                string title = heading.Split(',')[0];
                if (!Covered(Norm(title), haystack)) missing.Add(Path(target.transform) + " = " + heading);
            }
            Section(sb, "tooltips not in any buffer", missing);
        }

        // Interactable widgets whose caption no readout speaks (a widget with no caption is listed too).
        private static void UnreadWidgets(StringBuilder sb, List<string> haystack)
        {
            var missing = new List<string>();
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Selectable>()))
            {
                var sel = o != null ? o.TryCast<Selectable>() : null;
                if (sel == null || !sel.gameObject.activeInHierarchy || !sel.interactable) continue;
                if (Faded(sel.transform)) continue;
                string kind = sel.TryCast<Button>() != null ? "button"
                    : sel.TryCast<Toggle>() != null ? "toggle"
                    : sel.TryCast<Slider>() != null ? "slider"
                    : sel.TryCast<Scrollbar>() != null ? "scrollbar"
                    : sel.TryCast<TMP_Dropdown>() != null ? "dropdown"
                    : sel.TryCast<TMP_InputField>() != null ? "input"
                    : "selectable";
                if (kind == "scrollbar") continue;
                var label = sel.GetComponentInChildren<TMP_Text>(false);
                string caption = label != null ? Norm(label.text) : "";
                if (caption.Length > 0 && Covered(caption, haystack)) continue;
                missing.Add(kind + " " + Path(sel.transform) + (caption.Length > 0 ? " = " + caption : " (no caption)"));
            }
            Section(sb, "widgets not read", missing);
        }

        private static void Section(StringBuilder sb, string title, List<string> items)
        {
            sb.Append(title).Append(" (").Append(items.Count).Append("):\n");
            foreach (var item in items) sb.Append("    ").Append(item).Append('\n');
        }

        // A text counts as covered when a spoken line contains it, or it contains a spoken line
        // (a caption merged into a longer line, or a long text a line quotes part of).
        private static bool Covered(string norm, List<string> haystack)
        {
            foreach (var h in haystack)
            {
                if (h.Length == 0) continue;
                if (h.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (h.Length >= 12 && norm.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // Tags and sprites gone, whitespace folded, so the game's markup and our spoken form compare.
        private static string Norm(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = TextFilter.Clean(Speech.SpriteNames(text)) ?? "";
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c)) { space = true; continue; }
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        // Under a canvas group at alpha 0: drawn nowhere, so not a gap.
        private static bool Faded(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                var group = p.GetComponent<CanvasGroup>();
                if (group != null && group.alpha <= 0.01f) return true;
            }
            return false;
        }

        private static string Path(Transform t)
        {
            var names = new List<string>();
            for (var p = t; p != null && names.Count < 4; p = p.parent) names.Add(p.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
