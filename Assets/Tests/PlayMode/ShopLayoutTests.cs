// TideWall 3D — the shop's grid: rows of four, moved through up and down from the top.
using NUnit.Framework;

namespace TideWall.Tests
{
    public class ShopLayoutTests
    {
        static Menu Grid(int items)
        {
            var m = new Menu { Layout = MenuLayout.Grid };
            for (int i = 0; i < items; i++) m.Items.Add(new MenuItem { Label = i.ToString() });
            return m;
        }

        [Test]
        public void TheGridGoesDownInRowsOfFour()
        {
            var m = Grid(10);
            Assert.AreEqual(0, m.Selected, "it starts at the top");
            m.MoveGrid(0, 1);
            Assert.AreEqual(4, m.Selected, "down is the next row");
            m.MoveGrid(1, 0);
            Assert.AreEqual(5, m.Selected);
            m.MoveGrid(0, 1);
            Assert.AreEqual(9, m.Selected, "the short last row: its last item");
            m.MoveGrid(0, 1);
            Assert.AreEqual(1, m.Selected, "past the bottom: back to the top");
            m.MoveGrid(0, -1);
            Assert.AreEqual(1, m.Selected, "up from the top row stays there");
        }
    }
}
