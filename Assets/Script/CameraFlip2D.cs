using UnityEngine;
using DG.Tweening;

public class CameraFlip2D : MonoBehaviour
{
    [SerializeField] private float flipDuration = 0.18f;
    [SerializeField] private Ease flipEase = Ease.InOutSine;

    private Tween rotateTween;

    private void OnEnable() => WorldShiftManager.OnWorldChanged += HandleWorldChanged;
    private void OnDisable() => WorldShiftManager.OnWorldChanged -= HandleWorldChanged;

    private void Start()
    {
        if (WorldShiftManager.I != null)
            HandleWorldChanged(WorldShiftManager.I.SolidWorld);
    }

    private void HandleWorldChanged(WorldState solidWorld)
    {
        float targetZ = (solidWorld == WorldState.White) ? 180f : 0f;

        rotateTween?.Kill();
        rotateTween = transform
            .DORotate(new Vector3(0f, 0f, targetZ), flipDuration, RotateMode.Fast)
            .SetEase(flipEase);
    }
}