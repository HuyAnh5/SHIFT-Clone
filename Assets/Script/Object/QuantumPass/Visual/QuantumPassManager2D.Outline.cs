using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;

public partial class QuantumPassManager2D
{
    private void BuildOutlines()
    {
        if (dashMaterial == null)
        {
            Debug.LogWarning("[QuantumPass] buildOutline is ON but dashMaterial is null. Skipping outlines.");
            return;
        }

        if (outlineRoot == null)
        {
            var go = new GameObject("QuantumPassOutlines");
            go.transform.SetParent(transform, false);
            outlineRoot = go.transform;
        }

        for (int i = outlineRoot.childCount - 1; i >= 0; i--)
            Destroy(outlineRoot.GetChild(i).gameObject);

        if (_dashMatInstance != null) Destroy(_dashMatInstance);
        _dashMatInstance = new Material(dashMaterial);
        _dashMatInstance.mainTextureScale = new Vector2(dashTilingX, 1f);

        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];
            g.outlines.Clear();

            var loops = BuildPerimeterLoops(g.cells);
            for (int l = 0; l < loops.Count; l++)
            {
                var loop = loops[l];
                if (loop.Count < 3) continue;

                var lrObj = new GameObject($"QP_Outline_G{g.id}_L{l}");
                lrObj.transform.SetParent(outlineRoot, false);

                var lr = lrObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.textureMode = LineTextureMode.Tile;
                lr.alignment = LineAlignment.TransformZ;
                lr.widthMultiplier = outlineWidth;
                lr.numCornerVertices = 0;
                lr.numCapVertices = 0;

                lr.sortingLayerName = outlineSortingLayer;
                lr.sortingOrder = outlineSortingOrder;

                lr.material = _dashMatInstance;

                lr.startColor = Color.white;
                lr.endColor = Color.white;

                Vector3[] pts = new Vector3[loop.Count];
                for (int p = 0; p < loop.Count; p++)
                {
                    var v = loop[p];
                    pts[p] = markerTilemap.CellToWorld(new Vector3Int(v.x, v.y, 0));
                }

                lr.positionCount = pts.Length;
                lr.SetPositions(pts);

                g.outlines.Add(lr);
            }
        }

        ApplyOutlineColor((WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black);
        StartDashTween();
    }

    private void StartDashTween()
    {
        _dashTween?.Kill();
        _dashTween = null;

        if (_dashMatInstance == null) return;
        if (dashScrollSpeed <= 0f) return;

        _dashOffsetX = 0f;
        _dashMatInstance.SetFloat("_Offset", 0f);

        float duration = 1f / dashScrollSpeed;
        _dashTween = DOTween.To(
                () => _dashOffsetX,
                v =>
                {
                    _dashOffsetX = v;
                    _dashMatInstance.SetFloat("_Offset", Mathf.Repeat(_dashOffsetX, 1f));
                },
                1f,
                duration
            )
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
    }

    // ===== perimeter loops =====
    private static List<List<Vector2Int>> BuildPerimeterLoops(HashSet<Vector3Int> cells)
    {
        HashSet<Edge> edges = new();

        foreach (var c in cells)
        {
            int x = c.x;
            int y = c.y;

            if (!cells.Contains(new Vector3Int(x, y + 1, 0))) edges.Add(new Edge(new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1)));
            if (!cells.Contains(new Vector3Int(x + 1, y, 0))) edges.Add(new Edge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1)));
            if (!cells.Contains(new Vector3Int(x, y - 1, 0))) edges.Add(new Edge(new Vector2Int(x, y), new Vector2Int(x + 1, y)));
            if (!cells.Contains(new Vector3Int(x - 1, y, 0))) edges.Add(new Edge(new Vector2Int(x, y), new Vector2Int(x, y + 1)));
        }

        Dictionary<Vector2Int, List<Vector2Int>> adj = new();
        foreach (var e in edges)
        {
            AddAdj(adj, e.a, e.b);
            AddAdj(adj, e.b, e.a);
        }

        HashSet<Edge> remaining = new(edges);
        List<List<Vector2Int>> loops = new();

        int safety = 0;
        while (remaining.Count > 0 && safety++ < 10000)
        {
            Vector2Int start = PickLowestVertex(remaining);

            var neigh = adj[start];
            Vector2Int next = ChooseInitialNeighbor(start, neigh);

            List<Vector2Int> loop = new() { start };

            Vector2Int prev = start;
            Vector2Int curr = next;

            remaining.Remove(new Edge(prev, curr));
            loop.Add(curr);

            int iter = 0;
            while (curr != start && iter++ < 200000)
            {
                var nbs = adj[curr];
                Vector2Int chosen = ChooseNext(prev, curr, nbs);

                prev = curr;
                curr = chosen;

                remaining.Remove(new Edge(prev, curr));

                if (curr != start)
                    loop.Add(curr);
            }

            if (loop.Count >= 3)
                loops.Add(loop);
        }

        return loops;
    }

    private static void AddAdj(Dictionary<Vector2Int, List<Vector2Int>> adj, Vector2Int a, Vector2Int b)
    {
        if (!adj.TryGetValue(a, out var list))
        {
            list = new List<Vector2Int>(2);
            adj[a] = list;
        }
        list.Add(b);
    }

    private static Vector2Int PickLowestVertex(HashSet<Edge> edges)
    {
        bool has = false;
        Vector2Int best = default;

        foreach (var e in edges)
        {
            Check(e.a);
            Check(e.b);
        }

        return best;

        void Check(Vector2Int v)
        {
            if (!has)
            {
                has = true;
                best = v;
                return;
            }

            if (v.y < best.y || (v.y == best.y && v.x < best.x))
                best = v;
        }
    }

    private static Vector2Int ChooseInitialNeighbor(Vector2Int start, List<Vector2Int> neigh)
    {
        int bestIdx = 999;
        Vector2Int best = neigh[0];

        for (int i = 0; i < neigh.Count; i++)
        {
            var d = neigh[i] - start;
            int idx = DirIndex(d);
            if (idx < bestIdx)
            {
                bestIdx = idx;
                best = neigh[i];
            }
        }
        return best;
    }

    private static Vector2Int ChooseNext(Vector2Int prev, Vector2Int curr, List<Vector2Int> neigh)
    {
        if (neigh.Count == 1) return neigh[0];
        if (neigh.Count == 2) return (neigh[0] == prev) ? neigh[1] : neigh[0];

        Vector2Int incoming = curr - prev;
        int inDir = DirIndex(incoming);

        int[] pref = { (inDir + 3) & 3, inDir, (inDir + 1) & 3, (inDir + 2) & 3 };

        for (int p = 0; p < pref.Length; p++)
        {
            int want = pref[p];
            for (int i = 0; i < neigh.Count; i++)
            {
                var cand = neigh[i];
                if (cand == prev) continue;
                var d = cand - curr;
                if (DirIndex(d) == want) return cand;
            }
        }

        for (int i = 0; i < neigh.Count; i++)
            if (neigh[i] != prev) return neigh[i];

        return prev;
    }

    private static int DirIndex(Vector2Int d)
    {
        if (d.x == 1) return 0;    // Right
        if (d.y == 1) return 1;    // Up
        if (d.x == -1) return 2;   // Left
        return 3;                  // Down
    }
}
