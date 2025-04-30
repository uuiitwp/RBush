using System.Runtime.InteropServices;

namespace RBush;

/// <summary>
/// Extension methods for the <see cref="RBush{T}"/> object.
/// </summary>
public static class RBushExtensions
{
	/// <summary>
	/// Represents an item and its associated distance from a reference point or element.
	/// </summary>
	/// <typeparam name="T">The type of the item being measured.</typeparam>
	/// <param name="Item">The item being measured.</param>
	/// <param name="Distance">The distance of the item from a reference point or element.</param>
	/// <remarks>
	/// This structure is commonly used in nearest neighbor searches, such as KNN (k-nearest neighbors),
	/// to store the result of a spatial query along with the calculated distance.
	/// </remarks>
	[StructLayout(LayoutKind.Sequential)]
	public record struct ItemDistance<T>(T Item, double Distance);

	/// <summary>
	/// Get the <paramref name="k"/> nearest neighbors to a specific point.
	/// </summary>
	/// <typeparam name="T">The type of elements in the index.</typeparam>
	/// <param name="tree">An index of points.</param>
	/// <param name="k">The number of points to retrieve.</param>
	/// <param name="x">The x-coordinate of the center point.</param>
	/// <param name="y">The y-coordinate of the center point.</param>
	/// <param name="maxDistance">The maximum distance of points to be considered "near"; optional.</param>
	/// <param name="predicate">A function to test each element for a condition; optional.</param>
	/// <returns>The list of up to <paramref name="k"/> elements nearest to the given point.</returns>
	public static IReadOnlyList<T> Knn<T>(
		this ISpatialIndex<T> tree,
		int k,
		double x,
		double y,
		double? maxDistance = null,
		Func<T, bool>? predicate = null)
		where T : ISpatialData
	{
		ArgumentNullException.ThrowIfNull(tree);

		var items = maxDistance == null
			? tree.Search()
			: tree.Search(
				new Envelope(
					MinX: x - maxDistance.Value,
					MinY: y - maxDistance.Value,
					MaxX: x + maxDistance.Value,
					MaxY: y + maxDistance.Value));

		var distances = items
			.Select(i => new ItemDistance<T>(i, i.Envelope.DistanceTo(x, y)))
			.OrderBy(i => i.Distance)
			.AsEnumerable();

		if (maxDistance.HasValue)
			distances = distances.TakeWhile(i => i.Distance <= maxDistance.Value);

		if (predicate != null)
			distances = distances.Where(i => predicate(i.Item));

		if (k > 0)
			distances = distances.Take(k);

		return distances
			.Select(i => i.Item)
			.ToList();
	}

	/// <summary>
	/// Calculates the distance from the borders of an <see cref="Envelope"/>
	/// to a given point.
	/// </summary>
	/// <param name="envelope">The <see cref="Envelope"/> from which to find the distance</param>
	/// <param name="x">The x-coordinate of the given point</param>
	/// <param name="y">The y-coordinate of the given point</param>
	/// <returns>The calculated Euclidean shortest distance from the <paramref name="envelope"/> to a given point.</returns>
	public static double DistanceTo(this in Envelope envelope, double x, double y)
	{
		var dX = AxisDistance(x, envelope.MinX, envelope.MaxX);
		var dY = AxisDistance(y, envelope.MinY, envelope.MaxY);
		return Math.Sqrt((dX * dX) + (dY * dY));

		static double AxisDistance(double p, double min, double max) =>
		   p < min ? min - p :
		   p > max ? p - max :
		   0;
	}

	/// <summary>
	/// Retrieves the <paramref name="k"/> nearest neighbors to the specified <paramref name="element"/> 
	/// in the given <paramref name="tree"/> using a custom distance function.
	/// </summary>
	/// <typeparam name="T1">The type of elements stored in the <paramref name="tree"/>.</typeparam>
	/// <typeparam name="T2">The type of the query <paramref name="element"/>.</typeparam>
	/// <param name="tree">An <see cref="RBush{T1}"/> instance containing spatial data.</param>
	/// <param name="k">The number of nearest neighbors to retrieve. Must be greater than 0.</param>
	/// <param name="element">The query element for which the nearest neighbors are to be found.</param>
	/// <param name="func">A function that calculates the distance between an element of type <typeparamref name="T1"/> 
	/// and the query element of type <typeparamref name="T2"/>.</param>
	/// <returns>
	/// A list of up to <paramref name="k"/> nearest neighbors, represented as 
	/// <see cref="ItemDistance{T1}"/> objects, sorted by ascending distance.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="tree"/> or <paramref name="func"/> is <see langword="null"/>.
	/// </exception>
	/// <remarks>
	/// This method uses a priority queue (min-heap) to efficiently find the nearest neighbors.
	/// It traverses the R-tree, pruning branches that cannot contain closer neighbors than 
	/// the current farthest neighbor in the result set.
	/// </remarks>
	public static IReadOnlyList<ItemDistance<T1>> Knn1<T1, T2>(
		this RBush<T1> tree,
		int k,
		T2 element,
		Func<T1, T2, double> func)
		where T1 : ISpatialData
		where T2 : ISpatialData
	{
		ArgumentNullException.ThrowIfNull(tree);
		ArgumentNullException.ThrowIfNull(func);

		if (k <= 0)
		{
			return [];
		}

		// minHeap, size = k
		var distances = new SortedList<double, T1>(k, s_compareDouble);
		var queue = new SortedList<double, RBush<T1>.Node>(s_compareDouble)
		{
			{ MinDistance(in tree.Root.Envelope, in element.Envelope), tree.Root },
		};

		while (queue.Count > 0)
		{
			var item = queue.First().Value;
			queue.RemoveAt(0);
			if (item.IsLeaf)
			{
				var q = from x in item.Items
						let e = (T1)x
						let distance = func(e, element)
						orderby distance
						select new { distance, e };
				foreach (var i in q)
				{
					if (distances.Count < k)
					{
						distances.Add(i.distance, i.e);
					}
					else if (i.distance < distances.Last().Key)
					{
						distances.RemoveAt(distances.Count - 1);
						distances.Add(i.distance, i.e);
					}
				}
			}
			else
			{
				foreach (var i in item.Items)
				{
					var min = MinDistance(in i.Envelope, in element.Envelope);
					if (distances.Count < k || distances.Last().Key > min)
					{
						queue.Add(min, (RBush<T1>.Node)i);
					}
				}
			}
		}

		return distances.Select(x => new ItemDistance<T1>(x.Value, x.Key)).ToList();
	}

	private static readonly IComparer<double> s_compareDouble =
	Comparer<double>.Create((x, y) =>
	{
		var result = x.CompareTo(y);
		return result == 0 ? -1 : result;
	});

	private static double MinDistance(in Envelope e1, in Envelope e2)
	{
		if (e1.Intersects(e2))
		{
			return 0;
		}

		var dx = Math.Max(0, Math.Max(e1.MinX - e2.MaxX, e2.MinX - e1.MaxX));
		var dy = Math.Max(0, Math.Max(e1.MinY - e2.MaxY, e2.MinY - e1.MaxY));
		return Math.Sqrt((dx * dx) + (dy * dy));
	}
}
