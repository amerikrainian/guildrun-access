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
        private bool _underBus;        // our group hangs under the SFX bus's (else the master's)
        private FMOD.Studio.Bus? _lockedBus; // the bus we locked ourselves, to unlock on teardown
        private double _nextBusTry;    // the SFX bus group appears only once the studio system has built it
        private bool _busWarned;

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
                ChannelGroup master;
                if (core.getMasterChannelGroup(out master) == RESULT.OK) master.addGroup(_group);
                _started = true;
                CoreLog.Info("audio: cue channel group opened");
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                CoreLog.Warning("audio: FMOD unavailable; cues disabled: " + e.Message);
                return false;
            }
        }

        // Move our group under the SFX bus's, so the game's SFX slider applies. The bus's channel
        // group exists only once the bus is locked and the studio system has built it, which a
        // reload's moment does not always have ready: tried again on later plays, a second apart,
        // the group playing under the master meanwhile (a move re-parents it, nothing restarts).
        private void TryAttachToSfxBus()
        {
            double now = UnityEngine.Time.unscaledTimeAsDouble;
            if (now < _nextBusTry) return;
            _nextBusTry = now + 1.0;
            string step = "getBus";
            try
            {
                var studio = RuntimeManager.StudioSystem;
                FMOD.Studio.Bus bus;
                var result = studio.getBus("bus:/SFX", out bus);
                if (result == RESULT.OK)
                {
                    // Locked by us, or still locked by the generation before a reload: the group exists either way.
                    step = "lockChannelGroup";
                    result = bus.lockChannelGroup();
                    if (result == RESULT.OK) _lockedBus = bus;
                    else if (result == RESULT.ERR_ALREADY_LOCKED) result = RESULT.OK;
                }
                if (result == RESULT.OK) { step = "flushCommands"; result = studio.flushCommands(); }
                ChannelGroup parent = default(ChannelGroup);
                if (result == RESULT.OK) { step = "getChannelGroup"; result = bus.getChannelGroup(out parent); }
                if (result == RESULT.OK && !parent.hasHandle()) { step = "bus group not built yet"; result = RESULT.ERR_NOTREADY; }
                if (result == RESULT.OK) { step = "addGroup"; result = parent.addGroup(_group); }
                if (result == RESULT.OK)
                {
                    _underBus = true;
                    CoreLog.Info("audio: cue channel group moved under the SFX bus");
                    return;
                }
                if (!_busWarned) { _busWarned = true; CoreLog.Warning("audio: SFX bus not reached at " + step + " (" + result + "); cues play under the master group until it is"); }
            }
            catch (Exception e)
            {
                if (!_busWarned) { _busWarned = true; CoreLog.Warning("audio: SFX bus attach failed at " + step + ": " + e.Message); }
            }
        }

        public void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f)
        {
            if (volume <= 0f || !EnsureStarted()) return;
            if (!_underBus) TryAttachToSfxBus();
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
                if (_lockedBus != null) _lockedBus.Value.unlockChannelGroup();
                _lockedBus = null;
            }
            catch (Exception e) { CoreLog.Warning("audio: release failed: " + e.Message); }
            _started = false;
        }
    }
}
