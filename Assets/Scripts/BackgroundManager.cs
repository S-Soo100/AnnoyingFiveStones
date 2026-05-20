using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별 배경 무드를 관리하는 placeholder 시스템 (v8-2).
/// Sky 그라데이션 / Table / Cloth 색상 갱신 + 주변 Primitive props 스폰.
/// 향후 실제 asset으로 교체 예정.
/// </summary>
public class BackgroundManager : MonoBehaviour
{
    [SerializeField] private SkyGradient skyGradient;
    [SerializeField] private Renderer tableRenderer;
    [SerializeField] private Renderer clothRenderer;
    [SerializeField] private Transform propsRoot;

    private List<GameObject> spawnedProps = new List<GameObject>();
    private MaterialPropertyBlock tableBlock;
    private MaterialPropertyBlock clothBlock;

    // v8-2b: 풀스크린 배경 이미지 quad (이미지가 지정된 stage에서만 활성)
    private GameObject bgImageQuad;

    // v8-2c: 라이브 튜닝 (Play 중 Inspector로 quad offset/scale 미세조정). 만족 시 값을 StageConfig에 옮긴다.
    [Header("Live Tuning — Play 중 Inspector로 조정")]
    [SerializeField] private bool liveTuningEnabled = false;
    [SerializeField] private Vector2 liveOffset = Vector2.zero;
    [SerializeField] private Vector2 liveScale  = Vector2.one;

    private StageConfig lastImageConfig; // 최근 이미지 모드 stage 기록

    private void Awake()
    {
        // 씬 참조 자동 해결
        if (skyGradient == null)
            skyGradient = FindFirstObjectByType<SkyGradient>();
        if (tableRenderer == null)
            tableRenderer = GameObject.Find("Table")?.GetComponent<Renderer>();
        if (clothRenderer == null)
            clothRenderer = GameObject.Find("Cloth")?.GetComponent<Renderer>();
        if (propsRoot == null)
        {
            var rootGo = new GameObject("StageProps");
            propsRoot = rootGo.transform;
        }

        tableBlock = new MaterialPropertyBlock();
        clothBlock = new MaterialPropertyBlock();
    }

    /// <summary>스테이지 전환 시 GameManager.StartStage에서 호출.</summary>
    public void ApplyStage(StageConfig config)
    {
        if (config == null) return;

        bool useImage = !string.IsNullOrEmpty(config.BackgroundImage);

        if (useImage)
        {
            // 풀스크린 이미지 사용 — Sky/Props 숨김 (이미지가 이미 다 그려진 풀배경)
            ApplyBgImage(config.BackgroundImage, config.BgImageOffset, config.BgImageScale);
            if (skyGradient != null) skyGradient.gameObject.SetActive(false);
            ClearProps();

            // 보드(Table)/매트(Cloth)는 Renderer만 끔 — 이미지에 그려진 매트/책상이 그대로 노출.
            // GameObject는 active 유지 → BoardBounds(Cloth.Renderer.bounds) 캐시 정상.
            if (tableRenderer != null) tableRenderer.enabled = false;
            if (clothRenderer != null) clothRenderer.enabled = false;

            lastImageConfig = config;
            // 라이브 튜닝 시작 값 = stage 기본값(편의)
            if (Application.isPlaying)
            {
                liveOffset = config.BgImageOffset;
                liveScale  = config.BgImageScale;
            }
        }
        else
        {
            // placeholder 모드 — 이미지 quad 숨기고 기존 색상/Props 사용
            if (bgImageQuad != null) bgImageQuad.SetActive(false);
            lastImageConfig = null;
            if (skyGradient != null) skyGradient.gameObject.SetActive(true);
            skyGradient?.ApplyColors(config.SkyBottom, config.SkyTop);

            // 보드/매트 Renderer 복구
            if (tableRenderer != null) tableRenderer.enabled = true;
            if (clothRenderer != null) clothRenderer.enabled = true;

            // 색상 갱신 (placeholder 모드에서만 의미 있음)
            if (tableRenderer != null)
            {
                tableRenderer.GetPropertyBlock(tableBlock);
                tableBlock.SetColor("_BaseColor", config.TableColor);
                tableRenderer.SetPropertyBlock(tableBlock);
            }
            if (clothRenderer != null)
            {
                clothRenderer.GetPropertyBlock(clothBlock);
                clothBlock.SetColor("_BaseColor", config.ClothColor);
                clothRenderer.SetPropertyBlock(clothBlock);
            }

            ClearProps();
            if (config.Props != null)
            {
                foreach (var prop in config.Props)
                    SpawnProp(prop);
            }
        }
    }

