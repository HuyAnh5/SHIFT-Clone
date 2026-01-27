using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMarkSwapController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode markKey = KeyCode.E;
    [SerializeField] private KeyCode swapKey = KeyCode.F;

    [Header("Mark")]
    [SerializeField] private float markRadius = 0.7f;
    [SerializeField] private LayerMask swapBlockMask;

    [Header("Swap Constraints")]
    [SerializeField] private bool requireAirborne = true;

    [Header("Optional: destination safety check")]
    [SerializeField] private bool validateDestination = false;
    [SerializeField] private LayerMask solidMask; // dùng cùng layer với ground/wall nếu muốn check kẹt

    [Header("Refs")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraShake2D cameraShake;

    private Rigidbody2D rb;
    private Collider2D col;

    // mark riêng theo world: index 0=Black, 1=White
    private SwapBlock2D[] markedByWorld = new SwapBlock2D[2];

    private readonly Collider2D[] overlapHits = new Collider2D[12];
    private bool swapping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (cameraShake == null && Camera.main != null)
            cameraShake = Camera.main.GetComponent<CameraShake2D>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(markKey) || MobileUIInput.ConsumeMarkDown())
            TryMarkToggle();

        //if (Input.GetKeyDown(swapKey) || MobileUIInput.ConsumeSwapDown())
        //    TrySwap();
    }

    private void TryMarkToggle()
    {
        WorldState w = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        int wi = WorldIndex(w);

        // Nếu đã mark trong world hiện tại => bấm Mark lần 2 sẽ Swap luôn
        if (markedByWorld[wi] != null)
        {
            TrySwap();
            return;
        }

        // Chưa mark => tìm block gần nhất để mark
        var candidate = FindNearestSwapBlockInCurrentWorld(w);
        if (candidate == null)
        {
            cameraShake?.ShakeFail();
            return;
        }

        candidate.SetMarked(true);
        markedByWorld[wi] = candidate;
    }

    private void TrySwap()
    {
        if (swapping) { cameraShake?.ShakeFail(); return; }

        WorldState w = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        int wi = WorldIndex(w);

        var target = markedByWorld[wi];
        if (target == null || !target.IsActiveInCurrentWorld())
        {
            cameraShake?.ShakeFail();
            return;
        }

        // không cho swap khi đang shift animation
        if (playerController != null && playerController.IsShifting)
        {
            cameraShake?.ShakeFail();
            return;
        }

        if (validateDestination)
        {
            if (!IsDestinationFreeForPlayer(target.Rb.position))
            {
                cameraShake?.ShakeFail();
                return;
            }
        }

        StartCoroutine(SwapRoutine(target, wi));
    }

    private IEnumerator SwapRoutine(SwapBlock2D target, int wi)
    {
        swapping = true;

        // làm trong fixed để ít “giật” hơn
        yield return new WaitForFixedUpdate();

        Vector2 playerPos = rb.position;
        Vector2 playerVel = rb.linearVelocity;

        Rigidbody2D brb = target.Rb;
        Vector2 blockPos = brb.position;
        Vector2 blockVel = brb.linearVelocity;

        // swap vị trí, giữ quán tính riêng
        rb.position = blockPos;
        rb.linearVelocity = playerVel;

        brb.position = playerPos;
        brb.linearVelocity = blockVel;

        Physics2D.SyncTransforms();

        // auto-unmark sau khi swap
        target.SetMarked(false);
        markedByWorld[wi] = null;

        swapping = false;
    }

    private SwapBlock2D FindNearestSwapBlockInCurrentWorld(WorldState w)
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, markRadius, overlapHits, swapBlockMask);
        if (count <= 0) return null;

        float bestD2 = float.PositiveInfinity;
        SwapBlock2D best = null;

        for (int i = 0; i < count; i++)
        {
            var c = overlapHits[i];
            if (c == null) continue;

            var sb = c.GetComponentInParent<SwapBlock2D>();
            if (sb == null) continue;
            if (sb.OwnerWorld != w) continue;
            if (!sb.IsActiveInCurrentWorld()) continue;

            float d2 = ((Vector2)sb.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = sb; }
        }

        return best;
    }

    private bool IsDestinationFreeForPlayer(Vector2 destPos)
    {
        // check overlap bằng bounds collider player tại vị trí mới
        Bounds b = col.bounds;
        Vector2 size = b.size;
        Vector2 offset = (Vector2)b.center - rb.position;

        Vector2 checkCenter = destPos + offset;
        var hit = Physics2D.OverlapBox(checkCenter, size * 0.95f, 0f, solidMask);
        return hit == null;
    }

    // UI gọi khi bấm nút Mark
    public void UI_MarkToggle()
    {
        TryMarkToggle();
    }

    // UI gọi khi bấm nút Swap (hoặc Action khi airborne)
    public void UI_Swap()
    {
        TrySwap();
    }

    public bool UI_ShouldShowSwapIcon()
    {
        WorldState w = (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        int wi = WorldIndex(w);

        var t = markedByWorld[wi];

        // nếu null => không có mark => hiện icon Mark
        if (t == null) return false;

        // nếu target không còn active/đúng world nữa => auto clear để khỏi kẹt icon swap
        if (!t.IsActiveInCurrentWorld())
        {
            markedByWorld[wi] = null;
            return false;
        }

        // CỐT LÕI: chỉ cần đang có mark là hiện icon Swap (không check grounded/airborne)
        return true;
    }


    private static int WorldIndex(WorldState w) => (w == WorldState.Black) ? 0 : 1;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, markRadius);
    }
#endif
}