using System;
using System.Collections.Generic;
using System.IO;
using FMOD;
using FMODUnity;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Audio;

namespace GuildrunAccess.Module.Audio
{
    /// <summary>
    /// The cue engine over the game's own FMOD core system: one channel group of ours, parented under
    /// the studio SFX bus's channel group so the player's SFX volume slider governs the cues (the
    /// master group when that bus cannot be reached), one <see cref="Sound"/> per cue loaded from
    /// assets/audio on first play and kept. Unity's own audio is switched off in this game (no
    /// AudioSource is audible), so FMOD is the only way to a speaker. A missing file logs once and
    /// stays silent; a failed system disables the engine for the session. Released on module
    /// teardown: FMOD handles are native and survive nothing of a reload.
    /// </summary>
    internal sealed class FmodCueEngine : IAudioEngine, IDisposable
    {
        private readonly string _assetRoot;
        private readonly Dictionary<AudioCue, Sound> _sounds = new Dictionary<AudioCue, Sound>();
        private readonly HashSet<AudioCue> _warned = new HashSet<AudioCue>();
        private ChannelGroup _group;
        private bool _started;
        private bool _failed;

        public FmodCueEngine(string assetRoot) => _assetRoot = assetRoot;

        public bool Available => !_failed;

        private bool EnsureStarted()
        {
            if (_started) return true;
            if (_failed) return false;
            try
            {
                if (!RuntimeManager.IsInitialized) return false; // not failed: the studio system comes up later
                var core = RuntimeManager.CoreSystem;
                var result = core.createChannelGroup("GuildrunAccess", out _group);
                if (result != RESULT.OK) throw new InvalidOperationException("createChannelGroup: " + result);
                // Under the SFX bus, so the game's SFX slider applies; the bus's group exists only once
                // it is locked and the studio system has updated, hence the fallback to the master.
                ChannelGroup parent;
                bool underBus = false;
                var bus = default(FMOD.Studio.Bus);
                if (RuntimeManager.StudioSystem.getBus("bus:/SFX", out bus) == RESULT.OK && bus.lockChannelGroup() == RESULT.OK)
                {
                    RuntimeManager.StudioSystem.flushCommands();
                    if (bus.getChannelGroup(out parent) == RESULT.OK && parent.hasHandle())
                        underBus = parent.addGroup(_group) == RESULT.OK;
                }
                if (!underBus && core.getMasterChannelGroup(out parent) == RESULT.OK)
                    parent.addGroup(_group);
                _started = true;
                CoreLog.Info("audio: cue channel group opened" + (underBus ? " under the SFX bus" : " under the master group"));
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                CoreLog.Warning("audio: FMOD unavailable; cues disabled: " + e.Message);
                return false;
            }
        }

        public void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f)
        {
            if (volume <= 0f || !EnsureStarted()) return;
            try
            {
                if (!TryLoad(cue, out var sound)) return;
                Channel channel;
                var result = RuntimeManager.CoreSystem.playSound(sound, _group, true, out channel);
                if (result != RESULT.OK)
                {
                    CoreLog.Warning("audio: playSound " + cue + ": " + result);
                    return;
                }
                channel.setVolume(Math.Max(0f, volume));
                channel.setPan(Math.Max(-1f, Math.Min(1f, pan)));
                if (Math.Abs(pitch - 1f) > 0.001f) channel.setPitch(pitch);
                channel.setPaused(false);
                CoreLog.Info("audio: " + cue + " at " + volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                CoreLog.Warning("audio: cue " + cue + " failed: " + e.Message);
            }
        }

        private bool TryLoad(AudioCue cue, out Sound sound)
        {
            if (_sounds.TryGetValue(cue, out sound)) return true;
            string path = Path.Combine(_assetRoot, AudioCues.GroupOf(cue).ToString().ToLowerInvariant(), AudioCues.FileName(cue) + ".wav");
            if (!File.Exists(path))
            {
                if (_warned.Add(cue)) CoreLog.Warning("audio: no file for " + cue + " at " + path);
                return false;
            }
            var result = RuntimeManager.CoreSystem.createSound(path, MODE.DEFAULT | MODE.LOOP_OFF, out sound);
            if (result != RESULT.OK)
            {
                if (_warned.Add(cue)) CoreLog.Warning("audio: createSound " + cue + ": " + result);
                return false;
            }
            _sounds[cue] = sound;
            return true;
        }

        public void Dispose()
        {
            try
            {
                foreach (var sound in _sounds.Values) sound.release();
                _sounds.Clear();
                if (_started) _group.release();
            }
            catch (Exception e) { CoreLog.Warning("audio: release failed: " + e.Message); }
            _started = false;
        }
    }
}
