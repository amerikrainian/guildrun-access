namespace GuildrunAccess.Core.UI
{
    /// <summary>A neighbour of a cell on a pointy-top hex grid: the six edges of a hexagon whose points
    /// face up and down. Left and Right share the row; the other four sit on the rows above and below,
    /// half a cell aside. No neighbour lies straight up or down.</summary>
    public enum HexDir { UpLeft, UpRight, Left, Right, DownLeft, DownRight }

    /// <summary>
    /// Pointy-top hex arithmetic in "odd-r" offset coordinates: horizontal rows, y growing upward, every
    /// odd row shifted half a cell to the right of the even rows (Unity's hexagon Grid layout, which the
    /// game's board is). Engine-free; the module maps the game's cells onto plain ints.
    /// </summary>
    public static class HexGrid
    {
        /// <summary>The cell one step in <paramref name="dir"/> from (x, y); it may lie off any grid.</summary>
        public static void Neighbor(int x, int y, HexDir dir, out int nx, out int ny)
        {
            bool odd = (y & 1) != 0;
            switch (dir)
            {
                case HexDir.Left: nx = x - 1; ny = y; break;
                case HexDir.Right: nx = x + 1; ny = y; break;
                case HexDir.UpLeft: nx = odd ? x : x - 1; ny = y + 1; break;
                case HexDir.UpRight: nx = odd ? x + 1 : x; ny = y + 1; break;
                case HexDir.DownLeft: nx = odd ? x : x - 1; ny = y - 1; break;
                default: nx = odd ? x + 1 : x; ny = y - 1; break;
            }
        }

        /// <summary>A cell's horizontal position in cell widths, the odd rows' half-cell shift included:
        /// which cell of another row stands nearest straight ahead.</summary>
        public static float Column(int x, int y) => x + ((y & 1) != 0 ? 0.5f : 0f);
    }
}
