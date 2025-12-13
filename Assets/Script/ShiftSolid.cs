using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ShiftSolid : MonoBehaviour
{
    [SerializeField] private WorldState ownerWorld = WorldState.Black;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.15f;

    private Collider2D col;
    private SpriteRenderer sr;
    private Color baseColor;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    private void OnEnable() => WorldShiftManager.OnWorldChanged += Apply;
    private void OnDisable() => WorldShiftManager.OnWorldChanged -= Apply;

    private void Start()
    {
        if (WorldShiftManager.I != null)
            Apply(WorldShiftManager.I.SolidWorld);
    }

    private void Apply(WorldState solidWorld)
    {
        bool active = (solidWorld == ownerWorld);
        col.enabled = active;

        if (sr != null)
        {
            var c = baseColor;
            c.a = active ? 1f : inactiveAlpha;
            sr.color = c;
        }
    }
}
