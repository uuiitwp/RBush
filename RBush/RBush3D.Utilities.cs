using System.Collections;

namespace RBush;

public partial class RBush3D<T> : IEnumerable<T>
{
	private static readonly IComparer<ISpatialData3D> s_compareMinX =
		Comparer<ISpatialData3D>.Create((x, y) => Comparer<double>.Default.Compare(x.Envelope.MinX, y.Envelope.MinX));
	private static readonly IComparer<ISpatialData3D> s_compareMinY =
		Comparer<ISpatialData3D>.Create((x, y) => Comparer<double>.Default.Compare(x.Envelope.MinY, y.Envelope.MinY));
	private static readonly IComparer<ISpatialData3D> s_compareMinZ =
		Comparer<ISpatialData3D>.Create((x, y) => Comparer<double>.Default.Compare(x.Envelope.MinZ, y.Envelope.MinZ));

	private List<T> DoSearch(in Envelope3D boundingBox)
	{
		if (!Root.Envelope.Intersects(boundingBox))
		{
			return [];
		}

		List<T> intersections = [];
		Queue<Node> queue = new();
		queue.Enqueue(Root);

		while (queue.Count != 0)
		{
			var item = queue.Dequeue();

			if (item.IsLeaf)
			{
				foreach (var i in item.Items)
				{
					if (i.Envelope.Intersects(boundingBox))
					{
						intersections.Add((T)i);
					}
				}
			}
			else
			{
				foreach (var i in item.Items)
				{
					if (i.Envelope.Intersects(boundingBox))
					{
						queue.Enqueue((Node)i);
					}
				}
			}
		}

		return intersections;
	}

	private List<Node> FindCoveringVolume(in Envelope3D volume, int depth)
	{
		List<Node> path = [];
		var node = Root;

		while (true)
		{
			path.Add(node);
			if (node.IsLeaf || path.Count == depth)
			{
				return path;
			}

			var next = node.Items[0];
			var nextVolume = next.Envelope.Extend(volume).Volume;

			foreach (var i in node.Items)
			{
				var newVolume = i.Envelope.Extend(volume).Volume;
				if (newVolume > nextVolume)
				{
					continue;
				}

				if (newVolume == nextVolume
					&& i.Envelope.Volume >= next.Envelope.Volume)
				{
					continue;
				}

				next = i;
				nextVolume = newVolume;
			}

			node = (next as Node)!;
		}
	}

	private void Insert(ISpatialData3D data, int depth)
	{
		var path = FindCoveringVolume(data.Envelope, depth);

		var insertNode = path[^1];
		insertNode.Add(data);

		while (--depth >= 0)
		{
			if (path[depth].Items.Count > _maxEntries)
			{
				var newNode = SplitNode(path[depth]);
				if (depth == 0)
				{
					SplitRoot(newNode);
				}
				else
				{
					path[depth - 1].Add(newNode);
				}
			}
			else
			{
				path[depth].ResetEnvelope();
			}
		}
	}

	private void SplitRoot(Node newNode) =>
		Root = new Node([Root, newNode], Root.Height + 1);

	private Node SplitNode(Node node)
	{
		SortChildren(node);

		var splitPoint = GetBestSplitIndex(node.Items);
		var newChildren = node.Items.Skip(splitPoint).ToList();
		node.RemoveRange(splitPoint, node.Items.Count - splitPoint);
		return new Node(newChildren, node.Height);
	}

	private void SortChildren(Node node)
	{
		node.Items.Sort(s_compareMinX);
		var splitsByX = GetPotentialSplitMargins(node.Items);
		node.Items.Sort(s_compareMinY);
		var splitsByY = GetPotentialSplitMargins(node.Items);
		node.Items.Sort(s_compareMinZ);
		var splitsByZ = GetPotentialSplitMargins(node.Items);

		if (splitsByX < splitsByY && splitsByX < splitsByZ)
		{
			node.Items.Sort(s_compareMinX);
		}
		else if (splitsByY < splitsByX && splitsByY < splitsByZ)
		{
			node.Items.Sort(s_compareMinY);
		}
	}

	private double GetPotentialSplitMargins(List<ISpatialData3D> children) =>
		GetPotentialEnclosingMargins(children) +
		GetPotentialEnclosingMargins(children.AsEnumerable().Reverse().ToList());

