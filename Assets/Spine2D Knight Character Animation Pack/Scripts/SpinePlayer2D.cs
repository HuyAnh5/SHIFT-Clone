using UnityEngine;
using Spine;
using Spine.Unity;

public class SpinePlayer2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private Rigidbody2D rb;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.12f;
    [SerializeField] private LayerMask groundMask;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpVelocity = 12f;
    [SerializeField] private bool allowAirControl = true;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 0.2f;
    [SerializeField] private bool canAttackInAir = true;
    [SerializeField] private float attackBlendOut = 0.08f;

    [Header("Animation Names")]
    [SerializeField] private string idleAnim = "idle";
    [SerializeField] private string runAnim = "run";
    [SerializeField] private string jumpAnim = "jump";
    [SerializeField] private string attackAnim = "attack";
    [SerializeField] private string deadAnim = "dead";

    [Header("Flip")]
    [SerializeField] private bool flipByTransformScale = true;

    private Spine.AnimationState animState;
    private string currentBaseAnim;
    private float moveX;
    private bool isGrounded;
    private bool facingRight = true;
    private float lastAttackTime = -999f;

    private const int BASE_TRACK = 0;
    private const int ATTACK_TRACK = 1;

    void Awake()
    {
        if (!skeletonAnimation) skeletonAnimation = GetComponent<SkeletonAnimation>();
        if (!rb) rb = GetComponent<Rigidbody2D>();

        skeletonAnimation.Initialize(false);
        animState = skeletonAnimation.AnimationState;

        animState.Data.SetMix(idleAnim, runAnim, 0.12f);
        animState.Data.SetMix(runAnim, idleAnim, 0.12f);

        SetBaseAnim(idleAnim, true);
    }

    void Update()
    {
        moveX = 0f;
        if (Input.GetKey(KeyCode.A)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D)) moveX += 1f;

        if (Input.GetKeyDown(KeyCode.Space))
            TryJump();

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J) && isGrounded)
            TryAttack();

        if (Input.GetKey(KeyCode.Escape))
        {
           var current = animState.SetAnimation(0, deadAnim, false);
            current.Complete += (t) =>
            {
                animState.SetAnimation(0, idleAnim, true);
            };
        }

        UpdateGrounded();
        UpdateFacing();
        UpdateBaseAnimation();
    }


    void FixedUpdate()
    {
        // Movement
        if (isGrounded || allowAirControl)
        {
            rb.linearVelocity = new Vector2(moveX * moveSpeed, rb.linearVelocity.y);
        }
    }

    void TryJump()
    {
        UpdateGrounded();
        if (!isGrounded) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
    }

    void TryAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown) return;
        if (!canAttackInAir)
        {
            UpdateGrounded();
            if (!isGrounded) return;
        }

        lastAttackTime = Time.time;

        animState.SetAnimation(ATTACK_TRACK, attackAnim, false);
        animState.AddEmptyAnimation(ATTACK_TRACK, attackBlendOut, 0f);
    }

    void UpdateGrounded()
    {
        if (!groundCheck)
        {
            isGrounded = true; // fallback
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundMask);
    }

    void UpdateFacing()
    {
        if (Mathf.Abs(moveX) < 0.01f) return;

        bool wantRight = moveX > 0f;
        if (wantRight == facingRight) return;

        facingRight = wantRight;

        if (flipByTransformScale)
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (facingRight ? 1f : -1f);
            transform.localScale = s;
        }
        else
        {
            skeletonAnimation.Skeleton.ScaleX = Mathf.Abs(skeletonAnimation.Skeleton.ScaleX) * (facingRight ? 1f : -1f);
        }
    }

    void UpdateBaseAnimation()
    {
        float vx = Mathf.Abs(rb.linearVelocity.x);
        float vy = rb.linearVelocity.y;

        if (!isGrounded)
        {
            if (vy > 0.1f) SetBaseAnim(jumpAnim, true);
            else
            {
                SetBaseAnim(jumpAnim, true);
            }
            return;
        }

        if (vx > 0.05f) SetBaseAnim(runAnim, true);
        else SetBaseAnim(idleAnim, true);
    }

    void SetBaseAnim(string animName, bool loop)
    {
        if (currentBaseAnim == animName) return;
        currentBaseAnim = animName;
        animState.SetAnimation(BASE_TRACK, animName, loop);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
#endif
}
