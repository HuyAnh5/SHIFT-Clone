using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;      // Kéo SettingsPanel (root) vào đây
    [SerializeField] private Button btnClose;       // Nút X (optional)
    [SerializeField] private bool pauseWhenOpen = true;

    private void Awake()
    {
        // panel có thể đang inactive sẵn cũng được
        if (btnClose != null) btnClose.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (btnClose != null) btnClose.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (panel == null) return;
        panel.SetActive(true);
        if (pauseWhenOpen) Time.timeScale = 0f;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
    }
}