	private double GetPotentialEnclosingMargins(List<ISpatialData3D> children)
	{
		var envelope = Envelope3D.EmptyBounds;
		var i = 0;
		for (; i < _minEntries; i++)
		{
			envelope = envelope.Extend(children[i].Envelope);
		}

		var totalMargin = envelope.Margin;
		for (; i < children.Count - _minEntries; i++)
		{
			envelope = envelope.Extend(children[i].Envelope);
			totalMargin += envelope.Margin;
		}

		return totalMargin;
	}

	private int GetBestSplitIndex(List<ISpatialData3D> children)
	{
		return Enumerable.Range(_minEntries, children.Count - _minEntries)
			.Select(i =>
			{
				var leftEnvelope = GetEnclosingEnvelope(children.Take(i));
				var rightEnvelope = GetEnclosingEnvelope(children.Skip(i));

				var overlap = leftEnvelope.Intersection(rightEnvelope).Volume;
				var totalArea = leftEnvelope.Volume + rightEnvelope.Volume;
				return new { i, overlap, totalArea };
			})
			.OrderBy(x => x.overlap)
			.ThenBy(x => x.totalArea)
			.Select(x => x.i)
			.First();
	}
	private Node BuildTree(T[] data)
	{
		var treeHeight = GetDepth(data.Length);
		var rootMaxEntries = (int)Math.Ceiling(data.Length / Math.Pow(_maxEntries, treeHeight - 1));
		return BuildNodes(new ArraySegment<T>(data), treeHeight, rootMaxEntries);
	}

	private int GetDepth(int numNodes) =>
		(int)Math.Ceiling(Math.Log(numNodes) / Math.Log(_maxEntries));

	private Node BuildNodes(ArraySegment<T> data, int height, int maxEntries)
	{
		if (data.Count <= maxEntries)
		{
			return height == 1
				? new Node(data.Cast<ISpatialData3D>().ToList(), height)
				: new Node(
					[
						BuildNodes(data, height - 1, _maxEntries),
					],
					height);
		}

		// after much testing, this is faster than using Array.Sort() on the provided array
		// in spite of the additional memory cost and copying. go figure!

		ArraySegment<T> byX = new(data.OrderBy(i => i.Envelope.MinX).ToArray());

		var n1 = (data.Count + (maxEntries - 1)) / maxEntries;
		var n2 = n1 * (int)Math.Ceiling(Math.Pow(maxEntries, 2.0 / 3.0));
		var n3 = n1 * (int)Math.Ceiling(Math.Pow(maxEntries, 1.0 / 3.0));

		List<ISpatialData3D> children = new(maxEntries);
		foreach (var subData in Chunk(byX, n3))
		{
			ArraySegment<T> byY = new(subData.OrderBy(d => d.Envelope.MinY).ToArray());

			foreach (var nodeData in Chunk(byY, n2))
			{
				ArraySegment<T> byZ = new(nodeData.OrderBy(d => d.Envelope.MinZ).ToArray());

				foreach (var subNodeData in Chunk(byZ, n1))
				{
					children.Add(BuildNodes(subNodeData, height - 1, _maxEntries));
				}
			}
		}

		return new Node(children, height);
	}

	private static IEnumerable<ArraySegment<T>> Chunk(ArraySegment<T> values, int chunkSize)
	{
		var start = 0;
		while (start < values.Count)
		{
			var len = Math.Min(values.Count - start, chunkSize);
			yield return new ArraySegment<T>(values.Array!, values.Offset + start, len);
			start += chunkSize;
		}
	}

	private static Envelope3D GetEnclosingEnvelope(IEnumerable<ISpatialData3D> items)
	{
		var envelope = Envelope3D.EmptyBounds;
		foreach (var data in items)
		{
			envelope = envelope.Extend(data.Envelope);
		}

		return envelope;
	}

	private static List<T> GetAllChildren(List<T> list, Node n)
	{
		if (n.IsLeaf)
		{
			list.AddRange(n.Items.Cast<T>());
		}
		else
		{
			foreach (var node in n.Items.Cast<Node>())
			{
				_ = GetAllChildren(list, node);
			}
		}

		return list;
	}

	/// <summary>
	/// Returns an enumerator that iterates through the collection.
	/// </summary>
	/// <returns></returns>
	public IEnumerator<T> GetEnumerator()
	{
		Queue<Node> queue = new();
		queue.Enqueue(Root);
		while (queue.Count > 0)
		{
			var item = queue.Dequeue();

			if (item.IsLeaf)
			{
				foreach (var i in item.Items)
				{
					yield return (T)i;
				}
			}
			else
			{
				foreach (var i in item.Items)
				{
					queue.Enqueue((Node)i);
				}
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
