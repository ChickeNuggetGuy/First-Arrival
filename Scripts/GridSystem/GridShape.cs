using Godot;
using Godot.Collections;
using System.Collections.Generic;
using FirstArrival.Scripts.Utility;

[GlobalClass, Tool]
public partial class GridShape : Resource
{
	private int _sizeX = 1;
	private int _sizeY = 1;
	private int _sizeZ = 1;

	[Export]
	public int SizeX
	{
		get => _sizeX;
		set => ResizeAxis(ref _sizeX, value, Axis.X);
	}

	[Export]
	public int SizeY
	{
		get => _sizeY;
		set => ResizeAxis(ref _sizeY, value, Axis.Y);
	}

	[Export]
	public int SizeZ
	{
		get => _sizeZ;
		set => ResizeAxis(ref _sizeZ, value, Axis.Z);
	}

	[Export] public Vector3I PivotCell { get; set; } = Vector3I.Zero;
	[Export] public Array<bool> OccupiedCells { get; set; } = new Array<bool> { true };

	private enum Axis
	{
		X,
		Y,
		Z
	}

	public GridShape()
	{
		EnsureValidArray();
	}

	private void ResizeAxis(ref int field, int newValue, Axis axis)
	{
		newValue = Mathf.Max(1, newValue);
		if (field == newValue) return;

		int oldX = _sizeX, oldY = _sizeY, oldZ = _sizeZ;
		field = newValue;

		RebuildArray(oldX, oldY, oldZ);
		ClampPivot();
		NotifyChanged();
	}

	private void RebuildArray(int oldX, int oldY, int oldZ)
	{
		int newSize = _sizeX * _sizeY * _sizeZ;
		var newArray = new Array<bool>();
		newArray.Resize(newSize);
		newArray.Fill(false);

		if (OccupiedCells != null && OccupiedCells.Count > 0)
		{
			int copyX = Mathf.Min(oldX, _sizeX);
			int copyY = Mathf.Min(oldY, _sizeY);
			int copyZ = Mathf.Min(oldZ, _sizeZ);

			for (int y = 0; y < copyY; y++)
			{
				for (int z = 0; z < copyZ; z++)
				{
					for (int x = 0; x < copyX; x++)
					{
						int oldIdx = CoordToIndex(x, y, z, oldX, oldZ);
						int newIdx = CoordToIndex(x, y, z, _sizeX, _sizeZ);

						if (oldIdx < OccupiedCells.Count && newIdx < newSize)
							newArray[newIdx] = OccupiedCells[oldIdx];
					}
				}
			}
		}

		OccupiedCells = newArray;
	}

	private void ClampPivot()
	{
		PivotCell = new Vector3I(
			Mathf.Clamp(PivotCell.X, 0, _sizeX - 1),
			Mathf.Clamp(PivotCell.Y, 0, _sizeY - 1),
			Mathf.Clamp(PivotCell.Z, 0, _sizeZ - 1)
		);
	}

	private void EnsureValidArray()
	{
		int expectedSize = _sizeX * _sizeY * _sizeZ;
		if (OccupiedCells == null || OccupiedCells.Count != expectedSize)
		{
			OccupiedCells = new Array<bool>();
			OccupiedCells.Resize(expectedSize);
			OccupiedCells.Fill(false);
			if (expectedSize > 0) OccupiedCells[0] = true;
		}
	}

	private static int CoordToIndex(int x, int y, int z, int sizeX, int sizeZ)
	{
		return x + (z * sizeX) + (y * sizeX * sizeZ);
	}

	public bool IsOccupied(int x, int y, int z)
	{
		if (!IsValidLocal(x, y, z)) return false;
		int idx = CoordToIndex(x, y, z, _sizeX, _sizeZ);
		return idx < OccupiedCells.Count && OccupiedCells[idx];
	}

	public void SetOccupied(int x, int y, int z, bool value)
	{
		if (!IsValidLocal(x, y, z)) return;
		int idx = CoordToIndex(x, y, z, _sizeX, _sizeZ);
		if (idx < OccupiedCells.Count && OccupiedCells[idx] != value)
		{
			OccupiedCells[idx] = value;
			NotifyChanged();
		}
	}

	public bool IsValidLocal(int x, int y, int z)
	{
		return x >= 0 && x < _sizeX &&
		       y >= 0 && y < _sizeY &&
		       z >= 0 && z < _sizeZ;
	}

