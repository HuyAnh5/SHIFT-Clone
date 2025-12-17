using System.Collections;
using UnityEngine;
using DarkTonic.PoolBoss;

public class DestroySelf : MonoBehaviour
{
    [SerializeField] private float lifeSeconds = 2.5f;
    private Coroutine _co;

    private void OnEnable()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(DespawnAfter());
    }

    private void OnDisable()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
    }

    private IEnumerator DespawnAfter()
    {
        yield return new WaitForSeconds(lifeSeconds);

        if (PoolBoss.IsReady)
            PoolBoss.Despawn(transform);
        else
            gameObject.SetActive(false);
    }
}
