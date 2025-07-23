using System.Runtime.InteropServices;

namespace RBush;

/// <summary>
/// A bounding envelope, used to identify the bounds of of the points within
/// a particular node.
/// </summary>
/// <param name="MinX">The minimum X value of the bounding box.</param>
/// <param name="MinY">The minimum Y value of the bounding box.</param>
/// <param name="MinZ">The minimum Z value of the bounding box.</param>
/// <param name="MaxX">The maximum X value of the bounding box.</param>
/// <param name="MaxY">The maximum Y value of the bounding box.</param>
/// <param name="MaxZ">The maximum Z value of the bounding box.</param>
[StructLayout(LayoutKind.Sequential)]
public record struct Envelope3D(
	double MinX,
	double MinY,
	double MinZ,
	double MaxX,
	double MaxY,
	double MaxZ)
{
	/// <summary>
	/// The calculated volume of the bounding box.
	/// </summary>
	public readonly double Volume =>
		Math.Max(MaxX - MinX, 0) *
		Math.Max(MaxY - MinY, 0) *
		Math.Max(MaxZ - MinZ, 0);

	/// <summary>
	/// Half of the linear perimeter of the bounding box
	/// </summary>
	public readonly double Margin =>
		Math.Max(MaxX - MinX, 0) +
		Math.Max(MaxY - MinY, 0) +
		Math.Max(MaxZ - MinZ, 0);

	/// <summary>
	/// Extends a bounding box to include another bounding box
	/// </summary>
	/// <param name="other">The other bounding box</param>
	/// <returns>A new bounding box that encloses both bounding boxes.</returns>
	/// <remarks>Does not affect the current bounding box.</remarks>
	public readonly Envelope3D Extend(in Envelope3D other) =>
		new(
			MinX: Math.Min(MinX, other.MinX),
			MinY: Math.Min(MinY, other.MinY),
			MinZ: Math.Min(MinZ, other.MinZ),
			MaxX: Math.Max(MaxX, other.MaxX),
			MaxY: Math.Max(MaxY, other.MaxY),
			MaxZ: Math.Max(MaxZ, other.MaxZ));

	/// <summary>
	/// Intersects a bounding box to only include the common volume
	/// of both bounding boxes
	/// </summary>
	/// <param name="other">The other bounding box</param>
	/// <returns>A new bounding box that is the intersection of both bounding boxes.</returns>
	/// <remarks>Does not affect the current bounding box.</remarks>
	public readonly Envelope3D Intersection(in Envelope3D other) =>
		new(
			MinX: Math.Max(MinX, other.MinX),
			MinY: Math.Max(MinY, other.MinY),
			MinZ: Math.Max(MinZ, other.MinZ),
			MaxX: Math.Min(MaxX, other.MaxX),
			MaxY: Math.Min(MaxY, other.MaxY),
			MaxZ: Math.Min(MaxZ, other.MaxZ));

	/// <summary>
	/// Determines whether <paramref name="other"/> is contained
	/// within this bounding box.
	/// </summary>
	/// <param name="other">The other bounding box</param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="other"/> is
	/// completely contained within this bounding box; 
	/// <see langword="false" /> otherwise.
	/// </returns>
	public readonly bool Contains(in Envelope3D other) =>
		MinX <= other.MinX &&
		MinY <= other.MinY &&
		MinZ <= other.MinZ &&
		MaxX >= other.MaxX &&
		MaxY >= other.MaxY &&
		MaxZ >= other.MaxZ;

	/// <summary>
	/// Determines whether <paramref name="other"/> intersects
	/// this bounding box.
	/// </summary>
	/// <param name="other">The other bounding box</param>
	/// <returns>
	/// <see langword="true" /> if <paramref name="other"/> is
	/// intersects this bounding box in any way; 
	/// <see langword="false" /> otherwise.
	/// </returns>
	public readonly bool Intersects(in Envelope3D other) =>
		MinX <= other.MaxX &&
		MinY <= other.MaxY &&
		MinZ <= other.MaxZ &&
		MaxX >= other.MinX &&
		MaxY >= other.MinY &&
		MaxZ >= other.MinZ;

	/// <summary>
	/// Determines whether a point is contained
	/// </summary>
	/// <param name="ptX"></param>
	/// <param name="ptY"></param>
	/// <param name="ptZ"></param>
	/// <returns></returns>
	public readonly bool Intersects(double ptX, double ptY, double ptZ) =>
		MinX <= ptX &&
		MinY <= ptY &&
		MinZ <= ptZ &&
		MaxX >= ptX &&
		MaxY >= ptY &&
		MaxZ >= ptZ;

	/// <summary>
	/// A bounding box that contains the entire 3-d space.
	/// </summary>
	public static Envelope3D InfiniteBounds { get; } =
		new(
			MinX: double.NegativeInfinity,
			MinY: double.NegativeInfinity,
			MinZ: double.NegativeInfinity,
			MaxX: double.PositiveInfinity,
			MaxY: double.PositiveInfinity,
			MaxZ: double.PositiveInfinity);

	/// <summary>
	/// An empty bounding box.
	/// </summary>
	public static Envelope3D EmptyBounds { get; } =
		new(
			MinX: double.PositiveInfinity,
			MinY: double.PositiveInfinity,
			MinZ: double.PositiveInfinity,
			MaxX: double.NegativeInfinity,
			MaxY: double.NegativeInfinity,
			MaxZ: double.NegativeInfinity);
}
