using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelSelectGridUI : MonoBehaviour
{
    [SerializeField] private Button levelButtonPrefab;
    [SerializeField] private int levelCount = 50;
    [SerializeField] private int columns = 5;
    [SerializeField] private string gameSceneName = "Game";

    private const string KEY_UNLOCKED = "unlocked_level";
    private const string KEY_START = "start_level_index";

    private void Start()
    {
        Build();
    }

    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        int unlocked = PlayerPrefs.GetInt(KEY_UNLOCKED, 1);

        for (int i = 1; i <= levelCount; i++)
        {
            int idx = i;
            var btn = Instantiate(levelButtonPrefab, transform);

            var tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = $"LV_{idx}";

            bool isLocked = idx > unlocked;
            btn.interactable = !isLocked;

            // (tuỳ) nếu prefab có icon khóa, bạn tự bật/tắt ở đây

            btn.onClick.AddListener(() =>
            {
                PlayerPrefs.SetInt(KEY_START, idx);
                PlayerPrefs.Save();
                SceneManager.LoadScene(gameSceneName);
            });
        }
    }
}
