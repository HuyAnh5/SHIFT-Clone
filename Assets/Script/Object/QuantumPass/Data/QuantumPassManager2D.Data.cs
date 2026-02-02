using System;
using System.Collections.Generic;
using UnityEngine;

public partial class QuantumPassManager2D
{
    private static readonly Vector3Int[] N4 =
    {
        new Vector3Int( 1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int( 0, 1, 0),
        new Vector3Int( 0,-1, 0),
    };

    private sealed class Group
    {
        public readonly int id;
        public readonly HashSet<Vector3Int> cells = new();
        public WorldState openWorld;

        public bool isInside;
        public bool countdownActive;
        public float countdownEndTime;

        public WorldState countdownClosingWorld;

        public readonly List<LineRenderer> outlines = new();

        public Group(int id, WorldState startOpen)
        {
            this.id = id;
            openWorld = startOpen;
        }
    }

    private readonly struct Edge : IEquatable<Edge>
    {
        public readonly Vector2Int a;
        public readonly Vector2Int b;

        public Edge(Vector2Int p1, Vector2Int p2)
        {
            if (p1.y < p2.y || (p1.y == p2.y && p1.x <= p2.x))
            {
                a = p1; b = p2;
            }
            else
            {
                a = p2; b = p1;
            }
        }

        public bool Equals(Edge other) => a.Equals(other.a) && b.Equals(other.b);
        public override bool Equals(object obj) => obj is Edge e && Equals(e);
        public override int GetHashCode()
        {
            unchecked { return (a.GetHashCode() * 397) ^ b.GetHashCode(); }
        }
    }
}
