using MSMask.Core.Models;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Box"/> geometry and <see cref="PipelineResult.KindCounts"/>.
    /// </summary>
    public class BoxTests
    {
        /// <summary>
        /// A box compared with itself overlaps completely, so its IoU is 1.
        /// </summary>
        [Fact]
        public void Iou_SameBox_ReturnsOne()
        {
            var a = new Box(0, 0, 10, 10);

            Assert.Equal(1.0, a.Iou(a), precision: 6);
        }

        /// <summary>
        /// Boxes that are far apart have no intersection and an IoU of 0.
        /// </summary>
        [Fact]
        public void Iou_DisjointBoxes_ReturnsZero()
        {
            var a = new Box(0, 0, 10, 10);
            var b = new Box(20, 20, 30, 30);

            Assert.Null(a.Intersection(b));
            Assert.Equal(0.0, a.Iou(b));
        }

        /// <summary>
        /// Boxes that only share an edge are not considered overlapping.
        /// </summary>
        [Fact]
        public void Intersection_TouchingEdges_ReturnsNull()
        {
            var a = new Box(0, 0, 10, 10);
            var b = new Box(10, 0, 20, 10);

            Assert.Null(a.Intersection(b));
            Assert.Equal(0.0, a.Iou(b));
        }

        /// <summary>
        /// Two 10x10 boxes shifted by 5 pixels share 50 px² out of 150 px² in total, so IoU = 1/3.
        /// </summary>
        [Fact]
        public void Iou_HalfOverlap_ReturnsOneThird()
        {
            var a = new Box(0, 0, 10, 10);
            var b = new Box(5, 0, 15, 10);

            Assert.Equal(new Box(5, 0, 10, 10), a.Intersection(b));
            Assert.Equal(new Box(0, 0, 15, 10), a.Union(b));
            Assert.Equal(1.0 / 3, a.Iou(b), precision: 6);
        }

        /// <summary>
        /// An inverted box (right edge left of the left edge) has zero width and zero area.
        /// </summary>
        [Fact]
        public void Width_InvertedBox_ReturnsZero()
        {
            var a = new Box(10, 0, 5, 10);

            Assert.Equal(0.0, a.Width);
            Assert.Equal(0.0, a.Area);
        }

        /// <summary>
        /// Expand moves each horizontal edge by dx and each vertical edge by dy.
        /// </summary>
        [Fact]
        public void Expand_GrowsEverySide()
        {
            var box = new Box(10, 10, 20, 20);

            Assert.Equal(new Box(8, 7, 22, 23), box.Expand(2, 3));
        }

        /// <summary>
        /// Clip pulls edges that fall outside the image back to the image bounds.
        /// </summary>
        [Fact]
        public void Clip_BoxOutsideImage_IsLimitedToImage()
        {
            var box = new Box(-5, -5, 50, 50);

            Assert.Equal(new Box(0, 0, 40, 30), box.Clip(40, 30));
        }

        /// <summary>
        /// UnionAll returns the smallest box that contains every input box.
        /// </summary>
        [Fact]
        public void UnionAll_ThreeBoxes_ReturnsBoundingBox()
        {
            Box[] boxes = [new(0, 0, 1, 1), new(5, 5, 6, 6), new(2, -3, 3, 0)];

            Assert.Equal(new Box(0, -3, 6, 6), Box.UnionAll(boxes));
        }

        /// <summary>
        /// UnionAll cannot produce a box from an empty sequence.
        /// </summary>
        [Fact]
        public void UnionAll_EmptySequence_Throws()
        {
            Assert.Throws<ArgumentException>(() => Box.UnionAll([]));
        }

        /// <summary>
        /// KindCounts groups detections by kind and counts each group.
        /// </summary>
        [Fact]
        public void KindCounts_CountsDetectionsPerKind()
        {
            var result = new PipelineResult();
            var box = new Box(0, 0, 1, 1);
            result.Detections.Add(new Detection(Kinds.Tckn, box, "10000000146", "pattern"));
            result.Detections.Add(new Detection(Kinds.Tckn, box, "12345678950", "pattern"));
            result.Detections.Add(new Detection(Kinds.Telefon, box, "0532 123 45 67", "pattern"));

            Dictionary<string, int> counts = result.KindCounts();

            Assert.Equal(2, counts[Kinds.Tckn]);
            Assert.Equal(1, counts[Kinds.Telefon]);
            Assert.Equal(2, counts.Count);
        }
    }
}
