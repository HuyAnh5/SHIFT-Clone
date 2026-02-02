using UnityEngine;

public partial class SwapBlock2D
{
    private void FixedUpdate()
    {
        if (!lockToGrid || !activeInWorld || runtimeGrid == null) return;

        Vector2 pos = rb.position;
        Vector3 cellCenter = runtimeGrid.GetCellCenterWorld(runtimeGrid.WorldToCell(pos));

        if (lockXToCellCenter)
            pos.x = cellCenter.x;

        if (lockYWhenNearlyStopped && Mathf.Abs(rb.linearVelocity.y) <= ySnapVelThreshold)
            pos.y = cellCenter.y;

        rb.position = pos;
    }

    private void SnapImmediate()
    {
        if (!lockToGrid || runtimeGrid == null) return;
        Vector3 c = runtimeGrid.GetCellCenterWorld(runtimeGrid.WorldToCell(transform.position));
        rb.position = c;
        transform.position = c;
    }
}
