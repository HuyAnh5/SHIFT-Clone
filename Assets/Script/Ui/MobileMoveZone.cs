using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// MoveZone: 1 vùng điều khiển (1 ngón) tự chia đôi trái/phải.
/// - Touch/drag nửa trái => MobileUIInput.LeftHeld = true
/// - Touch/drag nửa phải => MobileUIInput.RightHeld = true
/// - Ở giữa (dead zone) => không di chuyển
/// 
/// Visual feedback (tuỳ chọn): gán 2 CanvasGroup để làm nút trái/phải sáng lên.
/// Lưu ý: các icon/image con chỉ để vẽ nên đặt Raycast Target = OFF,
/// chỉ panel MoveZone (Image trong suốt) Raycast Target = ON.
/// </summary>
public class MobileMoveZone : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Input")]
    [SerializeField] private RectTransform zone;              // để trống = RectTransform của object này
    [SerializeField] private float deadZonePixels = 12f;      // vùng giữa không đi

    [Header("Visual (optional)")]
    [SerializeField] private CanvasGroup leftVisual;          // gán CanvasGroup của icon/nút trái (visual)
    [SerializeField] private CanvasGroup rightVisual;         // gán CanvasGroup của icon/nút phải (visual)
    [SerializeField] private float idleAlpha = 0.35f;         // alpha khi không được chọn
    [SerializeField] private float pressedAlpha = 1f;         // alpha khi đang chọn

    private int activePointerId = int.MinValue;

    private enum MoveState { None, Left, Right }
    private MoveState state = MoveState.None;

    private RectTransform RT => zone != null ? zone : (RectTransform)transform;

    private void OnEnable()
    {
        SetState(MoveState.None);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (activePointerId != int.MinValue) return; // chỉ nhận 1 ngón
        activePointerId = e.pointerId;
        UpdateMove(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;
        UpdateMove(e);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != activePointerId) return;
        activePointerId = int.MinValue;
        SetState(MoveState.None);
    }

    private void UpdateMove(PointerEventData e)
    {
        // Nếu trượt ra khỏi zone thì thả
        if (!RectTransformUtility.RectangleContainsScreenPoint(RT, e.position, e.pressEventCamera))
        {
            SetState(MoveState.None);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(RT, e.position, e.pressEventCamera, out var local);

        // local đang tính theo pivot => quy về trục X tính theo "tâm" rect để pivot nào cũng đúng
        float centeredX = local.x - RT.rect.width * (0.5f - RT.pivot.x);

        if (Mathf.Abs(centeredX) <= deadZonePixels)
        {
            SetState(MoveState.None);
            return;
        }

        SetState(centeredX < 0f ? MoveState.Left : MoveState.Right);
    }

    private void SetState(MoveState newState)
    {
        if (state == newState) return;
        state = newState;

        // Input: đảm bảo không bao giờ vừa Left vừa Right
        MobileUIInput.SetLeft(state == MoveState.Left);
        MobileUIInput.SetRight(state == MoveState.Right);

        // Visual: chỉ 1 nút được "sáng" tại 1 thời điểm
        if (leftVisual != null)
            leftVisual.alpha = (state == MoveState.Left) ? pressedAlpha : idleAlpha;

        if (rightVisual != null)
            rightVisual.alpha = (state == MoveState.Right) ? pressedAlpha : idleAlpha;
    }
}
