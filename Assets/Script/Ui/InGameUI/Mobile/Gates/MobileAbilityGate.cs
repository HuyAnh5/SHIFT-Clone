using UnityEngine;

public class MobileAbilityGate : MonoBehaviour
{
    [Header("Unlock by Level (LV_#)")]
    [SerializeField] private bool useMaxLevelReached = false;

    [Min(1)][SerializeField] private int unlockJumpAtLevel = 2;
    [Min(1)][SerializeField] private int unlockActionAtLevel = 3;
    [Min(1)][SerializeField] private int unlockSwapAtLevel = 4;

    private const string PREF_MAX_LEVEL = "MaxLevelReached";
    private int gateLevelCached = 1;
    private int lastLevelIndexSeen = -1;

    public int GateLevel => gateLevelCached;
    public bool JumpUnlocked => gateLevelCached >= unlockJumpAtLevel;
    public bool ActionUnlocked => gateLevelCached >= unlockActionAtLevel;
    public bool SwapUnlocked => gateLevelCached >= unlockSwapAtLevel;

    private void LateUpdate() => Refresh();

    public void Refresh(bool force = false)
    {
        int currentLevel = 1;
        if (LevelManager.I != null)
            currentLevel = Mathf.Max(1, LevelManager.I.CurrentLevelIndex);

        if (!force && currentLevel == lastLevelIndexSeen) return;
        lastLevelIndexSeen = currentLevel;

        if (!useMaxLevelReached)
        {
            gateLevelCached = currentLevel;
            return;
        }

        int max = PlayerPrefs.GetInt(PREF_MAX_LEVEL, currentLevel);
        if (currentLevel > max)
        {
            max = currentLevel;
            PlayerPrefs.SetInt(PREF_MAX_LEVEL, max);
        }
        gateLevelCached = Mathf.Max(1, max);
    }
}
