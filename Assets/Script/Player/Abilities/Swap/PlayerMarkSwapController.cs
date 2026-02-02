using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public partial class PlayerMarkSwapController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode markKey = KeyCode.E;
    [SerializeField] private KeyCode swapKey = KeyCode.F; // có thể set None nếu không muốn

    [Header("Mark")]
    [SerializeField] private float markRadius = 0.7f;
    [SerializeField] private LayerMask swapBlockMask;

    [Header("Swap Constraints (optional)")]
    [Tooltip("Giữ lại để sau này bật lại nếu muốn. Hiện tại KHÔNG ép điều kiện airborne (để không phá behavior hiện tại).")]
    [SerializeField] private bool requireAirborne = true;

    [Tooltip("Nếu bật, swap sẽ fail khi player đang grounded và requireAirborne = true.")]
    [SerializeField] private bool enforceAirborneConstraint = false;

    [Header("Mark press behavior")]
    [Tooltip("Bấm Mark lần 2 khi đang có mark trong world hiện tại => Swap luôn (đúng behavior bạn đang dùng).")]
    [SerializeField] private bool markPressAgainToSwap = true;

    [Header("Optional: destination safety check")]
    [SerializeField] private bool validateDestination = false;
    [SerializeField] private LayerMask solidMask;

    [Header("Refs")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraShake2D cameraShake;

    private Rigidbody2D rb;
    private Collider2D col;

    // mark riêng theo world: index 0=Black, 1=White
    private readonly SwapBlock2D[] markedByWorld = new SwapBlock2D[2];

    private readonly Collider2D[] overlapHits = new Collider2D[12];
    private bool swapping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (cameraShake == null) cameraShake = CameraShake2D.I;
    }

    private void OnDisable()
    {
        swapping = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(markKey) || MobileUIInput.ConsumeMarkDown())
            TryMarkToggle();

        // Hỗ trợ swap key / mobile swap nếu bạn muốn dùng nút swap riêng
        if ((swapKey != KeyCode.None && Input.GetKeyDown(swapKey)) || MobileUIInput.ConsumeSwapDown())
            TrySwap();
    }

    // UI gọi khi bấm nút Mark
    public void UI_MarkToggle() => TryMarkToggle();

    // UI gọi khi bấm nút Swap
    public void UI_Swap() => TrySwap();

    public bool UI_ShouldShowSwapIcon()
    {
        int wi = CurrentWorldIndex;
        var t = markedByWorld[wi];

        if (t == null) return false;

        // nếu target không còn active nữa => clear để khỏi kẹt icon swap
        if (!t.IsActiveInCurrentWorld())
        {
            markedByWorld[wi] = null;
            return false;
        }

        // CỐT LÕI: chỉ cần đang có mark là hiện icon Swap (không check grounded/airborne)
        return true;
    }

    private WorldState CurrentWorld => (WorldShiftManager.I != null) ? WorldShiftManager.I.SolidWorld : WorldState.Black;
    private int CurrentWorldIndex => WorldIndex(CurrentWorld);

    private static int WorldIndex(WorldState w) => (w == WorldState.Black) ? 0 : 1;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, markRadius);
    }
#endif
}
