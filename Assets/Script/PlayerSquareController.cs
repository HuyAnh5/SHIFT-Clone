using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerSquareController : MonoBehaviour
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

    [Tooltip("Nếu shift xong bị kẹt trong collider -> tự rollback.")]
    [SerializeField] private bool rollbackIfStuck = true;

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
            if (!requireGroundedToShift || IsGrounded())
            {
                if (!(blockShiftWhenStandingOnWall && IsStandingOnWall()))
                    DoShift();
            }
        }

        if (!shifting && Input.GetButtonDown("Jump") && IsGrounded())
            Jump();
    }


    private void FixedUpdate()
    {
        if (shifting) return;

        float x = Input.GetAxisRaw("Horizontal");

        // camera xoay 180 => đảo input để “trái vẫn là trái theo màn hình”
        if (WorldShiftManager.I != null && WorldShiftManager.I.IsViewFlipped)
            x *= -1f;

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

        // khoá physics ngắn (để tween mượt và không bị gravity kéo)
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // xuyên collider trong lúc shift
        box.isTrigger = true;

        // đổi world + đảo gravity NGAY (camera sẽ xoay do CameraFlip2D nghe event)
        WorldShiftManager.I.Toggle();
        rb.gravityScale = -beforeGravityScale;

        Sequence seq = DOTween.Sequence();

        // Animation chìm qua platform trong lúc camera đang xoay
        seq.Append(rb.DOMove(beforePos + (Vector2)(oldGravityDir * push), shiftAnimDuration*2f).SetEase(Ease.InOutSine));

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
            // bật lại va chạm + trả physics về dynamic
            box.isTrigger = false;
            rb.bodyType = beforeBodyType;

            if (rollbackIfStuck && IsOverlappingSolid())
            {
                // rollback: quay về trạng thái cũ
                rb.bodyType = RigidbodyType2D.Kinematic;
                box.isTrigger = true;

                // đảo lại world + gravity
                WorldShiftManager.I.Toggle();
                rb.gravityScale = beforeGravityScale;

                rb.position = beforePos;

                box.isTrigger = false;
                rb.bodyType = beforeBodyType;
            }

            shifting = false;
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
        Vector2 center = box.bounds.center;
        float extY = box.bounds.extents.y;

        Vector2 dir = (GravitySign > 0f) ? Vector2.down : Vector2.up;
        float dist = extY + groundCheckExtra;

        RaycastHit2D hit = Physics2D.Raycast(center, dir, dist, groundMask);
        return hit.collider != null;
    }

    private bool IsStandingOnWall()
    {
        Vector2 center = box.bounds.center;
        float extY = box.bounds.extents.y;

        Vector2 dir = (GravitySign > 0f) ? Vector2.down : Vector2.up;
        float dist = extY + groundCheckExtra;

        RaycastHit2D hit = Physics2D.Raycast(center, dir, dist, groundMask);
        if (!hit.collider) return false;

        int hitLayer = hit.collider.gameObject.layer;
        return ((1 << hitLayer) & wallMask) != 0;
    }


    private bool IsOverlappingSolid()
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = solidMask, useTriggers = false };
        Collider2D[] hits = new Collider2D[8];
        return box.Overlap(filter, hits) > 0;
    }
}