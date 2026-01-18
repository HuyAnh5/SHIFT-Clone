using System.Collections;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager I { get; private set; }

    [Header("Level Prefabs (Resources)")]
    [SerializeField] private string resourcesFolder = "Levels";
    [SerializeField] private string levelPrefix = "LV_";
    [SerializeField] private int startLevelIndex = 1;

    [Header("Refs")]
    [SerializeField] private Transform levelParent;
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody2D playerRb;

    [Header("Reset On Load")]
    [SerializeField] private WorldState startWorld = WorldState.White;
    [SerializeField] private float playerDefaultGravity = 5f;

    private GameObject currentLevelInstance;
    private int currentLevelIndex;
    public int CurrentLevelIndex => currentLevelIndex;

    private Coroutine postLoadCo;

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;

        if (levelParent == null) levelParent = this.transform;
        if (playerRb == null && player != null) playerRb = player.GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        LoadLevel(startLevelIndex);
    }

    public void LoadNextLevel()
    {
        LoadLevel(currentLevelIndex + 1);
    }

    public void LoadLevel(int levelIndex)
    {
        string path = $"{resourcesFolder}/{levelPrefix}{levelIndex}";
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[LevelManager] Missing level prefab at Resources/{path}.prefab");
            return;
        }

        if (currentLevelInstance != null)
            Destroy(currentLevelInstance);

        currentLevelIndex = levelIndex;
        currentLevelInstance = Instantiate(prefab, levelParent);

        ResetPlayer(Vector3.zero);
        ResetWorld();

        // IMPORTANT:
        // Đợi 1 frame rồi force Exit rebuild plate cache để tránh case Destroy(level cũ) chưa kịp
        // nhưng Exit đã scan transform.root và bị x2 plate.
        if (postLoadCo != null) StopCoroutine(postLoadCo);
        postLoadCo = StartCoroutine(PostLoadRefresh());
    }

    private IEnumerator PostLoadRefresh()
    {
        yield return null;

        // Refresh tất cả exit trong scene (hoặc chỉ trong currentLevelInstance nếu muốn)
        var exits = FindObjectsOfType<ExitTilemapTrigger>(true);
        for (int i = 0; i < exits.Length; i++)
        {
            if (exits[i] == null) continue;
            exits[i].RebuildPlateCache();
            exits[i].ForceRefresh();
        }
    }

    private void ResetPlayer(Vector3 pos)
    {
        if (player == null) return;

        player.position = pos;

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
            playerRb.angularVelocity = 0f;
            playerRb.gravityScale = Mathf.Abs(playerDefaultGravity);
        }
    }

    public void ReloadCurrentLevel()
    {
        if (currentLevelIndex <= 0)
            LoadLevel(startLevelIndex);
        else
            LoadLevel(currentLevelIndex);
    }

    private void ResetWorld()
    {
        if (WorldShiftManager.I != null)
            WorldShiftManager.I.SetWorld(startWorld);

        if (CameraFlip2D.I != null)
            CameraFlip2D.I.ResetExtraFlip();
    }
}
