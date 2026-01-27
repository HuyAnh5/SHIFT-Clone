using System.Collections;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 80f;
    [SerializeField] private float deceleration = 90f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    private bool jumpQueued;
    private bool jumpAvailable = true; // anti double-impulse when spamming/buffer

    [Header("Jump Buffer")]
    [Tooltip("If you press jump slightly BEFORE landing, jump will trigger as soon as you're allowed to jump (ground/coyote).")]
    [SerializeField] private float jumpBufferTime = 0.10f;
    private float jumpBufferTimer;

    [Header("Celeste Feel - Input Forgiveness")]
    [Tooltip("Still can jump for this long after leaving ground.")]
    [SerializeField] private float coyoteTime = 0.10f;

    [Header("Ground Filter (anti wall-ray)")]
    [SerializeField, Range(0f, 1f)] private float groundMaxNormalX = 0.2f; // tile vuông: 0.1–0.25


    [Header("Physics / Contact")]
    [SerializeField] private LayerMask solidMask = ~0;
    [SerializeField] private LayerMask wallMask = 0;

    [Tooltip("Dot threshold to consider contact as ground (normal vs up relative to gravity).")]
    [SerializeField, Range(0f, 1f)] private float groundNormalThreshold = 0.55f;

    [Tooltip("Dot threshold to consider contact as wall (normal vs left/right).")]
    [SerializeField, Range(0f, 1f)] private float wallNormalThreshold = 0.75f;

    [Header("Clamp Vertical Speed (optional)")]
    [SerializeField] private bool clampVerticalSpeed = true;
    [SerializeField] private float maxFallSpeed = 22f;
    [SerializeField] private float maxRiseSpeed = 40f;

    [Header("Shift (core)")]
    [SerializeField] private KeyCode shiftKey = KeyCode.LeftShift;
    [SerializeField] private float shiftCooldown = 0.12f;

    [Tooltip("Use the same duration as CameraFlip2D.flipDuration for best feel.")]
    [SerializeField] private float shiftAnimDuration = 0.18f;

    [Tooltip("Extra push to reliably pass through the platform you stand on.")]
    [SerializeField] private float passExtra = 0.15f;

    [Tooltip("Only allow shift when grounded (with edge-support assist).")]
    [SerializeField] private bool requireGroundedToShift = true;

    [Header("Shift Edge Assist")]
    [Tooltip("Max overhang fraction allowed to shift. 0.2 means at least 80% supported.")]
    [SerializeField, Range(0f, 0.49f)] private float maxOverhangFractionToShift = 0.2f;

    [Tooltip("How many rays to test support (more = more stable).")]
    [SerializeField, Min(3)] private int edgeSupportRays = 5;

    [Tooltip("Ignore a margin near collider edges to avoid grazing hits.")]
    [SerializeField, Range(0f, 0.2f)] private float edgeRayMargin = 0.04f;

    [Tooltip("Ray extra distance beyond foot.")]
    [SerializeField] private float groundCheckExtra = 0.06f;

    [Tooltip("If slightly overhanging but within allowed, nudge player inward before shift.")]
    [SerializeField] private bool nudgeBackBeforeShift = true;

    [SerializeField] private float nudgeStep = 0.02f;
    [SerializeField] private float nudgeMaxDistance = 0.45f;

    [Header("Shift Stuck Handling")]
    [Tooltip("If overlap after shift, try resolve push-out first. If fails, rollback world.")]
    [SerializeField] private bool resolveInsteadOfRollback = true;

    [SerializeField] private bool rollbackIfStuck = true;

    [Tooltip("Wait 1 FixedUpdate before overlap check (tilemap collider toggles may lag).")]
    [SerializeField] private bool delayOverlapCheckOneFixed = true;

    [SerializeField, Min(1)] private int resolveIterations = 10;
    [SerializeField] private float resolveMaxStep = 0.35f;
    [SerializeField] private float resolveSkin = 0.01f;

    [Tooltip("Only consider stuck if penetration bigger than this (avoid contact offset false positives).")]
    [SerializeField] private float stuckPenetrationEpsilon = 0.02f;

    [Header("Shift Block Rule (Wall always-solid)")]
    [SerializeField] private bool blockShiftWhenStandingOnWall = true;

    [Header("Failsafe")]
    [Tooltip("If shift tween never completes (killed/paused), force-finish after this many seconds.")]
    [SerializeField] private float shiftFailsafeSeconds = 0.9f;

    [Header("Collider")]
    [SerializeField, Min(0f)] private float colliderEdgeRadius = 0.05f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Squash / Stretch (Jump / Land)")]
    [SerializeField] private bool enableSquashStretch = true;

    [SerializeField] private float jumpSquashX = 1.20f;
    [SerializeField] private float jumpSquashY = 0.80f;
    [SerializeField] private float jumpStretchX = 0.85f;
    [SerializeField] private float jumpStretchY = 1.25f;
    [SerializeField] private float jumpSquashDuration = 0.08f;
    [SerializeField] private float jumpStretchDuration = 0.10f;

    [SerializeField] private float landSquashX = 1.25f;
    [SerializeField] private float landSquashY = 0.75f;
    [SerializeField] private float landDuration = 0.10f;

    [SerializeField] private Ease squashEase = Ease.OutQuad;
    [SerializeField] private Ease stretchEase = Ease.OutQuad;

    [Header("Push Blocking (only dynamic bodies)")]
    [SerializeField] private bool blockPushingDynamicBodies = true;

    private bool dynLeftFixed;
    private bool dynRightFixed;

    [Header("Land Trigger Gate")]
    [SerializeField] private float minAirTimeForLand = 0.06f;
    [SerializeField] private float minFallSpeedForLand = 2.5f;
    [SerializeField] private float landTriggerCooldown = 0.10f;

    [Header("Optional: Fail Shake")]
    [SerializeField] private CameraShake2D failShake;

    private Rigidbody2D rb;
    private BoxCollider2D box;

    private float cd;
    private bool shifting;
    private Tween shiftTween;
    private Coroutine finishShiftRoutine;
    private float shiftFailsafeTimer;

    private bool controlsInverted;

    // Coyote + Buffer runtime
    private float coyoteTimer;

    // Contacts (fixed)
    private bool groundedFixed;
    private bool wallLeftFixed;
    private bool wallRightFixed;

    // Land gate runtime (FIX: missing vars)
    private float airTime;
    private float lastLandAt = -999f;
    private bool groundedPrevFixed;

    // Visual
    private Tween squashTween;
    private Vector3 baseVisualScale;

    // Shift snapshots (for rollback / failsafe finish)
    private WorldState shiftBeforeWorld;
    private float shiftBeforeGravityScale;
    private Vector2 shiftBeforePos;
    private RigidbodyType2D shiftBeforeBodyType;

    private float GravitySign => Mathf.Sign(Mathf.Approximately(rb.gravityScale, 0f) ? 1f : rb.gravityScale);
    private Vector2 GravityDown => (GravitySign > 0f) ? Vector2.down : Vector2.up;
    private Vector2 GravityUp => -GravityDown;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();

        rb.freezeRotation = true;

        box.edgeRadius = colliderEdgeRadius;

        if (visualRoot == null) visualRoot = transform;
        baseVisualScale = visualRoot.localScale;

        if (spriteRenderer == null)
            spriteRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);

        if (failShake == null)
            failShake = FindAnyObjectByType<CameraShake2D>();
    }

    private void OnDisable()
    {
        HardResetShiftState();
        squashTween?.Kill();
        squashTween = null;
    }

    private void Update()
    {
        // Reload
        if (Input.GetKeyDown(KeyCode.R) && !shifting && LevelManager.I != null)
        {
            ForceCancelShiftForReload();
            LevelManager.I.ReloadCurrentLevel();
            return;
        }

        if (cd > 0f) cd -= Time.deltaTime;

        // Jump input buffer (Celeste-style)
        // Press slightly before landing => jump will trigger on landing.
        bool jumpDown = Input.GetButtonDown("Jump") || MobileUIInput.ConsumeJumpDown();
        if (jumpDown)
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }

        // SHIFT input
        if (!shifting && cd <= 0f && (Input.GetKeyDown(shiftKey) || MobileUIInput.ConsumeShiftDown()))
            TryStartShift();

        // Shift failsafe
        if (shifting)
        {
            shiftFailsafeTimer += Time.deltaTime;
            if (shiftFailsafeTimer >= shiftFailsafeSeconds)
                ForceFinishShiftNow();

            coyoteTimer = 0f;
            return;
        }


    }

    private void FixedUpdate()
    {
        if (shifting) return;

        UpdateContactsFixed();

        // --- Stable Land Trigger (FixedUpdate) ---
        bool groundedNow = groundedFixed; // use real contacts for jump/land (avoid early ray support)

        if (!groundedNow)
        {
            airTime += Time.fixedDeltaTime;
        }
        else
        {
            // Just landed
            if (!groundedPrevFixed && enableSquashStretch && airTime >= minAirTimeForLand)
            {
                float fallSpeed = Vector2.Dot(rb.linearVelocity, GravityDown); // + = falling

                if (fallSpeed >= minFallSpeedForLand && (Time.time - lastLandAt) >= landTriggerCooldown)
                {
                    PlayLandSquash();
                    lastLandAt = Time.time;
                }
            }

            airTime = 0f;
        }

        // Reset jump availability only when we truly LAND (prevents double jump from buffered spam)
        if (groundedNow && !groundedPrevFixed)
            jumpAvailable = true;

        // Coyote timer updated in FixedUpdate for deterministic physics order
        if (groundedNow && jumpAvailable)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);

        // Consume jump buffer ONLY when jump is actually available
        if (jumpBufferTimer > 0f && jumpAvailable && coyoteTimer > 0f)
        {
            jumpQueued = true;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        groundedPrevFixed = groundedNow;

        // Input
        float xMobile = MobileUIInput.Horizontal;
        float x = Mathf.Abs(xMobile) > 0.01f ? xMobile : Input.GetAxisRaw("Horizontal");

        if (CameraFlip2D.I != null && CameraFlip2D.I.IsViewFlipped)
            x *= -1f;

        if (controlsInverted)
            x *= -1f;

        float targetVx = x * moveSpeed;

        // Block pushing ONLY when contacting dynamic bodies
        if (blockPushingDynamicBodies)
        {
            if (targetVx > 0f && dynRightFixed) targetVx = 0f;
            if (targetVx < 0f && dynLeftFixed) targetVx = 0f;
        }

        float vx = rb.linearVelocity.x;
        float rate = Mathf.Abs(targetVx) > 0.01f ? acceleration : deceleration;
        vx = Mathf.MoveTowards(vx, targetVx, rate * Time.fixedDeltaTime);

        Vector2 v = rb.linearVelocity;
        v.x = vx;

        if (clampVerticalSpeed)
        {
            float vyRel = v.y * GravitySign;
            float maxUp = Mathf.Max(0.01f, maxRiseSpeed);
            float maxDown = Mathf.Max(0.01f, maxFallSpeed);

            vyRel = Mathf.Clamp(vyRel, -maxDown, maxUp);
            v.y = vyRel * GravitySign;
        }

        rb.linearVelocity = v;

        if (jumpQueued)
        {
            jumpQueued = false;
            Jump();
        }


        float face = Mathf.Abs(x) > 0.01f ? x : rb.linearVelocity.x;
        ApplyFacing(face);
    }

    private void UpdateContactsFixed()
    {
        groundedFixed = false;
        wallLeftFixed = false;
        wallRightFixed = false;
        dynLeftFixed = false;
        dynRightFixed = false;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = solidMask,
            useTriggers = false
        };

        ContactPoint2D[] contacts = new ContactPoint2D[16];
        int count = rb.GetContacts(filter, contacts);

        Vector2 upRelToGravity = GravityUp;

        for (int i = 0; i < count; i++)
        {
            Vector2 n = contacts[i].normal;

            float dotGround = Vector2.Dot(n, upRelToGravity);
            if (dotGround >= groundNormalThreshold)
                groundedFixed = true;

            float dotLeft = Vector2.Dot(n, Vector2.right);
            float dotRight = Vector2.Dot(n, Vector2.left);

            if (dotLeft >= wallNormalThreshold) wallLeftFixed = true;
            if (dotRight >= wallNormalThreshold) wallRightFixed = true;

            // Dynamic side contact (for anti-push)
            var col = contacts[i].collider;
            var otherRb = col != null ? col.attachedRigidbody : null;
            bool otherDynamic = otherRb != null && otherRb.bodyType == RigidbodyType2D.Dynamic;

            float dotGround_dyn = dotGround; // reuse but keep name separate in case you tweak later
            if (otherDynamic && dotGround_dyn < groundNormalThreshold)
            {
                if (dotLeft >= wallNormalThreshold) dynLeftFixed = true;
                if (dotRight >= wallNormalThreshold) dynRightFixed = true;
            }
        }
    }

    private void ApplyFacing(float x)
    {
        if (spriteRenderer == null) return;
        if (Mathf.Abs(x) < 0.01f) return;

        spriteRenderer.flipX = (x < 0f);
    }

    private void Jump()
    {
        if (shifting) return;

        jumpAvailable = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        Vector2 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(GravityUp * jumpForce, ForceMode2D.Impulse);

        if (enableSquashStretch)
            PlayJumpStretch();
    }

    private void PlayJumpStretch()
    {
        if (visualRoot == null) return;

        squashTween?.Kill();

        Vector3 squash = new Vector3(baseVisualScale.x * jumpSquashX, baseVisualScale.y * jumpSquashY, baseVisualScale.z);
        Vector3 stretch = new Vector3(baseVisualScale.x * jumpStretchX, baseVisualScale.y * jumpStretchY, baseVisualScale.z);

        squashTween = DOTween.Sequence()
            .Append(visualRoot.DOScale(squash, jumpSquashDuration).SetEase(squashEase))
            .Append(visualRoot.DOScale(stretch, jumpStretchDuration).SetEase(stretchEase))
            .Append(visualRoot.DOScale(baseVisualScale, 0.08f).SetEase(Ease.OutQuad))
            .SetLink(gameObject);
    }

    private void PlayLandSquash()
    {
        if (visualRoot == null) return;

        squashTween?.Kill();

        Vector3 land = new Vector3(baseVisualScale.x * landSquashX, baseVisualScale.y * landSquashY, baseVisualScale.z);

        squashTween = DOTween.Sequence()
            .Append(visualRoot.DOScale(land, landDuration).SetEase(squashEase))
            .Append(visualRoot.DOScale(baseVisualScale, landDuration * 0.9f).SetEase(Ease.OutQuad))
            .SetLink(gameObject);
    }

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

            // (optional) nếu bị “hit 0 distance” ở corner thì bỏ qua
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

    public void InvertControls(bool state) => controlsInverted = state;

    public void FlipGravity()
    {
        rb.gravityScale *= -1f;
    }

    public void UI_Jump()
    {
        if (shifting) return;

        // Only set buffer here. Actual consume happens in FixedUpdate (with groundedFixed + jumpAvailable)
        jumpBufferTimer = jumpBufferTime;
    }


    public void UI_Shift()
    {
        TryStartShift();
    }

    public bool IsShifting => shifting;
    public bool IsGroundedNow => groundedFixed || IsSupportedByRays();
}