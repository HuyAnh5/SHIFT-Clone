using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerWorldColor : MonoBehaviour
{
    [SerializeField] private Color colorInBlackWorld = Color.green;
    [SerializeField] private Color colorInWhiteWorld = Color.blue;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        WorldShiftManager.OnWorldChanged += Apply;
    }

    private void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= Apply;
    }

    private void Start()
    {
        if (WorldShiftManager.I != null)
            Apply(WorldShiftManager.I.SolidWorld);
        else
            sr.color = colorInBlackWorld;
    }

    private void Apply(WorldState solidWorld)
    {
        // Quy ước: world Black -> player xanh lá, world White -> player xanh biển
        sr.color = (solidWorld == WorldState.Black) ? colorInBlackWorld : colorInWhiteWorld;
    }
}
