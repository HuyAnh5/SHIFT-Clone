using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameButtonsManager : MonoBehaviour
{
    [Header("Buttons (Gameplay)")]
    [SerializeField] private Button btnReset;
    [SerializeField] private Button btnHome;
    [SerializeField] private Button btnOpenSettings;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;     // panel setting (đang SetActive(false) sẵn cũng ok)
    [SerializeField] private Button btnCloseSettings;      // optional: nút X trong panel
    [SerializeField] private bool pauseWhenSettingsOpen = true;

    [Header("Scene Names")]
    [SerializeField] private string homeSceneName = "Home";

    [Header("Optional Refs")]
    [Tooltip("Nếu để trống, script sẽ auto FindAnyObjectByType<PlayerController>().")]
    [SerializeField] private PlayerController player;

    [Tooltip("Nếu bạn dùng GearQuickMenuDOTween (menu bánh răng), kéo vào để bấm icon xong thì tự đóng menu.")]
    [SerializeField] private GearQuickMenuDOTween gearMenu;

    [Tooltip("Nếu muốn khi không reset được (đang shift) thì rung camera.")]
    [SerializeField] private CameraShake2D failShake;

    [SerializeField] private bool autoCloseGearMenuOnAction = true;

    // reflection cache: đọc private bool shifting trong PlayerController (để chặn reset y như nút R)
    private static FieldInfo shiftingField;

    private void Awake()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (gearMenu == null) gearMenu = FindAnyObjectByType<GearQuickMenuDOTween>();
        if (failShake == null) failShake = FindAnyObjectByType<CameraShake2D>();

        if (settingsPanel != null) settingsPanel.SetActive(false);

        CacheReflection();

        if (btnReset != null) btnReset.onClick.AddListener(OnResetClicked);
        if (btnHome != null) btnHome.onClick.AddListener(OnHomeClicked);
        if (btnOpenSettings != null) btnOpenSettings.onClick.AddListener(OnOpenSettingsClicked);
        if (btnCloseSettings != null) btnCloseSettings.onClick.AddListener(CloseSettings);
    }

    private void OnDestroy()
    {
        if (btnReset != null) btnReset.onClick.RemoveListener(OnResetClicked);
        if (btnHome != null) btnHome.onClick.RemoveListener(OnHomeClicked);
        if (btnOpenSettings != null) btnOpenSettings.onClick.RemoveListener(OnOpenSettingsClicked);
        if (btnCloseSettings != null) btnCloseSettings.onClick.RemoveListener(CloseSettings);
    }

    // -------------------- Actions --------------------

    private void OnResetClicked()
    {
        // Giống Player: không cho reset khi đang shift
        if (IsPlayerShifting())
        {
            if (failShake != null) failShake.ShakeFail();
            return;
        }

        if (autoCloseGearMenuOnAction && gearMenu != null) gearMenu.Close();

        if (LevelManager.I != null)
            LevelManager.I.ReloadCurrentLevel();
        else
            Debug.LogWarning("[InGameButtonsManager] LevelManager.I is null (cannot reset).");
    }

    private void OnHomeClicked()
    {
        if (autoCloseGearMenuOnAction && gearMenu != null) gearMenu.Close();

        // đảm bảo không bị kẹt pause khi về Home
        Time.timeScale = 1f;

        SceneManager.LoadScene(homeSceneName);
    }

    private void OnOpenSettingsClicked()
    {
        if (autoCloseGearMenuOnAction && gearMenu != null) gearMenu.Close();

        if (settingsPanel == null)
        {
            Debug.LogWarning("[InGameButtonsManager] settingsPanel is null.");
            return;
        }

        settingsPanel.SetActive(true);

        if (pauseWhenSettingsOpen)
            Time.timeScale = 0f;
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    // -------------------- Helpers --------------------

    private static void CacheReflection()
    {
        if (shiftingField != null) return;
        shiftingField = typeof(PlayerController).GetField("shifting", BindingFlags.Instance | BindingFlags.NonPublic);
        // nếu field không tìm thấy thì IsPlayerShifting() sẽ fallback = false (không chặn reset)
    }

    private bool IsPlayerShifting()
    {
        if (player == null) return false;
        if (shiftingField == null) return false;

        try
        {
            object v = shiftingField.GetValue(player);
            return v is bool b && b;
        }
        catch
        {
            return false;
        }
    }
}
