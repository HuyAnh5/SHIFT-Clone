using System.Collections;
using UnityEngine;

public abstract partial class PlateBase2D
{
    private void CacheRenderers()
    {
        if (!autoCollectChildRenderers && explicitRenderers != null && explicitRenderers.Length > 0)
        {
            cachedRenderers = explicitRenderers;
            return;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
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

    protected virtual void OnBecameInactiveWorld()
    {
        // default: không làm gì.
        // HoldPlate/TimedPlate override để reset state hoặc start countdown theo spec.
    }
}
