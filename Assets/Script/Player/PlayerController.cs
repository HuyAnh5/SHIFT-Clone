using System.Collections;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    // =========================================================
    // Inspector
    // =========================================================

    [Header("Move")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float accel = 90f;          // tăng để responsive hơn
    [SerializeField] private float decel = 110f;         // giảm để dừng nhanh hơn

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;

    [Header("Celeste Feel - Input Forgiveness")]
    [SerializeField] private float coyoteTime = 0.10f;
    [SerializeField] private float jumpBufferTime = 0.10f;

    [Header("Contact Classification")]
    [Tooltip("Normal phải 'hướng lên' theo gravity tối thiểu bao nhiêu để tính là ground.")]
    [SerializeField, Range(0.1f, 0.98f)] private float groundNormalThreshold = 0.60f;

    [Tooltip("Normal phải 'hướng ngang' tối thiểu bao nhiêu để tính là wall side.")]
    [SerializeField, Range(0.1f, 0.98f)] private float wallNormalThreshold = 0.60f;

    [Header("Masks")]
    [Tooltip("Solid để collide (ground + wall).")]
    [SerializeField] private LayerMask solidMask;

    [Tooltip("Wall tilemap (always-solid) để chặn shift khi đứng trên.")]
    [SerializeField] private LayerMask wallMask;

    [Header("Anti-Tunnel (fall too fast)")]
    [SerializeField] private bool clampFallSpeed = true;
    [SerializeField] private float maxFallSpeed = 22f;
    [SerializeField] private float maxRiseSpeed = 35f;

    [Header("Collider")]
    [SerializeField, Min(0f)] private float colliderEdgeRadius = 0.02f;

    [Header("Shift (core)")]
    [SerializeField] private KeyCode shiftKey = KeyCode.LeftShift;
    [SerializeField] private float shiftCooldown = 0.12f;
    [SerializeField] private float shiftAnimDuration = 0.18f;
    [SerializeField] private float passExtra = 0.15f;
    [SerializeField] private bool requireGroundedToShift = true;
    [SerializeField] private bool blockShiftWhenStandingOnWall = true;

    [Header("Shift Safety")]
    [Tooltip("Nếu tween shift bị kill/kẹt, sau thời gian này sẽ tự trả trigger/collider về bình thường.")]
    [SerializeField] private float shiftFailsafeSeconds = 0.75f;

    [Header("Squash & Stretch (visual only)")]
    [SerializeField] private Transform visualRoot; // child "Visual" chứa Sprite/Animator
    [SerializeField] private bool enableSquashStretch = true;

    [SerializeField] private float landSquashCooldown = 0.12f;   // chặn spam squash
    [SerializeField] private float rearmAirTime = 0.08f;         // phải bay liên tục >= thời gian này mới "arm" lại
    [SerializeField] private float minFallSpeedToCount = 0.5f;   // tránh re-arm do rung rất nhẹ
    [SerializeField] private float airBlendSpeed = 14f;


    private bool landSquashArmed;
    private float landSquashCd;


    // =========================================================
    // Runtime
    // =========================================================

    private Rigidbody2D rb;
    private BoxCollider2D box;

    private float cd;
    private bool shifting;
    private Tween shiftTween;
    private float shiftFailsafeTimer;

    private bool controlsInverted;

    // coyote + buffer
    private float coyoteTimer;
    private float jumpBufferTimer;

    // contacts (FixedUpdate)
    private bool groundedFixed;
    private bool wallLeftFixed;
    private bool wallRightFixed;

    private readonly ContactPoint2D[] contactBuf = new ContactPoint2D[20];

    // squash/stretch
    private Tween squashTween;
    private Vector3 baseVisualScale;
    private bool wasGrounded;
    private float airTime;

    private float GravitySign => Mathf.Sign(rb.gravityScale == 0 ? 1f : rb.gravityScale);
    private Vector2 GravityDown => (GravitySign > 0f) ? Vector2.down : Vector2.up;   // hướng rơi theo gravity
    private Vector2 GravityUp => -GravityDown;

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();

        // masks fallback
        if (solidMask.value == 0) solidMask = Physics2D.DefaultRaycastLayers;

        // collider safe defaults
        box.enabled = true;
        box.isTrigger = false;
        box.edgeRadius = colliderEdgeRadius;

        // rigidbody safe defaults
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.freezeRotation = true;

        // visual
        if (visualRoot == null)
        {
            var v = transform.Find("Visual");
            visualRoot = v != null ? v : transform;
        }
        baseVisualScale = visualRoot.localScale;
        wasGrounded = false;
        airTime = 0f;

        HardResetShiftState();
    }

    private void OnDisable()
    {
        // tránh case đổi scene/reload mà còn kẹt trigger
        HardResetShiftState();
    }

    private void Update()
    {
        if (landSquashCd > 0f) landSquashCd -= Time.deltaTime;

        // Reload
        if (Input.GetKeyDown(KeyCode.R) && LevelManager.I != null)
        {
            ForceCancelShiftForReload();
            LevelManager.I.ReloadCurrentLevel();
            return;
        }

        if (cd > 0f) cd -= Time.deltaTime;

        // SHIFT input (desktop + mobile)
        if (!shifting && cd <= 0f && (Input.GetKeyDown(shiftKey) || MobileUIInput.ConsumeShiftDown()))
            TryStartShift();

        // failsafe: nếu đang shift mà tween bị kill/kẹt -> trả lại trigger
        if (shifting)
        {
            shiftFailsafeTimer += Time.deltaTime;
            if (shiftFailsafeTimer >= shiftFailsafeSeconds || shiftTween == null || !shiftTween.IsActive())
            {
                HardResetShiftState();
            }
            // trong lúc shift: không xử lý jump/buffer
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            return;
        }

        // ===== grounded từ FixedUpdate (ổn định, không bị tường làm grounded giả) =====
        bool groundedNow = groundedFixed;

        // squash/land
        HandleSquashStretch(groundedNow);

        // coyote
        coyoteTimer = groundedNow ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);

        // buffer countdown
        if (jumpBufferTimer > 0f)
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);

        // register jump input (desktop + mobile)
        if (Input.GetButtonDown("Jump") || MobileUIInput.ConsumeJumpDown())
            jumpBufferTimer = jumpBufferTime;

        // consume
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            Jump();
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (shifting) return;

        UpdateContactsFixed(); // cập nhật grounded/wall từ contact normals

        // input
        float xMobile = MobileUIInput.Horizontal;
        float x = Mathf.Abs(xMobile) > 0.01f ? xMobile : Input.GetAxisRaw("Horizontal");

        // đảo input theo camera flip
        if (CameraFlip2D.I != null && CameraFlip2D.I.IsViewFlipped)
            x *= -1f;

        if (controlsInverted)
            x *= -1f;

        // Movement: dùng accel/decel để tránh rung khi solver đẩy ra khỏi tường
        Vector2 v = rb.linearVelocity;

        float targetVx = x * moveSpeed;

        // Nếu đang tì vào tường và input đang đẩy vào tường -> không cho target đẩy tiếp
        if ((x < -0.01f && wallLeftFixed) || (x > 0.01f && wallRightFixed))
            targetVx = 0f;

        float rate = (Mathf.Abs(targetVx) > Mathf.Abs(v.x)) ? accel : decel;
        v.x = Mathf.MoveTowards(v.x, targetVx, rate * Time.fixedDeltaTime);

        // Anti-tunnel: clamp tốc độ rơi theo gravity
        if (clampFallSpeed)
        {
            float vRel = v.y * GravitySign; // rơi => âm
            vRel = Mathf.Clamp(vRel, -maxFallSpeed, maxRiseSpeed);
            v.y = vRel * GravitySign;
        }

        rb.linearVelocity = v;
    }

    // =========================================================
    // Contacts (ground/wall) - 핵 fix chống rung + grounded giả
    // =========================================================

    private void UpdateContactsFixed()
    {
        groundedFixed = false;
        wallLeftFixed = false;
        wallRightFixed = false;

        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = solidMask,
            useTriggers = false
        };

        int count = rb.GetContacts(filter, contactBuf);

        Vector2 up = GravityUp;

        for (int i = 0; i < count; i++)
        {
            Vector2 n = contactBuf[i].normal;

            // Ground: normal phải hướng theo "up" (ngược gravity)
            if (Vector2.Dot(n, up) >= groundNormalThreshold)
                groundedFixed = true;

            // Wall side: normal phải hướng ngang đủ lớn
            if (Mathf.Abs(n.x) >= wallNormalThreshold)
            {
                // normal hướng từ tường -> player
                if (n.x > 0f) wallLeftFixed = true;
                else wallRightFixed = true;
            }
        }
    }

    // =========================================================
    // Actions
    // =========================================================

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        PlayJumpStretch();
        rb.AddForce(Vector2.up * jumpForce * GravitySign, ForceMode2D.Impulse);
    }

    private void TryStartShift()
    {
        if (shifting || cd > 0f) return;

        if (requireGroundedToShift && !groundedFixed)
            return;

        if (blockShiftWhenStandingOnWall && IsStandingOnWall())
            return;

        DoShift();
    }

    private bool IsStandingOnWall()
    {
        if (!groundedFixed) return false;

        // nếu đang grounded mà collider dưới chân thuộc wallMask -> true
        // cách nhẹ: dùng contacts đã có, tìm contact ground và check layer của collider
        // (ContactPoint2D không luôn có collider, nên dùng Overlap nhỏ dưới chân)
        Vector2 down = GravityDown;

        Bounds b = box.bounds;
        Vector2 probe = (Vector2)b.center + down * (b.extents.y + 0.02f);

        Collider2D hit = Physics2D.OverlapCircle(probe, 0.05f, wallMask);
        return hit != null;
    }

    private void DoShift()
    {
        if (WorldShiftManager.I == null) return;

        // clear buffer/coyote để tránh "shift xong tự nhảy"
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;

        shifting = true;
        cd = shiftCooldown;
        shiftFailsafeTimer = 0f;

        shiftTween?.Kill(false);

        // lưu state rollback
        WorldState beforeWorld = WorldShiftManager.I.SolidWorld;
        float beforeGravityScale = rb.gravityScale;
        Vector2 beforePos = rb.position;
        RigidbodyType2D beforeBodyType = rb.bodyType;

        // hướng gravity cũ để “chìm qua mặt đang đứng”
        Vector2 oldGravityDir = GravityDown;

        float push = ComputePassDistance(oldGravityDir);

        // khóa physics trong lúc tween để solver không giằng co
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // trigger để đi xuyên trong lúc shift
        box.isTrigger = true;

        // đổi world + đảo gravity
        WorldShiftManager.I.Toggle();
        rb.gravityScale = -beforeGravityScale;

        // squash visual nhẹ (tuỳ thích)
        if (enableSquashStretch) KillSquashTween();

        Sequence seq = DOTween.Sequence();

        seq.Append(
            rb.DOMove(beforePos + oldGravityDir * push, shiftAnimDuration * 2f)
              .SetEase(Ease.InOutSine)
        );

        seq.OnComplete(() =>
        {
            // trả về state
            box.isTrigger = false;
            rb.bodyType = beforeBodyType;

            shifting = false;
            shiftFailsafeTimer = 0f;

            Physics2D.SyncTransforms();

            // nếu kẹt thì rollback nhanh
            if (IsOverlappingSolid())
            {
                RollbackShift(beforeWorld, beforeGravityScale, beforePos, beforeBodyType);
            }
        });

        shiftTween = seq;
    }

    private float ComputePassDistance(Vector2 oldGravityDir)
    {
        // cast từ center theo gravity cũ để tìm mặt đang đứng
        Vector2 center = box.bounds.center;
        float extY = box.bounds.extents.y;

        float rayDist = extY + 2f;
        RaycastHit2D hit = Physics2D.Raycast(center, oldGravityDir, rayDist, solidMask);

        if (hit.collider != null)
            return hit.distance + extY + passExtra;

        return extY * 2f + passExtra;
    }

    // =========================================================
    // Overlap/rollback safety
    // =========================================================

    private bool IsOverlappingSolid()
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = solidMask, useTriggers = false };
        Collider2D[] hits = new Collider2D[12];
        int count = box.Overlap(filter, hits);
        return count > 0;
    }

    private void RollbackShift(WorldState beforeWorld, float beforeGravityScale, Vector2 beforePos, RigidbodyType2D beforeBodyType)
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        box.isTrigger = true;

        if (WorldShiftManager.I != null)
            WorldShiftManager.I.SetWorld(beforeWorld);

        rb.gravityScale = beforeGravityScale;
        rb.position = beforePos;

        Physics2D.SyncTransforms();

        box.isTrigger = false;
        rb.bodyType = beforeBodyType;

        shifting = false;
        shiftFailsafeTimer = 0f;
    }

    private void HardResetShiftState()
    {
        shifting = false;
        shiftFailsafeTimer = 0f;

        shiftTween?.Kill(false);
        shiftTween = null;

        if (box != null)
            box.isTrigger = false;

        if (rb != null)
        {
            if (!rb.simulated) rb.simulated = true;
            if (rb.bodyType != RigidbodyType2D.Dynamic) rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    public void ForceCancelShiftForReload()
    {
        StopAllCoroutines();
        HardResetShiftState();

        cd = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    // =========================================================
    // Public API (UI/Triggers)
    // =========================================================

    public void InvertControls(bool state) => controlsInverted = state;
    public void FlipGravity() => rb.gravityScale *= -1f;

    // Nếu bạn muốn UI gọi trực tiếp thay vì MobileUIInput
    public void UI_Jump() { if (!shifting) jumpBufferTimer = jumpBufferTime; }
    public void UI_Shift() { if (!shifting) TryStartShift(); }

    public bool IsShifting => shifting;
    public bool IsGroundedNow => groundedFixed;

    // =========================================================
    // Squash & Stretch
    // =========================================================

    private void KillSquashTween()
    {
        if (squashTween != null && squashTween.IsActive())
            squashTween.Kill(false);
        squashTween = null;
    }

    private void PlayJumpStretch()
    {
        if (!enableSquashStretch || visualRoot == null) return;

        KillSquashTween();
        visualRoot.localScale = baseVisualScale;

        Vector3 squash = new Vector3(baseVisualScale.x * 1.10f, baseVisualScale.y * 0.90f, baseVisualScale.z);
        Vector3 stretch = new Vector3(baseVisualScale.x * 0.85f, baseVisualScale.y * 1.22f, baseVisualScale.z);

        var seq = DOTween.Sequence();
        seq.Append(visualRoot.DOScale(squash, 0.05f).SetEase(Ease.OutQuad));
        seq.Append(visualRoot.DOScale(stretch, 0.08f).SetEase(Ease.OutQuad));
        seq.Append(visualRoot.DOScale(baseVisualScale, 0.12f).SetEase(Ease.OutQuad));
        squashTween = seq;
    }

    private void PlayLandSquash()
    {
        if (!enableSquashStretch || visualRoot == null) return;

        KillSquashTween();
        visualRoot.localScale = baseVisualScale;

        Vector3 impact = new Vector3(baseVisualScale.x * 1.25f, baseVisualScale.y * 0.78f, baseVisualScale.z);

        var seq = DOTween.Sequence();
        seq.Append(visualRoot.DOScale(impact, 0.06f).SetEase(Ease.OutQuad));
        seq.Append(visualRoot.DOScale(baseVisualScale, 0.12f).SetEase(Ease.OutBack));
        squashTween = seq;
    }

    private void HandleSquashStretch(bool groundedNow)
    {
        if (!enableSquashStretch || visualRoot == null)
        {
            wasGrounded = groundedNow;
            return;
        }

        // vRel < 0 nghĩa là đang rơi theo gravity (dù gravity đảo)
        float vRel = rb.linearVelocity.y * GravitySign;

        if (!groundedNow)
        {
            airTime += Time.deltaTime;

            // Chỉ re-arm nếu:
            // 1) ở trên không liên tục đủ lâu
            // 2) và thực sự đang rơi/di chuyển đáng kể (tránh rung sát đất)
            if (airTime >= rearmAirTime && vRel <= -minFallSpeedToCount)
                landSquashArmed = true;

            wasGrounded = false;
            return;
        }

        // grounded
        airTime = 0f;

        // ĐÁP ĐẤT: chỉ squash 1 lần khi đang armed + cooldown đã hết
        if (landSquashArmed && landSquashCd <= 0f)
        {
            PlayLandSquash();
            landSquashArmed = false;
            landSquashCd = landSquashCooldown;
        }

        // về scale gốc nếu không có tween
        if (squashTween == null || !squashTween.IsActive())
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, baseVisualScale, Time.deltaTime * airBlendSpeed);

        wasGrounded = true;
    }

}
