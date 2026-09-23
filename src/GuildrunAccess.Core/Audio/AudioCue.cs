namespace GuildrunAccess.Core.Audio
{
    /// <summary>
    /// A named one-shot cue (the engine owns the sound file behind each name, so Core stays free of
    /// paths): what the fight shows but never sounds, for a player who cannot watch the board. One
    /// cue per file under assets/audio; a placeholder is replaced 1:1 by dropping in a file with the
    /// same name. The dd2a11y (Harkest Dungeon) pattern.
    /// </summary>
    public enum AudioCue
    {
        // Combat (assets/audio/combat).
        /// <summary>A hero takes damage.</summary>
        HeroDamaged,
        /// <summary>An enemy takes damage.</summary>
        EnemyDamaged,
        /// <summary>A critical hit lands, on either side.</summary>
        Crit,
        /// <summary>A hero is healed.</summary>
        HeroHealed,
        /// <summary>A hero drops below a quarter of its health (once, until it recovers past it).</summary>
        HeroLowHealth,
        /// <summary>A hero falls.</summary>
        HeroDied,
        /// <summary>An enemy falls.</summary>
        EnemyDied,
        /// <summary>A hero casts its active ability.</summary>
        HeroCast,
        /// <summary>An enemy casts an ability.</summary>
        EnemyCast,
        /// <summary>A status lands on a hero.</summary>
        StatusOnHero,
        /// <summary>A hero's mana fills: its ability is ready.</summary>
        ManaFull,
        /// <summary>A rush effect starts.</summary>
        RushStarted,
        /// <summary>A stall effect starts.</summary>
        StallStarted,
        /// <summary>Burn lands on a unit (panned to its side: heroes left, enemies right).</summary>
        StatusBurn,
        /// <summary>Frost lands on a unit (panned to its side).</summary>
        StatusFrost,
        /// <summary>Poison lands on a unit (panned to its side).</summary>
        StatusPoison,
        /// <summary>A unit is stunned (panned to its side).</summary>
        StatusStun,
        /// <summary>A unit gains a shield (panned to its side).</summary>
        ShieldGained,
        /// <summary>Shards are gained mid-fight (the game's shard feedback).</summary>
        ShardsGained,
        /// <summary>A unit's stats go up mid-fight (the game's stat-gain feedback).</summary>
        StatUp,
        /// <summary>A hero taunts the enemies onto itself.</summary>
        Taunt,
    }

    /// <summary>The glossary's grouping of cues, mirroring the assets/audio folders.</summary>
    public enum AudioCueGroup
    {
        Combat,
    }

    public static class AudioCues
    {
        /// <summary>A cue's group: every cue is a combat cue so far.</summary>
        public static AudioCueGroup GroupOf(AudioCue cue) => AudioCueGroup.Combat;

        /// <summary>The file a cue plays, under the group's folder, without an extension:
        /// "hero_damaged" for <see cref="AudioCue.HeroDamaged"/> (the enum name in snake case).</summary>
        public static string FileName(AudioCue cue)
        {
            string name = cue.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
