using System;
using System.Collections.Generic;
using GuildrunAccess.Contracts;
using GuildrunAccess.Core.Audio;
using GuildrunAccess.Core.Strings;
using Xunit;

namespace GuildrunAccess.Tests
{
    // The sound settings and the cue gate, over an in-memory store and a recording engine.
    public class SoundTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public bool GetBool(string key, bool defaultValue) => Values.TryGetValue(key, out var v) ? v == "true" : defaultValue;
            public void SetBool(string key, bool value) => Values[key] = value ? "true" : "false";
            public int GetInt(string key, int defaultValue) => Values.TryGetValue(key, out var v) ? int.Parse(v) : defaultValue;
            public void SetInt(string key, int value) => Values[key] = value.ToString();
            public string GetString(string key, string defaultValue) => Values.TryGetValue(key, out var v) ? v : defaultValue;
            public void SetString(string key, string value) => Values[key] = value;
        }

        private sealed class FakeEngine : IAudioEngine
        {
            public readonly List<(AudioCue Cue, float Volume)> Plays = new List<(AudioCue, float)>();
            public bool Available => true;
            public void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f) => Plays.Add((cue, volume));
        }

        [Fact]
        public void EveryCueHasALabelAndAFileName()
        {
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                Assert.False(string.IsNullOrWhiteSpace(Strings.SoundLabel(cue)), "no label for " + cue);
                Assert.Matches("^[a-z_]+$", AudioCues.FileName(cue));
            }
            Assert.Equal("hero_low_health", AudioCues.FileName(AudioCue.HeroLowHealth));
        }

        [Fact]
        public void AVolumeIsAnOffsetFromTheMasterAndMovesWithIt()
        {
            var store = new MemoryStore();
            var sounds = new SoundVolumes(store);
            var crit = sounds.All[(int)AudioCue.Crit];
            Assert.Equal(100, crit.Value);
            Assert.True(crit.Adjust(-1));
            Assert.Equal(90, crit.Value);
            Assert.Equal("-10", store.Values[crit.Key]);
            Assert.True(sounds.Master.Adjust(+1));
            Assert.Equal(100, crit.Value); // the offset rides the master
            Assert.Equal(1f, sounds.Gain(AudioCue.Crit));
            for (int i = 0; i < 30; i++) crit.Adjust(+1);
            Assert.Equal(SoundVolume.MaxVolume, crit.Value); // clamped
            Assert.False(crit.Adjust(+1));

            // Reloaded from the store, the same values come back.
            var again = new SoundVolumes(store);
            Assert.Equal(110, again.Master.Value);
            Assert.Equal(SoundVolume.MaxVolume, again.All[(int)AudioCue.Crit].Value);
        }

        [Fact]
        public void AnIntervalIsBlankByDefaultAndTakesSecondsOrBlank()
        {
            var store = new MemoryStore();
            var sounds = new SoundVolumes(store);
            var interval = sounds.Interval(AudioCue.HeroDamaged);
            Assert.Null(interval.Seconds);
            Assert.True(interval.TrySet("1.5"));
            Assert.Equal(1.5, interval.Seconds);
            Assert.Equal("1.5", store.Values[interval.Key]);
            Assert.True(interval.TrySet("0,25")); // a comma is a decimal point too
            Assert.Equal(0.25, interval.Seconds);
            Assert.False(interval.TrySet("abc"));
            Assert.False(interval.TrySet("-1"));
            Assert.Equal(0.25, interval.Seconds); // a rejected value changes nothing
            Assert.True(interval.TrySet("  "));
            Assert.Null(interval.Seconds);
            Assert.Equal("", store.Values[interval.Key]);
            Assert.Null(new SoundVolumes(store).Interval(AudioCue.HeroDamaged).Seconds);
        }

        [Fact]
        public void TheGateLetsEveryEventThroughUntilAnIntervalIsSet()
        {
            var store = new MemoryStore();
            var sounds = new SoundVolumes(store);
            var engine = new FakeEngine();
            double now = 0;
            var player = new CuePlayer(new VolumeScaledEngine(engine, sounds), sounds, () => now);

            Assert.True(player.Play(AudioCue.HeroDamaged));
            now = 0.01;
            Assert.True(player.Play(AudioCue.HeroDamaged)); // blank: every event
            Assert.Equal(2, engine.Plays.Count);

            sounds.Interval(AudioCue.HeroDamaged).TrySet("1");
            now = 0.5;
            Assert.False(player.Play(AudioCue.HeroDamaged)); // too soon after the last
            Assert.True(player.Play(AudioCue.EnemyDamaged));  // another cue keeps its own clock
            now = 1.01;
            Assert.True(player.Play(AudioCue.HeroDamaged));
            Assert.Equal(4, engine.Plays.Count);

            // The volume setting scales what reaches the engine.
            sounds.All[(int)AudioCue.HeroDamaged].Adjust(-1);
            now = 3;
            Assert.True(player.Play(AudioCue.HeroDamaged));
            Assert.Equal(0.9f, engine.Plays[engine.Plays.Count - 1].Volume, 3);

            player.Reset();
            now = 3.1;
            Assert.True(player.Play(AudioCue.HeroDamaged)); // a new fight starts the clocks over
        }
    }
}
