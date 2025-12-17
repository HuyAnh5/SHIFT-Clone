using UnityEngine;

public class TurretShooter2D : MonoBehaviour
{
    public enum Facing { Left, Right }
    public enum FacingMode { Fixed, AutoByRaycast }

    [Header("Facing")]
    [SerializeField] private FacingMode facingMode = FacingMode.Fixed;
    [SerializeField] private Facing fixedFacing = Facing.Left;
    [SerializeField] private Transform projectilesParent; // kéo Projectiles vào đây

    [Tooltip("Layer solid để turret dò (WorldBlack/WorldWhite/Wall...)")]
    [SerializeField] private LayerMask solidMask;
    [SerializeField] private float probeDistance = 2f;

    [Header("Shoot")]
    [SerializeField] private float cooldown = 2f;     // yêu cầu: 2s
    [SerializeField] private float bulletSpeed = 5f;  // yêu cầu: 5f
    [SerializeField] private Bullet2D bulletPrefab;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.6f, 0f);
    [SerializeField] private bool shootImmediately = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool debugRays = true;

    private Facing facing;
    private float timer;
    private Collider2D myCollider;

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>(); // có thể null, không sao
    }

    private void OnEnable()
    {
        timer = shootImmediately ? cooldown : 0f;
    }

    private void Start()
    {
        ChooseFacing();
        if (debugLogs)
            Debug.Log($"[Turret] Start name={name} mode={facingMode} facing={facing} bulletPrefab={(bulletPrefab ? bulletPrefab.name : "NULL")}", this);
    }

    private void Update()
    {
        if (bulletPrefab == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[Turret] bulletPrefab is NULL on {name}. Drag Bullet prefab into TurretShooter2D.", this);
            return;
        }

        timer += Time.deltaTime;
        if (timer >= cooldown)
        {
            timer = 0f;
            Shoot();
        }
    }

    private void ChooseFacing()
    {
        if (facingMode == FacingMode.Fixed)
        {
            facing = fixedFacing;
            return;
        }

        // Auto: dò trái/phải để chọn phía "thoáng"
        Vector2 origin = transform.position;
        RaycastHit2D hitL = Physics2D.Raycast(origin, Vector2.left, probeDistance, solidMask);
        RaycastHit2D hitR = Physics2D.Raycast(origin, Vector2.right, probeDistance, solidMask);

        if (debugRays)
        {
            Debug.DrawRay(origin, Vector2.left * probeDistance, Color.red, 1f);
            Debug.DrawRay(origin, Vector2.right * probeDistance, Color.red, 1f);
        }

        bool blockedL = hitL.collider != null;
        bool blockedR = hitR.collider != null;

        // Nếu 1 bên bị chặn, bắn về bên còn lại
        if (blockedL && !blockedR) facing = Facing.Right;
        else if (blockedR && !blockedL) facing = Facing.Left;
        else facing = fixedFacing; // cả hai giống nhau => dùng fixedFacing làm default
    }

    private void Shoot()
    {
        Vector2 dir = (facing == Facing.Left) ? Vector2.left : Vector2.right;
        float sign = (facing == Facing.Left) ? -1f : 1f;

        Vector3 spawnPos = transform.position + new Vector3(spawnOffset.x * sign, spawnOffset.y, 0f);

        Bullet2D b = (PoolManager.I != null)
            ? PoolManager.I.SpawnBullet(bulletPrefab, spawnPos, Quaternion.identity, projectilesParent)
            : Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectilesParent);

        b.Fire(dir, bulletSpeed, myCollider);


        b.Fire(dir, bulletSpeed, myCollider);


        if (debugLogs)
            Debug.Log($"[Turret] Shoot facing={facing} spawn={spawnPos} cooldown={cooldown} speed={bulletSpeed}", this);
    }
}
