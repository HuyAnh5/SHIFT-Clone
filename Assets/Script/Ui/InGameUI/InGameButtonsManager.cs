using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameButtonsManager : MonoBehaviour
{
    [Header("Buttons (Gameplay)")]
    [SerializeField] private Button btnReset;
    [SerializeField] private Button btnHome;
    [SerializeField] private Button btnOpenSettings;

    [Header("Settings")]
    [SerializeField] private SettingsPanelController settings;

    [Header("Scene Names")]
    [SerializeField] private string homeSceneName = "Home";

    [Header("Optional Refs")]
    [Tooltip("Nếu để trống, script sẽ auto FindAnyObjectByType<PlayerController>().")]
    [SerializeField] private PlayerController player;

    [Tooltip("Nếu bạn dùng GearQuickMenuDOTween, kéo vào để bấm icon xong thì tự đóng menu.")]
    [SerializeField] private GearQuickMenuDOTween gearMenu;

    [Tooltip("Nếu muốn khi không reset được (đang shift) thì rung camera.")]
    [SerializeField] private CameraShake2D failShake;

    [SerializeField] private bool autoCloseGearMenuOnAction = true;

    private void Awake()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (gearMenu == null) gearMenu = FindAnyObjectByType<GearQuickMenuDOTween>();
        if (failShake == null) failShake = FindAnyObjectByType<CameraShake2D>();

        if (btnReset != null) btnReset.onClick.AddListener(OnResetClicked);
        if (btnHome != null) btnHome.onClick.AddListener(OnHomeClicked);
        if (btnOpenSettings != null) btnOpenSettings.onClick.AddListener(OnOpenSettingsClicked);
    }

    private void OnDestroy()
    {
        if (btnReset != null) btnReset.onClick.RemoveListener(OnResetClicked);
        if (btnHome != null) btnHome.onClick.RemoveListener(OnHomeClicked);
        if (btnOpenSettings != null) btnOpenSettings.onClick.RemoveListener(OnOpenSettingsClicked);
    }

    private void OnResetClicked()
    {
        // Không cho reset khi đang shift
        if (player != null && player.IsShifting)
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
        if (settings != null) settings.Close();
        else Time.timeScale = 1f;

        SceneManager.LoadScene(homeSceneName);
    }

    private void OnOpenSettingsClicked()
    {
        if (autoCloseGearMenuOnAction && gearMenu != null) gearMenu.Close();

        if (settings == null)
        {
            Debug.LogWarning("[InGameButtonsManager] SettingsPanelController is null.");
            return;
        }

        settings.Open();
    }
}
