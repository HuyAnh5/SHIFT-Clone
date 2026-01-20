using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;

[RequireComponent(typeof(Collider2D))]
public class ExitTilemapTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    [Header("UI")]
    [SerializeField] private TMP_Text progressText;

    [Header("Colors")]
    [SerializeField] private Color lockedColor = new Color(0.6f, 0.6f, 0.6f, 1f); // xám
    [SerializeField] private Color unlockedColor = Color.red; // “bình thường” đỏ

    [Header("Plate search")]
    [SerializeField] private bool searchOnlyInLevelRoot = true;

    private Tilemap tilemap;
    private Collider2D col;

    private readonly List<PlateBase2D> plates = new();
    private int total;
    private int done;
    private bool unlocked;
    private bool used;

    private Coroutine initCo;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        tilemap = GetComponent<Tilemap>();
        if (progressText == null) progressText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Start()
    {
        // IMPORTANT:
        // Khi Reload Level: Level cũ bị Destroy cuối frame nhưng Level mới được Instantiate ngay.
        // Nếu cache plates ngay trong Start, transform.root có thể chứa CẢ level cũ + level mới => total bị x2.
        // Delay 1 frame để Unity hoàn tất Destroy(level cũ) trước khi scan.
        if (initCo != null) StopCoroutine(initCo);
        initCo = StartCoroutine(InitNextFrame());
    }

    private IEnumerator InitNextFrame()
    {
        yield return null;
        RebuildPlateCache();
        RecalculateAndApply();
    }

    private void OnDestroy()
    {
        UnsubscribePlates();
    }

    public void RebuildPlateCache()
    {
        UnsubscribePlates();
        plates.Clear();

        var unique = new HashSet<int>();

        PlateBase2D[] found = searchOnlyInLevelRoot
            ? transform.root.GetComponentsInChildren<PlateBase2D>(true)
            : FindObjectsOfType<PlateBase2D>(true);

        for (int i = 0; i < found.Length; i++)
        {
            var p = found[i];
            if (p == null) continue;
            int id = p.GetInstanceID();
            if (!unique.Add(id)) continue; // chống add trùng

            plates.Add(p);
            p.OnStateChanged += OnPlateStateChanged;
        }
    }

    // Cho LevelManager gọi sau khi load/reload (nếu muốn force refresh)
    public void ForceRefresh()
    {
        RecalculateAndApply();
    }

    private void UnsubscribePlates()
    {
        for (int i = 0; i < plates.Count; i++)
        {
            if (plates[i] != null)
                plates[i].OnStateChanged -= OnPlateStateChanged;
        }
    }

    private void OnPlateStateChanged(PlateBase2D _plate, bool _isOn)
    {
        RecalculateAndApply();
    }

    private void RecalculateAndApply()
    {
        // loại plate đã Destroy nhưng vẫn còn reference trong list
        plates.RemoveAll(p => p == null);

        total = 0;
        done = 0;

        var unique = new HashSet<int>();
        for (int i = 0; i < plates.Count; i++)
        {
            var p = plates[i];
            if (p == null) continue;
            if (!unique.Add(p.GetInstanceID())) continue;

            total++;
            if (p.IsOn) done++;
        }

        unlocked = (total == 0) || (done >= total);

        // Text
        if (progressText != null)
            progressText.text = (total > 0) ? $"{done}/{total}" : "";

        // Color
        if (tilemap != null)
            tilemap.color = unlocked ? unlockedColor : lockedColor;

        // Collider mode: unlocked = trigger, locked = solid
        col.isTrigger = unlocked;

        // reset used khi state đổi (để không bị kẹt nếu bạn quay lại)
        used = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!col.isTrigger) return;        // chỉ khi unlocked
        if (used) return;
        if (!other.CompareTag(playerTag)) return;

        if (LevelManager.I == null)
        {
            Debug.LogError("[EXIT] LevelManager.I is NULL");
            return;
        }
        // SAVE PROGRESS (unlock next level)
        int current = LevelManager.I.CurrentLevelIndex;
        int next = current + 1;

        int unlocked = PlayerPrefs.GetInt("unlocked_level", 1);
        if (next > unlocked)
        {
            PlayerPrefs.SetInt("unlocked_level", next);
            PlayerPrefs.Save();
        }

        LevelManager.I.LoadNextLevel();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (col.isTrigger) return; // chỉ khi locked (solid)
        if (!collision.collider.CompareTag(playerTag)) return;

        // TODO: fail feedback (shake/sfx)
        // CameraShake2D.I?.ShakeFail();
        Debug.Log("[EXIT] LOCKED: not enough plates");
    }
}
