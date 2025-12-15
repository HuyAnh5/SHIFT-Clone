using System;
using UnityEngine;

public enum WorldState { Black, White }

public class WorldShiftManager : MonoBehaviour
{
    public static WorldShiftManager I { get; private set; }
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
        SolidWorld = (SolidWorld == WorldState.Black) ? WorldState.White : WorldState.Black;
        OnWorldChanged?.Invoke(SolidWorld);
    }

    public void SetWorld(WorldState world)
    {
        if (SolidWorld == world) return;
        SolidWorld = world;
        OnWorldChanged?.Invoke(SolidWorld);
    }

}
