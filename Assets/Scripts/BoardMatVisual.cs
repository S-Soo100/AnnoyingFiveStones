using UnityEngine;

/// <summary>
/// v17 — 보드를 "단색 도형"이 아니라 **바닥에 깔린 돗자리**로 읽히게 만든다.
///
/// 왜 필요한가:
/// 배경마다 그려진 면의 원근이 제각각(측정: 0.57~0.80)이라, 하나의 보드가 모든 배경과
/// 변이 나란해질 수는 없다. 그런데 **실제 돗자리는 책상 모서리와 나란하지 않아도 어색하지 않다.**
/// 지금 어색한 진짜 이유는 단색 빨간 도형이라 "잘못 그린 면"으로 읽히기 때문이다.
/// → 테두리 + 짜임 + 밑그림자를 주어 "위에 놓인 물건"으로 읽히게 하면 원근 차이가 용서된다.
///
/// 텍스처는 런타임 생성한다(그림자·말풍선과 동일한 이 프로젝트의 방식).
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class BoardMatVisual : MonoBehaviour
{
    private const int TexSize = 256;

    // 돗자리 배색 — 기획서 v2의 "빨간 보드"를 유지하되 질감을 준다.
    private static readonly Color MatBase   = new Color(0.62f, 0.20f, 0.18f);
    private static readonly Color WeaveDark = new Color(0.54f, 0.16f, 0.15f);
    private static readonly Color Border    = new Color(0.40f, 0.11f, 0.10f);
    private static readonly Color Trim      = new Color(0.85f, 0.72f, 0.45f); // 가장자리 실 — 물건감의 핵심

    private const float BorderRatio = 0.055f; // 테두리 두께 (텍스처 비율)
    private const float TrimRatio   = 0.018f; // 테두리 안쪽 밝은 실

    private GameObject dropShadow;

    private void Start()
    {
        ApplyMatTexture();
        BuildDropShadow();
    }

    private void ApplyMatTexture()
    {
        var rd = GetComponent<MeshRenderer>();
        var tex = CreateMatTexture();

        var mat = rd.material; // 인스턴스 (공유 머티리얼 오염 방지)
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

        // EmissiveQuad가 조명 없이 보이게 해주는 구조라, 발광에도 같은 텍스처를 물려야
        // 질감이 죽지 않는다(발광이 단색이면 텍스처가 씻겨 보인다).
        if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.white);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }

    /// <summary>테두리 + 안쪽 실 + 짜임(위빙) 패턴을 가진 돗자리 텍스처.</summary>
    private static Texture2D CreateMatTexture()
    {
        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        int border = Mathf.RoundToInt(TexSize * BorderRatio);
        int trim   = Mathf.RoundToInt(TexSize * TrimRatio);
        var px = new Color[TexSize * TexSize];

        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, TexSize - 1 - x), Mathf.Min(y, TexSize - 1 - y));
                Color c;

                if (edge < border)
                {
                    c = Border;                                  // 바깥 테두리
                }
                else if (edge < border + trim)
                {
                    c = Trim;                                    // 밝은 실 — "천으로 감싼 가장자리"
                }
                else
                {
                    // 짜임: 가로/세로 격자를 번갈아 — 촘촘할수록 직물처럼 보인다.
                    bool warp = ((x / 5) + (y / 5)) % 2 == 0;
                    c = warp ? MatBase : WeaveDark;
                    // 미세한 결 — 완전 균일하면 프린트처럼 보인다.
                    float grain = (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.05f;
                    c = new Color(c.r + grain, c.g + grain, c.b + grain, 1f);
                }
                px[y * TexSize + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>돗자리 밑에 깔리는 옅은 그림자 — "바닥에 놓여 있다"를 만드는 결정적 단서.</summary>
    private void BuildDropShadow()
    {
        if (dropShadow != null) return;

        dropShadow = new GameObject("BoardMatDropShadow");
        dropShadow.transform.SetParent(transform, false);
        // 같은 사다리꼴 메시를 공유 → 모양이 항상 일치한다.
        var mf = dropShadow.AddComponent<MeshFilter>();
        mf.sharedMesh = GetComponent<MeshFilter>().sharedMesh;

        var rd = dropShadow.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader);
        mat.color = new Color(0f, 0f, 0f, 0.28f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.28f));
        SetTransparent(mat);
        rd.material = mat;
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rd.receiveShadows = false;

        // 살짝 크고 살짝 아래 + 돗자리 뒤(z+)에 배치 → 바닥에 눌린 그림자처럼 보인다.
        dropShadow.transform.localScale = new Vector3(1.02f, 1.06f, 1f);
        dropShadow.transform.localPosition = new Vector3(0f, -0.012f, 0.01f);
    }

    private static void SetTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);           // Transparent
        m.SetFloat("_Blend", 0f);             // Alpha
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
