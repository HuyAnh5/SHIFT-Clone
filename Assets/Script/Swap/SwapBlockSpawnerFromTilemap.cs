using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SwapBlockSpawnerFromTilemap : MonoBehaviour
{
    [Header("Marker Tilemaps (inside level prefab instance)")]
    [SerializeField] private Tilemap markerBlack;
    [SerializeField] private Tilemap markerWhite;

    [Header("Prefabs")]
    [SerializeField] private SwapBlock2D prefabBlack;
    [SerializeField] private SwapBlock2D prefabWhite;

    [Header("Spawn Parent")]
    [SerializeField] private Transform spawnParent;

    [Header("Options")]
    [SerializeField] private bool hideMarkerRenderers = true;
    [SerializeField] private bool clearMarkerTilesAfterSpawn = true;

    [Header("Auto clear solids under swap blocks")]
    [Tooltip("Nếu bật: cell nào có swap marker thì sẽ xoá tile ở các tilemap solid (để swap block không bị kẹt).")]
    [SerializeField] private bool clearSolidsUnderSwapBlocks = true;

    [Tooltip("Nếu bật: xoá ở CẢ 2 world tilemap (đúng theo concept 'ở world kia là khoảng trống').")]
    [SerializeField] private bool clearInBothWorlds = true;

    [Tooltip("Nếu bật: cũng xoá tile ở Wall tilemap tại cell đó.")]
    [SerializeField] private bool clearWallToo = true;

    [Header("Solid Tilemaps to clear (optional, auto-find by name if null)")]
    [SerializeField] private Tilemap solidBlack;
    [SerializeField] private Tilemap solidWhite;
    [SerializeField] private Tilemap wallTilemap;

    private readonly List<GameObject> spawned = new();
    private Grid runtimeGrid;

    private void Awake()
    {
        if (spawnParent == null) spawnParent = transform;

        // auto-find marker theo tên nếu chưa kéo ref
        if (markerBlack == null || markerWhite == null)
        {
            var tms = GetComponentsInChildren<Tilemap>(true);
            foreach (var tm in tms)
            {
                if (markerBlack == null && tm.name.Contains("SwapMarker_Black"))
                    markerBlack = tm;
                if (markerWhite == null && tm.name.Contains("SwapMarker_White"))
                    markerWhite = tm;
            }
        }

        // auto-find solids theo tên nếu chưa kéo ref
        if (solidBlack == null || solidWhite == null || wallTilemap == null)
        {
            var tms = GetComponentsInChildren<Tilemap>(true);
            foreach (var tm in tms)
            {
                if (solidBlack == null && (tm.name.Contains("WorldBlack") || tm.name.Contains("Tilemap_Black")))
                    solidBlack = tm;

                if (solidWhite == null && (tm.name.Contains("WorldWhite") || tm.name.Contains("Tilemap_White")))
                    solidWhite = tm;

                if (wallTilemap == null && tm.name.Contains("Wall"))
                    wallTilemap = tm;
            }
        }

        runtimeGrid = GetComponentInParent<Grid>();
        if (runtimeGrid == null) runtimeGrid = FindAnyObjectByType<Grid>();

        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        // clear old
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();

        SpawnFrom(markerBlack, prefabBlack, WorldState.Black);
        SpawnFrom(markerWhite, prefabWhite, WorldState.White);

        if (hideMarkerRenderers)
        {
            HideRenderer(markerBlack);
            HideRenderer(markerWhite);
        }

        if (clearMarkerTilesAfterSpawn)
        {
            if (markerBlack != null) markerBlack.ClearAllTiles();
            if (markerWhite != null) markerWhite.ClearAllTiles();
        }
    }

    private void SpawnFrom(Tilemap marker, SwapBlock2D prefab, WorldState ownerWorld)
    {
        if (marker == null || prefab == null) return;

        marker.CompressBounds();
        var b = marker.cellBounds;

        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!marker.HasTile(cell)) continue;

                Vector3 worldPos = marker.GetCellCenterWorld(cell);

                if (clearSolidsUnderSwapBlocks)
                    ClearSolidsAt(worldPos, ownerWorld);

                var inst = Instantiate(prefab, worldPos, Quaternion.identity, spawnParent);
                inst.Initialize(runtimeGrid);
                spawned.Add(inst.gameObject);
            }
    }

    private void ClearSolidsAt(Vector3 worldPos, WorldState ownerWorld)
    {
        // theo concept: swap block chiếm cell => ở world kia cũng phải là khoảng trống
        if (clearInBothWorlds)
        {
            ClearTileAt(solidBlack, worldPos);
            ClearTileAt(solidWhite, worldPos);
        }
        else
        {
            // chỉ clear world của chính nó
            if (ownerWorld == WorldState.Black) ClearTileAt(solidBlack, worldPos);
            if (ownerWorld == WorldState.White) ClearTileAt(solidWhite, worldPos);
        }

        if (clearWallToo)
            ClearTileAt(wallTilemap, worldPos);
    }

    private void ClearTileAt(Tilemap tm, Vector3 worldPos)
    {
        if (tm == null) return;
        Vector3Int c = tm.WorldToCell(worldPos);
        if (tm.HasTile(c))
        {
            tm.SetTile(c, null);
            tm.RefreshTile(c);
        }
    }

    private void HideRenderer(Tilemap tm)
    {
        if (tm == null) return;
        var r = tm.GetComponent<TilemapRenderer>();
        if (r != null) r.enabled = false;
    }
}
