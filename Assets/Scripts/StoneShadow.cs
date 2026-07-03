using UnityEngine;

/// <summary>
/// v6-1: 공중 낙하 중 그림자 연출.
/// Stone의 자식으로 생성 (Stone.Awake에서 AddComponent).
/// InAir / Bouncing 상태일 때만 활성화되며, 높이에 따라 크기와 투명도가 변한다.
///
/// 2.5D 좌표 매핑:
///   X = 좌우, Y = 상하, Z = 깊이 (카메라=-10이므로 작을수록(음수일수록) 카메라에 가까움)
///   Cloth(보드)가 Z=-0.05에 있으므로 그림자는 Cloth 앞(Z=-0.06)에 배치해야 보임
///   Quad 회전 없음 (XY 평면 유지, 카메라 정면에서 평평하게 보임)
/// </summary>
[RequireComponent(typeof(Stone))]
public class StoneShadow : MonoBehaviour
{
    private Stone stone;
    private GameObject shadowObj;
    private Renderer shadowRenderer;
    private MaterialPropertyBlock mpb;

    // 그림자 크기 범위 (높이 낮을수록 작고 진하게)
    private const float ScaleAtGround  = 0.35f; // shadowSurfaceY에서의 크기
    private const float ScaleAtPeak    = 0.9f;  // 최고점(10유닛 위)에서의 크기
    private const float AlphaAtGround  = 0.60f; // shadowSurfaceY에서의 불투명도
    private const float AlphaTtPeak    = 0.10f; // 최고점에서의 불투명도
    private const float HeightNormMax  = 10f;   // 정규화 기준 높이

    private const float ShadowZ        = -0.06f; // Cloth(Z=-0.05) 바로 앞 — 카메라(-10)에 더 가까워야 보임

    // v11-fix2: 그림자 Y는 매 프레임 돌 X 위치 기반 사다리꼴 내부 점으로 계산 (perspective 보정).
    // BoardBounds.QuadPoint(u, 0.5) 사용 — v=0.5는 사다리꼴 중심 수평선.
    // (필드 제거: 매 프레임 ComputeShadowY로 계산)

    private void Awake()
    {
        stone = GetComponent<Stone>();
        mpb = new MaterialPropertyBlock();

        // 그림자 Quad 생성
        shadowObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadowObj.name = "StoneShadow";
        shadowObj.transform.SetParent(null); // 부모 없음 — 독립적으로 보드 표면에 위치
        shadowObj.SetActive(false);

        // Quad Collider 제거 (판정 불필요)
        var col = shadowObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // 그라데이션 텍스처 런타임 생성
        var tex = CreateCircleGradientTexture(64);

        // Unlit/Transparent 머티리얼 생성 (URP)
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
        {
            // URP Unlit 폴백
            mat = new Material(Shader.Find("Unlit/Transparent"));
        }

        mat.mainTexture = tex;

        // 알파 블렌드 설정
        mat.SetFloat("_Surface", 1f);    // Transparent
        mat.SetFloat("_Blend", 0f);      // Alpha
        mat.SetFloat("_SrcBlend", 5f);   // SrcAlpha
        mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        shadowRenderer = shadowObj.GetComponent<Renderer>();
        shadowRenderer.material = mat;

        // Quad 회전 없음 (XY 평면 유지 — 카메라 정면에서 보임)
        shadowObj.transform.rotation = Quaternion.identity;
    }

    // v11-fix2: Start 제거 — 매 프레임 LateUpdate에서 ComputeShadowY로 계산.

    private void OnDestroy()
    {
        if (shadowObj != null)
            Destroy(shadowObj);
    }

    /// <summary>Stone.SetState에서 호출: InAir/Bouncing이면 활성화, 나머지 비활성화</summary>
    public void UpdateVisibility(Stone.State newState)
    {
        bool active = (newState == Stone.State.InAir || newState == Stone.State.Bouncing);
        if (shadowObj != null)
            shadowObj.SetActive(active);
    }

    private void LateUpdate()
    {
        if (shadowObj == null || !shadowObj.activeSelf) return;

        // v11-fix2: 돌 X 기반 perspective 보정 Y. 사다리꼴 내부 v=0.5 라인 (중심선).
        float stoneX = transform.position.x;
        float shadowY = ComputeShadowY(stoneX);

        float stoneY  = transform.position.y;
        float heightAbove = stoneY - shadowY;
        float normalizedH = Mathf.Clamp01(heightAbove / HeightNormMax);

        // 크기: 높이 낮을수록 작게 (가까울수록 실제 그림자처럼 선명하고 작게)
        float scale = Mathf.Lerp(ScaleAtGround, ScaleAtPeak, normalizedH);
        shadowObj.transform.localScale = new Vector3(scale, scale, 1f);

        // 위치: 돌 X, 사다리꼴 내부 Y + 0.01f, Z = ShadowZ (돌보다 뒤)
        shadowObj.transform.position = new Vector3(
            stoneX,
            shadowY + 0.01f,
            ShadowZ
        );

        // 투명도: 높이 낮을수록 진하게
        float alpha = Mathf.Lerp(AlphaAtGround, AlphaTtPeak, normalizedH);
        shadowRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", new Color(0f, 0f, 0f, alpha));
        // Unlit 셰이더의 경우 _Color 사용
        mpb.SetColor("_Color", new Color(0f, 0f, 0f, alpha));
        shadowRenderer.SetPropertyBlock(mpb);
    }

    /// <summary>돌 X를 사다리꼴 뒷변 x 범위에 매핑 후 v=0.5(중심선) 라인의 Y 반환.
    /// quad 없으면 MatRect 중심선 fallback.</summary>
    private static float ComputeShadowY(float stoneX)
    {
        var rect = BoardBounds.MatRect;
        if (!BoardBounds.HasQuad)
        {
            // fallback: 단순 중심선
            return (rect.yMin + rect.yMax) * 0.5f;
        }
        // u = stoneX의 [xMin, xMax] 내 정규화 위치 (사다리꼴 AABB 기준)
        float u = Mathf.Clamp01(Mathf.InverseLerp(rect.xMin, rect.xMax, stoneX));
        // QuadPoint(u, 0.5) — v=0.5는 사다리꼴 중심 수평선
        Vector2 pt = BoardBounds.QuadPoint(u, 0.5f);
        return pt.y;
    }

    /// <summary>중심 진하고 가장자리 투명한 원형 그라데이션 텍스처 생성</summary>
    private static Texture2D CreateCircleGradientTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float radius = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float normalized = Mathf.Clamp01(dist / radius);
                // 중심에서 가장자리로 갈수록 투명 (부드러운 그라데이션)
                float alpha = Mathf.Clamp01(1f - normalized * normalized);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }
}
