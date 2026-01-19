using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Bullet2D : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D rb;
    private Collider2D col;

    private float t;
    private bool released;

    private Collider2D owner;
    private IObjectPool<Bullet2D> pool;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        col.isTrigger = true;
        rb.gravityScale = 0f;
    }

    // PoolManager sẽ gọi
    public void AssignPool(IObjectPool<Bullet2D> p) => pool = p;

    // PoolManager gọi khi Get/Release
    public void OnSpawned()
    {
        released = false;
        t = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void OnDespawned()
    {
        // bỏ ignore collision với owner cũ (quan trọng khi reuse)
        if (owner != null)
            Physics2D.IgnoreCollision(col, owner, false);

        owner = null;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void Fire(Vector2 dir, float speed, Collider2D ownerCollider = null)
    {
        owner = ownerCollider;
        if (owner != null)
            Physics2D.IgnoreCollision(col, owner, true);

        rb.linearVelocity = dir.normalized * speed;
        t = 0f;

        if (debugLogs)
            Debug.Log($"[Bullet] Fire dir={dir} speed={speed} owner={(owner ? owner.name : "null")}", this);
    }

    private void Update()
    {
        t += Time.deltaTime;
        if (t >= lifeTime)
            Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other == owner) return;

        if (other.CompareTag(playerTag))
        {
            // reset màn đúng theo LevelManager (không reload scene)
            if (LevelManager.I != null) LevelManager.I.ReloadCurrentLevel();
            else
            {
                // fallback: nếu không có LevelManager thì mới reload scene
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            }
            return;
        }

        Despawn();
    }


    public void Despawn()
    {
        if (released) return;
        released = true;

        if (pool != null)
            pool.Release(this);
        else
            Destroy(gameObject); // fallback
    }
}
