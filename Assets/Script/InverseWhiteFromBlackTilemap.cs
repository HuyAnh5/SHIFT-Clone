using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-1000)]
public class InverseWhiteFromBlackTilemap : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap blackTilemap;
    [SerializeField] private Tilemap whiteTilemap;

    [Header("White Fill Tile")]
    [SerializeField] private TileBase whiteTile;

    [Header("Map Bounds (cell coordinates)")]
    [SerializeField] private Vector2Int originCell = new Vector2Int(-20, -10);
    [SerializeField] private Vector2Int size = new Vector2Int(40, 20);

    [Header("Options")]
    [SerializeField] private bool generateOnAwake = true;

    [SerializeField] private UnityEngine.Tilemaps.Tilemap wallTilemap;

    [SerializeField] private UnityEngine.Tilemaps.Tilemap spikeTilemap;



    private void Awake()
    {
        if (generateOnAwake)
            Generate();
    }

    [ContextMenu("Generate White From Black")]
    public void Generate()
    {
        if (blackTilemap == null || whiteTilemap == null || whiteTile == null)
        {
            Debug.LogError("[InverseWhiteFromBlackTilemap] Missing references.");
            return;
        }

        whiteTilemap.ClearAllTiles();

        int xMin = originCell.x;
        int yMin = originCell.y;
        int xMax = originCell.x + size.x;
        int yMax = originCell.y + size.y;

        for (int x = xMin; x < xMax; x++)
        {
            for (int y = yMin; y < yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);

                // Black => lỗ
                if (blackTilemap.HasTile(pos))
                    continue;

                // Wall => không đè lên wall
                if (wallTilemap != null && wallTilemap.HasTile(pos))
                    continue;

                // Spike => lỗ (để White không có collider dưới spike)
                if (spikeTilemap != null && spikeTilemap.HasTile(pos))
                    continue;

                whiteTilemap.SetTile(pos, whiteTile);
            }
        }

        whiteTilemap.RefreshAllTiles();
    }

}
