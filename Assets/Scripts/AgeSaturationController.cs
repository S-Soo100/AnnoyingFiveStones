using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AgeSaturationController : MonoBehaviour
{
    public static AgeSaturationController Instance { get; private set; }

    private ColorAdjustments colorAdjustments;
    private float currentSaturation = 0f;
    private Coroutine lerpCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 런타임 Volume GameObject 생성 (자식으로)
        var volumeGO = new GameObject("AgeSaturationVolume");
        volumeGO.transform.SetParent(transform);

        var volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        colorAdjustments = profile.Add<ColorAdjustments>();
        colorAdjustments.saturation.Override(0f);
        volume.profile = profile;

        // Camera Post Processing 활성화 확인
        var cam = Camera.main;
        if (cam != null)
        {
            var urpCamData = cam.GetUniversalAdditionalCameraData();
            if (urpCamData != null)
                urpCamData.renderPostProcessing = true;
            Debug.Log($"[AgeSaturation] Camera Post Processing: {urpCamData?.renderPostProcessing}");
        }
        else
        {
            Debug.LogWarning("[AgeSaturation] Camera.main is null!");
        }
        Debug.Log($"[AgeSaturation] Volume created. Profile has ColorAdjustments: {colorAdjustments != null}");
    }

    private void OnDestroy()
    {
        Instance = null;
    }

    /// <summary>
    /// 나이 기반 목표 채도로 부드러운 전환 (1.0초 Lerp)
    /// </summary>
    public void UpdateSaturation(int age)
    {
        // v4: 10~40살은 거의 변화 없음, 45살+ 부터 급격히 채도 감소 (지수 커브)
        float normalized = Mathf.Clamp01((age - 10f) / 45f); // 10살=0, 55살=1
        float curved = normalized * normalized * normalized;  // x³ → 후반부 집중
        float target = -curved * 80f;
        Debug.Log($"[AgeSaturation] UpdateSaturation(age={age}) → target={target}, colorAdj={colorAdjustments != null}");
        if (colorAdjustments == null) return;
        if (lerpCoroutine != null) StopCoroutine(lerpCoroutine);
        lerpCoroutine = StartCoroutine(LerpSaturation(currentSaturation, target, 1.0f));
    }

    /// <summary>
    /// 즉시 풀컬러 복귀 (게임 재시작)
    /// </summary>
    public void ResetSaturation()
    {
        if (lerpCoroutine != null) StopCoroutine(lerpCoroutine);
        lerpCoroutine = null;
        currentSaturation = 0f;
        if (colorAdjustments != null)
            colorAdjustments.saturation.Override(0f);
    }

    /// <summary>
    /// Stage 10 전용: 완전 흑백 전환 (saturation → -100, 0.3초 페이드)
    /// </summary>
    public void SetFullMonochrome()
    {
        if (colorAdjustments == null) return;
        if (lerpCoroutine != null) StopCoroutine(lerpCoroutine);
        lerpCoroutine = StartCoroutine(LerpSaturation(currentSaturation, -100f, 0.3f));
    }

    /// <summary>
    /// Stage 10 전용: 모노톤 해제 → 현재 나이 기반 채도로 즉시 복귀
    /// </summary>
    public void RestoreFromMonochrome(int age)
    {
        if (colorAdjustments == null) return;
        if (lerpCoroutine != null) StopCoroutine(lerpCoroutine);
        lerpCoroutine = null;

        // 나이 기반 목표 채도 재계산 (UpdateSaturation 로직과 동일)
        float normalized = Mathf.Clamp01((age - 10f) / 45f);
        float curved = normalized * normalized * normalized;
        float target = -curved * 80f;
        currentSaturation = target;
        colorAdjustments.saturation.Override(target);
    }

    private IEnumerator LerpSaturation(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentSaturation = Mathf.Lerp(from, to, t);
            colorAdjustments.saturation.Override(currentSaturation);
            yield return null;
        }
        currentSaturation = to;
        colorAdjustments.saturation.Override(to);
        lerpCoroutine = null;
    }
}
