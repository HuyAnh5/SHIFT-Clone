using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GravityFlipTrigger : MonoBehaviour
{
    private void Reset()
    {
        // đảm bảo collider là trigger
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 1) Flip gravity (same-world)
        player.FlipGravity();

        // 2) Flip camera view giống Shift (nhưng không đổi world)
        if (CameraFlip2D.I != null)
            CameraFlip2D.I.ToggleLocalFlip();
        else
            Debug.LogWarning("GravityFlipTrigger: CameraFlip2D not found");
    }
}
