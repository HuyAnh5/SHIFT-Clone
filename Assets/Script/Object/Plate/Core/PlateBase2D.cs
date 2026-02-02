using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public abstract partial class PlateBase2D : MonoBehaviour
{
    [Header("World Owner")]
    [SerializeField] private WorldState ownerWorld = WorldState.Black;
    public WorldState OwnerWorld => ownerWorld;

    [Header("Refs")]
    [SerializeField] protected Rigidbody2D rb;
    [SerializeField] protected BoxCollider2D solidCollider;     // nền đứng (NOT trigger)
    [SerializeField] protected PlateSensor2D sensor;            // child trigger
    [Tooltip("Nếu để trống, script sẽ auto-disable tất cả Renderer con khi plate inactive world.")]
    [SerializeField] protected SpriteRenderer visualRenderer;   // optional (legacy)

    [Header("World-specific Visual Toggle")]
    [Tooltip("Auto cache tất cả Renderer con (SpriteRenderer, etc.) để bật/tắt khi SHIFT world.")]
    [SerializeField] private bool autoCollectChildRenderers = true;
    [Tooltip("Nếu muốn chỉ toggle 1 số renderer nhất định, kéo vào đây và tắt autoCollectChildRenderers.")]
    [SerializeField] private Renderer[] explicitRenderers;

    [Header("Rotation (fixed by OWNER world, NOT by shift)")]
    [SerializeField] private bool rotateByOwnerWorld = true;
    [Tooltip("Nếu trống sẽ xoay ROOT (transform).")]
    [SerializeField] private Transform rotationTarget;
    [SerializeField] private float whiteWorldZRotation = 180f;

    [Header("Press Motion (DOTween)")]
    [SerializeField] protected float sinkDistance = 0.12f;
    [SerializeField] protected float pressDelay = 0.05f;
    [SerializeField] protected float pressDuration = 0.25f;
    [SerializeField] protected float raiseDuration = 0.15f;
    [SerializeField] protected Ease pressEase = Ease.InOutSine;
    [SerializeField] protected Ease raiseEase = Ease.InOutSine;
    [SerializeField] protected float epsilon = 0.002f;

    public bool IsOn { get; protected set; }
    public event Action<PlateBase2D, bool> OnStateChanged;

    protected bool activeInWorld;

    // snapshot khi plate bị tắt do SHIFT (để derived xử lý đúng: HoldPlate reset, TimedPlate start countdown...)
    protected bool deactivatedHadPlayer;
    protected bool deactivatedHadSwapBlock;

    [Header("Cross-world Condition")]
    [Tooltip("Nếu TRUE: SwapBlock đặt lên plate sẽ vẫn giữ điều kiện ON xuyên SHIFT (dùng cho puzzle/Exit).")]
    [SerializeField] protected bool keepSwapBlockConditionAcrossWorlds = true;
    protected bool swapBlockLatched;

    // Cached visuals
    private Renderer[] cachedRenderers;

    protected Vector2 basePos;     // vị trí "trồi lên"
    protected Vector2 pressedPos;  // vị trí "chìm hết"
    protected Vector2 lastPressDir;

    [Header("Latch Probe (optional)")]
    [Tooltip("Heartbeat interval (giây) để chống miss trigger khi enable/disable, SHIFT, teleport, swap, v.v.")]
    [SerializeField] private float heartbeatInterval = 0.10f; // 0.1s đủ nhẹ + chắc
    private float nextHeartbeatTime = 0f;

    [Header("SwapBlock latch stability (anti 1-frame miss)")]
    [Tooltip("Số lần heartbeat liên tiếp KHÔNG thấy SwapBlock thì mới clear latch.")]
    [SerializeField] private int swapLatchClearMissFrames = 3;
    [Tooltip("Grace time (giây) trước khi clear latch. Dùng cùng missFrames; cái nào đến trước thì clear.")]
    [SerializeField] private float swapLatchClearGraceSeconds = 0.12f;

    private float swapLatchLastSeenTime = -999f;
    private int swapLatchMissCount = 0;

    // Pre-SHIFT snapshot (chốt trước khi collider bị tắt). Fix bug: đặt block rồi SHIFT lần đầu bị miss.
    private bool hasPreShiftSnapshot;
    private bool preShiftHadPlayer;
    private bool preShiftHadSwap;

    [Header("Debug")]
    [SerializeField] private bool debugPlate = false;
    [SerializeField] private bool debugProbeHits = false;

    private ContactFilter2D probeBoxFilter;
    private readonly List<Collider2D> probeBoxResults = new(16);

    // ===== Occupancy tracking =====
    // Track theo OBJECT (PlayerController/SwapBlock2D) để không kẹt khi nhiều collider.
    protected enum OccupantKind { Player, SwapBlock }

    protected struct OccupantInfo
    {
        public int refCount;
        public OccupantKind kind;
    }

    private readonly Dictionary<int, OccupantInfo> occupants = new();

    // DOTween move tween (platform movement)
    protected Tween moveTween;

    // When a world becomes active, other world-specific objects (e.g., SwapBlock) might enable their
    // colliders a moment later (subscriber order / physics sync). We defer occupancy rebuild briefly to
    // avoid a one-frame "reset" where the plate raises and pushes the block.
    private Coroutine reactivationRoutine;

    protected virtual void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (solidCollider == null) solidCollider = GetComponent<BoxCollider2D>();
        if (sensor == null) sensor = GetComponentInChildren<PlateSensor2D>(true);
        if (visualRenderer == null) visualRenderer = GetComponentInChildren<SpriteRenderer>(true);

        CacheRenderers();

        if (rotationTarget == null) rotationTarget = transform;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        basePos = rb.position;

        if (sensor != null) sensor.Bind(this);
        SetupProbeBoxFilter();

        ApplyOwnerRotationOnce();
        ApplyWorld(WorldShiftManager.I != null ? WorldShiftManager.I.SolidWorld : ownerWorld);
    }

    protected virtual void OnEnable()
    {
        WorldShiftManager.OnWorldChanged += ApplyWorld;
        WorldShiftManager.OnPreWorldChange += HandlePreWorldChange;
    }

    protected virtual void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= ApplyWorld;
        WorldShiftManager.OnPreWorldChange -= HandlePreWorldChange;
    }

    public void InitializeAt(Vector2 worldPos, WorldState world)
    {
        ownerWorld = world;

        rb.position = worldPos;
        transform.position = worldPos;
        basePos = worldPos;

        ApplyOwnerRotationOnce();
        ApplyWorld(WorldShiftManager.I != null ? WorldShiftManager.I.SolidWorld : ownerWorld);
    }

    public void SetOwnerWorld(WorldState world)
    {
        ownerWorld = world;
        ApplyOwnerRotationOnce();
        ApplyWorld(WorldShiftManager.I != null ? WorldShiftManager.I.SolidWorld : ownerWorld);
    }

    protected abstract void OnOccupancyChanged();
}
