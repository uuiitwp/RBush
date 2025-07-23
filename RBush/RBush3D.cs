using System.Diagnostics.CodeAnalysis;

namespace RBush;

/// <summary>
/// An implementation of the R-tree data structure for 3-d spatial indexing.
/// </summary>
/// <typeparam name="T">The type of elements in the index.</typeparam>
public partial class RBush3D<T> : ISpatialDatabase3D<T>, ISpatialIndex3D<T> where T : ISpatialData3D
{
	private const int DefaultMaxEntries = 16;
	private const int MinimumMaxEntries = 8;
	private const int MinimumMinEntries = 4;
	private const double DefaultFillFactor = 0.4;

	private readonly IEqualityComparer<T> _comparer;
	private readonly int _maxEntries;
	private readonly int _minEntries;

	/// <summary>
	/// The root of the R-tree.
	/// </summary>
	public Node Root { get; private set; }

	/// <summary>
	/// The bounding box of all elements currently in the data structure.
	/// </summary>
	public ref readonly Envelope3D Envelope => ref Root.Envelope;

	/// <summary>
	/// Initializes a new instance of the <see cref="RBush3D{T}"/> that is
	/// empty and has the default tree width and default <see cref="IEqualityComparer{T}"/>.
	/// </summary>
	public RBush3D()
		: this(DefaultMaxEntries, EqualityComparer<T>.Default) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="RBush3D{T}"/> that is
	/// empty and has a custom max number of elements per tree node
	/// and default <see cref="IEqualityComparer{T}"/>.
	/// </summary>
	/// <param name="maxEntries"></param>
	public RBush3D(int maxEntries)
		: this(maxEntries, EqualityComparer<T>.Default) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="RBush3D{T}"/> that is
	/// empty and has a custom max number of elements per tree node
	/// and a custom <see cref="IEqualityComparer{T}"/>.
	/// </summary>
	/// <param name="maxEntries"></param>
	/// <param name="comparer"></param>
	public RBush3D(int maxEntries, IEqualityComparer<T> comparer)
	{
		_comparer = comparer;
		_maxEntries = Math.Max(MinimumMaxEntries, maxEntries);
		_minEntries = Math.Max(MinimumMinEntries, (int)Math.Ceiling(_maxEntries * DefaultFillFactor));

		Clear();
	}

	private RBush3D(int maxEntries, int minEntries, int count, Node root, IEqualityComparer<T> comparer)
	{
		_maxEntries = maxEntries;
		_minEntries = minEntries;
		Count = count;
		Root = root;
		_comparer = comparer;
	}

	/// <summary>
	/// Gets the number of items currently stored in the <see cref="RBush3D{T}"/>
	/// </summary>
	public int Count { get; private set; }

	/// <summary>
	/// Removes all elements from the <see cref="RBush3D{T}"/>.
	/// </summary>
	[MemberNotNull(nameof(Root))]
	public void Clear()
	{
		Root = new Node([], 1);
		Count = 0;
	}

	/// <summary>
	/// Get all of the elements within the current <see cref="RBush3D{T}"/>.
	/// </summary>
	/// <returns>
	/// A list of every element contained in the <see cref="RBush3D{T}"/>.
	/// </returns>
	public IReadOnlyList<T> Search() =>
		GetAllChildren([], Root);

	/// <summary>
	/// Get all of the elements from this <see cref="RBush3D{T}"/>
	/// within the <paramref name="boundingBox"/> bounding box.
	/// </summary>
	/// <param name="boundingBox">The area for which to find elements.</param>
	/// <returns>
	/// A list of the points that are within the bounding box
	/// from this <see cref="RBush3D{T}"/>.
	/// </returns>
	public IReadOnlyList<T> Search(in Envelope3D boundingBox) => DoSearch(boundingBox);

	/// <summary>
	/// Adds an object to the <see cref="RBush3D{T}"/>
	/// </summary>
	/// <param name="item">
	/// The object to be added to <see cref="RBush3D{T}"/>.
	/// </param>
	public void Insert(T item)
	{
		Insert(item, Root.Height);
		Count++;
	}

	/// <summary>
	/// Adds all of the elements from the collection to the <see cref="RBush3D{T}"/>.
	/// </summary>
	/// <param name="items">
	/// A collection of items to add to the <see cref="RBush3D{T}"/>.
	/// </param>
	/// <remarks>
	/// For multiple items, this method is more performant than 
	/// adding items individually via <see cref="Insert(T)"/>.
	/// </remarks>
	public void BulkLoad(IEnumerable<T> items)
	{
		var data = items.ToArray();
		if (data.Length == 0)
		{
			return;
		}

		if (Root.IsLeaf &&
			Root.Items.Count + data.Length < _maxEntries)
		{
			foreach (var i in data)
			{
				Insert(i);
			}

			return;
		}

		if (data.Length < _minEntries)
		{
			foreach (var i in data)
			{
				Insert(i);
			}

			return;
		}

		var dataRoot = BuildTree(data);
		Count += data.Length;

		if (Root.Items.Count == 0)
		{
			Root = dataRoot;
		}
		else if (Root.Height == dataRoot.Height)
		{
			if (Root.Items.Count + dataRoot.Items.Count <= _maxEntries)
			{
				foreach (var isd in dataRoot.Items)
				{
					Root.Add(isd);
				}
			}
			else
			{
				SplitRoot(dataRoot);
			}
		}
		else
		{
			if (Root.Height < dataRoot.Height)
			{
				(dataRoot, Root) = (Root, dataRoot);
			}

			Insert(dataRoot, Root.Height - dataRoot.Height);
		}
	}

	/// <summary>
	/// Removes an object from the <see cref="RBush3D{T}"/>.
	/// </summary>
	/// <param name="item">
	/// The object to be removed from the <see cref="RBush3D{T}"/>.
	/// </param>
	/// <returns><see langword="bool" /> indicating whether the item was deleted.</returns>
	public bool Delete(T item) =>
		DoDelete(Root, item);

	private bool DoDelete(Node node, T item)
	{
		if (!node.Envelope.Contains(item.Envelope))
		{
			return false;
		}

		if (node.IsLeaf)
		{
			var cnt = node.Items.RemoveAll(i => _comparer.Equals((T)i, item));
			if (cnt == 0)
			{
				return false;
			}

			Count -= cnt;
			node.ResetEnvelope();
			return true;

		}

		var flag = false;
		foreach (var n in node.Items)
		{
			flag |= DoDelete((Node)n, item);
		}

		if (flag)
		{
			node.ResetEnvelope();
		}

		return flag;
	}

	/// <summary>
	/// Creates a copy of the current <see cref="RBush3D{T}"/> instance.
	/// </summary>
	/// <returns></returns>
	public RBush3D<T> Clone()
	{
		Queue<Node> queue = new();
		Dictionary<Node, Node> map = [];
		queue.Enqueue(Root);

		while (queue.Count != 0)
		{
			var old = queue.Dequeue();
			map[old] = new(new(old.Items.Count), old.Height, old.Envelope);

			if (!old.IsLeaf)
			{
				foreach (var item in old.Items.Cast<Node>())
				{
					queue.Enqueue(item);
				}
			}
		}

		foreach (var kv in map)
		{
			if (kv.Key.IsLeaf)
			{
				foreach (var item in kv.Key.Items)
				{
					kv.Value.Items.Add(item);
				}
			}
			else
			{
				foreach (var item in kv.Key.Items.Cast<Node>())
				{
					kv.Value.Items.Add(map[item]);
				}
			}
		}

		RBush3D<T> result = new(_maxEntries, _minEntries, Count, map[Root], _comparer);
		return result;
	}
}
