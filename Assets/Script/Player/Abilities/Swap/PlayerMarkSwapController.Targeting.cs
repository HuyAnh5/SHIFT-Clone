using UnityEngine;

public partial class PlayerMarkSwapController
{
    private void TryMarkToggle()
    {
        WorldState w = CurrentWorld;
        int wi = CurrentWorldIndex;

        // Nếu đã mark trong world hiện tại
        if (markedByWorld[wi] != null)
        {
            if (markPressAgainToSwap)
            {
                TrySwap();
                return;
            }

            // Nếu muốn “toggle unmark” thay vì swap
            ClearMark(wi);
            return;
        }

        // Chưa mark => tìm block gần nhất để mark
        var candidate = FindNearestSwapBlockInCurrentWorld(w);
        if (candidate == null)
        {
            ShakeFail();
            return;
        }

        SetMark(wi, candidate);
    }

    private void SetMark(int wi, SwapBlock2D target)
    {
        if (target == null) return;

        // đảm bảo 1 mark / world
        ClearMark(wi);

        target.SetMarked(true);
        markedByWorld[wi] = target;
    }

    private void ClearMark(int wi)
    {
        var old = markedByWorld[wi];
        if (old != null)
            old.SetMarked(false);

        markedByWorld[wi] = null;
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
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = sb;
            }
        }

        return best;
    }
}
