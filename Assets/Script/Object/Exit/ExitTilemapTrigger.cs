using UnityEngine;

public class ExitTilemapTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    private bool used;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[EXIT] hit {other.name} tag={other.tag}");
        if (used) return;
        if (!other.CompareTag(playerTag)) return;

        if (LevelManager.I == null)
        {
            Debug.LogError("[EXIT] LevelManager.I is NULL");
            return;
        }

        used = true;
        LevelManager.I.LoadNextLevel();
    }
}
