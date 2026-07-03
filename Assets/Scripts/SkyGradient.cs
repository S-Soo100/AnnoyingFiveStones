using UnityEngine;

/// <summary>
/// 하늘 영역에 위로 갈수록 진해지는 파란 그라데이션.
/// Emission 기반으로 조명 없이도 색상이 보인다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class SkyGradient : MonoBehaviour
{
    [Header("Gradient Colors")]
    [SerializeField] private Color bottomColor = new Color(0.53f, 0.81f, 0.98f);
    [SerializeField] private Color topColor = new Color(0.08f, 0.25f, 0.65f);

    private Texture2D currentTex;

    private void Start()
    {
        Refresh();
    }

    /// <summary>스테이지별 하늘 색 갱신. BackgroundManager에서 호출.</summary>
    public void ApplyColors(Color bottom, Color top)
    {
        bottomColor = bottom;
        topColor = top;
        Refresh();
    }

    private void Refresh()
    {
        // 이전 텍스처 해제 (메모리 누수 방지)
        if (currentTex != null)
            Destroy(currentTex);

        currentTex = new Texture2D(1, 256, TextureFormat.RGBA32, false);
        currentTex.wrapMode = TextureWrapMode.Clamp;
        currentTex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 256; y++)
        {
            float t = y / 255f;
            currentTex.SetPixel(0, y, Color.Lerp(bottomColor, topColor, t));
        }
        currentTex.Apply();

        var renderer = GetComponent<MeshRenderer>();
        var mat = renderer.material;

        // Albedo 텍스처
        mat.SetTexture("_BaseMap", currentTex);
        mat.SetColor("_BaseColor", Color.white);

        // Emission으로 자체발광 (조명 불필요)
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", currentTex);
        mat.SetColor("_EmissionColor", Color.white);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }
}
