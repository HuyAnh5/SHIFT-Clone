using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PlateSensor2D : MonoBehaviour
{
    [SerializeField] private LayerMask detectMask = ~0; // Everything by default
    [SerializeField] private bool debugDraw = false;

    private BoxCollider2D col;
    private PlateBase2D owner;
    private readonly List<Collider2D> results = new(16);
    private ContactFilter2D filter;

    public LayerMask DetectMask => detectMask;

    private int lastOverlapCount;
    public int LastOverlapCount => lastOverlapCount;

    public Bounds WorldBounds
    {
        get
        {
            if (col == null) col = GetComponent<BoxCollider2D>();
            return col != null ? col.bounds : new Bounds(transform.position, Vector3.zero);
        }
    }

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = detectMask,
            useTriggers = false // IMPORTANT: chỉ lấy collider thường (player/swap), bỏ trigger
        };
    }

    public void Bind(PlateBase2D plate) => owner = plate;

    public void SetEnabled(bool enabled)
    {
        if (col != null) col.enabled = enabled;
    }

    public List<Collider2D> OverlapNow()
    {
        results.Clear();
        if (col == null || !col.enabled) return results;

        Physics2D.SyncTransforms();

        col.Overlap(filter, results);
        lastOverlapCount = results.Count;
        return results;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null) return;
        owner.NotifyEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (owner == null) return;
        owner.NotifyExit(other);
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDraw) return;
        var b = GetComponent<BoxCollider2D>();
        if (b == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(b.offset, b.size);
    }
}
