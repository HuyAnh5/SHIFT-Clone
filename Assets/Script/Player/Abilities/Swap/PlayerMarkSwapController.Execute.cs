using System.Collections;
using UnityEngine;

public partial class PlayerMarkSwapController
{
    private void TrySwap()
    {
        if (swapping) { ShakeFail(); return; }

        int wi = CurrentWorldIndex;
        var target = markedByWorld[wi];

        if (target == null || !target.IsActiveInCurrentWorld())
        {
            ShakeFail();
            return;
        }

        // không cho swap khi ?ang shift animation
        if (playerController != null && playerController.IsShifting)
        {
            ShakeFail();
            return;
        }

        // OPTIONAL: n?u b?n mu?n b?t l?i ?i?u ki?n airborne
        if (enforceAirborneConstraint && requireAirborne && playerController != null && playerController.IsGroundedNow)
        {
            ShakeFail();
            return;
        }

        if (validateDestination)
        {
            if (!IsDestinationFreeForPlayer(target.Rb.position))
            {
                ShakeFail();
                return;
            }
        }

        StartCoroutine(SwapRoutine(target, wi));
    }

    private IEnumerator SwapRoutine(SwapBlock2D target, int wi)
    {
        swapping = true;

        yield return new WaitForFixedUpdate();

        Vector2 playerPos = rb.position;
        Vector2 playerVel = rb.linearVelocity;

        Rigidbody2D brb = target.Rb;
        Vector2 blockPos = brb.position;
        Vector2 blockVel = brb.linearVelocity;

        // swap v? trí, gi? quán tính riêng
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

    private bool IsDestinationFreeForPlayer(Vector2 destPos)
    {
        Bounds b = col.bounds;
        Vector2 size = b.size;

        // offset t? rb.position t?i center collider
        Vector2 offset = (Vector2)b.center - rb.position;
        Vector2 checkCenter = destPos + offset;

        var hit = Physics2D.OverlapBox(checkCenter, size * 0.95f, 0f, solidMask);
        return hit == null;
    }
}
