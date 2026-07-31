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
    // ⚠️ 텍스처 비율은 **보드의 화면 점유 비율과 같아야** 픽셀이 월드에서 정사각형이 된다.
    // 1:1로 만들면 4:1 면에 붙으면서 가로로 4배 늘어나 테두리 두께가 좌우만 두꺼워진다.
    // 디자이너에게 외주 줄 때도 동일 스펙(직사각형, 아래 비율, 테두리는 텍스처 안에)으로 요청한다.
    private static float BoardAspect =>
        (BoardSpace.FrontHalfWidth * 2f) / Mathf.Abs(BoardSpace.FrontScreenY - BoardSpace.BackScreenY);

    private const int TexHeight = 256;
    private static int TexWidth => Mathf.RoundToInt(TexHeight * BoardAspect);

    // 돗자리 배색 — 기획서 v2의 "빨간 보드"를 유지하되 질감을 준다.
    private static readonly Color MatBase   = new Color(0.62f, 0.20f, 0.18f);
    private static readonly Color WeaveDark = new Color(0.54f, 0.16f, 0.15f);
    private static readonly Color Border    = new Color(0.40f, 0.11f, 0.10f);
    private static readonly Color Trim      = new Color(0.85f, 0.72f, 0.45f); // 가장자리 실 — 물건감의 핵심

    private const float BorderRatio = 0.055f; // 테두리 두께 (텍스처 비율)
    private const float TrimRatio   = 0.018f; // 테두리 안쪽 밝은 실

    /// <summary>돗자리 불투명도. 배경(책상·패드)이 비쳐 "얇게 깔린 천" 느낌이 난다. ⚠️ 재튜닝 대상.</summary>
    private const float MatAlpha = 0.78f;
    /// <summary>테두리는 더 진하게 — 가장자리가 흐리면 물건 윤곽이 무너진다.</summary>
    private const float BorderAlpha = 0.92f;

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

        // 반투명 — 배경(책상·마우스패드)이 비쳐 "얇게 깔린 천"으로 읽힌다.
        SetTransparent(mat);
    }

    /// <summary>테두리 + 안쪽 실 + 짜임(위빙) 패턴을 가진 돗자리 텍스처.</summary>
    private static Texture2D CreateMatTexture()
    {
        int W = TexWidth, H = TexHeight;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // 테두리 두께는 **짧은 변(세로) 기준**으로 잡아야 사방이 같은 두께로 보인다.
        int border = Mathf.RoundToInt(H * BorderRatio);
        int trim   = Mathf.RoundToInt(H * TrimRatio);
        var px = new Color[W * H];

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                Color c;

                float alpha;
                if (edge < border)
                {
                    c = Border; alpha = BorderAlpha;             // 바깥 테두리
                }
                else if (edge < border + trim)
                {
                    c = Trim; alpha = BorderAlpha;               // 밝은 실 — "천으로 감싼 가장자리"
                }
                else
                {
                    alpha = MatAlpha;
                    // 짜임: 가로/세로 격자를 번갈아 — 촘촘할수록 직물처럼 보인다.
                    bool warp = ((x / 5) + (y / 5)) % 2 == 0;
                    c = warp ? MatBase : WeaveDark;
                    // 미세한 결 — 완전 균일하면 프린트처럼 보인다.
                    float grain = (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.05f;
                    c = new Color(c.r + grain, c.g + grain, c.b + grain);
                }
                px[y * W + x] = new Color(c.r, c.g, c.b, alpha);
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
