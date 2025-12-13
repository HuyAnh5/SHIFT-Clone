using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldTilemapSwitcher : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap whiteTilemap;
    [SerializeField] private Tilemap blackTilemap;

    [Header("Colliders To Toggle (drag TilemapCollider2D + CompositeCollider2D if you use it)")]
    [SerializeField] private Collider2D[] whiteColliders;
    [SerializeField] private Collider2D[] blackColliders;

    [Header("Visual Alpha")]
    [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.15f;

    private void OnEnable() => WorldShiftManager.OnWorldChanged += Apply;
    private void OnDisable() => WorldShiftManager.OnWorldChanged -= Apply;

    private void Start()
    {
        if (WorldShiftManager.I != null)
            Apply(WorldShiftManager.I.SolidWorld);
    }

    private void Apply(WorldState solidWorld)
    {
        bool whiteSolid = (solidWorld == WorldState.White);
        bool blackSolid = (solidWorld == WorldState.Black);

        SetColliders(whiteColliders, whiteSolid);
        SetColliders(blackColliders, blackSolid);

        SetTilemapAlpha(whiteTilemap, whiteSolid ? activeAlpha : inactiveAlpha);
        SetTilemapAlpha(blackTilemap, blackSolid ? activeAlpha : inactiveAlpha);
    }

    private void SetColliders(Collider2D[] cols, bool enabled)
    {
        if (cols == null) return;
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = enabled;
        }
    }

    private void SetTilemapAlpha(Tilemap tm, float a)
    {
        if (tm == null) return;
        var c = tm.color;
        c.a = a;
        tm.color = c;
    }
}
