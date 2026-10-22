using NUnit.Framework;
using Rth.Client.Ui.Modals;

namespace Rth.Client.Tests
{
    /// <summary>The world map's zoom steps and panning.</summary>
    public class WorldMapViewTests
    {
        // The shipped continent is 1920 x 1824 tiles; the modal's map is about 1000 x 560 pixels.
        private static WorldMapView View()
        {
            var view = new WorldMapView(1920f, 1824f);
            view.Resize(1000f, 560f);
            return view;
        }

        [Test]
        public void TheStepsRunFromTheWholeMapToCloseIn()
        {
            WorldMapView view = View();
            Assert.AreEqual(560f / 1824f * 0.96f, view.FitScale, 1e-4f);
            Assert.AreEqual(new[] { view.FitScale, 0.75f, 1f, 1.5f, 2f, 3f }, view.Levels);
            Assert.AreEqual(1f, view.Scale); // opens at a tile a pixel
        }

        [Test]
        public void ZoomingKeepsThePointUnderThePointerStill()
        {
            WorldMapView view = View();
            view.CentreOn(800f, 900f);
            (float mx, float my) = view.ToMap(300f, 200f);
            Assert.IsTrue(view.Zoom(+1, 300f, 200f));
            Assert.AreEqual(1.5f, view.Scale);
            (float x, float y) = view.ToView(mx, my);
            Assert.AreEqual(300f, x, 1e-3f);
            Assert.AreEqual(200f, y, 1e-3f);

            view.Zoom(+1, 0f, 0f);
            view.Zoom(+1, 0f, 0f);
            Assert.IsFalse(view.Zoom(+1, 0f, 0f)); // the last step
            Assert.IsFalse(view.CanZoomIn);
            for (int i = 0; i < 10; i++) view.Zoom(-1, 500f, 280f);
            Assert.AreEqual(view.FitScale, view.Scale);
            Assert.IsFalse(view.CanZoomOut);
        }

        [Test]
        public void TheMapCantBeDraggedOffScreen()
        {
            WorldMapView view = View();
            view.PanBy(5000f, 5000f);
            Assert.AreEqual(0f, view.PanX);
            Assert.AreEqual(0f, view.PanY);
            view.PanBy(-99999f, -99999f);
            Assert.AreEqual(1000f - 1920f, view.PanX);
            Assert.AreEqual(560f - 1824f, view.PanY);

            // Zoomed out to the whole map, it sits in the middle and doesn't move.
            for (int i = 0; i < 10; i++) view.Zoom(-1, 0f, 0f);
            float panX = view.PanX;
            view.PanBy(100f, 0f);
            Assert.AreEqual(panX, view.PanX);
            Assert.AreEqual((1000f - 1920f * view.Scale) / 2f, view.PanX, 1e-3f);
        }

        [Test]
        public void CentringPutsAPointInTheMiddleUnlessThatShowsPastTheEdge()
        {
            WorldMapView view = View();
            view.CentreOn(960f, 900f);
            (float x, float y) = view.ToView(960f, 900f);
            Assert.AreEqual(500f, x, 1e-3f);
            Assert.AreEqual(280f, y, 1e-3f);
            view.CentreOn(10f, 10f); // the corner: clamped
            Assert.AreEqual(0f, view.PanX);
            Assert.AreEqual(0f, view.PanY);
        }
    }
}
