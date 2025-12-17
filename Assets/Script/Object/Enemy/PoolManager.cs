using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour
{
    public static PoolManager I { get; private set; }

    [SerializeField] private Transform poolRoot; 

    [Serializable]
    public class BulletPoolConfig
    {
        public Bullet2D prefab;
        public int prewarm = 20;
        public int maxSize = 200;
    }

    [Header("Bullet Pools")]
    [SerializeField] private List<BulletPoolConfig> bulletPools = new();

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly Dictionary<Bullet2D, IObjectPool<Bullet2D>> pools = new();

    private void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;

        if (poolRoot == null) poolRoot = this.transform;

        BuildPools();
    }

    private void BuildPools()
    {
        pools.Clear();

        foreach (var cfg in bulletPools)
        {
            if (cfg.prefab == null) continue;
            if (pools.ContainsKey(cfg.prefab)) continue;

            ObjectPool<Bullet2D> pool = null;

            pool = new ObjectPool<Bullet2D>(
                createFunc: () =>
                {
                    var b = Instantiate(cfg.prefab);
                    b.AssignPool(pool);
                    b.gameObject.SetActive(false);
                    return b;
                },
                actionOnGet: b =>
                {
                    b.gameObject.SetActive(true);
                    b.OnSpawned();
                },
                actionOnRelease: b =>
                {
                    b.OnDespawned();
                    b.gameObject.SetActive(false);
                },
                actionOnDestroy: b =>
                {
                    if (b != null) Destroy(b.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, cfg.prewarm),
                maxSize: Mathf.Max(1, cfg.maxSize)
            );

            pools.Add(cfg.prefab, pool);

            // prewarm
            for (int i = 0; i < cfg.prewarm; i++)
            {
                var b = pool.Get();
                pool.Release(b);
            }

            if (debugLogs)
                Debug.Log($"[PoolManager] Built pool for {cfg.prefab.name} prewarm={cfg.prewarm} max={cfg.maxSize}");
        }
    }

    public Bullet2D SpawnBullet(Bullet2D prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[PoolManager] SpawnBullet called with null prefab.");
            return null;
        }

        if (!pools.TryGetValue(prefab, out var pool))
        {
            Debug.LogWarning($"[PoolManager] No pool for prefab '{prefab.name}'. Add it to PoolManager list.");
            return Instantiate(prefab, pos, rot, parent);
        }

        var b = pool.Get();

        b.transform.SetParent(parent != null ? parent : poolRoot, worldPositionStays: false);
        b.transform.SetPositionAndRotation(pos, rot);

        return b;
    }


    public void DespawnBullet(Bullet2D instance)
    {
        if (instance == null) return;

        if (poolRoot != null)
            instance.transform.SetParent(poolRoot, worldPositionStays: false);

        instance.Despawn(); // gọi Release về pool (bullet đang cầm reference pool)
    }

}
