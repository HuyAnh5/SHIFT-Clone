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

    private readonly List<GameObject> spawned = new();
    private Grid runtimeGrid;

    private void Awake()
    {
        if (spawnParent == null) spawnParent = transform;

        // auto-find theo tên nếu chưa kéo ref
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

        SpawnFrom(markerBlack, prefabBlack);
        SpawnFrom(markerWhite, prefabWhite);

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

    private void SpawnFrom(Tilemap marker, SwapBlock2D prefab)
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
                var inst = Instantiate(prefab, worldPos, Quaternion.identity, spawnParent);
                inst.Initialize(runtimeGrid);

                spawned.Add(inst.gameObject);
            }
    }

    private void HideRenderer(Tilemap tm)
    {
        if (tm == null) return;
        var r = tm.GetComponent<TilemapRenderer>();
        if (r != null) r.enabled = false;
    }
}