	/// <summary>
	/// Returns all occupied cells as world grid coordinates relative to anchor.
	/// No rotation applied - shape is already in world orientation from collision calculation.
	/// </summary>
	public List<Vector3I> GetWorldCoordinates(Vector3I anchorGridPos)
	{
		var results = new List<Vector3I>();

		for (int y = 0; y < _sizeY; y++)
		{
			for (int z = 0; z < _sizeZ; z++)
			{
				for (int x = 0; x < _sizeX; x++)
				{
					if (!IsOccupied(x, y, z)) continue;

					int relX = x - PivotCell.X;
					int relY = y - PivotCell.Y;
					int relZ = z - PivotCell.Z;

					results.Add(new Vector3I(
						anchorGridPos.X + relX,
						anchorGridPos.Y + relY,
						anchorGridPos.Z + relZ
					));
				}
			}
		}

		return results;
	}

	public IEnumerable<Vector3I> GetOccupiedLocalCells()
	{
		for (int y = 0; y < _sizeY; y++)
		{
			for (int z = 0; z < _sizeZ; z++)
			{
				for (int x = 0; x < _sizeX; x++)
				{
					if (IsOccupied(x, y, z))
						yield return new Vector3I(x, y, z);
				}
			}
		}
	}

	public void FillAll(bool value)
	{
		bool changed = false;
		for (int i = 0; i < OccupiedCells.Count; i++)
		{
			if (OccupiedCells[i] != value)
			{
				OccupiedCells[i] = value;
				changed = true;
			}
		}

		if (changed) NotifyChanged();
	}

	private void NotifyChanged()
	{
		if (Engine.IsEditorHint())
		{
			NotifyPropertyListChanged();
			EmitChanged();
		}
	}

	#region Static Factory

	public static GridShape CreateFromCollisionBounds(
		Node3D root,
		GridConfiguration config,
		Vector3 pivotWorldCenter,
		bool recursive = true
	)
	{
		if (root == null || config == null)
		{
			var fallback = new GridShape();
			fallback._sizeX = 1;
			fallback._sizeY = 1;
			fallback._sizeZ = 1;
			fallback.PivotCell = Vector3I.Zero;
			fallback.OccupiedCells[0] = true;
			return fallback;
		}

		var pivotCoords = config.WorldToGrid(pivotWorldCenter);
		Vector3 pivotCenter = config.GridToWorld(pivotCoords, cellCenter: true);

		if (
			!root.TryGetCollisionShapes(
				out var colliders,
				recursive,
				includeDisabled: false
			) || colliders.Count == 0
		)
		{
			var fallback = new GridShape();
			fallback._sizeX = 1;
			fallback._sizeY = 1;
			fallback._sizeZ = 1;
			fallback.PivotCell = Vector3I.Zero;
			fallback.OccupiedCells[0] = true;
			return fallback;
		}

		Vector3 cellSize = config.CellSize;
		float halfX = cellSize.X * 0.5f;
		float halfY = cellSize.Y * 0.5f;
		float halfZ = cellSize.Z * 0.5f;

		float minCellDim = Mathf.Min(cellSize.X, Mathf.Min(cellSize.Y, cellSize.Z));
		float eps = Mathf.Max(0.001f, minCellDim * 0.01f);

		static Aabb ShrinkSafe(Aabb aabb, float e)
		{
			Vector3 shrink = new Vector3(e * 2f, e * 2f, e * 2f);
			Vector3 newSize = aabb.Size - shrink;
			newSize.X = Mathf.Max(0f, newSize.X);
			newSize.Y = Mathf.Max(0f, newSize.Y);
			newSize.Z = Mathf.Max(0f, newSize.Z);
			return new Aabb(aabb.Position + new Vector3(e, e, e), newSize);
		}

		var colliderAabbs = new List<Aabb>(colliders.Count);
		bool first = true;
		Aabb combined = default;

		for (int i = 0; i < colliders.Count; i++)
		{
			var cs = colliders[i];
			if (cs == null || cs.Shape == null || cs.Disabled) continue;

			var aabb = ShrinkSafe(GetWorldAabb(cs), eps);
			colliderAabbs.Add(aabb);
			combined = first ? aabb : combined.Merge(aabb);
			first = false;
		}

		if (colliderAabbs.Count == 0)
		{
			var fallback = new GridShape();
			fallback._sizeX = 1;
			fallback._sizeY = 1;
			fallback._sizeZ = 1;
			fallback.PivotCell = Vector3I.Zero;
			fallback.OccupiedCells[0] = true;
			return fallback;
		}

		Vector3 relMin = combined.Position - pivotCenter;
		Vector3 relMax = combined.End - pivotCenter;

		int minX = Mathf.FloorToInt((relMin.X + halfX + eps) / cellSize.X);
		int minY = Mathf.FloorToInt((relMin.Y + halfY + eps) / cellSize.Y);
		int minZ = Mathf.FloorToInt((relMin.Z + halfZ + eps) / cellSize.Z);

		int maxX = Mathf.FloorToInt((relMax.X + halfX - eps) / cellSize.X);
		int maxY = Mathf.FloorToInt((relMax.Y + halfY - eps) / cellSize.Y);
		int maxZ = Mathf.FloorToInt((relMax.Z + halfZ - eps) / cellSize.Z);

		int sizeX = Mathf.Max(1, maxX - minX + 1);
		int sizeY = Mathf.Max(1, maxY - minY + 1);
		int sizeZ = Mathf.Max(1, maxZ - minZ + 1);

		var result = new GridShape
		{
			_sizeX = sizeX,
			_sizeY = sizeY,
			_sizeZ = sizeZ,
			PivotCell = new Vector3I(
				Mathf.Clamp(-minX, 0, sizeX - 1),
				Mathf.Clamp(-minY, 0, sizeY - 1),
				Mathf.Clamp(-minZ, 0, sizeZ - 1)
			)
		};

		int total = sizeX * sizeY * sizeZ;
		result.OccupiedCells = new Godot.Collections.Array<bool>();
		result.OccupiedCells.Resize(total);

		Vector3 half = new Vector3(halfX, halfY, halfZ);

		for (int y = 0; y < sizeY; y++)
		{
			for (int z = 0; z < sizeZ; z++)
			{
				for (int x = 0; x < sizeX; x++)
				{
					Vector3 cellCenterWorld = pivotCenter + new Vector3(
						(minX + x) * cellSize.X,
						(minY + y) * cellSize.Y,
						(minZ + z) * cellSize.Z
					);

					var cellAabb = ShrinkSafe(
						new Aabb(cellCenterWorld - half, cellSize),
						eps
					);

					bool occupied = false;
					for (int i = 0; i < colliderAabbs.Count; i++)
					{
						if (cellAabb.Intersects(colliderAabbs[i]))
						{
							occupied = true;
							break;
						}
					}

					int idx = CoordToIndex(x, y, z, sizeX, sizeZ);
					result.OccupiedCells[idx] = occupied;
				}
			}
		}

		int pivotIdx = CoordToIndex(
			result.PivotCell.X,
			result.PivotCell.Y,
			result.PivotCell.Z,
			sizeX,
			sizeZ
		);
		if (pivotIdx >= 0 && pivotIdx < result.OccupiedCells.Count)
			result.OccupiedCells[pivotIdx] = true;

		return result;
	}

