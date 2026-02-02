using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GravityFlipTrigger : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Keyboard Action")]
    [SerializeField] private KeyCode actionKey = KeyCode.E;

    [Header("Anti spam")]
    [SerializeField] private float activateCooldown = 0.15f;

    private readonly HashSet<Collider2D> inside = new();
    private PlayerController playerInside;
    private float nextActivateTime;

    // --------- Global (for UI priority) ----------
    private static readonly List<GravityFlipTrigger> s_active = new();
    public static bool PlayerInsideAny => s_active.Count > 0;

    /// <summary>UI/Zone gọi để ưu tiên flip gravity nếu player đang đứng trong trigger.</summary>
    public static bool TryActivateFromUI()
    {
        if (s_active.Count == 0) return false;

        // Ưu tiên trigger mới vào nhất
        var t = s_active[s_active.Count - 1];
        if (t == null) { s_active.RemoveAt(s_active.Count - 1); return false; }

        return t.TryActivate();
    }

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    private void Update()
    {
        // Keyboard: chỉ flip khi đang trong vùng + bấm action
        if (playerInside == null) return;
        if (Time.unscaledTime < nextActivateTime) return;

        if (Input.GetKeyDown(actionKey))
            TryActivate();
    }

    private bool TryActivate()
    {
        if (playerInside == null) return false;
        if (Time.unscaledTime < nextActivateTime) return false;

        nextActivateTime = Time.unscaledTime + activateCooldown;

        // 1) đảo gravity + hướng di chuyển (giữ nguyên logic hiện tại của PlayerController)
        playerInside.FlipGravity();

        // 2) đảo camera
        if (CameraFlip2D.I != null)
            CameraFlip2D.I.ToggleExtraFlip();
        else
            Debug.LogWarning("GravityFlipTrigger: CameraFlip2D not found");

        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (inside.Contains(other)) return;

        if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag))
            return;

        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;

        inside.Add(other);

        // collider đầu tiên của player vào vùng -> đăng ký active
        if (inside.Count == 1)
        {
            playerInside = p;
            if (!s_active.Contains(this)) s_active.Add(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!inside.Remove(other)) return;

        // collider cuối cùng ra khỏi vùng -> bỏ active
        if (inside.Count == 0)
        {
            playerInside = null;
            s_active.Remove(this);
        }
    }

    private void OnDisable()
    {
        // tránh bị “kẹt active” nếu object bị disable khi đang đứng trong vùng
        inside.Clear();
        playerInside = null;
        s_active.Remove(this);
    }
}
