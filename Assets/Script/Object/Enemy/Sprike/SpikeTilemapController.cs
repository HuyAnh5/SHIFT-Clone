using System.Collections.Generic;
using UnityEngine;
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

    [Header("Celeste Feel - Forgiving Hitbox")]
    [Tooltip("Bật để spike không 'ác': trigger lớn nhưng chỉ kill khi overlap vùng inset thật.")]
    [SerializeField] private bool useForgivingHitbox = true;

    [Tooltip("Shrink bounds của player khi check spike (0.1 = shrink 10% mỗi chiều).")]
    [SerializeField, Range(0f, 0.3f)] private float playerBoundsShrink = 0.12f;

    [Tooltip("Inset vùng nguy hiểm trong mỗi ô spike (tính theo % kích thước cell).")]
    [SerializeField, Range(0f, 0.45f)] private float spikeCellInsetFraction = 0.18f;

    private readonly List<Vector3Int> spikeCells = new();
    private readonly HashSet<Vector3Int> spikeSet = new();

    private static readonly Matrix4x4 ROT_0 = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
    private static readonly Matrix4x4 ROT_180 = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 180f), Vector3.one);

    private int _lastKillFrame = -1;
    
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
        spikeSet.Clear();

        spikeTilemap.CompressBounds();
        var b = spikeTilemap.cellBounds;

        for (int x = b.xMin; x < b.xMax; x++)
        {
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (spikeTilemap.HasTile(pos))
                {
                    spikeCells.Add(pos);
                    spikeSet.Add(pos);
                }
            }
        }
    }

    private void ApplyRotation(WorldState world)
    {
        Matrix4x4 m = (world == rotateWhenWorldIs) ? ROT_180 : ROT_0;

        for (int i = 0; i < spikeCells.Count; i++)
            spikeTilemap.SetTransformMatrix(spikeCells[i], m);

        spikeTilemap.RefreshAllTiles();

        spikeTilemap.color = (world == WorldState.Black) ? spikeColorInBlackWorld : spikeColorInWhiteWorld;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryKill(other);
    }

    // Quan trọng: nếu Enter xảy ra khi chỉ sượt trigger (chưa lethal),
    // Stay sẽ bắt trường hợp player đi sâu vào spike trong khi vẫn đang overlap trigger.
    private void OnTriggerStay2D(Collider2D other)
    {
        TryKill(other);
    }


    private void TryKill(Collider2D other)
    {
        // CHỈ nhận Hurtbox
        if (!other.TryGetComponent<PlayerHurtbox2D>(out _)) return;

        // chặn double-trigger trong cùng frame (vì có Enter + Stay)
        if (_lastKillFrame == Time.frameCount) return;
        _lastKillFrame = Time.frameCount;

        if (useForgivingHitbox && !IsLethalOverlap(other))
            return;

        if (LevelManager.I != null)
            LevelManager.I.ReloadCurrentLevel();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
    }


    private bool IsLethalOverlap(Collider2D playerCol)
    {
        Bounds pb = playerCol.bounds;

        // shrink player bounds (forgiving)
        Vector3 shrink = new Vector3(pb.size.x * playerBoundsShrink, pb.size.y * playerBoundsShrink, 0f);
        pb.Expand(-shrink);

        // get nearby cell range
        Vector3Int cMin = spikeTilemap.WorldToCell(pb.min);
        Vector3Int cMax = spikeTilemap.WorldToCell(pb.max);

        Vector3 cellSize3 = spikeTilemap.layoutGrid.cellSize;
        float insetX = Mathf.Abs(cellSize3.x) * spikeCellInsetFraction;
        float insetY = Mathf.Abs(cellSize3.y) * spikeCellInsetFraction;

        for (int x = cMin.x; x <= cMax.x; x++)
        {
            for (int y = cMin.y; y <= cMax.y; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!spikeSet.Contains(cell)) continue;

                Vector3 center = spikeTilemap.GetCellCenterWorld(cell);
                float w = Mathf.Abs(cellSize3.x) - insetX * 2f;
                float h = Mathf.Abs(cellSize3.y) - insetY * 2f;

                if (w <= 0.001f || h <= 0.001f) continue;

                Rect spikeRect = new Rect(
                    center.x - (Mathf.Abs(cellSize3.x) * 0.5f) + insetX,
                    center.y - (Mathf.Abs(cellSize3.y) * 0.5f) + insetY,
                    w, h
                );

                // player bounds -> rect
                Rect playerRect = new Rect(pb.min.x, pb.min.y, pb.size.x, pb.size.y);

                if (spikeRect.Overlaps(playerRect))
                    return true;
            }
        }

        return false;
    }
}