	private static Aabb GetCombinedWorldAabb(List<CollisionShape3D> colliders)
	{
		bool first = true;
		Aabb combined = default;

		foreach (var cs in colliders)
		{
			Aabb worldAabb = GetWorldAabb(cs);
			combined = first ? worldAabb : combined.Merge(worldAabb);
			first = false;
		}

		return combined;
	}

	private static Aabb GetWorldAabb(CollisionShape3D cs)
	{
		Aabb local = GetShapeLocalAabb(cs.Shape);
		return TransformAabb(cs.GlobalTransform, local);
	}

	private static Aabb GetShapeLocalAabb(Shape3D shape)
	{
		switch (shape)
		{
			case BoxShape3D box:
				return new Aabb(-box.Size * 0.5f, box.Size);
			case SphereShape3D sphere:
				Vector3 ext = Vector3.One * sphere.Radius;
				return new Aabb(-ext, ext * 2f);
			case CapsuleShape3D cap:
				Vector3 capExt = new Vector3(cap.Radius, cap.Height * 0.5f, cap.Radius);
				return new Aabb(-capExt, capExt * 2f);
			case CylinderShape3D cyl:
				Vector3 cylExt = new Vector3(cyl.Radius, cyl.Height * 0.5f, cyl.Radius);
				return new Aabb(-cylExt, cylExt * 2f);
			default:
				var mesh = shape.GetDebugMesh();
				return mesh?.GetAabb() ?? new Aabb(Vector3.Zero, Vector3.One);
		}
	}

	private static Aabb TransformAabb(Transform3D xf, Aabb local)
	{
		Vector3 min = local.Position;
		Vector3 max = local.End;

		Vector3 newMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 newMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

		for (int xi = 0; xi < 2; xi++)
		{
			for (int yi = 0; yi < 2; yi++)
			{
				for (int zi = 0; zi < 2; zi++)
				{
					Vector3 corner = xf * new Vector3(
						xi == 0 ? min.X : max.X,
						yi == 0 ? min.Y : max.Y,
						zi == 0 ? min.Z : max.Z
					);
					newMin = newMin.Min(corner);
					newMax = newMax.Max(corner);
				}
			}
		}

		return new Aabb(newMin, newMax - newMin);
	}

	#endregion
}