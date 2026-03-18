using UnityEngine;

/// <summary>
/// 하늘 영역에 위로 갈수록 진해지는 파란 그라데이션.
/// Emission 기반으로 조명 없이도 색상이 보인다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class SkyGradient : MonoBehaviour
{
    [Header("Gradient Colors")]
    [SerializeField] private Color bottomColor = new Color(0.68f, 0.85f, 0.95f);
    [SerializeField] private Color topColor = new Color(0.15f, 0.35f, 0.65f);

    private void Start()
    {
        var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 256; y++)
        {
            float t = y / 255f;
            tex.SetPixel(0, y, Color.Lerp(bottomColor, topColor, t));
        }
        tex.Apply();

        var renderer = GetComponent<MeshRenderer>();
        var mat = renderer.material;

        // Albedo 텍스처
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);

        // Emission으로 자체발광 (조명 불필요)
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", Color.white);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }
}
