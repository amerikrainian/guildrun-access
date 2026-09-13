namespace GuildrunAccess.Core.Buffers
{
    /// <summary>
    /// The registered buffer keys, in cycling order. The ui buffer carries the focused control's own
    /// lines (<see cref="NodeLines"/>); the hero and item buffers are filled by the focused control
    /// through <see cref="Graph.NodeVtable.SideLines"/> (the hero it concerns, the items it carries);
    /// the relics buffer lists the run's relics; the party and enemies buffers read the battlefield
    /// and are empty outside a fight; the combat buffer is the battle events log.
    /// </summary>
    public static class BufferKeys
    {
        public const string Ui = "ui";
        public const string Hero = "hero";
        public const string Item = "item";
        public const string Relic = "relic";
        public const string Party = "party";
        public const string Enemies = "enemies";
        public const string Combat = "combat";
    }
}
