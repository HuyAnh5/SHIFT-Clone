using System.Collections;
using UnityEngine;
using DG.Tweening;

public partial class PlayerController
{
    private void TryStartShift()
    {
        if (shifting || cd > 0f) return;

        if (requireGroundedToShift)
        {
            if (!CanStartShiftFromEdge())
            {
                failShake?.ShakeFail();
                return;
            }
        }

        if (blockShiftWhenStandingOnWall && IsStandingOnWall())
        {
            failShake?.ShakeFail();
            return;
        }

        DoShift();
    }

    private void DoShift()
    {
        if (WorldShiftManager.I == null) return;

        coyoteTimer = 0f;

        shifting = true;
        cd = shiftCooldown;
        shiftFailsafeTimer = 0f;

        shiftTween?.Kill();
        shiftTween = null;

        if (finishShiftRoutine != null)
        {
            StopCoroutine(finishShiftRoutine);
            finishShiftRoutine = null;
        }

        shiftBeforeWorld = WorldShiftManager.I.SolidWorld;
        shiftBeforeGravityScale = rb.gravityScale;
        shiftBeforePos = rb.position;
        shiftBeforeBodyType = rb.bodyType;

        Vector2 oldGravityDir = GravityDown;
        float push = ComputePassDistance(oldGravityDir);

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        box.isTrigger = true;

        WorldShiftManager.I.Toggle();
        rb.gravityScale = -shiftBeforeGravityScale;

        shiftTween = rb.DOMove(shiftBeforePos + oldGravityDir * push, shiftAnimDuration * 2f)
            .SetEase(Ease.InOutSine)
            .SetUpdate(UpdateType.Fixed)
            .OnComplete(() => { EndShiftAndCheckOverlap(); });
    }

    private void EndShiftAndCheckOverlap()
    {
        box.isTrigger = false;
        rb.bodyType = shiftBeforeBodyType;

        Physics2D.SyncTransforms();

        finishShiftRoutine = StartCoroutine(FinishShift(shiftBeforeWorld, shiftBeforeGravityScale, shiftBeforePos, shiftBeforeBodyType));
    }

    private void ForceFinishShiftNow()
    {
        if (!shifting) return;

        shiftTween?.Kill();
        shiftTween = null;

        EndShiftAndCheckOverlap();
    }

    private IEnumerator FinishShift(WorldState beforeWorld, float beforeGravityScale, Vector2 beforePos, RigidbodyType2D beforeBodyType)
    {
        if (delayOverlapCheckOneFixed)
            yield return new WaitForFixedUpdate();

        Physics2D.SyncTransforms();

        if (IsOverlappingSolid())
        {
            bool resolved = false;
            if (resolveInsteadOfRollback)
                resolved = TryResolveOverlap();

            if (!resolved && rollbackIfStuck)
                RollbackShift(beforeWorld, beforeGravityScale, beforePos, beforeBodyType);
        }

        shifting = false;
        shiftFailsafeTimer = 0f;
        finishShiftRoutine = null;
    }

    private float ComputePassDistance(Vector2 oldGravityDir)
    {
        Vector2 center = box.bounds.center;
        float extY = box.bounds.extents.y;

        float rayDist = extY + 2f;
        RaycastHit2D hit = Physics2D.Raycast(center, oldGravityDir, rayDist, solidMask);

        if (hit.collider != null)
            return hit.distance + extY + passExtra;

        return extY * 2f + passExtra;
    }

    private bool IsSupportedByRays()
    {
        float support = GetGroundSupportFraction(Mathf.Max(3, edgeSupportRays), out _, out _);
        return support > 0f;
    }

    private float GetGroundSupportFraction(int rays, out int leftHits, out int rightHits)
    {
        rays = Mathf.Clamp(rays, 3, 21);
        leftHits = 0;
        rightHits = 0;

        Bounds b = box.bounds;
        Vector2 dir = GravityDown;

        float footY = (GravitySign > 0f) ? b.min.y : b.max.y;

        float margin = Mathf.Clamp01(edgeRayMargin);
        float xMin = Mathf.Lerp(b.min.x, b.max.x, margin);
        float xMax = Mathf.Lerp(b.max.x, b.min.x, margin);

        float dist = groundCheckExtra + 0.12f;
        const float inset = 0.02f;

        int hits = 0;
        int mid = rays / 2;

        for (int i = 0; i < rays; i++)
        {
            float t = (float)i / (rays - 1);
            float x = Mathf.Lerp(xMin, xMax, t);
            Vector2 origin = new Vector2(x, footY) - dir * inset;

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, solidMask);
            if (!hit.collider) continue;

            float dotUp = Vector2.Dot(hit.normal, GravityUp);
            if (dotUp < groundNormalThreshold) continue;
            if (Mathf.Abs(hit.normal.x) > groundMaxNormalX) continue;
            if (hit.distance <= 0.0001f) continue;

            hits++;

            if (i < mid) leftHits++;
            else if (i > mid) rightHits++;
            else { leftHits++; rightHits++; }
        }

