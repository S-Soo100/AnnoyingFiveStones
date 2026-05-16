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

        // Sky 그라데이션 갱신
        skyGradient?.ApplyColors(config.SkyBottom, config.SkyTop);

        // Table 색 갱신 (MaterialPropertyBlock — 머테리얼 인스턴스 생성 방지)
        if (tableRenderer != null)
        {
            tableRenderer.GetPropertyBlock(tableBlock);
            tableBlock.SetColor("_BaseColor", config.TableColor);
            tableRenderer.SetPropertyBlock(tableBlock);
        }

        // Cloth 색 갱신
        if (clothRenderer != null)
        {
            clothRenderer.GetPropertyBlock(clothBlock);
            clothBlock.SetColor("_BaseColor", config.ClothColor);
            clothRenderer.SetPropertyBlock(clothBlock);
        }

        // 이전 Props 제거 + 새 Props 스폰
        ClearProps();
        if (config.Props != null)
        {
            foreach (var prop in config.Props)
                SpawnProp(prop);
        }
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
