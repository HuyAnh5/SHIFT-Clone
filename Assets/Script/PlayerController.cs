using System.Collections;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckExtra = 0.06f;

    [Header("Shift (SHIFT game core)")]
    [SerializeField] private KeyCode shiftKey = KeyCode.LeftShift;
    [SerializeField] private float shiftCooldown = 0.12f;

    [Tooltip("Thời gian animation chìm + camera xoay (hãy set bằng flipDuration của CameraFlip2D).")]
    [SerializeField] private float shiftAnimDuration = 0.18f;

    [Tooltip("Đẩy thêm để chắc chắn chui qua mặt platform.")]
    [SerializeField] private float passExtra = 0.15f;

    [Tooltip("Chỉ cho shift khi đang đứng trên nền.")]
    [SerializeField] private bool requireGroundedToShift = true;

    [Header("Shift Edge Assist")]
    [Tooltip("Cho phép nhô ra tối đa bao nhiêu (tính theo % bề ngang collider). 0.2 = 1/5.")]
    [SerializeField, Range(0f, 0.49f)] private float maxOverhangFractionToShift = 0.2f;

    [Tooltip("Số tia kiểm tra độ bám (càng nhiều càng ổn định ở mép).")]
    [SerializeField, Min(3)] private int edgeSupportRays = 5;

    [Tooltip("Bỏ qua sát mép collider (tính theo % width) để tránh ray dính cạnh).")]
    [SerializeField, Range(0f, 0.2f)] private float edgeRayMargin = 0.04f;

    [Tooltip("Nếu chưa quá ngưỡng nhô ra, sẽ tự đẩy player vào trong nền trước khi shift.")]
    [SerializeField] private bool nudgeBackBeforeShift = true;

    [SerializeField] private float nudgeStep = 0.02f;
    [SerializeField] private float nudgeMaxDistance = 0.45f;

    [Tooltip("Nếu shift xong bị kẹt trong collider -> tự rollback.")]
    [SerializeField] private bool rollbackIfStuck = true;

    [Header("Shift Post-Resolve (avoid rollback 'double shift')")]
    [Tooltip("Đợi 1 FixedUpdate trước khi check overlap (tilemap collider enable/disable có thể cập nhật trễ 1 frame physics).")]
    [SerializeField] private bool delayOverlapCheckOneFixed = true;

    [Tooltip("Nếu bị overlap sau shift, ưu tiên đẩy ra khỏi collider trước khi rollback world.")]
    [SerializeField] private bool resolveInsteadOfRollback = true;

    [SerializeField, Min(1)] private int resolveIterations = 10;
    [SerializeField] private float resolveMaxStep = 0.35f;
    [SerializeField] private float resolveSkin = 0.01f;

    [Tooltip("Chỉ coi là 'kẹt' khi penetration > epsilon (tránh false positive do contact offset).")]
    [SerializeField] private float stuckPenetrationEpsilon = 0.02f;

    [SerializeField] private LayerMask solidMask; // thường = groundMask

    [Header("Optional squash (visual)")]
    [SerializeField] private bool squashOnShift = true;
    [SerializeField, Range(0.2f, 1f)] private float squashY = 0.35f;

    [Header("Wall (always solid)")]
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private bool blockShiftWhenStandingOnWall = true;

    private Rigidbody2D rb;
    private BoxCollider2D box;

    private float cd;
    private bool shifting;
    private Tween shiftTween;

    private bool controlsInverted;

    private float GravitySign => Mathf.Sign(rb.gravityScale == 0 ? 1f : rb.gravityScale);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
        rb.freezeRotation = true;

        if (solidMask.value == 0) solidMask = groundMask;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && !shifting && LevelManager.I != null)
            LevelManager.I.ReloadCurrentLevel();

        if (cd > 0f) cd -= Time.deltaTime;

        if (!shifting && cd <= 0f && Input.GetKeyDown(shiftKey))
        {
            if (!requireGroundedToShift)
            {
                if (!(blockShiftWhenStandingOnWall && IsStandingOnWall()))
                    DoShift();
            }
            else
            {
                if (CanStartShiftFromEdge())
                {
                    if (!(blockShiftWhenStandingOnWall && IsStandingOnWall()))
                        DoShift();
                }
            }
        }

        if (!shifting && Input.GetButtonDown("Jump") && IsGrounded())
            Jump();
    }

    private void FixedUpdate()
    {
        float x = Input.GetAxisRaw("Horizontal");

        // ƯU TIÊN theo camera state thật (cả Shift + GravityTrigger)
        bool viewFlipped = (CameraFlip2D.I != null) && CameraFlip2D.I.IsViewFlipped;

        // fallback nếu bạn vẫn dùng flag cũ ở WorldShiftManager
        if (!viewFlipped && WorldShiftManager.I != null)
            viewFlipped = WorldShiftManager.I.IsViewFlipped;

        if (viewFlipped) x *= -1f;

        rb.linearVelocity = new Vector2(x * moveSpeed, rb.linearVelocity.y);

    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce * GravitySign, ForceMode2D.Impulse);
    }

    private void DoShift()
    {
        if (WorldShiftManager.I == null) return;

        shifting = true;
        cd = shiftCooldown;

        shiftTween?.Kill();

        // Lưu trạng thái để rollback nếu kẹt
        WorldState beforeWorld = WorldShiftManager.I.SolidWorld;
        float beforeGravityScale = rb.gravityScale;
        Vector2 beforePos = rb.position;
        RigidbodyType2D beforeBodyType = rb.bodyType;

        // hướng trọng lực CŨ (để “chìm qua mặt đang đứng/bám”)
        Vector2 oldGravityDir = (GravitySign > 0f) ? Vector2.down : Vector2.up;

        // tính khoảng đẩy để qua mặt platform
        float push = ComputePassDistance(oldGravityDir);

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // xuyên collider trong lúc shift
        box.isTrigger = true;

        // đổi world + đảo gravity NGAY
        WorldShiftManager.I.Toggle();
        rb.gravityScale = -beforeGravityScale;

        Sequence seq = DOTween.Sequence();

        seq.Append(rb.DOMove(beforePos + oldGravityDir * push, shiftAnimDuration * 2f).SetEase(Ease.InOutSine));

        if (squashOnShift)
        {
            Vector3 baseScale = transform.localScale;
            seq.Join(transform
                .DOScale(new Vector3(baseScale.x, baseScale.y * squashY, baseScale.z), shiftAnimDuration * 0.75f)
                .SetEase(Ease.InOutSine)
                .SetLoops(2, LoopType.Yoyo));
        }

        seq.OnComplete(() =>
        {
            box.isTrigger = false;
            rb.bodyType = beforeBodyType;

            Physics2D.SyncTransforms();
            StartCoroutine(FinishShift(beforeWorld, beforeGravityScale, beforePos, beforeBodyType));
        });

        shiftTween = seq;
    }

    private float ComputePassDistance(Vector2 oldGravityDir)
    {
        Vector2 center = box.bounds.center;
        float extY = box.bounds.extents.y;

        float rayDist = extY + 2f;
        RaycastHit2D hit = Physics2D.Raycast(center, oldGravityDir, rayDist, groundMask);

        if (hit.collider != null)
            return hit.distance + extY + passExtra;

        return extY * 2f + passExtra;
    }

    private bool IsGrounded()
    {
        float support = GetGroundSupportFraction(3, out _, out _);
        return support > 0f;
    }

    private bool IsStandingOnWall()
    {
        int rays = Mathf.Max(3, edgeSupportRays);
        Bounds b = box.bounds;
        Vector2 dir = (GravitySign > 0f) ? Vector2.down : Vector2.up;
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

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, groundMask);
            if (!hit.collider) continue;

            int hitLayer = hit.collider.gameObject.layer;
            if (((1 << hitLayer) & wallMask) != 0)
                return true;
        }

        return false;
    }

    private float GetGroundSupportFraction(int rays, out int leftHits, out int rightHits)
    {
        rays = Mathf.Clamp(rays, 3, 21);
        leftHits = 0;
        rightHits = 0;

        Bounds b = box.bounds;
        Vector2 dir = (GravitySign > 0f) ? Vector2.down : Vector2.up;
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

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, dist, groundMask);
            if (!hit.collider) continue;

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

    // ===== NEW CORE: penetration-based stuck check =====
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

            // loại trừ collider cùng rigidbody (nếu player có collider khác)
            if (other.attachedRigidbody != null && other.attachedRigidbody == rb) continue;

            ColliderDistance2D d = Physics2D.Distance(box, other);
            if (!d.isOverlapped) continue;

            float penetration = -d.distance;
            if (penetration <= stuckPenetrationEpsilon) continue;

            // QUAN TRỌNG: normal từ A->B, muốn đẩy A ra khỏi B => dùng -normal
            pushOut += (-d.normal) * (penetration + resolveSkin);
            any = true;
        }

        return any;
    }

    private bool IsOverlappingSolid()
    {
        return TryGetPenetrationVector(out _);
    }

    private IEnumerator FinishShift(
        WorldState beforeWorld,
        float beforeGravityScale,
        Vector2 beforePos,
        RigidbodyType2D beforeBodyType)
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

        Physics2D.SyncTransforms();

        box.isTrigger = false;
        rb.bodyType = beforeBodyType;
    }

    public void InvertControls(bool state)
    {
        controlsInverted = state;
    }

    public void FlipGravity()
    {
        rb.gravityScale *= -1f;
    }

    public bool IsShifting => shifting;
    public bool IsGroundedNow => IsGrounded();
}