        return hits / (float)rays;
    }

    private bool CanStartShiftFromEdge()
    {
        float minSupport = Mathf.Clamp01(1f - maxOverhangFractionToShift);
        float support = GetGroundSupportFraction(edgeSupportRays, out int leftHits, out int rightHits);

        if (support <= 0f) return false;
        if (support < minSupport) return false;

        if (nudgeBackBeforeShift && support < 0.999f)
        {
            Vector2 nudgeDir;
            if (rightHits < leftHits) nudgeDir = Vector2.left;
            else if (leftHits < rightHits) nudgeDir = Vector2.right;
            else nudgeDir = Vector2.zero;

            if (nudgeDir != Vector2.zero)
            {
                float moved = 0f;
                float bestSupport = support;

                while (moved < nudgeMaxDistance)
                {
                    if (!CanCastMove(nudgeDir, nudgeStep)) break;

                    rb.position += nudgeDir * nudgeStep;
                    moved += nudgeStep;

                    Physics2D.SyncTransforms();

                    float newSupport = GetGroundSupportFraction(edgeSupportRays, out _, out _);
                    if (newSupport > bestSupport + 0.001f) bestSupport = newSupport;

                    if (newSupport >= 0.999f) break;
                    if (newSupport + 0.001f < bestSupport) break;
                }
            }
        }

        return true;
    }

    private bool CanCastMove(Vector2 dir, float dist)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = solidMask, useTriggers = false };
        RaycastHit2D[] hits = new RaycastHit2D[8];
        return box.Cast(dir, filter, hits, dist) == 0;
    }

    private bool IsStandingOnWall()
    {
        if (wallMask.value == 0) return false;

        int rays = Mathf.Max(3, edgeSupportRays);
        Bounds b = box.bounds;
        Vector2 dir = GravityDown;
        float footY = (GravitySign > 0f) ? b.min.y : b.max.y;

        float margin = Mathf.Clamp01(edgeRayMargin);
        float xMin = Mathf.Lerp(b.min.x, b.max.x, margin);
        float xMax = Mathf.Lerp(b.max.x, b.min.x, margin);

        float dist = groundCheckExtra + 0.12f;
        const float inset = 0.02f;

        for (int i = 0; i < rays; i++)
        {
            float t = (rays == 1) ? 0.5f : (float)i / (rays - 1);
            float x = Mathf.Lerp(xMin, xMax, t);
            Vector2 origin = new Vector2(x, footY) - dir * inset;

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, wallMask);
            if (hit.collider != null)
                return true;
        }

        return false;
    }

    private bool TryGetPenetrationVector(out Vector2 pushOut)
    {
        pushOut = Vector2.zero;

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = solidMask, useTriggers = false };
        Collider2D[] hits = new Collider2D[24];
        int count = box.Overlap(filter, hits);

        bool any = false;

        for (int i = 0; i < count; i++)
        {
            Collider2D other = hits[i];
            if (other == null || other == box) continue;
            if (other.attachedRigidbody != null && other.attachedRigidbody == rb) continue;

            ColliderDistance2D d = Physics2D.Distance(box, other);
            if (!d.isOverlapped) continue;

            float penetration = -d.distance;
            if (penetration <= stuckPenetrationEpsilon) continue;

            pushOut += (-d.normal) * (penetration + resolveSkin);
            any = true;
        }

        return any;
    }

    private bool IsOverlappingSolid()
    {
        return TryGetPenetrationVector(out _);
    }

    private bool TryResolveOverlap()
    {
        for (int iter = 0; iter < resolveIterations; iter++)
        {
            if (!TryGetPenetrationVector(out Vector2 push))
                return true;

            if (push.sqrMagnitude < 1e-8f)
                break;

            push = Vector2.ClampMagnitude(push, resolveMaxStep);
            rb.position += push;

            Physics2D.SyncTransforms();
        }

        return !TryGetPenetrationVector(out _);
    }

    private void RollbackShift(WorldState beforeWorld, float beforeGravityScale, Vector2 beforePos, RigidbodyType2D beforeBodyType)
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        box.isTrigger = true;

        if (WorldShiftManager.I != null)
            WorldShiftManager.I.SetWorld(beforeWorld);

        rb.gravityScale = beforeGravityScale;
        rb.position = beforePos;
        rb.linearVelocity = Vector2.zero;

        Physics2D.SyncTransforms();

        box.isTrigger = false;
        rb.bodyType = beforeBodyType;
    }

    private void HardResetShiftState()
    {
        shiftTween?.Kill();
        shiftTween = null;

        if (finishShiftRoutine != null)
        {
            StopCoroutine(finishShiftRoutine);
            finishShiftRoutine = null;
        }

        shifting = false;
        shiftFailsafeTimer = 0f;

        if (box != null) box.isTrigger = false;
        if (rb != null && rb.bodyType != RigidbodyType2D.Dynamic)
            rb.bodyType = RigidbodyType2D.Dynamic;
    }

    private void ForceCancelShiftForReload()
    {
        HardResetShiftState();

        if (visualRoot != null)
            visualRoot.localScale = baseVisualScale;

        squashTween?.Kill();
        squashTween = null;
    }
}
