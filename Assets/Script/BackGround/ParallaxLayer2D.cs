using UnityEngine;
using DG.Tweening;

[ExecuteAlways]
public class ParallaxWorldLayer2D : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        [Header("Refs")]
        public Transform root;              // object sẽ được move parallax (thường là chính child)
        public SpriteRenderer sr;

        [Header("Parallax")]
        [Range(0f, 1f)] public float parallaxX = 0.2f;
        [Range(0f, 1f)] public float parallaxY = 0f;

        [Header("Axis Locks")]
        public bool lockX = false;
        public bool lockY = true;

        [Header("Infinite Loop")]
        public bool loopX = false;

        [HideInInspector] public Vector3 startCamPos;
        [HideInInspector] public Vector3 startLayerPos;
        [HideInInspector] public float unitSizeX;

        public void Cache(Transform cam)
        {
            if (root == null && sr != null) root = sr.transform;
            if (root == null) return;

            startLayerPos = root.position;
            if (cam != null) startCamPos = cam.position;

            RecalcUnitSize();
        }

        public void RecalcUnitSize()
        {
            unitSizeX = (sr != null && sr.sprite != null) ? sr.bounds.size.x : 0f;
        }

        public void ApplyParallax(Transform cam)
        {
            if (root == null || cam == null) return;

            Vector3 camDelta = cam.position - startCamPos;

            float targetX = startLayerPos.x + camDelta.x * parallaxX;
            float targetY = startLayerPos.y + camDelta.y * parallaxY;

            Vector3 pos = root.position;

            if (!lockX) pos.x = targetX;
            if (!lockY) pos.y = targetY;

            root.position = pos;

            if (loopX && unitSizeX > 0.01f)
            {
                float diffX = cam.position.x - pos.x;
                if (Mathf.Abs(diffX) >= unitSizeX)
                {
                    float offset = (diffX % unitSizeX);
                    startLayerPos.x = cam.position.x - offset;
                    startCamPos = cam.position; // reset để không drift
                }
            }
        }

        public void SetAlpha(float a)
        {
            if (sr == null) return;
            var c = sr.color;
            c.a = a;
            sr.color = c;
        }

        public void FadeAlpha(float a, float dur)
        {
            if (sr == null) return;
            sr.DOFade(a, dur).SetUpdate(true);
        }
    }

    [Header("Camera Ref")]
    public Transform cam;

    [Header("Layers")]
    public Layer materialLayer = new Layer();
    public Layer blueprintLayer = new Layer();

    [Header("World Rule")]
    [Tooltip("World nào sẽ ưu tiên hiển thị blueprint overlay (thường là White).")]
    public WorldState blueprintWorld = WorldState.White;

    [Header("Alpha (Material vs Blueprint Overlay)")]
    [Range(0f, 1f)] public float materialOnAlpha = 1.00f;
    [Range(0f, 1f)] public float materialOffAlpha = 0.20f;

    [Range(0f, 1f)] public float blueprintOnAlpha = 0.35f;
    [Range(0f, 1f)] public float blueprintOffAlpha = 0.00f;

    [Header("Fade")]
    public float fadeDuration = 0.12f;

    private void OnEnable()
    {
        if (!cam) cam = Camera.main ? Camera.main.transform : null;

        AutoWireIfMissing();

        materialLayer.Cache(cam);
        blueprintLayer.Cache(cam);

        WorldShiftManager.OnWorldChanged += HandleWorldChanged;

        var w = WorldShiftManager.I != null ? WorldShiftManager.I.SolidWorld : WorldState.Black;
        ApplyNow(w);
    }

    private void OnDisable()
    {
        WorldShiftManager.OnWorldChanged -= HandleWorldChanged;
    }

    private void OnValidate()
    {
        AutoWireIfMissing();
        materialLayer.RecalcUnitSize();
        blueprintLayer.RecalcUnitSize();
    }

    private void AutoWireIfMissing()
    {
        // Auto-find cam
        if (!cam) cam = Camera.main ? Camera.main.transform : null;

        // Nếu chưa kéo SR thủ công, thử tự tìm 2 SR con đầu tiên
        if (materialLayer.sr == null || blueprintLayer.sr == null)
        {
            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            if (srs != null && srs.Length > 0)
            {
                if (materialLayer.sr == null) materialLayer.sr = srs[0];
                if (blueprintLayer.sr == null && srs.Length > 1) blueprintLayer.sr = srs[1];
            }
        }

        if (materialLayer.root == null && materialLayer.sr != null) materialLayer.root = materialLayer.sr.transform;
        if (blueprintLayer.root == null && blueprintLayer.sr != null) blueprintLayer.root = blueprintLayer.sr.transform;
    }

    private void HandleWorldChanged(WorldState solidWorld)
    {
        ApplyTween(solidWorld);

        // Nếu sprite khác size và bạn loopX thì cần recalc
        materialLayer.RecalcUnitSize();
        blueprintLayer.RecalcUnitSize();
    }

    private void ApplyNow(WorldState solidWorld)
    {
        bool bp = (solidWorld == blueprintWorld);

        materialLayer.SetAlpha(bp ? materialOffAlpha : materialOnAlpha);
        blueprintLayer.SetAlpha(bp ? blueprintOnAlpha : blueprintOffAlpha);
    }

    private void ApplyTween(WorldState solidWorld)
    {
        bool bp = (solidWorld == blueprintWorld);

        materialLayer.FadeAlpha(bp ? materialOffAlpha : materialOnAlpha, fadeDuration);
        blueprintLayer.FadeAlpha(bp ? blueprintOnAlpha : blueprintOffAlpha, fadeDuration);
    }

    private void LateUpdate()
    {
        if (!cam)
        {
            cam = Camera.main ? Camera.main.transform : null;
            if (!cam) return;

            materialLayer.Cache(cam);
            blueprintLayer.Cache(cam);
        }

        materialLayer.ApplyParallax(cam);
        blueprintLayer.ApplyParallax(cam);
    }
}
