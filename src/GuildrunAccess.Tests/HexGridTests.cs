using GuildrunAccess.Core.UI;
using Xunit;

namespace GuildrunAccess.Tests
{
    // Pointy-top, odd-r: the odd rows sit half a cell to the right, so a cell's upper and lower
    // neighbours depend on its row's parity; Left and Right never do.
    public class HexGridTests
    {
        private static (int, int) N(int x, int y, HexDir dir)
        {
            HexGrid.Neighbor(x, y, dir, out int nx, out int ny);
            return (nx, ny);
        }

        [Fact]
        public void AnEvenRowsUpperNeighboursSitLeftOfIt()
        {
            Assert.Equal((0, 1), N(1, 0, HexDir.UpLeft));
            Assert.Equal((1, 1), N(1, 0, HexDir.UpRight));
            Assert.Equal((0, -1), N(1, 0, HexDir.DownLeft));
            Assert.Equal((1, -1), N(1, 0, HexDir.DownRight));
            Assert.Equal((0, 0), N(1, 0, HexDir.Left));
            Assert.Equal((2, 0), N(1, 0, HexDir.Right));
        }

        [Fact]
        public void AnOddRowsUpperNeighboursSitRightOfIt()
        {
            Assert.Equal((0, 2), N(0, 1, HexDir.UpLeft));
            Assert.Equal((1, 2), N(0, 1, HexDir.UpRight));
            Assert.Equal((0, 0), N(0, 1, HexDir.DownLeft));
            Assert.Equal((1, 0), N(0, 1, HexDir.DownRight));
            Assert.Equal((-1, 1), N(0, 1, HexDir.Left));
            Assert.Equal((1, 1), N(0, 1, HexDir.Right));
        }

        [Fact]
        public void EveryStepReversesWithTheOppositeStep()
        {
            var opposites = new[] { (HexDir.UpLeft, HexDir.DownRight), (HexDir.UpRight, HexDir.DownLeft), (HexDir.Left, HexDir.Right) };
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 3; x++)
                    foreach (var (a, b) in opposites)
                    {
                        var (ax, ay) = N(x, y, a);
                        Assert.Equal((x, y), N(ax, ay, b));
                        var (bx, by) = N(x, y, b);
                        Assert.Equal((x, y), N(bx, by, a));
                    }
        }

        [Fact]
        public void ColumnsShiftHalfACellOnOddRows()
        {
            Assert.Equal(2f, HexGrid.Column(2, 0));
            Assert.Equal(2.5f, HexGrid.Column(2, 1));
            Assert.Equal(2f, HexGrid.Column(2, 2));
        }
    }
}
