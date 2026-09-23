using System;
using System.Collections.Generic;
using GuildrunAccess.Core.Audio;
using GuildrunAccess.Core.Graph;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using GuildrunAccess.Module.UI;
using Navigation = GuildrunAccess.Core.UI.Navigation;
using Screen = GuildrunAccess.Core.Screens.Screen;

namespace GuildrunAccess.Module.Screens
{
    /// <summary>
    /// The mod sounds glossary (a child of the mod menu; the dd2a11y pattern): the master volume, then
    /// for each cue a row naming what the mod plays it for, its volume as the value (Left and Right
    /// step it, Enter plays the sound once at that level, the sound alone being the feedback), and a
    /// row for its interval: the least seconds between two plays of it, blank for every event
    /// (the default). Enter on an interval row opens the mod's own number entry
    /// (<see cref="NumberEdit"/>): digits and a point, Enter commits (an empty entry clears it back
    /// to every event), Escape keeps what was there. The values live in the host's settings store,
    /// so they survive reloads and restarts.
    /// </summary>
    public sealed class ModSoundsScreen : Screen
    {
        public override string Key => "mod.sounds";
        public override string ScreenName => Strings.ModSounds;
        public override bool IsActive() => false; // only ever a child of the mod menu

        public override void Build(GraphBuilder b)
        {
            var sounds = ModuleMain.Sounds;
            var engine = ModuleMain.CueEngine;
            b.PushContext(Strings.ModSounds, Strings.RoleList);
            if (sounds == null || engine == null)
            {
                b.AddItem(ControlId.Structural("modsounds:none"), GameNodes.Text(() => Strings.NoTooltip));
                b.PopContext();
                return;
            }
            b.AddItem(ControlId.Structural("modsounds:master"), Adjustable(() => Strings.SoundMaster,
                () => Strings.SoundPercent(sounds.Master.Value), sign => sounds.Master.Adjust(sign), null));
            foreach (var volume in sounds.All)
            {
                var v = volume;
                var interval = sounds.Interval(v.Cue);
                b.AddItem(ControlId.Structural("modsounds:volume:" + AudioCues.FileName(v.Cue)), Adjustable(() => Strings.SoundLabel(v.Cue),
                    () => Strings.SoundPercent(v.Value), sign => v.Adjust(sign), () => engine.PlayCue(v.Cue, 1f, 0f)));
                b.AddItem(ControlId.Structural("modsounds:interval:" + AudioCues.FileName(v.Cue)), IntervalRow(v.Cue, interval));
            }
            b.PopContext();
        }

        // A volume row: a slider in all but the widget (Left and Right step it, the percent read at
        // once); Enter previews the cue when it has one.
        private static NodeVtable Adjustable(Func<string> label, Func<string> value, Func<int, bool> adjust, Action preview)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                OnAdjust = (sign, large) => { for (int i = 0; i < (large ? 5 : 1); i++) adjust(sign); },
                StateText = value,
                OnActivate = preview,
            };
        }

        // An interval row: an edit whose value is the seconds, or "blank, every event".
        private static NodeVtable IntervalRow(AudioCue cue, CueInterval interval)
        {
            Func<string> label = () => Strings.SoundIntervalOf(Strings.SoundLabel(cue));
            Func<string> value = () => interval.Text ?? Strings.SoundEveryEvent;
            return new NodeVtable
            {
                ControlType = ControlTypes.Edit,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                OnActivate = () => NumberEdit.Begin(
                    typed =>
                    {
                        // A new value is read by the row's live value itself; a refused one says why.
                        if (!interval.TrySet(typed)) Core.Speech.Say(Strings.SoundIntervalInvalid);
                    },
                    () => Navigation.AnnounceCurrent(),
                    hint: value()),
            };
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }
}
