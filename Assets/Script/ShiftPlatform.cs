using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ShiftPlatform : MonoBehaviour
{
    public WorldState ownerWorld = WorldState.Black;
    public bool alsoToggleRenderer = true;

    Collider2D col;
    Renderer rend;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        rend = GetComponent<Renderer>();
    }

    void OnEnable() => WorldShiftManager.OnWorldChanged += Apply;
    void OnDisable() => WorldShiftManager.OnWorldChanged -= Apply;

    void Start()
    {
        if (WorldShiftManager.I != null)
            Apply(WorldShiftManager.I.Current);
    }

    void Apply(WorldState current)
    {
        bool active = (current == ownerWorld);
        col.enabled = active;
        if (alsoToggleRenderer && rend) rend.enabled = active; // hoặc mờ alpha thay vì tắt
    }
}
