using System;
using UnityEngine;

public enum WorldState { Black, White }

public class WorldShiftManager : MonoBehaviour
{
    public static WorldShiftManager I { get; private set; }
    /// <summary>
    /// Fired BEFORE any world-specific objects toggle their colliders/renderers.
    /// Use this to snapshot occupancy/latches reliably (prevents 1-frame miss when SHIFT disables colliders).
    /// Args: fromWorld, toWorld
    /// </summary>
    public static event Action<WorldState, WorldState> OnPreWorldChange;
    public static event Action<WorldState> OnWorldChanged;

    [SerializeField] private WorldState startSolidWorld = WorldState.White;

    public WorldState SolidWorld { get; private set; }

    // alias cho code cũ (nếu còn file nào gọi Current)
    public WorldState Current => SolidWorld;

    // dùng cho đảo input khi camera xoay 180
    public bool IsViewFlipped => SolidWorld == WorldState.White;

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        SolidWorld = startSolidWorld;
    }

    private void Start()
    {
        OnWorldChanged?.Invoke(SolidWorld);
    }

    public void Toggle()
    {
        var from = SolidWorld;
        var to = (SolidWorld == WorldState.Black) ? WorldState.White : WorldState.Black;

        OnPreWorldChange?.Invoke(from, to);
        SolidWorld = to;
        OnWorldChanged?.Invoke(SolidWorld);
    }

    public void SetWorld(WorldState world)
    {
        if (SolidWorld == world) return;
        var from = SolidWorld;
        var to = world;

        OnPreWorldChange?.Invoke(from, to);
        SolidWorld = to;
        OnWorldChanged?.Invoke(SolidWorld);
    }

}
