using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject panelLevelSelect;
    [SerializeField] private GameObject panelSettings;

    [Header("Config")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private int levelCount = 50;

    private const string KEY_UNLOCKED = "unlocked_level";
    private const string KEY_START = "start_level_index";

    private void Start()
    {
        if (panelLevelSelect != null) panelLevelSelect.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(false);

        // đảm bảo có unlocked mặc định
        if (!PlayerPrefs.HasKey(KEY_UNLOCKED))
            PlayerPrefs.SetInt(KEY_UNLOCKED, 1);
    }

    public void OnPlay()
    {
        int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(KEY_UNLOCKED, 1), 1, levelCount);
        PlayerPrefs.SetInt(KEY_START, unlocked);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenSelectLevel()
    {
        if (panelLevelSelect != null) panelLevelSelect.SetActive(true);
    }

    public void OnCloseSelectLevel()
    {
        if (panelLevelSelect != null) panelLevelSelect.SetActive(false);
    }

    public void OnOpenSettings()
    {
        if (panelSettings != null) panelSettings.SetActive(true);
    }

    public void OnCloseSettings()
    {
        if (panelSettings != null) panelSettings.SetActive(false);
    }

    // Gọi từ nút "Reset Progress" (nên có popup confirm nếu bạn muốn)
    public void OnResetProgress()
    {
        PlayerPrefs.SetInt(KEY_UNLOCKED, 1);
        PlayerPrefs.SetInt(KEY_START, 1);
        PlayerPrefs.Save();

        // nếu đang mở level select thì refresh UI (tuỳ bạn làm thêm hàm Refresh trong level select)
    }

    public void OnQuit()
    {
        Application.Quit();
    }
}
