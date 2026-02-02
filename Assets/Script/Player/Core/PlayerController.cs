using System.Collections;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public partial class PlayerController : MonoBehaviour
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

    // Land gate runtime
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

        // Jump input buffer
        bool jumpDown = Input.GetButtonDown("Jump") || MobileUIInput.ConsumeJumpDown();
        if (jumpDown)
            jumpBufferTimer = jumpBufferTime;
        else if (jumpBufferTimer > 0f)
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);

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
        bool groundedNow = groundedFixed;

        if (!groundedNow)
        {
            airTime += Time.fixedDeltaTime;
        }
        else
        {
            if (!groundedPrevFixed && enableSquashStretch && airTime >= minAirTimeForLand)
            {
                float fallSpeed = Vector2.Dot(rb.linearVelocity, GravityDown);
                if (fallSpeed >= minFallSpeedForLand && (Time.time - lastLandAt) >= landTriggerCooldown)
                {
                    PlayLandSquash();
                    lastLandAt = Time.time;
                }
            }
            airTime = 0f;
        }

        if (groundedNow && !groundedPrevFixed)
            jumpAvailable = true;

        if (groundedNow && jumpAvailable)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);

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

    public void InvertControls(bool state) => controlsInverted = state;

    public void FlipGravity() => rb.gravityScale *= -1f;

    public void UI_Jump()
    {
        if (shifting) return;
        jumpBufferTimer = jumpBufferTime;
    }

    public void UI_Shift() => TryStartShift();

    public bool IsShifting => shifting;
    public bool IsGroundedNow => groundedFixed || IsSupportedByRays();
}
