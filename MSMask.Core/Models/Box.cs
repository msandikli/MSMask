using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core.Models
{
    /// <summary>
    /// Represents an axis-aligned rectangle in image pixel coordinates.
    /// </summary>
    /// <remarks>
    /// The origin (0, 0) is the top-left corner of the image; x grows to the right and y grows downwards.
    /// The rectangle spans from (<see cref="X0"/>, <see cref="Y0"/>) (top-left) to
    /// (<see cref="X1"/>, <see cref="Y1"/>) (bottom-right). The type is an immutable value type:
    /// every operation returns a new <see cref="Box"/> instead of modifying the current one.
    /// </remarks>
    /// <param name="X0">The x coordinate of the left edge.</param>
    /// <param name="Y0">The y coordinate of the top edge.</param>
    /// <param name="X1">The x coordinate of the right edge.</param>
    /// <param name="Y1">The y coordinate of the bottom edge.</param>
    public readonly record struct Box(double X0, double Y0, double X1, double Y1)
    {
        /// <summary>
        /// Gets the width of the box.
        /// </summary>
        /// <value>
        /// <c>X1 - X0</c>, or 0 when the box is inverted (<c>X1 &lt; X0</c>).
        /// </value>
        public double Width => Math.Max(0, X1 - X0);

        /// <summary>
        /// Gets the height of the box.
        /// </summary>
        /// <value>
        /// <c>Y1 - Y0</c>, or 0 when the box is inverted (<c>Y1 &lt; Y0</c>).
        /// </value>
        public double Height => Math.Max(0, Y1 - Y0);

        /// <summary>
        /// Gets the area of the box.
        /// </summary>
        /// <value>
        /// <see cref="Width"/> multiplied by <see cref="Height"/>; 0 for empty or inverted boxes.
        /// </value>
        public double Area => Width * Height;

        /// <summary>
        /// Computes the region shared by this box and another box.
        /// </summary>
        /// <param name="other">The box to intersect with.</param>
        /// <returns>
        /// The overlapping rectangle, or <see langword="null"/> when the boxes do not overlap.
        /// Boxes that only touch along an edge are considered non-overlapping.
        /// </returns>
        public Box? Intersection(Box other)
        {
            double x0 = Math.Max(X0, other.X0);
            double y0 = Math.Max(Y0, other.Y0);
            double x1 = Math.Min(X1, other.X1);
            double y1 = Math.Min(Y1, other.Y1);
            if (x1 <= x0 || y1 <= y0)
            {
                return null;
            }
            return new Box(x0, y0, x1, y1);
        }

        /// <summary>
        /// Computes the smallest box that contains both this box and another box.
        /// </summary>
        /// <param name="other">The box to combine with.</param>
        /// <returns>The bounding box of the two boxes.</returns>
        public Box Union(Box other) =>
            new(Math.Min(X0, other.X0), Math.Min(Y0, other.Y0), Math.Max(X1, other.X1), Math.Max(Y1, other.Y1));

        /// <summary>
        /// Computes the Intersection over Union (IoU) of this box and another box.
        /// </summary>
        /// <remarks>
        /// IoU = shared area / (area of this box + area of other box - shared area).
        /// The evaluation treats a prediction as correct when its IoU with a label is at least 0.5.
        /// </remarks>
        /// <param name="other">The box to compare with.</param>
        /// <returns>
        /// A value between 0 (no overlap) and 1 (identical boxes).
        /// Returns 0 when the boxes do not overlap or both boxes are empty.
        /// </returns>
        public double Iou(Box other)
        {
            Box? inter = Intersection(other);
            if (inter is null)
            {
                return 0;
            }
            double common = inter.Value.Area;
            double total = Area + other.Area - common;
            return total > 0 ? common / total : 0;
        }

        /// <summary>
        /// Grows the box outwards on every side.
        /// </summary>
        /// <remarks>
        /// Used to add a safety margin around masked text. Negative values shrink the box.
        /// </remarks>
        /// <param name="dx">The distance added to the left and right edges.</param>
        /// <param name="dy">The distance added to the top and bottom edges.</param>
        /// <returns>A new box that is <c>2 * dx</c> wider and <c>2 * dy</c> taller.</returns>
        public Box Expand(double dx, double dy) => new(X0 - dx, Y0 - dy, X1 + dx, Y1 + dy);

        /// <summary>
        /// Restricts the box to the bounds of an image.
        /// </summary>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>
        /// A new box whose edges lie within (0, 0)–(<paramref name="width"/>, <paramref name="height"/>).
        /// </returns>
        public Box Clip(double width, double height) =>
            new(Math.Max(0, X0), Math.Max(0, Y0), Math.Min(width, X1), Math.Min(height, Y1));

        /// <summary>
        /// Computes the smallest box that contains every box in a sequence.
        /// </summary>
        /// <remarks>
        /// Used to merge the word boxes of a multi-word match (for example a phone number
        /// read as "0532", "123", "45", "67") into a single box.
        /// </remarks>
        /// <param name="boxes">The boxes to combine. The sequence is enumerated once.</param>
        /// <returns>The bounding box of all boxes in <paramref name="boxes"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="boxes"/> is empty.</exception>
        public static Box UnionAll(IEnumerable<Box> boxes)
        {
            List<Box> list = boxes.ToList();
            if (list.Count == 0)
            {
                throw new ArgumentException("Cannot compute the union of an empty sequence of boxes.", nameof(boxes));
            }
            Box result = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                result = result.Union(list[i]);
            }
            return result;
        }
    }
}
