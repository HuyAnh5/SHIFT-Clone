using UnityEngine;
using DG.Tweening;

public partial class PlayerController
{
    private void ApplyFacing(float x)
    {
        if (spriteRenderer == null) return;
        if (Mathf.Abs(x) < 0.01f) return;
        spriteRenderer.flipX = (x < 0f);
    }

    private void PlayJumpStretch()
    {
        if (visualRoot == null) return;

        squashTween?.Kill();

        Vector3 squash = new Vector3(baseVisualScale.x * jumpSquashX, baseVisualScale.y * jumpSquashY, baseVisualScale.z);
        Vector3 stretch = new Vector3(baseVisualScale.x * jumpStretchX, baseVisualScale.y * jumpStretchY, baseVisualScale.z);

        squashTween = DOTween.Sequence()
            .Append(visualRoot.DOScale(squash, jumpSquashDuration).SetEase(squashEase))
            .Append(visualRoot.DOScale(stretch, jumpStretchDuration).SetEase(stretchEase))
            .Append(visualRoot.DOScale(baseVisualScale, 0.08f).SetEase(Ease.OutQuad))
            .SetLink(gameObject);
    }

    private void PlayLandSquash()
    {
        if (visualRoot == null) return;

        squashTween?.Kill();

        Vector3 land = new Vector3(baseVisualScale.x * landSquashX, baseVisualScale.y * landSquashY, baseVisualScale.z);

        squashTween = DOTween.Sequence()
            .Append(visualRoot.DOScale(land, landDuration).SetEase(squashEase))
            .Append(visualRoot.DOScale(baseVisualScale, landDuration * 0.9f).SetEase(Ease.OutQuad))
            .SetLink(gameObject);
    }
}
