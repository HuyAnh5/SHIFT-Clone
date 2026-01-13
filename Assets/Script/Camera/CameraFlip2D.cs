using UnityEngine;
using DG.Tweening;

public class CameraFlip2D : MonoBehaviour
{
    [SerializeField] private float flipDuration = 0.18f;
    [SerializeField] private Ease flipEase = Ease.InOutSine;

    public static CameraFlip2D I { get; private set; }

    // worldFlip: giống logic cũ (White => flipped)
    private bool worldFlip;
    // extraFlip: do GravityFlipTrigger (same-world)
    private bool extraFlip;

    public bool IsViewFlipped => worldFlip ^ extraFlip;

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
        if (WorldShiftManager.I != null)
            HandleWorldChanged(WorldShiftManager.I.SolidWorld);
        else
            ApplyRotation();
    }

    private void HandleWorldChanged(WorldState solidWorld)
    {
        // giữ convention hiện tại: White => camera flipped
        worldFlip = (solidWorld == WorldState.White);
        ApplyRotation();
    }

    // ===== API cho GravityFlipTrigger =====
    public void ToggleExtraFlip()
    {
        extraFlip = !extraFlip;
        ApplyRotation();
    }

    public void ResetExtraFlip()
    {
        if (!extraFlip) return;
        extraFlip = false;
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
