using System.Runtime.InteropServices;

namespace RBush;

/// <summary>
/// Extension methods for the <see cref="RBush3D{T}"/> object.
/// </summary>
public static class RBush3DExtensions
{
	/// <summary>
	/// Represents an item and its distance from a specified element in the R-tree.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="Item"></param>
	/// <param name="Distance"></param>
	[StructLayout(LayoutKind.Sequential)]
	public record struct ItemDistance<T>(T Item, double Distance);

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
	public static IReadOnlyList<ItemDistance<T1>> Knn<T1, T2>(
		this RBush3D<T1> tree,
		int k,
		T2 element,
		Func<T1, T2, double> func)
		where T1 : ISpatialData3D
		where T2 : ISpatialData3D
	{
		ArgumentNullException.ThrowIfNull(tree);
		ArgumentNullException.ThrowIfNull(element);
		ArgumentNullException.ThrowIfNull(func);

		if (k <= 0)
		{
			return [];
		}

		// minHeap, size = k
		SortedList<double, T1> distances = new(k, s_compareDouble);
		SortedList<double, RBush3D<T1>.Node> queue = new(s_compareDouble)
		{
			{ 0, tree.Root },
		};

		while (queue.Count > 0)
		{
			var item = queue.First().Value;
			queue.RemoveAt(0);
			if (item.IsLeaf)
			{
				var q =
					from x in item.Items
					let e = (T1)x
					let distance = func(e, element)
					orderby distance
					select (distance, e);
				foreach ((var distance, var e) in q)
				{
					if (distances.Count < k)
					{
						distances.Add(distance, e);
					}
					else if (distance < distances.Last().Key)
					{
						distances.RemoveAt(distances.Count - 1);
						distances.Add(distance, e);
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
						queue.Add(min, (RBush3D<T1>.Node)i);
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
		return result == 0 ? 1 : result;
	});

	private static double MinDistance(in Envelope3D e1, in Envelope3D e2)
	{
		if (e1.Intersects(e2))
		{
			return 0;
		}

		var dx = Math.Max(0, Math.Max(e1.MinX - e2.MaxX, e2.MinX - e1.MaxX));
		var dy = Math.Max(0, Math.Max(e1.MinY - e2.MaxY, e2.MinY - e1.MaxY));
		var dz = Math.Max(0, Math.Max(e1.MinZ - e2.MaxZ, e2.MinZ - e1.MaxZ));
		return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
	}
}
