using Godot;

/// <summary>
/// Conversion helpers for full-width equirectangular maps. Some source maps
/// omit equal portions of the north and south poles instead of using a 2:1
/// canvas; their latitude coverage can be recovered from the image aspect.
/// </summary>
public static class EquirectangularProjection
{
	public static readonly Vector2 FullLatitudeRange = new(-90.0f, 90.0f);

	public static Vector2 InferLatitudeRange(Vector2I textureSize)
	{
		if (textureSize.X <= 0 || textureSize.Y <= 0)
			return FullLatitudeRange;

		// A full 360 x 180 equirectangular image has a 2:1 aspect ratio.
		// A shorter image is assumed to have been cropped equally at both poles.
		float latitudeSpan = Mathf.Clamp(
			360.0f * textureSize.Y / textureSize.X,
			0.001f,
			180.0f
		);
		return new Vector2(-latitudeSpan * 0.5f, latitudeSpan * 0.5f);
	}

	public static bool IsValidLatitudeRange(Vector2 range)
		=> range.X >= -90.0f && range.Y <= 90.0f && range.Y > range.X;

	public static Vector2 LatLonToUv(
		Vector2 latLon,
		Vector2 angularOffset,
		Vector2 latitudeRange)
	{
		if (!IsValidLatitudeRange(latitudeRange))
			latitudeRange = FullLatitudeRange;

		float latitude = latLon.X + angularOffset.X;
		float longitude = latLon.Y + angularOffset.Y;

		longitude = Mathf.PosMod(longitude + 180.0f, 360.0f) - 180.0f;
		float u = (longitude + 180.0f) / 360.0f;
		float v = (latitudeRange.Y - latitude) /
		          (latitudeRange.Y - latitudeRange.X);

		// Clamping extends the nearest available polar row into a cropped cap.
		return new Vector2(u, Mathf.Clamp(v, 0.0f, 1.0f));
	}

	public static float UvToLatitude(float v, Vector2 latitudeRange)
	{
		if (!IsValidLatitudeRange(latitudeRange))
			latitudeRange = FullLatitudeRange;

		return Mathf.Lerp(latitudeRange.Y, latitudeRange.X, Mathf.Clamp(v, 0.0f, 1.0f));
	}
}
