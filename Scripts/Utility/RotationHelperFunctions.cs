using System.Collections.Generic;
using Godot;

namespace FirstArrival.Scripts.Utility;

public static class RotationHelperFunctions
{
  // CONFIG
  // Set true if your grid's "North" is +Z. Set false to use Godot's default (-Z).
  public const bool NorthIsPlusZ = true;

  // If your mesh faces +Z when rotation.y == 0, set 180.
  // If it faces -Z (Godot default), leave 0.
  public const float ModelForwardYawOffsetDeg = 0f;

  // Clockwise starting at North
  private static readonly Enums.Direction[] Ordered8 =
  {
    Enums.Direction.North, Enums.Direction.NorthEast, Enums.Direction.East,
    Enums.Direction.SouthEast, Enums.Direction.South, Enums.Direction.SouthWest,
    Enums.Direction.West, Enums.Direction.NorthWest
  };

  // Yaw in degrees you can set directly on Node3D.Rotation.Y to face each direction.
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

  // Convert a world-space forward vector to Godot yaw degrees (0° faces -Z)
  private static float VectorToYawWorldDeg(Vector3 v)
  {
    // Yaw mapping for Godot: yaw = atan2(x, -z)
    return NormalizeDeg(Mathf.RadToDeg(Mathf.Atan2(v.X, -v.Z)));
  }

  // Node yaw (deg) you can assign to Rotation.Y to face the direction
  private static float GetNodeYawDegForDirection(Enums.Direction dir)
  {
    Vector3 v = GetWorldVector3FromDirection(dir);
    if (v == Vector3.Zero) return 0f;

    float yawWorldDeg = VectorToYawWorldDeg(v);
    return NormalizeDeg(yawWorldDeg + ModelForwardYawOffsetDeg);
  }

  /// Returns rotation.y (radians) to face the given compass direction.
  public static float GetRotationRadians(Enums.Direction dir)
  {
    if (!DirectionAngles.TryGetValue(dir, out float deg)) return 0f;
    return Mathf.DegToRad(deg);
  }

  /// Given a Node3D rotation.y (radians), returns the nearest 8-way direction.
  public static Enums.Direction GetDirectionFromRotation3D(float rotationY)
  {
    // Convert node yaw to world yaw by removing model offset
    float yawWorldDeg = NormalizeDeg(
      Mathf.RadToDeg(rotationY) - ModelForwardYawOffsetDeg
    );

    // Define where "North" sits in world yaw
    float northWorldDeg = NorthIsPlusZ ? 180f : 0f;

    // Round to nearest 45° sector starting at North
    int idx = Mod8(Mathf.RoundToInt((yawWorldDeg - northWorldDeg) / 45f));
    return Ordered8[idx];
  }

  /// Sign-based 8-way direction between cells (no step count).
  /// Uses +X = East, -X = West. Z mapping depends on NorthIsPlusZ.
  public static Enums.Direction GetDirectionBetweenCells(
    GridCell fromCell, GridCell toCell)
  {
    if (fromCell == null || toCell == null) return Enums.Direction.None;

    int dx = toCell.gridCoordinates.X - fromCell.gridCoordinates.X;
    int dz = toCell.gridCoordinates.Z - fromCell.gridCoordinates.Z;

    if (NorthIsPlusZ)
    {
      if (dx > 0 && dz == 0) return Enums.Direction.East;
      if (dx < 0 && dz == 0) return Enums.Direction.West;
      if (dx == 0 && dz > 0) return Enums.Direction.North;
      if (dx == 0 && dz < 0) return Enums.Direction.South;
      if (dx > 0 && dz > 0) return Enums.Direction.NorthEast;
      if (dx < 0 && dz > 0) return Enums.Direction.NorthWest;
      if (dx > 0 && dz < 0) return Enums.Direction.SouthEast;
      if (dx < 0 && dz < 0) return Enums.Direction.SouthWest;
    }
    else
    {
      if (dx > 0 && dz == 0) return Enums.Direction.East;
      if (dx < 0 && dz == 0) return Enums.Direction.West;
      if (dx == 0 && dz > 0) return Enums.Direction.South;
      if (dx == 0 && dz < 0) return Enums.Direction.North;
      if (dx > 0 && dz > 0) return Enums.Direction.SouthEast;
      if (dx < 0 && dz > 0) return Enums.Direction.SouthWest;
      if (dx > 0 && dz < 0) return Enums.Direction.NorthEast;
      if (dx < 0 && dz < 0) return Enums.Direction.NorthWest;
    }

    return Enums.Direction.None;
  }

