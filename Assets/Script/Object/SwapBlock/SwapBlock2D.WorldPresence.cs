using UnityEngine;

public partial class SwapBlock2D
{
    private void ApplyWorld(WorldState solidWorld)
    {
        activeInWorld = (solidWorld == ownerWorld);

        // “world không active thì block không tồn tại”
        rb.simulated = activeInWorld;
        col.enabled = activeInWorld;
        sr.enabled = activeInWorld;

        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (!sr.enabled) return;
        sr.color = marked ? markedColor : baseColor;
    }
}
