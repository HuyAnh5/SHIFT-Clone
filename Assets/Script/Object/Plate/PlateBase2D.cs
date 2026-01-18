using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public abstract class PlateBase2D : MonoBehaviour
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

    protected virtual void FixedUpdate()
    {
        if (!activeInWorld) return;

        if (Time.time < nextHeartbeatTime) return;
        nextHeartbeatTime = Time.time + Mathf.Max(0.02f, heartbeatInterval);

        bool changed = false;

        // 1) Heartbeat rebuild occupancy to recover from missed TriggerEnter/Exit
        {
            bool beforeAny = HasOccupant;
            bool beforePlayer = HasOccupantKind(OccupantKind.Player);
            bool beforeSwap = HasOccupantKind(OccupantKind.SwapBlock);

            RebuildOccupancySilently();

            bool afterAny = HasOccupant;
            bool afterPlayer = HasOccupantKind(OccupantKind.Player);
            bool afterSwap = HasOccupantKind(OccupantKind.SwapBlock);

            if (beforeAny != afterAny || beforePlayer != afterPlayer || beforeSwap != afterSwap)
            {
                changed = true;
                if (debugPlate)
                    PlateDbg($"HeartbeatRebuild changed: any {beforeAny}->{afterAny}, player {beforePlayer}->{afterPlayer}, swap {beforeSwap}->{afterSwap}");
            }

            // If we definitely see a SwapBlock occupant, latch it immediately.
            if (keepSwapBlockConditionAcrossWorlds && afterSwap)
            {
                swapLatchLastSeenTime = Time.time;
                swapLatchMissCount = 0;
                if (!swapBlockLatched)
                {
                    swapBlockLatched = true;
                    changed = true;
                }
            }
        }

        // 2) SwapBlock latch hysteresis (don’t clear because of 1 bad frame)
        if (keepSwapBlockConditionAcrossWorlds)
        {
            bool seeSwapNow = ProbeHasKindNow(OccupantKind.SwapBlock) || ProbeHasKindBox(OccupantKind.SwapBlock);

            if (seeSwapNow)
            {
                swapLatchLastSeenTime = Time.time;
                swapLatchMissCount = 0;
                if (!swapBlockLatched)
                {
                    swapBlockLatched = true;
                    changed = true;
                }
            }
            else if (swapBlockLatched)
            {
                swapLatchMissCount++;
                bool timeExpired = (Time.time - swapLatchLastSeenTime) >= Mathf.Max(0.02f, swapLatchClearGraceSeconds);
                bool missExpired = swapLatchMissCount >= Mathf.Max(1, swapLatchClearMissFrames);

                if (timeExpired || missExpired)
                {
                    // Double-confirm with Box probe before clearing.
                    bool confirmSwap = ProbeHasKindBox(OccupantKind.SwapBlock);
                    if (!confirmSwap)
                    {
                        swapBlockLatched = false;
                        swapLatchMissCount = 0;
                        changed = true;
                    }
                    else
                    {
                        swapLatchLastSeenTime = Time.time;
                        swapLatchMissCount = 0;
                    }
                }
            }
        }

        if (changed)
            OnOccupancyChanged();
    }

    private void SetupProbeBoxFilter()
    {
        probeBoxFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = (sensor != null) ? sensor.DetectMask : (LayerMask)~0,
            useTriggers = false
        };
    }

    protected void PlateDbg(string msg)
    {
        if (!debugPlate) return;
        Debug.Log($"[Plate] {name} owner={ownerWorld} active={activeInWorld} on={IsOn} latched={swapBlockLatched} :: {msg}", this);
    }

    private string DescribeHits(List<Collider2D> hits)
    {
        if (hits == null) return "null";
        if (hits.Count == 0) return "0";

        int p = 0, s = 0, other = 0;
        for (int i = 0; i < hits.Count; i++)
        {
            if (TryGetOccupantKey(hits[i], out _, out OccupantKind k))
            {
                if (k == OccupantKind.Player) p++;
                else if (k == OccupantKind.SwapBlock) s++;
                else other++;
            }
            else other++;
        }
        return $"{hits.Count} (Player:{p}, Swap:{s}, Other:{other})";
    }


    private void CacheRenderers()
    {
        if (!autoCollectChildRenderers && explicitRenderers != null && explicitRenderers.Length > 0)
        {
            cachedRenderers = explicitRenderers;
            return;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
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

    private void HandlePreWorldChange(WorldState fromWorld, WorldState toWorld)
    {
        // We only need a snapshot when THIS plate is currently active and is about to become inactive.
        if (!activeInWorld) return;
        if (fromWorld != ownerWorld) return;
        if (toWorld == ownerWorld) return;

        Physics2D.SyncTransforms();

        bool hadPlayer =
            HasOccupantKind(OccupantKind.Player) ||
            ProbeHasKindNow(OccupantKind.Player) ||
            ProbeHasKindBox(OccupantKind.Player);

        bool hadSwap =
            HasOccupantKind(OccupantKind.SwapBlock) ||
            ProbeHasKindNow(OccupantKind.SwapBlock) ||
            ProbeHasKindBox(OccupantKind.SwapBlock);

        hasPreShiftSnapshot = true;
        preShiftHadPlayer = hadPlayer;
        preShiftHadSwap = hadSwap;

        if (keepSwapBlockConditionAcrossWorlds && hadSwap)
        {
            swapBlockLatched = true;
            swapLatchLastSeenTime = Time.time;
            swapLatchMissCount = 0;
        }

        if (debugPlate)
            PlateDbg($"PRE-SHIFT snapshot -> hadPlayer={hadPlayer}, hadSwap={hadSwap}, latched={swapBlockLatched}");
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

    private void ApplyOwnerRotationOnce()
    {
        if (!rotateByOwnerWorld) return;
        if (rotationTarget == null) rotationTarget = transform;

        float z = (ownerWorld == WorldState.White) ? whiteWorldZRotation : 0f;
        rotationTarget.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    private void ApplyWorld(WorldState solidWorld)
    {
        bool newActive = (solidWorld == ownerWorld);
        if (newActive == activeInWorld) return;

        // snapshot occupancy BEFORE toggling components
        if (!newActive)
        {
            // Prefer the pre-shift snapshot (taken BEFORE any collider gets disabled)
            if (hasPreShiftSnapshot)
            {
                deactivatedHadPlayer = preShiftHadPlayer;
                deactivatedHadSwapBlock = preShiftHadSwap;
                hasPreShiftSnapshot = false;

                if (keepSwapBlockConditionAcrossWorlds && deactivatedHadSwapBlock)
                {
                    swapBlockLatched = true;
                    swapLatchLastSeenTime = Time.time;
                    swapLatchMissCount = 0;
                }

                if (debugPlate)
                    PlateDbg($"DEACTIVATE snapshot (PRE) -> hadPlayer={deactivatedHadPlayer}, hadSwap={deactivatedHadSwapBlock}, latched={swapBlockLatched}");
            }
            else
            {
                // Fallback: Double-probe (NOW + BOX) để tránh miss khi vừa Swap/teleport hoặc enable/disable collider
                bool probeNowPlayer = ProbeHasKindNow(OccupantKind.Player);
                bool probeBoxPlayer = ProbeHasKindBox(OccupantKind.Player);

                bool probeNowSwap = ProbeHasKindNow(OccupantKind.SwapBlock);
                bool probeBoxSwap = ProbeHasKindBox(OccupantKind.SwapBlock);

                deactivatedHadPlayer =
                    HasOccupantKind(OccupantKind.Player) || probeNowPlayer || probeBoxPlayer;

                deactivatedHadSwapBlock =
                    HasOccupantKind(OccupantKind.SwapBlock) || probeNowSwap || probeBoxSwap;

                if (keepSwapBlockConditionAcrossWorlds && deactivatedHadSwapBlock)
                {
                    swapBlockLatched = true;
                    swapLatchLastSeenTime = Time.time;
                    swapLatchMissCount = 0;
                }

                if (debugPlate)
                {
                    PlateDbg(
                        $"DEACTIVATE snapshot (FALLBACK): hadPlayer={deactivatedHadPlayer} (occ={HasOccupantKind(OccupantKind.Player)} now={probeNowPlayer} box={probeBoxPlayer}) | " +
                        $"hadSwap={deactivatedHadSwapBlock} (occ={HasOccupantKind(OccupantKind.SwapBlock)} now={probeNowSwap} box={probeBoxSwap}) | " +
                        $"-> latched={swapBlockLatched}"
                    );
                }
            }
        }

        // IMPORTANT: toggle components FIRST để tránh "lòi"/render sai world khi đang tween.
        activeInWorld = newActive;
        ApplyWorldActiveToComponents(activeInWorld);
        OnWorldActiveChanged(activeInWorld);

        if (!activeInWorld)
        {
            if (reactivationRoutine != null)
            {
                StopCoroutine(reactivationRoutine);
                reactivationRoutine = null;
            }

            OnBecameInactiveWorld();
            KillMoveTweenOnly();
            ClearOccupantsSilently();

            if (debugPlate) PlateDbg("DEACTIVATE done.");
            return;
        }

        // ACTIVE AGAIN
        KillMoveTweenOnly();
        ClearOccupantsSilently();

        if (reactivationRoutine != null)
        {
            StopCoroutine(reactivationRoutine);
            reactivationRoutine = null;
        }

        if (IsSwapBlockLatched)
        {
            RecomputePressedPos();
            rb.position = pressedPos;
            SetOn(true, silent: true);

            if (debugPlate) PlateDbg("REACTIVATE: latched -> snap pressed + SetOn(true,silent)");
        }

        // Fast path rebuild
        RebuildOccupancySilently();

        if (keepSwapBlockConditionAcrossWorlds && HasOccupantKind(OccupantKind.SwapBlock))
        {
            swapBlockLatched = true;
            swapLatchLastSeenTime = Time.time;
            swapLatchMissCount = 0;
        }

        if (debugPlate)
            PlateDbg($"REACTIVATE fastRebuild: hasOcc={HasOccupant} occSwap={HasOccupantKind(OccupantKind.SwapBlock)} latched={swapBlockLatched}");

        if (HasOccupant)
        {
            OnOccupancyChanged();
        }
        else
        {
            reactivationRoutine = StartCoroutine(CoRebuildAfterReactivation());
        }
    }



    /// <summary>
    /// Defer occupancy rebuild a short time after reactivation.
    /// This prevents the "reset then re-press" flicker when SwapBlock collider becomes active slightly later.
    /// </summary>
    private IEnumerator CoRebuildAfterReactivation()
    {
        const int maxAttempts = 30;

        if (debugPlate) PlateDbg("Reactivation deferred rebuild START");

        for (int i = 0; i < maxAttempts; i++)
        {
            yield return new WaitForFixedUpdate();
            if (!activeInWorld) yield break;

            ClearOccupantsSilently();
            RebuildOccupancySilently();

            if (debugPlate && debugProbeHits)
                PlateDbg($"Deferred attempt {i + 1}/{maxAttempts}: hasOcc={HasOccupant} occSwap={HasOccupantKind(OccupantKind.SwapBlock)}");

            if (HasOccupant)
                break;
        }

        if (!activeInWorld) yield break;

        // Final latch settle: chỉ SET TRUE nếu thấy SwapBlock, không overwrite về FALSE (anti miss)
        if (keepSwapBlockConditionAcrossWorlds)
        {
            bool seenSwap = HasOccupantKind(OccupantKind.SwapBlock) || ProbeHasKindBox(OccupantKind.SwapBlock);
            if (seenSwap)
            {
                swapBlockLatched = true;
                swapLatchLastSeenTime = Time.time;
                swapLatchMissCount = 0;
            }
        }
        else
        {
            swapBlockLatched = false;
            swapLatchMissCount = 0;
        }

        if (debugPlate) PlateDbg($"Reactivation deferred rebuild END -> latched={swapBlockLatched}");

        OnOccupancyChanged();
        reactivationRoutine = null;
    }



    private void ApplyWorldActiveToComponents(bool isActive)
    {
        if (solidCollider != null) solidCollider.enabled = isActive;
        if (sensor != null) sensor.SetEnabled(isActive);

        // Robust: toggle ALL child renderers (fix "plate vẫn thấy ở world kia")
        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] == null) continue;
                cachedRenderers[i].enabled = isActive;
            }
        }
    }

    /// <summary>
    /// Hook cho derived (vd TimedPlate hide/show ring canvas).
    /// Gọi ngay sau khi plate bật/tắt theo world.
    /// </summary>
    protected virtual void OnWorldActiveChanged(bool isActive) { }

    // ===== Press direction theo VIEW (camera/gravity flip), không liên quan rotation owner world =====
    protected Vector2 GetPressDirection()
    {
        bool viewFlipped =
            (CameraFlip2D.I != null && CameraFlip2D.I.IsViewFlipped) ||
            (WorldShiftManager.I != null && WorldShiftManager.I.IsViewFlipped);

        // viewFlipped => “down on screen” là world UP
        return viewFlipped ? Vector2.up : Vector2.down;
    }

    protected void RecomputePressedPos()
    {
        lastPressDir = GetPressDirection();
        pressedPos = basePos + lastPressDir * Mathf.Abs(sinkDistance);
    }

    protected virtual void OnBecameInactiveWorld()
    {
        // default: không làm gì.
        // HoldPlate/TimedPlate override để reset state hoặc start countdown theo spec.
    }

    protected bool IsFullyPressed()
    {
        return Vector2.Distance(rb.position, pressedPos) <= epsilon;
    }

    // ===== Occupant resolve =====
    private bool TryGetOccupantKey(Collider2D other, out int key, out OccupantKind kind)
    {
        key = 0;
        kind = OccupantKind.Player;
        if (other == null) return false;

        // Plate chỉ tính default collider (NOT trigger)
        if (other.isTrigger) return false;

        var player = other.GetComponentInParent<PlayerController>();
        if (player != null) { key = player.GetInstanceID(); kind = OccupantKind.Player; return true; }

        var swap = other.GetComponentInParent<SwapBlock2D>();
        if (swap != null) { key = swap.GetInstanceID(); kind = OccupantKind.SwapBlock; return true; }

        return false;
    }

    public void NotifyEnter(Collider2D other)
    {
        if (!activeInWorld) return;
        if (!TryGetOccupantKey(other, out int key, out OccupantKind kind)) return;

        if (occupants.TryGetValue(key, out OccupantInfo info))
        {
            info.refCount += 1;
            occupants[key] = info;
        }
        else
        {
            occupants[key] = new OccupantInfo { refCount = 1, kind = kind };
            if (keepSwapBlockConditionAcrossWorlds && kind == OccupantKind.SwapBlock)
            {
                swapBlockLatched = true;
                swapLatchLastSeenTime = Time.time;
                swapLatchMissCount = 0;
            }
            OnOccupancyChanged();
        }
    }

    public void NotifyExit(Collider2D other)
    {
        if (!activeInWorld) return;
        if (!TryGetOccupantKey(other, out int key, out OccupantKind kind)) return;

        if (!occupants.TryGetValue(key, out OccupantInfo info)) return;

        info.refCount -= 1;
        if (info.refCount <= 0)
        {
            occupants.Remove(key);

            // IMPORTANT: do NOT clear swap latch immediately on Exit (Exit can be caused by collider disable during SHIFT).
            // Latch will be cleared by heartbeat hysteresis after N misses / grace time.
            if (keepSwapBlockConditionAcrossWorlds && kind == OccupantKind.SwapBlock)
            {
                // start miss window now; heartbeat will confirm and clear if truly gone.
                swapLatchMissCount = 0;
            }

            OnOccupancyChanged();
        }
        else
        {
            occupants[key] = info;
        }
    }

    private bool ProbeHasKindNow(OccupantKind kindWanted)
    {
        if (sensor == null) return false;

        var hits = sensor.OverlapNow();
        if (debugPlate && debugProbeHits)
            PlateDbg($"ProbeNow want={kindWanted} hits={DescribeHits(hits)}");

        for (int i = 0; i < hits.Count; i++)
        {
            if (!TryGetOccupantKey(hits[i], out _, out OccupantKind kind))
                continue;

            if (kind == kindWanted)
                return true;
        }
        return false;
    }

    private bool ProbeHasKindBox(OccupantKind kindWanted)
    {
        if (sensor == null) return false;

        // Đảm bảo teleport/Swap được sync vào physics trước khi query
        Physics2D.SyncTransforms();

        Bounds b = sensor.WorldBounds;

        probeBoxResults.Clear();
        Physics2D.OverlapBox((Vector2)b.center, (Vector2)b.size, 0f, probeBoxFilter, probeBoxResults);

        if (debugPlate && debugProbeHits)
            PlateDbg($"ProbeBox want={kindWanted} hits={DescribeHits(probeBoxResults)} boundsCenter={b.center} size={b.size}");

        for (int i = 0; i < probeBoxResults.Count; i++)
        {
            if (!TryGetOccupantKey(probeBoxResults[i], out _, out OccupantKind kind))
                continue;

            if (kind == kindWanted)
                return true;
        }
        return false;
    }


    /// <summary>
    /// TRUE nếu điều kiện plate đang được "giữ" bởi SwapBlock xuyên world (dù plate inactive).
    /// </summary>
    protected bool IsSwapBlockLatched => keepSwapBlockConditionAcrossWorlds && swapBlockLatched;

    protected bool HasOccupant => occupants.Count > 0;

    protected bool HasOccupantKind(OccupantKind kind)
    {
        foreach (var kv in occupants)
        {
            if (kv.Value.kind == kind) return true;
        }
        return false;
    }

    protected void ClearOccupantsSilently()
    {
        occupants.Clear();
    }

    protected void RebuildOccupancySilently()
    {
        occupants.Clear();
        if (sensor == null) return;

        var hits = sensor.OverlapNow();
        for (int i = 0; i < hits.Count; i++)
        {
            var c = hits[i];
            if (!TryGetOccupantKey(c, out int key, out OccupantKind kind)) continue;

            if (occupants.TryGetValue(key, out OccupantInfo info))
            {
                info.refCount += 1;
                occupants[key] = info;
            }
            else
            {
                occupants[key] = new OccupantInfo { refCount = 1, kind = kind };
            }
        }
    }

    protected void SetOn(bool value, bool silent = false)
    {
        if (IsOn == value) return;
        IsOn = value;
        if (!silent) OnStateChanged?.Invoke(this, IsOn);
    }

    // ===== DOTween helpers =====
    protected virtual void KillAllTweens()
    {
        moveTween?.Kill();
        moveTween = null;
    }

    protected void TweenMoveTo(Vector2 targetPos, float duration, float delay, Ease ease)
    {
        moveTween?.Kill();
        moveTween = rb.DOMove(targetPos, duration)
            .SetEase(ease)
            .SetDelay(delay)
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject);
    }

    protected void KillMoveTweenOnly()
    {
        moveTween?.Kill();
        moveTween = null;
    }

    protected void TweenPressToBottom()
    {
        RecomputePressedPos();
        TweenMoveTo(pressedPos, pressDuration, pressDelay, pressEase);
    }

    protected void TweenRaiseToTop(float duration, float delay = 0f, Ease? easeOverride = null)
    {
        TweenMoveTo(basePos, duration, delay, easeOverride ?? raiseEase);
    }

    protected abstract void OnOccupancyChanged();
}
