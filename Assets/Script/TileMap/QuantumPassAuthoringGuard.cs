using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Edit-mode guard: any cell marked in Tilemap_QuantumPass will be cleared from base Tilemap_Black/Tilemap_White,
/// so you can't accidentally paint solids over QuantumPass "holes".
/// Attach to LV_1 root (same place as QuantumPassManager2D).
/// </summary>
[ExecuteAlways]
public class QuantumPassAuthoringGuard : MonoBehaviour
{
    [Header("Marker")]
    [SerializeField] private Tilemap markerQuantumPass; // Tilemap_QuantumPass

    [Header("Base World Tilemaps (solids you paint level with)")]
    [SerializeField] private Tilemap baseBlack; // Tilemap_Black
    [SerializeField] private Tilemap baseWhite; // Tilemap_White

    [Header("Behavior")]
    [SerializeField] private bool autoCarveInEditMode = true;
    [SerializeField] private bool autoCarveOnPlay = true;

    // crude change detection
    private int _lastHash;

    private void OnEnable()
    {
        if (!Application.isPlaying && autoCarveInEditMode)
            CarveNow();
    }

    private void Start()
    {
        if (Application.isPlaying && autoCarveOnPlay)
            CarveNow();
    }

    private void Update()
    {
        if (Application.isPlaying) return;
        if (!autoCarveInEditMode) return;

        int h = ComputeMarkerHash();
        if (h != _lastHash)
        {
            _lastHash = h;
            CarveNow();
        }
    }

    [ContextMenu("Carve Base Tilemaps Under QuantumPass")]
    public void CarveNow()
    {
        if (markerQuantumPass == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (baseBlack) Undo.RecordObject(baseBlack, "QuantumPass Carve");
            if (baseWhite) Undo.RecordObject(baseWhite, "QuantumPass Carve");
        }
#endif

        markerQuantumPass.CompressBounds();
        var b = markerQuantumPass.cellBounds;

        for (int y = b.yMin; y < b.yMax; y++)
            for (int x = b.xMin; x < b.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!markerQuantumPass.HasTile(cell)) continue;

                if (baseBlack && baseBlack.HasTile(cell)) baseBlack.SetTile(cell, null);
                if (baseWhite && baseWhite.HasTile(cell)) baseWhite.SetTile(cell, null);
            }

        baseBlack?.RefreshAllTiles();
        baseWhite?.RefreshAllTiles();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (baseBlack) EditorUtility.SetDirty(baseBlack);
            if (baseWhite) EditorUtility.SetDirty(baseWhite);
        }
#endif
    }

    private int ComputeMarkerHash()
    {
        if (markerQuantumPass == null) return 0;

        markerQuantumPass.CompressBounds();
        var b = markerQuantumPass.cellBounds;

        unchecked
        {
            int h = 17;
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (!markerQuantumPass.HasTile(cell)) continue;
                    h = h * 31 + cell.x;
                    h = h * 31 + cell.y;
                }
            return h;
        }
    }
}
