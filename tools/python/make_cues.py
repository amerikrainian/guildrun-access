"""Generate the placeholder cue files under assets/audio: one short tone (or a few) per AudioCue,
distinct enough to tell apart, so the sounds exist until authored ones replace them 1:1 by name.

    uv run python tools/python/make_cues.py [--force]

Writes only the files that are missing (--force rewrites all), mono 16-bit 44.1 kHz WAV.
The names are AudioCues.FileName (the enum name in snake case) under the cue's group folder.
"""
import argparse
import math
import os
import struct
import wave

RATE = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "assets", "audio")

# group/name: (notes in Hz, note length s, gap s, waveform, amplitude)
CUES = {
    "combat/hero_damaged": ([330], 0.08, 0.02, "tri", 0.35),
    "combat/enemy_damaged": ([220], 0.06, 0.02, "square", 0.2),
    "combat/crit": ([880, 1320], 0.06, 0.02, "sine", 0.45),
    "combat/hero_healed": ([523, 659, 784], 0.07, 0.02, "sine", 0.35),
    "combat/hero_low_health": ([440, 415, 440, 415], 0.09, 0.01, "tri", 0.45),
    "combat/hero_died": ([392, 311, 233], 0.16, 0.02, "rich", 0.5),
    "combat/enemy_died": ([196, 262, 330], 0.09, 0.02, "rich", 0.4),
    "combat/hero_cast": ([659, 988], 0.09, 0.02, "sine", 0.4),
    "combat/enemy_cast": ([311, 262], 0.11, 0.02, "square", 0.22),
    "combat/status_on_hero": ([587, 587], 0.05, 0.03, "tri", 0.35),
    "combat/mana_full": ([784, 1047, 1319], 0.06, 0.02, "sine", 0.4),
    "combat/rush_started": ([523, 659, 784, 1047], 0.05, 0.005, "sine", 0.4),
    "combat/stall_started": ([523, 392, 262], 0.12, 0.01, "tri", 0.4),
    # These ship authored (assets/audio/combat, from the maintainer); the tones below are only a
    # fallback for a cue whose file went missing.
    "combat/status_burn": ([740, 880], 0.05, 0.01, "square", 0.2),
    "combat/status_frost": ([1175, 988], 0.06, 0.01, "sine", 0.35),
    "combat/status_poison": ([349, 370], 0.07, 0.01, "tri", 0.35),
    "combat/status_stun": ([494, 494, 494], 0.04, 0.02, "square", 0.2),
    "combat/shield_gained": ([392, 587], 0.08, 0.01, "rich", 0.4),
    "combat/shards_gained": ([1568, 2093], 0.04, 0.01, "sine", 0.35),
    "combat/stat_up": ([440, 554, 659], 0.05, 0.005, "sine", 0.4),
    "combat/taunt": ([262, 196], 0.1, 0.01, "rich", 0.45),
    # A status wearing off: the authored files are the arrival reversed (ffmpeg -af areverse); the
    # fallbacks play the arrival's notes backwards.
    "combat/status_on_hero_lost": ([587, 587], 0.05, 0.03, "tri", 0.3),
    "combat/status_burn_lost": ([880, 740], 0.05, 0.01, "square", 0.2),
    "combat/status_frost_lost": ([988, 1175], 0.06, 0.01, "sine", 0.35),
    "combat/status_poison_lost": ([370, 349], 0.07, 0.01, "tri", 0.35),
    "combat/status_stun_lost": ([494, 494], 0.04, 0.02, "square", 0.2),
    "combat/shield_lost": ([587, 392], 0.08, 0.01, "rich", 0.4),
}


def sample(kind, f, t):
    if kind == "sine":
        return math.sin(2 * math.pi * f * t)
    if kind == "square":
        return 1.0 if math.sin(2 * math.pi * f * t) > 0 else -1.0
    if kind == "tri":
        return 2 * abs(2 * ((t * f) % 1) - 1) - 1
    return math.sin(2 * math.pi * f * t) * 0.6 + math.sin(2 * math.pi * f * 2 * t) * 0.4


def tone(path, notes, dur, gap, kind, amp):
    frames = []
    for f in notes:
        n = int(RATE * dur)
        for i in range(n):
            env = min(1.0, i / 200) * min(1.0, (n - i) / (RATE * 0.03))
            frames.append(int(32767 * amp * env * sample(kind, f, i / RATE)))
        frames.extend([0] * int(RATE * gap))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", s) for s in frames))


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--force", action="store_true", help="rewrite files that exist")
    args = parser.parse_args()
    for name, spec in CUES.items():
        path = os.path.normpath(os.path.join(ROOT, name + ".wav"))
        if os.path.exists(path) and not args.force:
            print("kept   ", os.path.relpath(path))
            continue
        tone(path, *spec)
        print("written", os.path.relpath(path))


if __name__ == "__main__":
    main()
