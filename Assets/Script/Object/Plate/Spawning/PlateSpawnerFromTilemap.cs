using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlateSpawnerFromTilemap : MonoBehaviour
{
    [Header("Marker Tilemaps (inside level prefab instance)")]
    [SerializeField] private Tilemap markerBlack;
    [SerializeField] private Tilemap markerWhite;

    [Header("Marker Tiles")]
    [SerializeField] private TileBase holdTile;
    [SerializeField] private TileBase timedTile;

    [Header("Prefabs")]
    [SerializeField] private HoldPlate2D holdPrefab;
    [SerializeField] private TimedPlate2D timedPrefab;

    [Header("Spawn Parent")]
    [SerializeField] private Transform spawnParent;

    [Header("Options")]
    [SerializeField] private bool hideMarkerRenderers = true;
    [SerializeField] private bool clearMarkerTilesAfterSpawn = true;

    private readonly List<GameObject> spawned = new();

    private void Awake()
    {
        // IMPORTANT:
        // Đừng dùng transform.root nữa (root = LevelManager => plate sống dai qua reload).
        // Spawn parent phải nằm TRONG level instance để Destroy(level) là plate chết theo.
        if (spawnParent == null)
        {
            var t = transform.Find("PlatesRuntime");
            if (t == null)
            {
                var go = new GameObject("PlatesRuntime");
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            spawnParent = t;
        }

        // auto-find marker tilemaps by name
        if (markerBlack == null || markerWhite == null)
        {
            var tms = GetComponentsInChildren<Tilemap>(true);
            foreach (var tm in tms)
            {
                if (markerBlack == null && tm.name.Contains("PlateMarker_Black"))
                    markerBlack = tm;
                if (markerWhite == null && tm.name.Contains("PlateMarker_White"))
                    markerWhite = tm;
            }
        }

        Rebuild();
    }

    [ContextMenu("Rebuild Plates")]
    public void Rebuild()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();

        SpawnFrom(markerBlack, WorldState.Black);
        SpawnFrom(markerWhite, WorldState.White);

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

    private void SpawnFrom(Tilemap marker, WorldState ownerWorld)
    {
        if (marker == null) return;

        marker.CompressBounds();
        var b = marker.cellBounds;

        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = marker.GetTile(cell);
                if (tile == null) continue;

                Vector3 worldPos = marker.GetCellCenterWorld(cell);

                if (tile == holdTile && holdPrefab != null)
                {
                    var inst = Instantiate(holdPrefab, worldPos, Quaternion.identity, spawnParent);
                    inst.InitializeAt(worldPos, ownerWorld);
                    spawned.Add(inst.gameObject);
                }
                else if (tile == timedTile && timedPrefab != null)
                {
                    var inst = Instantiate(timedPrefab, worldPos, Quaternion.identity, spawnParent);
                    inst.InitializeAt(worldPos, ownerWorld);
                    spawned.Add(inst.gameObject);
                }
            }
    }

    private void HideRenderer(Tilemap tm)
    {
        if (tm == null) return;
        var r = tm.GetComponent<TilemapRenderer>();
        if (r != null) r.enabled = false;
    }
}
