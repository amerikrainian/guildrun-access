using System;
using System.Collections.Generic;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// Node factories for graph-native screens over Guildrun's uGUI + TextMeshPro widgets: assemble
    /// <see cref="NodeVtable"/>s with the shared spoken conventions (role words, disabled/selected state,
    /// "n of m" positions) and route activation through the widget's own event (the game's click handler
    /// runs exactly as a mouse click would). Every label reads LIVE from the widget at speak time.
    /// </summary>
    public static class GameNodes
    {
        /// <summary>The label part (always first in the standard order).</summary>
        public static NodeAnnouncement LabelPart(Func<string> label)
            => new NodeAnnouncement(label, kind: AnnouncementKinds.Label);

        /// <summary>The disabled-state part: silent while enabled, "disabled" otherwise; LIVE, so a
        /// control graying out under focus announces it.</summary>
        public static NodeAnnouncement DisabledPart(Func<bool> enabled)
            => new NodeAnnouncement(() => enabled == null || enabled() ? null : Strings.StateDisabled,
                live: true, kind: AnnouncementKinds.Enabled);

        /// <summary>The selected-state part: "selected" when selected, silent otherwise; LIVE.</summary>
        public static NodeAnnouncement SelectedPart(Func<bool> selected)
            => new NodeAnnouncement(() => selected != null && selected() ? Strings.StateSelected : null,
                live: true, kind: AnnouncementKinds.Selected);

        /// <summary>The spoken description part, read after the value.</summary>
        public static NodeAnnouncement TooltipPart(Func<string> description)
            => new NodeAnnouncement(description, kind: AnnouncementKinds.Tooltip);

        /// <summary>A plain read-only text line.</summary>
        public static NodeVtable Text(Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new[] { LabelPart(text) },
            SearchText = text,
        };

        /// <summary>A heading line ("text, heading").</summary>
        public static NodeVtable Heading(Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new List<NodeAnnouncement>
            {
                LabelPart(text),
                new NodeAnnouncement(() => Strings.RoleHeading, kind: AnnouncementKinds.Role),
            },
            SearchText = text,
        };

        /// <summary>A push button backed by a closure: "label, button[, disabled][, n of m]". A disabled
        /// button consumes activation silently.</summary>
        public static NodeVtable Button(Func<string> label, Action activate, Func<bool> enabled = null,
            ControlType type = null)
        {
            var anns = new List<NodeAnnouncement>
            {
                LabelPart(label),
                DisabledPart(enabled),
            };
            return new NodeVtable
            {
                ControlType = type ?? ControlTypes.Button,
                Announcements = anns,
                SearchText = label,
                OnActivate = () =>
                {
                    if (enabled != null && !enabled()) return;
                    activate?.Invoke();
                },
            };
        }

        /// <summary>A uGUI Button: label from its TMP child (or the given override), enabled from
        /// interactable, activation through its onClick so the game's own handler runs.</summary>
        public static NodeVtable Button(UnityEngine.UI.Button button, Func<string> label = null, ControlType type = null)
            => Button(label ?? (() => LabelOf(button)), () => button.onClick.Invoke(),
                () => button != null && button.interactable, type);

        /// <summary>A uGUI Toggle: "label, toggle, on/off"; activation flips it through isOn so the game's
        /// onValueChanged runs.</summary>
        public static NodeVtable Toggle(UnityEngine.UI.Toggle toggle, Func<string> label = null)
        {
            Func<string> lbl = label ?? (() => LabelOf(toggle));
            return new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(lbl),
                    new NodeAnnouncement(() => toggle.isOn ? Strings.StateOn : Strings.StateOff,
                        live: true, kind: AnnouncementKinds.Value),
                    DisabledPart(() => toggle.interactable),
                },
                SearchText = lbl,
                OnActivate = () => { if (toggle.interactable) toggle.isOn = !toggle.isOn; },
                StateText = () => toggle.isOn ? Strings.StateOn : Strings.StateOff,
            };
        }

        /// <summary>A uGUI Slider: "label, slider, value"; left/right adjust by a step (a tenth of the
        /// range, or 1 for whole-number sliders).</summary>
        public static NodeVtable Slider(UnityEngine.UI.Slider slider, Func<string> label, Func<string> valueText = null)
        {
            Func<string> val = valueText ?? (() => slider.wholeNumbers
                ? ((int)slider.value).ToString()
                : Math.Round(slider.value, 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(label),
                    new NodeAnnouncement(val, live: true, kind: AnnouncementKinds.Value),
                    DisabledPart(() => slider.interactable),
                },
                SearchText = label,
                OnAdjust = (sign, large) =>
                {
                    if (!slider.interactable) return;
                    float step = slider.wholeNumbers ? 1f : (slider.maxValue - slider.minValue) / 10f;
                    if (large) step *= 5f;
                    slider.value = Mathf.Clamp(slider.value + sign * step, slider.minValue, slider.maxValue);
                },
                StateText = val,
            };
        }

        /// <summary>The visible TMP text under a widget, or its object name when it has none.</summary>
        public static string LabelOf(Component widget)
        {
            if (widget == null) return null;
            var tmp = widget.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text;
            var legacy = widget.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (legacy != null && !string.IsNullOrEmpty(legacy.text)) return legacy.text;
            return widget.gameObject.name;
        }

        /// <summary>Whether a widget is both active in the hierarchy and worth listing.</summary>
        public static bool IsShown(Component widget)
            => widget != null && widget.gameObject != null && widget.gameObject.activeInHierarchy;
    }
}
