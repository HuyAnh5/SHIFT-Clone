// QuantumPassManager2D.cs
// Unity 6 / 2D Tilemap
//
// Attach this component to your Level prefab root (e.g., LV_1).
//
// Required objects (recommended names for auto-find):
//  - Marker:     Tilemap_QuantumPass (NO colliders; can render as indicator)
//  - Runtime:    QP_Black_SolidFill   (TilemapRenderer ON, CompositeCollider2D trigger OFF)
//               QP_Black_GhostTrig   (TilemapRenderer OFF, CompositeCollider2D trigger ON)
//               QP_White_SolidFill
//               QP_White_GhostTrig
//  - paintTile:  a single 1x1 TileBase (any sprite tile). Only 1 tile asset is enough.
//
// Layer matrix:
//  - Player collides with QuantumSolid (ON)
//  - Player collides with QuantumTrigger (ON)
//
// Outline (optional):
//  - dashMaterial: unlit material with dashed texture (Wrap Mode = Repeat)
//  - Outline color rule: Black world => white dash, White world => black dash
//  - Dash scroll uses DOTween (DG.Tweening)

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;

[DisallowMultipleComponent]
public class QuantumPassManager2D : MonoBehaviour
{
    [Header("Marker (draw once)")]
    [SerializeField] private Tilemap markerTilemap; // Tilemap_QuantumPass

    [Header("Runtime Tilemaps (paint by code)")]
    [SerializeField] private Tilemap blackSolidFill;     // QP_Black_SolidFill
    [SerializeField] private Tilemap blackGhostTrigger;  // QP_Black_GhostTrig (renderer OFF, trigger collider ON)
    [SerializeField] private Tilemap whiteSolidFill;     // QP_White_SolidFill
    [SerializeField] private Tilemap whiteGhostTrigger;  // QP_White_GhostTrig
    [SerializeField] private TileBase paintTile;         // 1x1 tile (any TileBase)

    [Header("Initial State")]
    [SerializeField] private WorldState startOpenWorld = WorldState.Black; // all groups OPEN in this world at start

    [Header("Swap Rule")]
    [SerializeField, Min(0f)] private float swapDelay = 0.5f;

    [Header("Player")]
    [SerializeField] private Collider2D playerCollider;  // assign Player main collider (BoxCollider2D)

