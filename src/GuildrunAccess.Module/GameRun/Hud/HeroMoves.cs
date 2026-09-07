using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using UnityEngine;

namespace GuildrunAccess.Module.GameRun
{
    /// <summary>
    /// The keyboard's version of dragging a hero: a hero picked up from a board cell or a reserve slot
    /// ("Move" / "To board" in its menu) waits here until Enter on a board cell drops it there, or
    /// Escape cancels. One pending move per run screen, shared by the board and party sections and the
    /// screen's Escape.
    /// </summary>
    internal sealed class HeroMoves
    {
        private enum Source { None, Board, Reserve }
        private Source _source = Source.None;
        private Vector2Int _from;
        private int _reserveIndex = -1;
        private string _hero;

        /// <summary>A hero is picked up and waiting for a cell.</summary>
        public bool Pending => _source != Source.None;

        public void PickUpFromBoard(string hero, Vector2Int cell)
        {
            _source = Source.Board;
            _from = cell;
            _hero = hero;
            Speech.Say(Strings.RunPickedUp(hero), interrupt: true);
        }

        public void PickUpFromReserve(string hero, int reserveIndex)
        {
            _source = Source.Reserve;
            _reserveIndex = reserveIndex;
            _hero = hero;
            Speech.Say(Strings.RunPickedUp(hero), interrupt: true);
        }

        /// <summary>Drop the pending hero on a cell (a swap with whatever stands there).</summary>
        public void Drop(Vector2Int cell)
        {
            if (!RunData.IsPlayerCell(cell)) { Speech.Say(Strings.RunMoveInvalid, interrupt: true); return; }
            bool ok = _source == Source.Board
                ? RunData.SwapBoard(_from, cell)
                : RunData.SwapReserveAndBoard(_reserveIndex, cell);
            string hero = _hero;
            Cancel(silent: true);
            Speech.Say(ok ? Strings.RunMoved(hero, RunLabels.CellName(cell)) : Strings.RunMoveFailed, interrupt: true);
        }

        /// <summary>Forget the pending hero; spoken unless silent (a pop, a completed drop).</summary>
        public void Cancel(bool silent = false)
        {
            bool had = Pending;
            _source = Source.None;
            _reserveIndex = -1;
            _hero = null;
            if (had && !silent) Speech.Say(Strings.RunMoveCancelled, interrupt: true);
        }
    }
}
