using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelRuntimeLoader : MonoBehaviour
{
    [Header("Which level to load on start")]
    [SerializeField] private int startLevelIndex = 1;

    [Header("Tilemaps (target)")]
    [SerializeField] private Transform gridRoot;
    [SerializeField] private Tilemap blackTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap spikeTilemap;
    [SerializeField] private Tilemap exitTilemap;

    [Header("Tiles (one per tilemap)")]
    [SerializeField] private TileBase blackTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase spikeTile;
    [SerializeField] private TileBase exitTile;

    [Header("Generate White After Load")]
    [SerializeField] private InverseWhiteFromBlackTilemap inverseWhiteGenerator;

    private void Start()
    {
        LoadLevel(startLevelIndex);
    }

    public void LoadLevel(int levelIndex)
    {
        TextAsset ta = Resources.Load<TextAsset>($"LevelData/LV_{levelIndex}");
        if (ta == null)
        {
            Debug.LogError($"[LevelRuntimeLoader] Missing Resources/LevelData/LV_{levelIndex}.json");
            return;
        }

        var data = JsonUtility.FromJson<LevelSaveData>(ta.text);

        if (gridRoot == null) gridRoot = transform;
        data.gridTransform.ApplyTo(gridRoot);

        // clear old tiles
        blackTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        spikeTilemap?.ClearAllTiles();
        exitTilemap?.ClearAllTiles();

        // apply tilemaps
        if (data.tilemaps != null)
        {
            foreach (var tm in data.tilemaps)
            {
                if (tm == null) continue;

                if (tm.name == "Black") LoadTilemap(blackTilemap, blackTile, tm);
                else if (tm.name == "Wall") LoadTilemap(wallTilemap, wallTile, tm);
                else if (tm.name == "Spike") LoadTilemap(spikeTilemap, spikeTile, tm);
                else if (tm.name == "Exit") LoadTilemap(exitTilemap, exitTile, tm);
            }
        }

        // regenerate white (inverse)
        if (inverseWhiteGenerator != null)
            inverseWhiteGenerator.Generate();
    }

    private void LoadTilemap(Tilemap tm, TileBase tile, TilemapSaveData data)
    {
        if (tm == null || tile == null || data == null) return;

        data.transform.ApplyTo(tm.transform);

        if (data.cells == null) return;
        for (int i = 0; i < data.cells.Length; i++)
        {
            var c = data.cells[i];
            tm.SetTile(new Vector3Int(c.x, c.y, 0), tile);
        }

        tm.RefreshAllTiles();
    }
}
