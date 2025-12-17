using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EnemyTilemapSpawner : MonoBehaviour
{
    [SerializeField] private Tilemap enemyTilemap;
    [SerializeField] private TileBase enemyLeftTile;
    [SerializeField] private TileBase enemyRightTile;

    [SerializeField] private TurretShooter2D turretPrefab;
    [SerializeField] private Transform spawnedParent;
    [SerializeField] private bool hideMarkerTilemapRenderer = true;

    private readonly List<GameObject> spawned = new();

    private void Awake()
    {
        if (enemyTilemap == null) enemyTilemap = GetComponent<Tilemap>();
        if (spawnedParent == null) spawnedParent = transform;

        if (hideMarkerTilemapRenderer)
        {
            var r = enemyTilemap.GetComponent<TilemapRenderer>();
            if (r != null) r.enabled = false;
        }
    }

    private void Start()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        // clear old
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);
        spawned.Clear();

        if (enemyTilemap == null || turretPrefab == null) return;

        enemyTilemap.CompressBounds();
        var b = enemyTilemap.cellBounds;

        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                var tile = enemyTilemap.GetTile(cell);
                if (tile == null) continue;

                var facing = TurretShooter2D.Facing.Left;
                if (enemyRightTile != null && tile == enemyRightTile) facing = TurretShooter2D.Facing.Right;
                else if (enemyLeftTile != null && tile == enemyLeftTile) facing = TurretShooter2D.Facing.Left;

                Vector3 worldPos = enemyTilemap.GetCellCenterWorld(cell);

                var turret = Instantiate(turretPrefab, worldPos, Quaternion.identity, spawnedParent);

                spawned.Add(turret.gameObject);
            }
    }
}
