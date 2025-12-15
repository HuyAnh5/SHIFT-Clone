using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class SpikeTilemapController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap spikeTilemap;

    [Header("Rotate to keep spike pointing up on screen")]
    [SerializeField] private WorldState rotateWhenWorldIs = WorldState.White; // camera đang xoay 180 ở world này
    [SerializeField] private bool rescanTilesOnEnable = true;

    [Header("Kill")]
    [SerializeField] private string playerTag = "Player";

    private readonly List<Vector3Int> spikeCells = new();

    private static readonly Matrix4x4 ROT_0 = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
    private static readonly Matrix4x4 ROT_180 = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 180f), Vector3.one);

    [Header("Color by world")]
    [SerializeField] private Color spikeColorInBlackWorld = Color.black;
    [SerializeField] private Color spikeColorInWhiteWorld = Color.white;


    private void Reset()
    {
        spikeTilemap = GetComponent<Tilemap>();
    }

    private void OnEnable()
    {
        if (spikeTilemap == null) spikeTilemap = GetComponent<Tilemap>();

        if (rescanTilesOnEnable)
            RebuildSpikeCache();

        WorldShiftManager.OnWorldChanged += ApplyRotation;
        if (WorldShiftManager.I != null)
            ApplyRotation(WorldShiftManager.I.SolidWorld);
    }

    private void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= ApplyRotation;
    }

    [ContextMenu("Rebuild Spike Cache")]
    public void RebuildSpikeCache()
    {
        spikeCells.Clear();

        spikeTilemap.CompressBounds();
        var b = spikeTilemap.cellBounds;

        for (int x = b.xMin; x < b.xMax; x++)
        {
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (spikeTilemap.HasTile(pos))
                    spikeCells.Add(pos);
            }
        }
    }

    private void ApplyRotation(WorldState world)
    {
        // Camera bạn xoay 180° theo world. Để spike vẫn “nhọn lên” theo màn hình:
        // -> khi camera 180° thì ta rotate spike 180° để bù lại.
        Matrix4x4 m = (world == rotateWhenWorldIs) ? ROT_180 : ROT_0;

        for (int i = 0; i < spikeCells.Count; i++)
            spikeTilemap.SetTransformMatrix(spikeCells[i], m);

        spikeTilemap.RefreshAllTiles();

        spikeTilemap.color = (world == WorldState.Black) ? spikeColorInBlackWorld : spikeColorInWhiteWorld;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (LevelManager.I != null)
            LevelManager.I.ReloadCurrentLevel();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
    }

}
