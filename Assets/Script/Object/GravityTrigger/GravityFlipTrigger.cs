using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GravityFlipTrigger : MonoBehaviour
{
    private readonly HashSet<Collider2D> inside = new();

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (inside.Contains(other)) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        inside.Add(other);

        // 1) đảo gravity
        player.FlipGravity();

        // 2) đảo view (giống Shift) nhưng là "extra flip"
        if (CameraFlip2D.I != null)
            CameraFlip2D.I.ToggleExtraFlip();
        else
            Debug.LogWarning("GravityFlipTrigger: CameraFlip2D not found");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (inside.Contains(other))
            inside.Remove(other);
    }
}
