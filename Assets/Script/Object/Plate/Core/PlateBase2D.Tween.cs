using UnityEngine;
using DG.Tweening;

public abstract partial class PlateBase2D
{
    protected void SetOn(bool value, bool silent = false)
    {
        if (IsOn == value) return;
        IsOn = value;
        if (!silent) OnStateChanged?.Invoke(this, IsOn);
    }

    // ===== DOTween helpers =====
    protected virtual void KillAllTweens()
    {
        moveTween?.Kill();
        moveTween = null;
    }

    protected void TweenMoveTo(Vector2 targetPos, float duration, float delay, Ease ease)
    {
        moveTween?.Kill();
        moveTween = rb.DOMove(targetPos, duration)
            .SetEase(ease)
            .SetDelay(delay)
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject);
    }

    protected void KillMoveTweenOnly()
    {
        moveTween?.Kill();
        moveTween = null;
    }

    protected void TweenPressToBottom()
    {
        RecomputePressedPos();
        TweenMoveTo(pressedPos, pressDuration, pressDelay, pressEase);
    }

    protected void TweenRaiseToTop(float duration, float delay = 0f, Ease? easeOverride = null)
    {
        TweenMoveTo(basePos, duration, delay, easeOverride ?? raiseEase);
    }
}
