using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelTilemapJsonSerializer : MonoBehaviour
{
    [Header("Level Id")]
    [SerializeField] private string levelName = "LV_1";

    [Header("Refs")]
    [SerializeField] private Transform gridRoot;

    [SerializeField] private Tilemap blackTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap spikeTilemap;
    [SerializeField] private Tilemap exitTilemap;

    [Header("Tile Assets (one per tilemap)")]
    [SerializeField] private TileBase blackTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase spikeTile;
    [SerializeField] private TileBase exitTile;

    [Header("Optional: regenerate white after load")]
    [SerializeField] private InverseWhiteFromBlackTilemap inverseWhiteGenerator;

    [Header("Save Location")]
    [Tooltip("N?u true: l?u vào Assets/Resources/LevelData (Editor only). N?u false: l?u vào persistentDataPath.")]
    [SerializeField] private bool saveToResourcesFolderInEditor = true;

    private string GetSavePath()
    {
#if UNITY_EDITOR
        if (saveToResourcesFolderInEditor)
        {
            string dir = Path.Combine(Application.dataPath, "Resources/LevelData");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"{levelName}.json");
        }
#endif
        string pdir = Path.Combine(Application.persistentDataPath, "LevelData");
        Directory.CreateDirectory(pdir);
        return Path.Combine(pdir, $"{levelName}.json");
    }

    [ContextMenu("Save Level To JSON")]
    public void SaveToJson()
    {
        if (gridRoot == null) gridRoot = transform;

        var tilemaps = new List<TilemapSaveData>
        {
            SaveTilemap(blackTilemap, "Black"),
            SaveTilemap(wallTilemap,  "Wall"),
            SaveTilemap(spikeTilemap, "Spike"),
            SaveTilemap(exitTilemap,  "Exit"),
        };

        var data = new LevelSaveData
        {
            levelName = levelName,
            gridTransform = TransformData.From(gridRoot),
            tilemaps = tilemaps.ToArray()
        };

        string json = JsonUtility.ToJson(data, true);
        string path = GetSavePath();
        File.WriteAllText(path, json);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        Debug.Log($"[LevelTilemapJsonSerializer] Saved: {path}");
    }

    [ContextMenu("Load Level From JSON")]
    public void LoadFromJson()
    {
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[LevelTilemapJsonSerializer] Missing json: {path}");
            return;
        }

        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<LevelSaveData>(json);

        if (gridRoot == null) gridRoot = transform;
        data.gridTransform.ApplyTo(gridRoot);

        // clear
        blackTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        spikeTilemap?.ClearAllTiles();
        exitTilemap?.ClearAllTiles();

        // apply each tilemap
        foreach (var tm in data.tilemaps)
        {
            if (tm == null) continue;

            if (tm.name == "Black") LoadTilemap(blackTilemap, blackTile, tm);
            else if (tm.name == "Wall") LoadTilemap(wallTilemap, wallTile, tm);
            else if (tm.name == "Spike") LoadTilemap(spikeTilemap, spikeTile, tm);
            else if (tm.name == "Exit") LoadTilemap(exitTilemap, exitTile, tm);
        }

        // regenerate white (optional)
        if (inverseWhiteGenerator != null)
            inverseWhiteGenerator.Generate();

        Debug.Log($"[LevelTilemapJsonSerializer] Loaded: {path}");
    }

    private TilemapSaveData SaveTilemap(Tilemap tm, string name)
    {
        if (tm == null) return new TilemapSaveData { name = name, cells = new CellPos[0] };

        tm.CompressBounds();
        var b = tm.cellBounds;

        var cells = new List<CellPos>(256);
        for (int x = b.xMin; x < b.xMax; x++)
        {
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (tm.HasTile(pos))
                    cells.Add(new CellPos(x, y));
            }
        }

        return new TilemapSaveData
        {
            name = name,
            transform = TransformData.From(tm.transform),
            cells = cells.ToArray()
        };
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
