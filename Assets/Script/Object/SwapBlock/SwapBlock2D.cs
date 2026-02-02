using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public partial class SwapBlock2D : MonoBehaviour
{
    [Header("World Owner")]
    [SerializeField] private WorldState ownerWorld = WorldState.Black;

    [Header("Mark Visual")]
    [SerializeField] private Color markedColor = new Color(1f, 0.55f, 0.1f, 1f);

    [Header("Physics: prevent push/pull")]
    [Tooltip("Freeze X để player không đẩy block sang ngang.")]
    [SerializeField] private bool freezeX = true;

    [Header("Grid Lock (optional)")]
    [SerializeField] private bool lockToGrid = true;
    [SerializeField] private bool lockXToCellCenter = true;
    [SerializeField] private bool lockYWhenNearlyStopped = true;
    [SerializeField] private float ySnapVelThreshold = 0.05f;

    public WorldState OwnerWorld => ownerWorld;
    public Rigidbody2D Rb => rb;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;

    private Color baseColor;
    private bool marked;
    private bool activeInWorld;
    private Grid runtimeGrid;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        baseColor = sr.color;

        rb.freezeRotation = true;
        if (freezeX)
            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;

        // grid sẽ được spawner inject; fallback nếu quên
        if (runtimeGrid == null)
            runtimeGrid = FindAnyObjectByType<Grid>();

        ApplyWorld(WorldShiftManager.I != null ? WorldShiftManager.I.SolidWorld : ownerWorld);
    }

    private void OnEnable() => WorldShiftManager.OnWorldChanged += ApplyWorld;
    private void OnDisable() => WorldShiftManager.OnWorldChanged -= ApplyWorld;

    public void Initialize(Grid grid)
    {
        runtimeGrid = grid;
        SnapImmediate();
    }

    public bool IsActiveInCurrentWorld() => activeInWorld;

    public void SetMarked(bool value)
    {
        marked = value;
        ApplyVisual();
    }
}
