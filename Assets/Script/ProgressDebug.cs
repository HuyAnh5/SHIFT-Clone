using UnityEngine;

public class ProgressDebug : MonoBehaviour
{
    public void Editor_ResetProgress() => ResetProgress();
    public void Editor_DeleteAll() => DeleteAll();


    [ContextMenu("RESET PROGRESS (unlocked_level=1)")]
    private void ResetProgress()
    {
        PlayerPrefs.SetInt("unlocked_level", 1);
        PlayerPrefs.SetInt("start_level_index", 1);
        PlayerPrefs.Save();
        Debug.Log("Progress reset.");
    }

    [ContextMenu("DELETE ALL PLAYERPREFS")]
    private void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("All PlayerPrefs deleted.");
    }
}
