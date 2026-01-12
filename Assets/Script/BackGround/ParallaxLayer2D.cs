using UnityEngine;

[ExecuteAlways]
public class ParallaxLayer2D : MonoBehaviour
{
    [Header("Refs")]
    public Transform cam;

    [Header("Parallax")]
    [Tooltip("0 = đứng yên như skybox, 1 = đi y như camera (không parallax). Thường: Far 0.05-0.15, Mid 0.2-0.4, Near 0.5-0.8")]
    [Range(0f, 1f)] public float parallaxX = 0.2f;

    [Tooltip("Nếu bạn muốn nền trôi theo Y khi nhảy/flip. Thường nhỏ hơn X hoặc = 0.")]
    [Range(0f, 1f)] public float parallaxY = 0f;

    [Header("Axis Locks")]
    public bool lockX = false;
    public bool lockY = true;

    [Header("Infinite Loop")]
    public bool loopX = true;

    private Vector3 _startCamPos;
    private Vector3 _startLayerPos;
    private float _unitSizeX;

    void OnEnable()
    {
        Cache();
    }

    void Cache()
    {
        if (!cam) cam = Camera.main ? Camera.main.transform : null;

        _startLayerPos = transform.position;
        if (cam) _startCamPos = cam.position;

        // Tính độ rộng sprite để loop
        var sr = GetComponent<SpriteRenderer>();
        _unitSizeX = (sr && sr.sprite) ? sr.bounds.size.x : 0f;
    }

    void LateUpdate()
    {
        if (!cam) { Cache(); if (!cam) return; }

        Vector3 camDelta = cam.position - _startCamPos;

        float targetX = _startLayerPos.x + camDelta.x * parallaxX;
        float targetY = _startLayerPos.y + camDelta.y * parallaxY;

        Vector3 pos = transform.position;

        if (!lockX) pos.x = targetX;
        if (!lockY) pos.y = targetY;

        transform.position = pos;

        if (loopX && _unitSizeX > 0.01f)
        {
            // Khi camera đi quá 1 "unit" sprite, dịch origin để loop mượt
            float diffX = cam.position.x - pos.x;
            if (Mathf.Abs(diffX) >= _unitSizeX)
            {
                float offset = (diffX % _unitSizeX);
                _startLayerPos.x = cam.position.x - offset;
                _startCamPos = cam.position; // reset delta để không bị drift
            }
        }
    }
}
