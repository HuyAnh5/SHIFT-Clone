// QuantumPassManager2D.cs
// Unity 6 / 2D Tilemap
//
// Attach this component to your Level prefab root (e.g., LV_1).
//
// Required objects (recommended names for auto-find):
//  - Marker:     Tilemap_QuantumPass (NO colliders; can render as indicator)
//  - Runtime:    QP_Black_SolidFill   (TilemapRenderer ON, CompositeCollider2D trigger OFF)
//               QP_Black_GhostTrig   (TilemapRenderer OFF, CompositeCollider2D trigger ON)
//               QP_White_SolidFill
//               QP_White_GhostTrig
//  - paintTile:  a single 1x1 TileBase (any sprite tile). Only 1 tile asset is enough.
//
// Outline (optional):
//  - dashMaterial: unlit material with dashed texture (Wrap Mode = Repeat)
//  - Dash scroll uses DOTween (DG.Tweening)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using DG.Tweening;

[DisallowMultipleComponent]
public partial class QuantumPassManager2D : MonoBehaviour
{
    [Header("Marker (draw once)")]
    [SerializeField] private Tilemap markerTilemap; // Tilemap_QuantumPass

    [Header("Runtime Tilemaps (paint by code)")]
    [SerializeField] private Tilemap blackSolidFill;     // QP_Black_SolidFill
    [SerializeField] private Tilemap blackGhostTrigger;  // QP_Black_GhostTrig
    [SerializeField] private Tilemap whiteSolidFill;     // QP_White_SolidFill
    [SerializeField] private Tilemap whiteGhostTrigger;  // QP_White_GhostTrig
    [SerializeField] private TileBase paintTile;         // 1x1 tile (any TileBase)

    [Header("Initial State")]
    [SerializeField] private WorldState startOpenWorld = WorldState.Black; // all groups OPEN in this world at start

    [Header("Per-Group Start Open (optional, by marker tile type)")]
    [SerializeField] private bool perGroupStartFromMarkerTile = true;
    [SerializeField] private TileBase markerOpenInBlackTile; // assign QP_OpenInBlack
    [SerializeField] private TileBase markerOpenInWhiteTile; // assign QP_OpenInWhite
    [SerializeField] private bool randomIfUnrecognized = false;

    [Header("Swap Rule")]
    [SerializeField, Min(0f)] private float swapDelay = 0.5f;

    [Header("Player")]
    [SerializeField] private Collider2D playerCollider;  // assign Player main collider

    [Header("World Visual (optional)")]
    [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.15f;

    [Header("Outline (optional)")]
    [SerializeField] private bool buildOutline = true;
    [SerializeField] private Transform outlineRoot;       // QuantumPassOutlines
    [SerializeField] private Material dashMaterial;       // dashed material (Repeat)
    [SerializeField, Min(0f)] private float dashScrollSpeed = 1.0f;
    [SerializeField, Min(0.001f)] private float outlineWidth = 0.08f;
    [SerializeField] private string outlineSortingLayer = "Default";
    [SerializeField] private int outlineSortingOrder = 20;

    [Header("QuantumPass Inside Detection (less sensitive)")]
    [SerializeField, Range(0.1f, 1f)] private float footProbeWidthFactor = 0.45f;
    [SerializeField, Min(0.001f)] private float footProbeHeight = 0.08f;
    [SerializeField] private float footProbeYOffset = 0.02f;

    [SerializeField] private Color outlineA_BlackWorld = Color.white;
    [SerializeField] private Color outlineA_WhiteWorld = Color.black;

    [SerializeField] private bool outlineB_UseSolidFillBaseColor = true;
    [SerializeField] private Color outlineB_Custom = Color.black;
    [SerializeField, Range(0f, 1f)] private float outlineB_Alpha = 1f;

    [SerializeField, Range(0.05f, 4f)] private float dashTilingX = 0.25f;

    [Header("Auto-find / Player Tag")]
    [SerializeField] private string playerTag = "Player";

    // ===== Internal =====
    private readonly List<Group> _groups = new();
    private readonly Dictionary<Vector3Int, int> _cellToGroup = new();

    private readonly Dictionary<Tilemap, Color> _baseColors = new();

    private Collider2D[] _blackCols;
    private Collider2D[] _whiteCols;

    private Material _dashMatInstance;
    private Tween _dashTween;
    private float _dashOffsetX;

    private Collider2D[] _overlapTmp; // optional future use

    private void OnEnable()
    {
        WorldShiftManager.OnWorldChanged += OnWorldChanged;
    }

    private void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= OnWorldChanged;
    }

    private void OnDestroy()
    {
        _dashTween?.Kill();
        _dashTween = null;

        if (_dashMatInstance != null)
        {
            Destroy(_dashMatInstance);
            _dashMatInstance = null;
        }
    }

    private void Start()
    {
        AutoFindIfMissing();

        if (markerTilemap == null)
        {
            Debug.LogError("[QuantumPass] Missing markerTilemap (Tilemap_QuantumPass).");
            enabled = false;
            return;
        }

        if (paintTile == null)
        {
            Debug.LogError("[QuantumPass] Missing paintTile (a 1x1 TileBase).");
            enabled = false;
            return;
        }

        BuildGroupsFromMarker();
        RepaintAllGroups();

        if (buildOutline)
            BuildOutlines();

        CacheBaseColorsAndColliders();

        // Apply visuals for current world
        var w = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        OnWorldChanged(w);

        // Initialize inside flags without starting countdowns
        RefreshInsideStatesNoCountdown();
    }

    private void FixedUpdate()
    {
        TickRuntime();
    }
}
