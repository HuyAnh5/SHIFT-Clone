using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine-first camera shake.
/// - If CinemachineBrain + active vcam Noise exist: shakes the ACTIVE vcam (whole view shakes).
/// - Else: DOTween shake on the Unity Camera transform (only reliable when Cinemachine is not driving the camera).
/// </summary>
public class CameraShake2D : MonoBehaviour
{
    public static CameraShake2D I { get; private set; }

    [Header("Target Camera (optional)")]
    [Tooltip("If empty, uses Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Header("Fail Shake (Cinemachine Noise or DOTween fallback)")]
    [SerializeField] private float duration = 0.12f;
    [SerializeField] private float amplitude = 1.0f;
    [SerializeField] private float frequency = 1.0f;

    [Header("Fallback (no Cinemachine)")]
    [Tooltip("Used only when Cinemachine noise is not available.")]
    [SerializeField] private float fallbackPosStrength = 0.20f;
    [SerializeField] private int fallbackVibrato = 14;
    [SerializeField] private float fallbackRandomness = 90f;

    private Tween tween;

    // Cinemachine runtime
    private CinemachineBrain brain;

    // remember base noise per vcam (so different vcams keep their own base settings)
    private readonly Dictionary<Object, (float amp, float freq)> baseNoise = new();

    // keep track of last shaken perlin so we can restore it if a new shake interrupts
    private CinemachineBasicMultiChannelPerlin lastPerlin;
    private (float amp, float freq) lastBase;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null) brain = targetCamera.GetComponent<CinemachineBrain>();
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    public void ShakeFail()
    {
        // If we interrupt a shake, restore last perlin first (avoid stuck amplitude)
        if (lastPerlin != null)
        {
            lastPerlin.AmplitudeGain = lastBase.amp;
            lastPerlin.FrequencyGain = lastBase.freq;
            lastPerlin = null;
        }

        tween?.Kill();

        // 1) Prefer Cinemachine: shake ACTIVE virtual camera noise
        if (TryShakeActiveCinemachineNoise())
            return;

        // 2) Fallback: shake camera transform (works only when Cinemachine is not driving this camera)
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        tween = targetCamera.transform.DOShakePosition(
                duration,
                strength: fallbackPosStrength,
                vibrato: fallbackVibrato,
                randomness: fallbackRandomness,
                snapping: false,
                fadeOut: true
            )
            .SetUpdate(UpdateType.Late);
    }

    private bool TryShakeActiveCinemachineNoise()
    {
        if (brain == null)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null) brain = targetCamera.GetComponent<CinemachineBrain>();
        }
        if (brain == null) return false;

        // CinemachineBrain.ActiveVirtualCamera returns ICinemachineCamera.
        // In CM3, it no longer exposes VirtualCameraGameObject, but it IS implemented by CinemachineVirtualCameraBase,
        // which is a MonoBehaviour, so we can cast to it and use .gameObject safely.
        var active = brain.ActiveVirtualCamera;
        var vcamBase = active as CinemachineVirtualCameraBase;
        if (vcamBase == null) return false;

        var perlin = vcamBase.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
        if (perlin == null)
        {
            Debug.LogWarning(
                "CameraShake2D: Active vcam has NO Noise (CinemachineBasicMultiChannelPerlin). " +
                "Select the active CinemachineCamera and add Noise -> Basic Multi Channel Perlin, then assign a Noise Profile."
            );
            return false;
        }

        // cache base values per virtual camera instance
        if (!baseNoise.ContainsKey(vcamBase))
            baseNoise[vcamBase] = (perlin.AmplitudeGain, perlin.FrequencyGain);

        var baseVals = baseNoise[vcamBase];
        perlin.AmplitudeGain = amplitude;
        perlin.FrequencyGain = frequency;

        lastPerlin = perlin;
        lastBase = baseVals;

        tween = DOVirtual.DelayedCall(duration, () =>
        {
            if (perlin == null) return;
            perlin.AmplitudeGain = baseVals.amp;
            perlin.FrequencyGain = baseVals.freq;
            if (lastPerlin == perlin) lastPerlin = null;
        });

        return true;
    }
}