    [Header("World Visual (optional)")]
    [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.15f;

    [Header("Outline (optional)")]
    [SerializeField] private bool buildOutline = true;
    [SerializeField] private Transform outlineRoot;       // QuantumPassOutlines (empty). Auto-create if null.
    [SerializeField] private Material dashMaterial;       // Unlit material + dashed texture (Repeat)
    [SerializeField, Min(0f)] private float dashScrollSpeed = 1.0f; // UV cycles per second
    [SerializeField, Min(0.001f)] private float outlineWidth = 0.08f;
    [SerializeField] private string outlineSortingLayer = "Default";
    [SerializeField] private int outlineSortingOrder = 20;

    [Header("QuantumPass Inside Detection (less sensitive)")]
    [SerializeField, Range(0.1f, 1f)] private float footProbeWidthFactor = 0.45f; // % width of player collider
    [SerializeField, Min(0.001f)] private float footProbeHeight = 0.08f;          // world units
    [SerializeField] private float footProbeYOffset = 0.02f;                      // lift probe slightly above minY

    [SerializeField] private Color outlineA_BlackWorld = Color.white;
    [SerializeField] private Color outlineA_WhiteWorld = Color.black;

    [SerializeField] private bool outlineB_UseSolidFillBaseColor = true; // B = “màu gốc khối”
    [SerializeField] private Color outlineB_Custom = Color.black;        // nếu không dùng base color
    [SerializeField, Range(0f, 1f)] private float outlineB_Alpha = 1f;   // set 0 => “B = 0”


    // ===== Internal =====
    private readonly List<Group> _groups = new();
    private readonly Dictionary<Vector3Int, int> _cellToGroup = new();

    private readonly Dictionary<Tilemap, Color> _baseColors = new();

    private Collider2D[] _blackCols;
    private Collider2D[] _whiteCols;

    private Material _dashMatInstance;
    private Tween _dashTween;
    private float _dashOffsetX;

    [SerializeField] private string playerTag = "Player";



    private static readonly Vector3Int[] N4 =
    {
        new Vector3Int( 1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int( 0, 1, 0),
        new Vector3Int( 0,-1, 0),
    };

    private void OnEnable()
    {
        WorldShiftManager.OnWorldChanged += OnWorldChanged;
    }

    private void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= OnWorldChanged;
    }

    private void OnDestroy()
    {
        _dashTween?.Kill();
        _dashTween = null;

        if (_dashMatInstance != null)
        {
            Destroy(_dashMatInstance);
            _dashMatInstance = null;
        }
    }

    private void Start()
    {
        AutoFindIfMissing();

        if (markerTilemap == null)
        {
            Debug.LogError("[QuantumPass] Missing markerTilemap (Tilemap_QuantumPass).");
            enabled = false;
            return;
        }

        if (paintTile == null)
        {
            Debug.LogError("[QuantumPass] Missing paintTile (a 1x1 TileBase).");
            enabled = false;
            return;
        }

        BuildGroupsFromMarker();
        RepaintAllGroups();

        if (buildOutline)
            BuildOutlines();

        CacheBaseColorsAndColliders();

        // Apply visuals for current world
        var w = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        OnWorldChanged(w);

        // Initialize inside flags without starting countdowns
        RefreshInsideStatesNoCountdown();
    }

    private void FixedUpdate()
    {
        if (playerCollider == null || markerTilemap == null || _groups.Count == 0)
            return;

        var activeWorld = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;

        // Overlap only counts for groups that are OPEN in the active world
        Bounds probe = GetFootProbeBounds(playerCollider.bounds);
        var insideNow = GetOverlappingOpenGroupIds(probe, activeWorld);

        float t = Time.time;
        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];

            // Only meaningful if group is OPEN in active world
            bool groupOpenHere = (g.openWorld == activeWorld);
            bool inside = groupOpenHere && insideNow.Contains(g.id);

            if (inside)
            {
                g.isInside = true;
                if (g.countdownActive)
                    g.countdownActive = false;

                continue;
            }

            // outside
            if (g.isInside)
            {
                // Just left fully (from OPEN space)
                g.isInside = false;
                g.countdownActive = true;
                g.countdownEndTime = t + swapDelay;
                g.countdownClosingWorld = activeWorld; // <-- NEW: lock world at leave-time
            }
            else if (g.countdownActive && t >= g.countdownEndTime)
            {
                // Still outside and timeout reached => swap using current active world at swap-time
                g.countdownActive = false;
                DoSwapFromWorld(g, g.countdownClosingWorld); // <-- use locked world
            }
        }
    }

    private void RefreshInsideFlagsOnly()
    {
        if (playerCollider == null || markerTilemap == null || _groups.Count == 0)
            return;

        var activeWorld = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;

        // Use FOOT-PROBE bounds (less sensitive than full collider bounds)
        Bounds probe = GetFootProbeBounds(playerCollider.bounds);

        // Only groups that are OPEN in the active world are considered "inside"
        var insideNow = GetOverlappingOpenGroupIds(probe, activeWorld);

        for (int i = 0; i < _groups.Count; i++)
        {
            _groups[i].isInside = insideNow.Contains(_groups[i].id);
            // IMPORTANT: do NOT touch countdownActive / countdownEndTime / countdownClosingWorld
        }
    }


    private Bounds GetFootProbeBounds(Bounds playerBounds)
    {
        float w = playerBounds.size.x * footProbeWidthFactor;
        float h = footProbeHeight;

        // centered at bottom
        Vector3 center = new Vector3(playerBounds.center.x, playerBounds.min.y + footProbeYOffset + h * 0.5f, 0f);
        Vector3 size = new Vector3(w, h, 0.1f);

        return new Bounds(center, size);
    }


    // ==============================
    // Build groups from marker
    // ==============================
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

            var g = new Group(id, startOpenWorld);
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

    // ==============================
    // Paint tiles into runtime maps
    // ==============================
    private void RepaintAllGroups()
    {
        if (blackSolidFill) blackSolidFill.ClearAllTiles();
        if (blackGhostTrigger) blackGhostTrigger.ClearAllTiles();
        if (whiteSolidFill) whiteSolidFill.ClearAllTiles();
        if (whiteGhostTrigger) whiteGhostTrigger.ClearAllTiles();

        for (int i = 0; i < _groups.Count; i++)
            ApplyGroupTiles(_groups[i]);
    }

    private void ApplyGroupTiles(Group g)
    {
        bool openInBlack = (g.openWorld == WorldState.Black);

        foreach (var cell in g.cells)
        {
            if (openInBlack)
            {
                // Black OPEN => ghost in black, solid in white
                if (blackGhostTrigger) blackGhostTrigger.SetTile(cell, paintTile);
                if (blackSolidFill) blackSolidFill.SetTile(cell, null);

                if (whiteSolidFill) whiteSolidFill.SetTile(cell, paintTile);
                if (whiteGhostTrigger) whiteGhostTrigger.SetTile(cell, null);
            }
            else
            {
                // White OPEN => ghost in white, solid in black
                if (whiteGhostTrigger) whiteGhostTrigger.SetTile(cell, paintTile);
                if (whiteSolidFill) whiteSolidFill.SetTile(cell, null);

                if (blackSolidFill) blackSolidFill.SetTile(cell, paintTile);
                if (blackGhostTrigger) blackGhostTrigger.SetTile(cell, null);
            }
        }
    }

    private void DoSwapFromWorld(Group g, WorldState closingWorld)
    {
        // Rule: closingWorld closes, other opens
        g.openWorld = (closingWorld == WorldState.Black) ? WorldState.White : WorldState.Black;
        ApplyGroupTiles(g);
    }


    // ==============================
    // Player overlap (only OPEN groups in active world)
    // ==============================
    private HashSet<int> GetOverlappingOpenGroupIds(Bounds b, WorldState activeWorld)
    {
        HashSet<int> result = new();

        var min = markerTilemap.WorldToCell(b.min);
        var max = markerTilemap.WorldToCell(b.max);

        // pad a bit
        int xMin = min.x - 1, xMax = max.x + 1;
        int yMin = min.y - 1, yMax = max.y + 1;

        Vector3 cellSize = markerTilemap.cellSize;

        for (int y = yMin; y <= yMax; y++)
            for (int x = xMin; x <= xMax; x++)
            {
                var c = new Vector3Int(x, y, 0);
                if (!_cellToGroup.TryGetValue(c, out int gid))
                    continue;

                var g = _groups[gid];
                if (g.openWorld != activeWorld)
                    continue; // CLOSED in this world => don't count inside

                // AABB vs cell rect
                Vector3 bl = markerTilemap.CellToWorld(c);
                float x0 = bl.x, x1 = bl.x + cellSize.x;
                float y0 = bl.y, y1 = bl.y + cellSize.y;

                if (b.max.x > x0 && b.min.x < x1 && b.max.y > y0 && b.min.y < y1)
                    result.Add(gid);
            }

        return result;
    }

    // When world flips, don't treat it as "leaving" => cancel countdowns and recompute inside flags.
    private void RefreshInsideStatesNoCountdown()
    {
        if (playerCollider == null || markerTilemap == null || _groups.Count == 0)
            return;

        var activeWorld = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        var insideNow = GetOverlappingOpenGroupIds(playerCollider.bounds, activeWorld);

        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];
            g.countdownActive = false;
            g.isInside = insideNow.Contains(g.id);
        }
    }

    // ==============================
    // Outlines (perimeter + dashed scroll)
    // ==============================
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

        // clear old
        for (int i = outlineRoot.childCount - 1; i >= 0; i--)
            Destroy(outlineRoot.GetChild(i).gameObject);

        // material instance so offset doesn't modify asset
        if (_dashMatInstance != null) Destroy(_dashMatInstance);
        _dashMatInstance = new Material(dashMaterial);

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

                // Convert corner-grid vertices to world points:
                // corner vertex (x,y) maps to CellToWorld(x,y) as corner on grid.
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
        StartDashTween(); // DOTween drives dash scroll
    }

    private void StartDashTween()
    {
        _dashTween?.Kill();
        _dashTween = null;

        if (_dashMatInstance == null) return;
        if (dashScrollSpeed <= 0f) return;

        _dashOffsetX = 0f;
        _dashMatInstance.SetFloat("_Offset", 0f);

        float duration = 1f / dashScrollSpeed; // cycles per second
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


    // Build perimeter edges then chain into loops (corner-grid coords)
    private static List<List<Vector2Int>> BuildPerimeterLoops(HashSet<Vector3Int> cells)
    {
        HashSet<Edge> edges = new();

        foreach (var c in cells)
        {
            int x = c.x;
            int y = c.y;

            // neighbor missing => boundary edge
            if (!cells.Contains(new Vector3Int(x, y + 1, 0))) edges.Add(new Edge(new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1))); // top
            if (!cells.Contains(new Vector3Int(x + 1, y, 0))) edges.Add(new Edge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1))); // right
            if (!cells.Contains(new Vector3Int(x, y - 1, 0))) edges.Add(new Edge(new Vector2Int(x, y), new Vector2Int(x + 1, y))); // bottom
            if (!cells.Contains(new Vector3Int(x - 1, y, 0))) edges.Add(new Edge(new Vector2Int(x, y), new Vector2Int(x, y + 1))); // left
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
        // prefer direction order: Right, Up, Left, Down
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

        // preference: right-turn, straight, left-turn, back
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

    // ==============================
    // World visuals + outline color
    // ==============================
    private void CacheBaseColorsAndColliders()
    {
        _baseColors.Clear();
        CacheBaseColor(markerTilemap);
        CacheBaseColor(blackSolidFill);
        CacheBaseColor(blackGhostTrigger);
        CacheBaseColor(whiteSolidFill);
        CacheBaseColor(whiteGhostTrigger);

        _blackCols = CollectColliders(blackSolidFill, blackGhostTrigger);
        _whiteCols = CollectColliders(whiteSolidFill, whiteGhostTrigger);
    }

    private void CacheBaseColor(Tilemap tm)
    {
        if (tm == null) return;
        if (!_baseColors.ContainsKey(tm))
            _baseColors.Add(tm, tm.color);
    }

    private static Collider2D[] CollectColliders(params Tilemap[] tms)
    {
        List<Collider2D> cols = new();
        for (int i = 0; i < tms.Length; i++)
        {
            if (tms[i] == null) continue;
            cols.AddRange(tms[i].GetComponents<Collider2D>());
        }
        return cols.ToArray();
    }

    private void OnWorldChanged(WorldState solidWorld)
    {
        bool blackActive = (solidWorld == WorldState.Black);

        SetColliders(_blackCols, blackActive);
        SetColliders(_whiteCols, !blackActive);

        SetAlpha(blackSolidFill, blackActive ? activeAlpha : inactiveAlpha);
        SetAlpha(blackGhostTrigger, blackActive ? activeAlpha : inactiveAlpha);
        SetAlpha(whiteSolidFill, !blackActive ? activeAlpha : inactiveAlpha);
        SetAlpha(whiteGhostTrigger, !blackActive ? activeAlpha : inactiveAlpha);

        ApplyOutlineColor(solidWorld);

        // Avoid accidental "leave" triggered by world flip
        RefreshInsideFlagsOnly();
    }

    private void ApplyOutlineColor(WorldState world)
    {
        if (!buildOutline) return;
        if (_dashMatInstance == null) return;

        Color a = (world == WorldState.Black) ? outlineA_BlackWorld : outlineA_WhiteWorld;

        Color b;
        if (outlineB_UseSolidFillBaseColor)
        {
            // “màu gốc khối” lấy từ solid fill tilemap của world đó
            Tilemap src = (world == WorldState.Black) ? blackSolidFill : whiteSolidFill;
            if (src != null && _baseColors.TryGetValue(src, out var baseC))
                b = baseC;
            else
                b = (world == WorldState.Black) ? Color.black : Color.white;

            b.a = 1f; // normalize alpha
        }
        else
        {
            b = outlineB_Custom;
        }

        b.a *= outlineB_Alpha; // nếu muốn “0” thì set outlineB_Alpha = 0

        _dashMatInstance.SetColor("_ColorA", a);
        _dashMatInstance.SetColor("_ColorB", b);
    }


    private static void SetColliders(Collider2D[] cols, bool enabled)
    {
        if (cols == null) return;
        for (int i = 0; i < cols.Length; i++)
            if (cols[i] != null) cols[i].enabled = enabled;
    }

    private void SetAlpha(Tilemap tm, float a)
    {
        if (tm == null) return;

        if (!_baseColors.TryGetValue(tm, out var baseC))
            baseC = tm.color;

        baseC.a = a;
        tm.color = baseC;
    }

    // ==============================
    // Auto-find by names (optional)
    // ==============================
    private void AutoFindIfMissing()
    {
        if (playerCollider == null)
        {
            // 1) Ưu tiên tag Player
            if (!TryAssignPlayerColliderFromTag())
            {
                // 2) Fallback cũ: tìm PlayerController
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) playerCollider = pc.GetComponent<Collider2D>();
            }

            if (playerCollider == null)
                Debug.LogWarning("[QuantumPass] playerCollider is still null. Assign it manually or ensure Player has tag + collider.");
        }

        if (markerTilemap == null || blackSolidFill == null || blackGhostTrigger == null || whiteSolidFill == null || whiteGhostTrigger == null)
        {
            var tms = GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < tms.Length; i++)
            {
                var tm = tms[i];
                switch (tm.name)
                {
                    case "Tilemap_QuantumPass": markerTilemap ??= tm; break;
                    case "QP_Black_SolidFill": blackSolidFill ??= tm; break;
                    case "QP_Black_GhostTrig": blackGhostTrigger ??= tm; break;
                    case "QP_White_SolidFill": whiteSolidFill ??= tm; break;
                    case "QP_White_GhostTrig": whiteGhostTrigger ??= tm; break;
                }
            }
        }

        if (outlineRoot == null)
        {
            var child = transform.Find("QuantumPassOutlines");
            if (child != null) outlineRoot = child;
        }
    }


    private bool TryAssignPlayerColliderFromTag()
    {
        GameObject go = null;

        try
        {
            go = GameObject.FindGameObjectWithTag(playerTag);
        }
        catch
        {
            // Tag chưa tồn tại trong Tag Manager -> FindGameObjectWithTag sẽ throw
            Debug.LogWarning($"[QuantumPass] Tag '{playerTag}' is missing. Add it in Tag Manager or change playerTag.");
            return false;
        }

        if (go == null) return false;

        // Ưu tiên collider NON-trigger (hitbox thật)
        var cols = go.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && !cols[i].isTrigger)
            {
                playerCollider = cols[i];
                return true;
            }
        }

        // Nếu không có non-trigger thì fallback lấy cái đầu tiên (ít khuyến khích)
        if (cols.Length > 0)
        {
            playerCollider = cols[0];
            Debug.LogWarning("[QuantumPass] Found only trigger colliders on Player. Please use a NON-trigger collider as hitbox.");
            return true;
        }

        return false;
    }


    // ==============================
    // Data
    // ==============================
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
