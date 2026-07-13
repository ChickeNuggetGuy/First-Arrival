using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FirstArrival.Scripts.Utility
{
  public static class RotationHelperFunctions
  {
    // Godot's Forward vector is -Z. If an imported mesh faces +Z instead,
    // set this to 180 so the logical and visual headings stay aligned.
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

    // Convert a world-space direction vector to "world yaw" degrees.
    // In Godot: atan2(x, z). 
    // 0 deg = +Z (South)
    // 90 deg = +X (East)
    // 180/-180 deg = -Z (North)
    // -90 deg = -X (West)
    private static float VectorToYawWorldDeg(Vector3 v)
    {
      return NormalizeDeg(Mathf.RadToDeg(Mathf.Atan2(v.X, v.Z)));
    }

    // Node yaw (deg) to face the given direction. With Godot's -Z forward,
    // zero yaw faces North and positive yaw turns toward West.
    private static float GetNodeYawDegForDirection(Enums.Direction dir)
    {
      if (!DirectionIndices.TryGetValue(dir, out int index)) return 0f;

      // Ordered8 runs clockwise, while positive Godot yaw runs
      // counter-clockwise when viewed from above.
      return NormalizeDeg(-45f * index + ModelForwardYawOffsetDeg);
    }

    // Returns rotation.y (radians) to face the given compass direction.
    public static float GetRotationRadians(Enums.Direction dir)
    {
      if (!DirectionAngles.TryGetValue(dir, out float deg)) return 0f;
      return Mathf.DegToRad(deg);
    }

    // Given a Node3D rotation.y (radians), returns the nearest 8-way direction.
    public static Enums.Direction GetDirectionFromRotation3D(float rotationY)
    {
      // Remove the imported-mesh forward correction before snapping.
      float rotDeg = NormalizeDeg(Mathf.RadToDeg(rotationY) - ModelForwardYawOffsetDeg);

      int yawSteps = Mod8(Mathf.RoundToInt(rotDeg / 45f));
      return Ordered8[Mod8(-yawSteps)];
    }

    // Calculates an 8-way direction from one grid cell to another based
    // on their grid coordinates.
    // Assumes grid X+ is East, Z+ is South.
    public static Enums.Direction GetDirectionBetweenCells(
      GridCell fromCell,
      GridCell toCell
    )
    {
      if (
        fromCell == null
        || toCell == null
        || fromCell.GridCoordinates == toCell.GridCoordinates
      )
        return Enums.Direction.None;

      var d = toCell.GridCoordinates - fromCell.GridCoordinates;
      var key = (System.Math.Sign(d.X), System.Math.Sign(d.Z));

      // Grid Mapping (Normal System):
      // Z decreases going North (-1)
      // Z increases going South (+1)
      // X increases going East (+1)
      // X decreases going West (-1)
      return key switch
      {
        (0, -1) => Enums.Direction.North,
        (1, -1) => Enums.Direction.NorthEast,
        (1, 0) => Enums.Direction.East,
        (1, 1) => Enums.Direction.SouthEast,
        (0, 1) => Enums.Direction.South,
        (-1, 1) => Enums.Direction.SouthWest,
        (-1, 0) => Enums.Direction.West,
        (-1, -1) => Enums.Direction.NorthWest,
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
    public static Enums.Direction GetDirectionFromVector3(Vector3 direction)
    {
      Vector3 flatDirection = new Vector3(direction.X, 0f, direction.Z);
      if (flatDirection.LengthSquared() < 0.001f)
        return Enums.Direction.None;

      float yawWorldDeg = VectorToYawWorldDeg(flatDirection.Normalized());

      // World yaw is 0 at South and increases toward East; Ordered8 is
      // clockwise from North.
      int clockwiseIndex = Mod8(
        Mathf.RoundToInt((180f - yawWorldDeg) / 45f)
      );
      return Ordered8[clockwiseIndex];
    }

    // Direction to normalized world-space Vector3.
    // North is -Z, East is +X.
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
