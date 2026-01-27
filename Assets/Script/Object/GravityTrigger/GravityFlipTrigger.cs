using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GravityFlipTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    private readonly HashSet<Collider2D> inside = new();

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (inside.Contains(other)) return;

        // Chỉ trigger khi chạm Tag Player (hỗ trợ collider con: check cả root)
        if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag))
            return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        inside.Add(other);

        // 1) đảo gravity
        player.FlipGravity();

        // 2) đảo view (extra flip)
        if (CameraFlip2D.I != null)
            CameraFlip2D.I.ToggleExtraFlip();
        else
            Debug.LogWarning("GravityFlipTrigger: CameraFlip2D not found");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        inside.Remove(other);
    }
}