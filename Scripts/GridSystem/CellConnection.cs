using Godot;
using System;

public struct CellConnection : IEquatable<CellConnection>
{
    // Store coordinates in a canonical order (smaller coordinate first)
    // to ensure (A,B) and (B,A) are treated as the same connection
    public Vector3I CellA { get; private set; }
    public Vector3I CellB { get; private set; }

    public CellConnection(Vector3I cell1, Vector3I cell2)
    {
        // Ensure consistent ordering for equality/hashing
        if (CompareCoords(cell1, cell2) <= 0)
        {
            CellA = cell1;
            CellB = cell2;
        }
        else
        {
            CellA = cell2;
            CellB = cell1;
        }
    }

    // Get the "other" cell in this connection
    public Vector3I GetOther(Vector3I cell)
    {
        if (cell == CellA) return CellB;
        if (cell == CellB) return CellA;
        return Vector3I.Zero; // Invalid
    }

    // Check if this connection involves a given cell
    public bool Contains(Vector3I cell)
    {
        return cell == CellA || cell == CellB;
    }

    // Comparison for ordering
    private static int CompareCoords(Vector3I a, Vector3I b)
    {
        if (a.Y != b.Y) return a.Y.CompareTo(b.Y);
        if (a.X != b.X) return a.X.CompareTo(b.X);
        return a.Z.CompareTo(b.Z);
    }

    // Equality and hash code for HashSet usage
    public bool Equals(CellConnection other)
    {
        return CellA == other.CellA && CellB == other.CellB;
    }

    public override bool Equals(object obj)
    {
        return obj is CellConnection other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CellA, CellB);
    }

    public static bool operator ==(CellConnection left, CellConnection right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CellConnection left, CellConnection right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"Connection({CellA} <-> {CellB})";
    }
}