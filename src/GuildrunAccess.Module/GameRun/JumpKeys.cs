using System;
using System.Collections.Generic;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Core.UI;
using Navigation = GuildrunAccess.Core.UI.Navigation;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The run's jump keys: Alt+B the board (or the panel that covers it: the shop's offers, an event's
    /// choices, the crossroads' paths), Alt+T the party, Alt+I the items and relics, each a landing on
    /// that Tab-stop as Tab would make it, from anywhere on the screen, in place of tabbing round to
    /// it. The keys are UI actions with no handler of their own: the navigator hands one to the
    /// focused screen by its key, and a screen offers it, through these, only while the stop is on
    /// show, so the key help lists a jump exactly where it lands somewhere.
    /// </summary>
    internal static class JumpKeys
    {
        public const string Board = "jump.board";
        public const string Party = "jump.party";
        public const string Items = "jump.items";

        /// <summary>The jump to a Tab-stop, or null while the render has no node in it.</summary>
        public static ElementAction Stop(string key, object stop, string label)
        {
            if (stop == null || !Navigation.HasStop(stop)) return null;
            return new ElementAction(key, Strings.JumpTo(label), _ => Navigation.JumpToStop(stop));
        }

        /// <summary>The jumps of these stops, in order, each only while its stop is up.</summary>
        public static IEnumerable<ElementAction> Stops(params (string key, object stop, string label)[] jumps)
        {
            foreach (var jump in jumps)
            {
                var action = Stop(jump.key, jump.stop, jump.label);
                if (action != null) yield return action;
            }
        }

        /// <summary>A jump with a landing of its own (an event's first choice), offered when
        /// <paramref name="available"/> says so.</summary>
        public static ElementAction To(string key, string label, bool available, Action jump)
            => available ? new ElementAction(key, Strings.JumpTo(label), _ => jump()) : null;
    }
}
