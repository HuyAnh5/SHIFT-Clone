using UnityEngine;
using UnityEngine.UI;

public class MobileMarkSwapIcon : MonoBehaviour
{
    [SerializeField] private PlayerMarkSwapController markSwap;
    [SerializeField] private MobileAbilityGate gate;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite iconMark;
    [SerializeField] private Sprite iconSwap;

    [SerializeField] private bool hideSwapIconWhenLocked = true;

    private bool lastShowSwap;

    private void Awake()
    {
        if (markSwap == null) markSwap = FindAnyObjectByType<PlayerMarkSwapController>();
        if (gate == null) gate = FindAnyObjectByType<MobileAbilityGate>();
    }

    private void LateUpdate()
    {
        if (markSwap == null || icon == null || iconMark == null || iconSwap == null) return;

        bool showSwap = markSwap.UI_ShouldShowSwapIcon();
        if (hideSwapIconWhenLocked && gate != null && !gate.SwapUnlocked)
            showSwap = false;

        if (showSwap == lastShowSwap) return;
        icon.sprite = showSwap ? iconSwap : iconMark;
        lastShowSwap = showSwap;
    }
}
