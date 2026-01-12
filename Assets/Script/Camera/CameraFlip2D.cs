using UnityEngine;
using DG.Tweening;

public class CameraFlip2D : MonoBehaviour
{
    [SerializeField] private float flipDuration = 0.18f;
    [SerializeField] private Ease flipEase = Ease.InOutSine;

    public static CameraFlip2D I { get; private set; }

    // Parity tách đôi:
    // - worldParity: do ShiftWorld (Black/White)
    // - localParity: do GravityFlipTrigger (same-world)
    private bool worldParity;
    private bool localParity;

    public bool IsViewFlipped => worldParity ^ localParity;

    private Tween rotateTween;

    private void Awake()
    {
        I = this;
    }

    private void OnEnable()
    {
        WorldShiftManager.OnWorldChanged += HandleWorldChanged;
    }

    private void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= HandleWorldChanged;

        if (I == this) I = null;
    }

    private void Start()
    {
        // Sync theo world hiện tại (để vào level đã đúng hướng)
        if (WorldShiftManager.I != null)
            HandleWorldChanged(WorldShiftManager.I.SolidWorld);
        else
            ApplyRotation(); // fallback
    }

    private void HandleWorldChanged(WorldState solidWorld)
    {
        // Base parity theo world (giữ behavior cũ của bạn)
        worldParity = (solidWorld == WorldState.White);
        ApplyRotation();
    }

    // ==== API cho GravityFlipTrigger (không đổi world) ====
    public void ToggleLocalFlip()
    {
        localParity = !localParity;
        ApplyRotation();
    }

    public void ResetLocalFlip()
    {
        if (!localParity) return;
        localParity = false;
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        float targetZ = IsViewFlipped ? 180f : 0f;

        rotateTween?.Kill();
        rotateTween = transform
            .DORotate(new Vector3(0f, 0f, targetZ), flipDuration, RotateMode.Fast)
            .SetEase(flipEase);
    }
}