    private void EnsureBgImageQuad()
    {
        if (bgImageQuad != null) return;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        bgImageQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgImageQuad.name = "BgImageQuad";
        var col = bgImageQuad.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // 카메라 자식 — 카메라 따라다닐 수 있도록.
        bgImageQuad.transform.SetParent(cam.transform, false);
        bgImageQuad.transform.localRotation = Quaternion.identity;
        // 위치/스케일은 ApplyQuadTransform에서 stage별로 갱신.

        var rd = bgImageQuad.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        rd.material = mat;

        bgImageQuad.SetActive(false);
    }

    /// <summary>quad 위치/스케일을 stage별 offset/scale로 갱신.</summary>
    private void ApplyQuadTransform(Vector2 offset, Vector2 scale)
    {
        if (bgImageQuad == null) return;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float h = 2f * cam.orthographicSize;
        float w = h * cam.aspect;
        // 매트(z=0)보다 멀리(z=50)에 배치. offset.xy로 미세조정.
        bgImageQuad.transform.localPosition = new Vector3(offset.x, offset.y, 50f);
        bgImageQuad.transform.localScale = new Vector3(w * scale.x, h * scale.y, 1f);
    }

    private void ApplyBgImage(string resourcePath, Vector2 offset, Vector2 scale)
    {
        EnsureBgImageQuad();
        if (bgImageQuad == null) return;

        ApplyQuadTransform(offset, scale);

        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null)
        {
            Debug.LogWarning($"[BackgroundManager] Background image not found: {resourcePath}");
            bgImageQuad.SetActive(false);
            return;
        }

        var rd = bgImageQuad.GetComponent<Renderer>();
        var mat = rd.material;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", Color.white);

        bgImageQuad.SetActive(true);
    }

    // 라이브 튜닝: Play 중 Inspector에서 liveOffset/liveScale 바꾸면 즉시 quad에 반영.
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (!liveTuningEnabled) return;
        if (lastImageConfig == null || bgImageQuad == null || !bgImageQuad.activeSelf) return;
        ApplyQuadTransform(liveOffset, liveScale);
    }

    private void ClearProps()
    {
        foreach (var go in spawnedProps)
        {
            if (go != null)
                Object.Destroy(go);
        }
        spawnedProps.Clear();
    }

    private void SpawnProp(BackgroundProp p)
    {
        PrimitiveType type = p.Shape switch
        {
            BackgroundPropShape.Cube     => PrimitiveType.Cube,
            BackgroundPropShape.Sphere   => PrimitiveType.Sphere,
            BackgroundPropShape.Cylinder => PrimitiveType.Cylinder,
            BackgroundPropShape.Capsule  => PrimitiveType.Capsule,
            _                           => PrimitiveType.Cube,
        };

        var go = GameObject.CreatePrimitive(type);
        go.name = $"BgProp_{type}";
        go.transform.SetParent(propsRoot, false);
        go.transform.position = p.Position;
        go.transform.localScale = p.Scale;

        // Collider 제거 — 낙 판정 / 물리에 영향을 주지 않도록
        var col = go.GetComponent<Collider>();
        if (col != null)
            Object.Destroy(col);

        // 머테리얼 색 + Emission (조명 의존 제거, URP _BaseColor)
        var rd = go.GetComponent<Renderer>();
        if (rd != null)
        {
            // 새 머테리얼 인스턴스에 색 적용 (다른 prop과 공유 방지)
            var mat = rd.material;
            mat.SetColor("_BaseColor", p.Color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", p.Color * 0.4f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        spawnedProps.Add(go);
    }
}
