using System.Collections;
using UnityEngine;
using DarkTonic.PoolBoss;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject _objectToSpawm;
    [SerializeField] private float spawnInterval = 0.02f; 

    private bool _ready;
    private float _nextTime;

    private IEnumerator Start()
    {
        while (!PoolBoss.IsReady)
            yield return null;

        _ready = true;
    }

    void Update()
    {
        if (!_ready) return;
        if (_objectToSpawm == null) return;
        if (!Input.GetMouseButton(0)) return;

        if (Time.time < _nextTime) return;
        _nextTime = Time.time + spawnInterval;

        if (Camera.main == null) return;

        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;

        PoolBoss.SpawnInPool(_objectToSpawm.transform, pos, Quaternion.identity);
    }
}
