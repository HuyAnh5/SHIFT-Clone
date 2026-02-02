using System.Collections.Generic;
using UnityEngine;

public abstract partial class PlateBase2D
{
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
}
