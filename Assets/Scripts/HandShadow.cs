using UnityEngine;

/// <summary>
/// v18 — 손이 보드 위에 떠 있다는 것을 그림자로 알린다.
///
/// 왜 필요한가:
/// 돌은 <see cref="StoneShadow"/>로 발밑이 읽히는데 손만 기준이 없어 공중에 떠 있었다.
/// 2.5D에서 "내 손이 보드의 어느 깊이인가"는 줍기 성패를 가르는 정보인데, 그걸 알 방법이
/// 손 그림 크기뿐이었다.
///
/// 기획서 v11 §8: "HUD·주석·하이라이트를 얹지 않는다. 정보는 오브젝트 자체가 말한다.
/// 손 판정 범위 → **손 그림**으로." 그래서 이 그림자의 반경은 장식이 아니라
/// **줍기 판정 반경 그 자체**다(HandController.PickRadiusBoard). 손이 그 위에 겹쳐 그려지므로
/// 실제로 보이는 것은 판정 경계의 테두리다 — 설명 없이 범위를 알려주는 유일한 수단.
///
/// 2.5D 좌표: X=좌우, Y=상하, Z=깊이(작을수록 카메라에 가까움).
/// 실측 배치 — 손 -0.5 / 돌 -0.15 / 그림자 -0.06 / 돗자리 -0.05.
/// 그림자는 **돌보다 뒤, 돗자리보다 앞**이라야 "바닥에 드리운 것"으로 읽힌다.
/// </summary>
public class HandShadow : MonoBehaviour
{
    /// <summary>돌 그림자와 같은 평면. 돌(-0.15) 뒤, 돗자리(-0.05) 앞.</summary>
    private const float ShadowZ = -0.06f;

    /// <summary>가장 진할 때의 불투명도. 돌 접지 그림자(0.62)보다 옅다 —
    /// 손은 바닥에 닿은 게 아니라 떠 있는 것이라 그림자도 그만큼 흐려야 한다.</summary>
    private const float MaxAlpha = 0.34f;

    private GameObject shadowObj;
    private Renderer shadowRenderer;
    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();

        shadowObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadowObj.name = "HandShadow";
        shadowObj.transform.SetParent(null); // 손의 원근 스케일에 딸려가면 안 된다 — 크기는 여기서 직접 정한다
        shadowObj.transform.rotation = Quaternion.identity;
        shadowObj.SetActive(false);

        var col = shadowObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            mat = new Material(Shader.Find("Unlit/Transparent"));

        mat.mainTexture = StoneShadow.CreateCircleGradientTexture(64);
        mat.SetFloat("_Surface", 1f);   // Transparent
        mat.SetFloat("_Blend", 0f);     // Alpha
        mat.SetFloat("_SrcBlend", 5f);  // SrcAlpha
        mat.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        shadowRenderer = shadowObj.GetComponent<Renderer>();
        shadowRenderer.material = mat;
    }

    private void OnDestroy()
    {
        if (shadowObj != null) Destroy(shadowObj);
    }

    /// <summary>손이 꺼지면 루트가 따로인 그림자도 같이 숨긴다.
    /// (StoneShadow가 같은 이유로 겪었던 "그림자만 남는" 버그를 미리 막는다)</summary>
    private void OnDisable()
    {
        if (shadowObj != null) shadowObj.SetActive(false);
    }

    /// <summary>매 프레임 HandController가 호출.</summary>
    /// <param name="palmWorld">손바닥 중심(월드) — 판정 기준점과 같아야 한다.</param>
    /// <param name="radiusX">가로 반경(월드).</param>
    /// <param name="radiusY">세로 반경(월드). 보드가 화면에서 세로로 눌려 있으므로 가로보다 작다 —
    /// 정원으로 그리면 세로 도달 범위를 과장해 **거짓 정보**가 된다.</param>
    /// <param name="strength">0이면 숨김. 보드를 벗어날수록 0으로 잦아든다.</param>
    public void SetShadow(Vector3 palmWorld, float radiusX, float radiusY, float strength)
    {
        if (shadowObj == null) return;

        if (strength <= 0.001f || radiusX <= 0.001f || radiusY <= 0.001f)
        {
            if (shadowObj.activeSelf) shadowObj.SetActive(false);
            return;
        }

        if (!shadowObj.activeSelf) shadowObj.SetActive(true);

        shadowObj.transform.localScale = new Vector3(radiusX * 2f, radiusY * 2f, 1f);
        shadowObj.transform.position = new Vector3(palmWorld.x, palmWorld.y, ShadowZ);

        var c = new Color(0f, 0f, 0f, MaxAlpha * Mathf.Clamp01(strength));
        shadowRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", c);
        mpb.SetColor("_Color", c);   // Unlit 폴백 셰이더용
        shadowRenderer.SetPropertyBlock(mpb);
    }
}
