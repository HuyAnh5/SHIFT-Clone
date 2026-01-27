using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelSelectGridUI : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private Button levelButtonPrefab;
    [SerializeField] private int levelCount = 50;
    [SerializeField] private int columns = 5; // hiện chưa dùng trong code (GridLayoutGroup tự xử)
    [SerializeField] private string gameSceneName = "Game";

    [Header("Lock")]
    [Tooltip("Bật = khóa theo unlocked_level. Tắt = không khóa level nào.")]
    [SerializeField] private bool useLock = false;

    private const string KEY_UNLOCKED = "unlocked_level";
    private const string KEY_START = "start_level_index";

    private void Start()
    {
        Build();
    }

    public void Build()
    {
        // Clear old buttons
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        int unlocked = useLock ? PlayerPrefs.GetInt(KEY_UNLOCKED, 1) : int.MaxValue;

        for (int i = 1; i <= levelCount; i++)
        {
            int idx = i;
            var btn = Instantiate(levelButtonPrefab, transform);

            var tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = $"LV_{idx}";

            // APPLY LOCK (or not)
            bool isLocked = useLock && (idx > unlocked);
            btn.interactable = !isLocked;

            // TODO (optional): nếu prefab có icon khóa thì bạn tự bật/tắt ở đây
            // Example:
            // var lockIcon = btn.transform.Find("LockIcon");
            // if (lockIcon != null) lockIcon.gameObject.SetActive(isLocked);

            btn.onClick.AddListener(() =>
            {
                PlayerPrefs.SetInt(KEY_START, idx);
                PlayerPrefs.Save();
                SceneManager.LoadScene(gameSceneName);
            });
        }
    }

    // Optional: gọi cái này nếu bạn muốn đổi lock runtime (ví dụ toggle trong settings)
    public void SetUseLock(bool value)
    {
        useLock = value;
        Build();
    }
}
