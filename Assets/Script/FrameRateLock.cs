using UnityEngine;

public class FrameRateLock : MonoBehaviour
{
    [SerializeField] private int targetFps = 60;
    [SerializeField] private bool disableVSync = true;

    private void Awake()
    {
        if (disableVSync) QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFps;
    }
}
