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
        public void EveryNeighbourIsOneStepAway()
        {
            var dirs = new[] { HexDir.UpLeft, HexDir.UpRight, HexDir.Left, HexDir.Right, HexDir.DownLeft, HexDir.DownRight };
            for (int y = -1; y < 4; y++)
                for (int x = 0; x < 3; x++)
                    foreach (var dir in dirs)
                    {
                        var (nx, ny) = N(x, y, dir);
                        Assert.Equal(1, HexGrid.Distance(x, y, nx, ny));
                        Assert.Equal(1, HexGrid.Distance(nx, ny, x, y));
                    }
        }

        [Fact]
        public void DistanceCountsTheStepsOfAWalk()
        {
            Assert.Equal(0, HexGrid.Distance(2, 3, 2, 3));
            Assert.Equal(4, HexGrid.Distance(0, 2, 4, 2));
            // Straight "up" zigzags, a step a row: the column comes back every second row.
            Assert.Equal(2, HexGrid.Distance(3, 0, 3, 2));
            Assert.Equal(5, HexGrid.Distance(3, 0, 3, 5));
            // Two rows up cover one column either way for free; the rest is walked along the row.
            Assert.Equal(2, HexGrid.Distance(3, 0, 2, 2));
            Assert.Equal(2, HexGrid.Distance(3, 0, 4, 2));
            Assert.Equal(3, HexGrid.Distance(3, 0, 5, 2));
            // From an odd row the free column is the one to the right.
            Assert.Equal(2, HexGrid.Distance(0, 1, 1, 3));
            Assert.Equal(3, HexGrid.Distance(0, 1, 2, 3));
        }

        [Fact]
        public void DistanceMatchesABreadthFirstWalk()
        {
            var dirs = new[] { HexDir.UpLeft, HexDir.UpRight, HexDir.Left, HexDir.Right, HexDir.DownLeft, HexDir.DownRight };
            foreach (var start in new[] { (3, 2), (3, 3) })
            {
                var steps = new System.Collections.Generic.Dictionary<(int, int), int> { [start] = 0 };
                var queue = new System.Collections.Generic.Queue<(int, int)>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var at = queue.Dequeue();
                    if (steps[at] == 4) continue;
                    foreach (var dir in dirs)
                    {
                        var next = N(at.Item1, at.Item2, dir);
                        if (steps.ContainsKey(next)) continue;
                        steps[next] = steps[at] + 1;
                        queue.Enqueue(next);
                    }
                }
                foreach (var pair in steps)
                    Assert.Equal(pair.Value, HexGrid.Distance(start.Item1, start.Item2, pair.Key.Item1, pair.Key.Item2));
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
