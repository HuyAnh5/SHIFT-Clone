using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;

public class CameraShake2D : MonoBehaviour
{
    [Header("Fail Shake")]
    [SerializeField] private float duration = 0.12f;
    [SerializeField] private float amplitude = 1.0f;
    [SerializeField] private float frequency = 1.0f;

    private Tween tween;

    private CinemachineVirtualCameraBase cmCam;
    private CinemachineBasicMultiChannelPerlin perlin;
    private float baseAmp;
    private float baseFreq;

    private void Awake()
    {
        // Cinemachine 3: dùng CinemachineCamera (?? obsolete). N?u không có thì fallback base.
        cmCam = FindAnyObjectByType<CinemachineCamera>();
        if (cmCam == null) cmCam = FindAnyObjectByType<CinemachineVirtualCameraBase>();

        if (cmCam != null)
        {
            perlin = cmCam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
            if (perlin != null)
            {
                baseAmp = perlin.AmplitudeGain;
                baseFreq = perlin.FrequencyGain;
            }
        }
    }

    public void ShakeFail()
    {
        tween?.Kill();

        if (perlin == null)
            return; // ch?a g?n Noise component trên CM Camera

        perlin.AmplitudeGain = amplitude;
        perlin.FrequencyGain = frequency;

        tween = DOVirtual.DelayedCall(duration, () =>
        {
            if (perlin == null) return;
            perlin.AmplitudeGain = baseAmp;
            perlin.FrequencyGain = baseFreq;
        });
    }
}
