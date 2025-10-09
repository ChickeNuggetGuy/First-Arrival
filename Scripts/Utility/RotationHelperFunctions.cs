using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstArrival.Scripts.Utility
{
  public static class RotationHelperFunctions
  {
    // Godot default: when rotation.y == 0, models face -Z. Use 180 so that
    // DirectionAngles maps directions to the correct Node3D.Rotation.Y.
    public const float ModelForwardYawOffsetDeg = 180f;

    // Ordered clockwise starting at North (-Z)
    private static readonly Enums.Direction[] Ordered8 =
    {
      Enums.Direction.North, Enums.Direction.NorthEast, Enums.Direction.East,
      Enums.Direction.SouthEast, Enums.Direction.South, Enums.Direction.SouthWest,
      Enums.Direction.West, Enums.Direction.NorthWest
    };

    private static readonly IReadOnlyDictionary<Enums.Direction, int>
      DirectionIndices = Ordered8
        .Select((dir, index) => new { dir, index })
        .ToDictionary(x => x.dir, x => x.index);

    // Yaw (in degrees) you can assign to Node3D.Rotation.Y to face each dir.
    public static readonly IReadOnlyDictionary<Enums.Direction, float>
      DirectionAngles = BuildDirectionAngles();

    private static IReadOnlyDictionary<Enums.Direction, float>
      BuildDirectionAngles()
    {
      var dict = new Dictionary<Enums.Direction, float>(8);
      foreach (var d in Ordered8)
        dict[d] = GetNodeYawDegForDirection(d);
      return dict;
    }

    private static float NormalizeDeg(float deg)
    {
      deg %= 360f;
      if (deg < 0f) deg += 360f;
      return deg;
    }

    private static int Mod8(int x) => ((x % 8) + 8) % 8;

    // Convert a world-space forward vector to "world yaw" degrees (0° = +Z).
    private static float VectorToYawWorldDeg(Vector3 v)
    {
      return NormalizeDeg(Mathf.RadToDeg(Mathf.Atan2(v.X, v.Z)));
    }

    // Node yaw (deg) to face the given direction with -Z model forward.
    private static float GetNodeYawDegForDirection(Enums.Direction dir)
    {
      Vector3 v = GetWorldVector3FromDirection(dir);
      if (v == Vector3.Zero) return 0f;

      float yawWorldDeg = VectorToYawWorldDeg(v);
      return NormalizeDeg(yawWorldDeg + ModelForwardYawOffsetDeg);
    }

    // Returns rotation.y (radians) to face the given compass direction.
    public static float GetRotationRadians(Enums.Direction dir)
    {
      if (!DirectionAngles.TryGetValue(dir, out float deg)) return 0f;
      return Mathf.DegToRad(deg);
    }

    // Given a Node3D rotation.y (radians), returns the nearest 8-way direction.
    // Mapping note:
    // - yawWorldDeg = rotationY(deg) - ModelForwardYawOffsetDeg
    // - world yaw 0° = +Z (South), 180° = -Z (North)
    // - Ordered8 index i = (4 - sector) mod 8, where sector = round(yaw/45)
    public static Enums.Direction GetDirectionFromRotation3D(float rotationY)
    {
      float yawWorldDeg = NormalizeDeg(
        Mathf.RadToDeg(rotationY) - ModelForwardYawOffsetDeg
      );
      int sector = Mathf.RoundToInt(yawWorldDeg / 45f);
      int idx = Mod8(4 - sector);
      return Ordered8[idx];
    }

    // Calculates an 8-way direction from one grid cell to another based
    // on their grid coordinates. Grid's -Z is North (forward).
    public static Enums.Direction GetDirectionBetweenCells(
      GridCell fromCell,
      GridCell toCell
    )
    {
      if (
        fromCell == null
        || toCell == null
        || fromCell.gridCoordinates == toCell.gridCoordinates
      )
        return Enums.Direction.None;

      var d = toCell.gridCoordinates - fromCell.gridCoordinates;
      var key = (System.Math.Sign(d.X), System.Math.Sign(d.Z));

      // Note: Grid Z increases towards world -Z (North).
      // A move North (e.g. z=0 to z=1) means d.Z is positive.
      return key switch
      {
        (0, 1) => Enums.Direction.North,
        (1, 1) => Enums.Direction.NorthEast,
        (1, 0) => Enums.Direction.East,
        (1, -1) => Enums.Direction.SouthEast,
        (0, -1) => Enums.Direction.South,
        (-1, -1) => Enums.Direction.SouthWest,
        (-1, 0) => Enums.Direction.West,
        (-1, 1) => Enums.Direction.NorthWest,
        _ => Enums.Direction.None
      };
    }

    // One-step clockwise or counter-clockwise in 45° increments.
    public static Enums.Direction GetNextDirection(
      Enums.Direction current,
      bool clockwise
    )
    {
      if (!DirectionIndices.TryGetValue(current, out int idx))
        return Enums.Direction.None;

      int next = Mod8(idx + (clockwise ? 1 : -1));
      return Ordered8[next];
    }

    // Converts a Vector3 direction to the nearest 8-way compass direction.
    // Uses the same world-yaw basis (0° = +Z) then maps to -Z=North index.
    public static Enums.Direction GetDirectionFromVector3(Vector3 direction)
    {
      if (direction.LengthSquared() < 0.001f)
        return Enums.Direction.None;

      float yawWorldDeg = VectorToYawWorldDeg(direction.Normalized());
      int sector = Mathf.RoundToInt(yawWorldDeg / 45f);
      int idx = Mod8(4 - sector);
      return Ordered8[idx];
    }

    // Direction to normalized world-space Vector3.
    // North is world -Z (Godot forward), East is world +X.
    public static Vector3 GetWorldVector3FromDirection(
      Enums.Direction direction
    )
    {
      return direction switch
      {
        Enums.Direction.North => new Vector3(0, 0, -1),
        Enums.Direction.South => new Vector3(0, 0, 1),
        Enums.Direction.East => new Vector3(1, 0, 0),
        Enums.Direction.West => new Vector3(-1, 0, 0),
        Enums.Direction.NorthEast => new Vector3(1, 0, -1).Normalized(),
        Enums.Direction.NorthWest => new Vector3(-1, 0, -1).Normalized(),
        Enums.Direction.SouthEast => new Vector3(1, 0, 1).Normalized(),
        Enums.Direction.SouthWest => new Vector3(-1, 0, 1).Normalized(),
        _ => Vector3.Zero
      };
    }

    public static Vector3 GetVector3FromDirection(Enums.Direction direction) =>
      GetWorldVector3FromDirection(direction);

    // Opposite direction.
    public static Enums.Direction GetOppositeDirection(
      Enums.Direction direction
    )
    {
      if (!DirectionIndices.TryGetValue(direction, out int idx))
        return Enums.Direction.None;

      return Ordered8[Mod8(idx + 4)];
    }

    // Shortest angular distance in 45° steps. Positive=CW, Negative=CCW.
    public static int GetRotationStepsBetweenDirections(
      Enums.Direction from,
      Enums.Direction to
    )
    {
      if (
        !DirectionIndices.TryGetValue(from, out int fromIndex)
        || !DirectionIndices.TryGetValue(to, out int toIndex)
      )
        return 0;

      int diff = Mod8(toIndex - fromIndex);
      return diff > 4 ? diff - 8 : diff;
    }
  }
}