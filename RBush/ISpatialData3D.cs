namespace RBush;

/// <summary>
/// Exposes an <see cref="Envelope3D"/> that describes the
/// bounding box of current object.
/// </summary>
public interface ISpatialData3D
{
	/// <summary>
	/// The bounding box of the current object.
	/// </summary>
	ref readonly Envelope3D Envelope { get; }
}
