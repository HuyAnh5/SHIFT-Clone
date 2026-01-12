using System;
using UnityEngine;

/// <summary>
/// Tracks whether the CAMERA/VIEW is currently flipped (rotated 180°).
/// View flip = (worldFlip) XOR (extraFlip).
/// - worldFlip: WorldShiftManager current solid world (White == flipped).
/// - extraFlip: toggled by gravity-only triggers (does NOT change world).
/// </summary>
public class ViewFlipManager : MonoBehaviour
{
    public static ViewFlipManager I { get; private set; }
    public static event Action<bool> OnViewFlipChanged;

    [SerializeField] private bool startExtraFlip = false;

    private bool extraFlip;
    private bool lastIsFlipped;

    public bool ExtraFlip => extraFlip;
    public bool IsViewFlipped => ComputeIsFlipped();

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;

        extraFlip = startExtraFlip;
        lastIsFlipped = ComputeIsFlipped();
    }

    private void OnEnable()
    {
        WorldShiftManager.OnWorldChanged += HandleWorldChanged;
        RecomputeAndBroadcast(force: true);
    }

    private void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= HandleWorldChanged;
    }

    private void Start()
    {
        RecomputeAndBroadcast(force: true);
    }

    private void HandleWorldChanged(WorldState _)
    {
        RecomputeAndBroadcast(force: false);
    }

    public void ToggleExtraFlip()
    {
        extraFlip = !extraFlip;
        RecomputeAndBroadcast(force: true);
    }

    private bool ComputeIsFlipped()
    {
        bool worldFlip = false;
        if (WorldShiftManager.I != null)
            worldFlip = (WorldShiftManager.I.SolidWorld == WorldState.White);

        return worldFlip ^ extraFlip;
    }

    private void RecomputeAndBroadcast(bool force)
    {
        bool now = ComputeIsFlipped();
        if (!force && now == lastIsFlipped) return;

        lastIsFlipped = now;
        OnViewFlipChanged?.Invoke(now);
    }
}
