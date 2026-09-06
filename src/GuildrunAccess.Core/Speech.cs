using System;

namespace GuildrunAccess.Core
{
    /// <summary>
    /// The speech seam for Core: the module points <see cref="Speak"/> at the host pipeline in Load;
    /// tests capture it. Convention carried from the reference mods: interrupt for focus MOVES (so a
    /// held key-repeat reads the item you land on), queue for screen entry and feedback lines.
    /// </summary>
    public static class Speech
    {
        public static Action<string, bool> Speak = (text, interrupt) => { };

        public static void Say(string text, bool interrupt = false)
        {
            if (!string.IsNullOrEmpty(text)) Speak(text, interrupt);
        }
    }
}