  /// One-step clockwise or counter-clockwise in 45° increments.
  public static Enums.Direction GetNextDirection(
    Enums.Direction current, bool clockwise)
  {
    int idx = System.Array.IndexOf(Ordered8, current);
    if (idx < 0) return Enums.Direction.None;
    int next = Mod8(idx + (clockwise ? 1 : -1));
    return Ordered8[next];
  }

  /// Converts a Vector3 direction to the nearest 8-way compass direction.
  public static Enums.Direction GetDirectionFromVector3(Vector3 direction)
  {
    if (direction.LengthSquared() < 0.001f) return Enums.Direction.None;
    direction = direction.Normalized();

    float yawWorldDeg = VectorToYawWorldDeg(direction);
    float northWorldDeg = NorthIsPlusZ ? 180f : 0f;

    int idx = Mod8(Mathf.RoundToInt((yawWorldDeg - northWorldDeg) / 45f));
    return Ordered8[idx];
  }

  /// Direction to normalized world-space Vector3.
  public static Vector3 GetWorldVector3FromDirection(Enums.Direction direction)
  {
    bool plusZ = NorthIsPlusZ;

    return direction switch
    {
      Enums.Direction.North => plusZ
        ? new Vector3(0, 0, 1)
        : new Vector3(0, 0, -1),
      Enums.Direction.South => plusZ
        ? new Vector3(0, 0, -1)
        : new Vector3(0, 0, 1),
      Enums.Direction.East => new Vector3(1, 0, 0),
      Enums.Direction.West => new Vector3(-1, 0, 0),
      Enums.Direction.NorthEast => plusZ
        ? new Vector3(1, 0, 1).Normalized()
        : new Vector3(1, 0, -1).Normalized(),
      Enums.Direction.NorthWest => plusZ
        ? new Vector3(-1, 0, 1).Normalized()
        : new Vector3(-1, 0, -1).Normalized(),
      Enums.Direction.SouthEast => plusZ
        ? new Vector3(1, 0, -1).Normalized()
        : new Vector3(1, 0, 1).Normalized(),
      Enums.Direction.SouthWest => plusZ
        ? new Vector3(-1, 0, -1).Normalized()
        : new Vector3(-1, 0, 1).Normalized(),
      _ => Vector3.Zero
    };
  }

  // Backward-compatible alias if you were calling the old name
  public static Vector3 GetVector3FromDirection(Enums.Direction direction) =>
    GetWorldVector3FromDirection(direction);

  /// Opposite direction.
  public static Enums.Direction GetOppositeDirection(
    Enums.Direction direction)
  {
    return direction switch
    {
      Enums.Direction.North => Enums.Direction.South,
      Enums.Direction.NorthEast => Enums.Direction.SouthWest,
      Enums.Direction.East => Enums.Direction.West,
      Enums.Direction.SouthEast => Enums.Direction.NorthWest,
      Enums.Direction.South => Enums.Direction.North,
      Enums.Direction.SouthWest => Enums.Direction.NorthEast,
      Enums.Direction.West => Enums.Direction.East,
      Enums.Direction.NorthWest => Enums.Direction.SouthEast,
      _ => Enums.Direction.None
    };
  }

  /// Shortest angular distance in 45° steps. Positive=CW, Negative=CCW.
  public static int GetRotationStepsBetweenDirections(
    Enums.Direction from, Enums.Direction to)
  {
    int fromIndex = System.Array.IndexOf(Ordered8, from);
    int toIndex = System.Array.IndexOf(Ordered8, to);
    if (fromIndex < 0 || toIndex < 0) return 0;

    int diffCW = Mod8(toIndex - fromIndex);
    int diffCCW = Mod8(fromIndex - toIndex);
    return diffCW <= diffCCW ? diffCW : -diffCCW;
  }
}