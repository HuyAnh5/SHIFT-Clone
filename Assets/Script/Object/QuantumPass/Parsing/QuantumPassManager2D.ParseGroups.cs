using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class QuantumPassManager2D
{
    private void BuildGroupsFromMarker()
    {
        _groups.Clear();
        _cellToGroup.Clear();

        HashSet<Vector3Int> unvisited = new();
        var bounds = markerTilemap.cellBounds;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var p = new Vector3Int(x, y, 0);
                if (markerTilemap.HasTile(p))
                    unvisited.Add(p);
            }

        int id = 0;
        Queue<Vector3Int> q = new();

        while (unvisited.Count > 0)
        {
            Vector3Int start = GetAny(unvisited);
            unvisited.Remove(start);

            var g = new Group(id, GetStartOpenForCell(start));
            q.Enqueue(start);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (!g.cells.Add(cur))
                    continue;

                _cellToGroup[cur] = g.id;

                for (int k = 0; k < N4.Length; k++)
                {
                    var nb = cur + N4[k];
                    if (unvisited.Contains(nb))
                    {
                        unvisited.Remove(nb);
                        q.Enqueue(nb);
                    }
                }
            }

            _groups.Add(g);
            id++;
        }

        Debug.Log($"[QuantumPass] Built {_groups.Count} group(s) from marker.");
    }

    private static Vector3Int GetAny(HashSet<Vector3Int> set)
    {
        foreach (var v in set) return v;
        return default;
    }

    private WorldState GetStartOpenForCell(Vector3Int cell)
    {
        if (!perGroupStartFromMarkerTile || markerTilemap == null)
            return startOpenWorld;

        var t = markerTilemap.GetTile(cell);

        if (t != null)
        {
            if (markerOpenInBlackTile != null && t == markerOpenInBlackTile) return WorldState.Black;
            if (markerOpenInWhiteTile != null && t == markerOpenInWhiteTile) return WorldState.White;
        }

        if (randomIfUnrecognized)
            return (UnityEngine.Random.value < 0.5f) ? WorldState.Black : WorldState.White;

        return startOpenWorld;
    }
}
