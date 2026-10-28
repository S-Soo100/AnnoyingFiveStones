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
    private Renderer stoneRenderer; // v27: 접지 그림자 크기 기준(보이는 돌 지름)
    private GameObject shadowObj;
    private Renderer shadowRenderer;
    private MaterialPropertyBlock mpb;

    // 그림자 크기 범위 (높이 낮을수록 작고 진하게)
    // v30: 지면 크기는 접지 그림자와 동일(돌 지름 비례)로 수렴 — 착지 프레임의 크기/모양 점프 제거.
    private const float ScaleAtPeak    = 0.9f;  // 최고점(10유닛 위)에서의 크기
    private const float AlphaAtGround  = 0.60f; // shadowSurfaceY에서의 불투명도
    private const float AlphaTtPeak    = 0.10f; // 최고점에서의 불투명도
    private const float HeightNormMax  = 10f;   // 정규화 기준 높이

    // v13-1: 보드에 놓인 돌(OnBoard)의 접지 그림자 — 자기 발밑 그림자.
    // v27: 고정 크기(구 1.15)·고정 오프셋(구 -0.20)은 "돌 지름 ≈1" 시절 값 — 원근으로 줄어든
    // 돌(지름 ~0.4대, 뒤쪽은 더 작음)에 그대로 쓰면 그림자가 돌의 2~3배로 깔려 떠 보인다.
    // → 보이는 돌 지름(renderer.bounds)에 비례 + 바닥 원근(세로 눌림)에 맞춘 납작 타원.
    private const float ContactDiaMul   = 1.25f;  // 그림자 가로 = 돌 지름 × 1.25 (발밑에서 살짝 삐져나옴)
    private const float ContactFlatten  = 0.55f;  // 세로 압축 — 보드 depthSquash(≈0.5)에 맞춘 접지감
    private const float ContactAlpha    = 0.55f;  // 크기가 맞아진 만큼 살짝 연하게 (구 0.62)
    private const float ContactYOffMul  = -0.35f; // 오프셋 = 돌 지름 × -0.35 (크기 비례)

    private const float ShadowZ        = -0.06f; // Cloth(Z=-0.05) 바로 앞 — 카메라(-10)에 더 가까워야 보임

    // v11-fix2: 그림자 Y는 매 프레임 돌 X 위치 기반 사다리꼴 내부 점으로 계산 (perspective 보정).
    // BoardBounds.QuadPoint(u, 0.5) 사용 — v=0.5는 사다리꼴 중심 수평선.
    // (필드 제거: 매 프레임 ComputeShadowY로 계산)

    private void Awake()
    {
        stone = GetComponent<Stone>();
        stoneRenderer = GetComponent<Renderer>();
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

        var mat = CreateTransparentUnlit(tex);

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

    /// <summary>돌이 비활성화되면(StonePool.SetActive(false)/스테이지 정리) 독립 루트인 shadowObj도 숨김.
    /// shadowObj가 부모 없는 루트라 Stone 비활성화만으론 안 꺼져 그림자가 누적되던 버그 방지.
    /// 재활성화 시엔 Stone.SetState→UpdateVisibility가 상태에 맞게 다시 표시.</summary>
    private void OnDisable()
    {
        if (shadowObj != null)
            shadowObj.SetActive(false);
    }

    /// <summary>Stone.SetState에서 호출: OnBoard(접지)/InAir/Bouncing(낙하)이면 활성화, 나머지 비활성화.
    /// M1-S6: 상태가 바뀌면 강제 표시(비행 그림자)·지면 차단도 초기화한다.</summary>
    public void UpdateVisibility(Stone.State newState)
    {
        forced = false;
        groundCut = false;
        cutMul = 1f;
        bool active = (newState == Stone.State.OnBoard
                    || newState == Stone.State.InAir
                    || newState == Stone.State.Bouncing);
        if (shadowObj != null)
            shadowObj.SetActive(active);
    }

    // ── M1-S6 (계획서 §5-4 "그림자 규칙", D2) ────────────────────────────────
    // 비행 그림자: 뿌리기 비행 중 돌은 InHand 상태라 원래 그림자가 꺼져 있다(C1) → 강제로 켠다.
    // 지면 차단: 낙 돌이 Ledge 선(허공)을 넘는 순간 0.08초에 걸쳐 0이 되고 다시 켜지지 않는다.
    //            Flat 너머(바닥이 이어짐)에서는 차단하지 않는다 — 그림자가 그 면에 깔린다.
    private const float GroundCutSeconds = 0.08f;
    private bool forced;
    private bool groundCut;
    private float cutMul = 1f;

    /// <summary>상태와 무관하게 낙하 그림자를 그린다(보드 좌표가 있는 돌만 의미가 있다).</summary>
    public void SetForced(bool on)
    {
        forced = on;
        if (shadowObj == null) return;
        if (on) shadowObj.SetActive(true);
        else if (stone != null) shadowObj.SetActive(stone.CurrentState == Stone.State.OnBoard
                                                  || stone.CurrentState == Stone.State.InAir
                                                  || stone.CurrentState == Stone.State.Bouncing);
    }

    /// <summary>true = 0.08초에 걸쳐 그림자 알파를 0으로(되돌아오지 않음). false = 즉시 원래대로.</summary>
    public void SetGroundCut(bool cut)
    {
        groundCut = cut;
        if (!cut) cutMul = 1f;
    }

    private void LateUpdate()
    {
        if (shadowObj == null || !shadowObj.activeSelf) return;

        if (groundCut && cutMul > 0f)
            cutMul = Mathf.Max(0f, cutMul - Time.deltaTime / GroundCutSeconds);

        // M1-S6: 강제 표시(뿌리기 비행) = 상태와 무관하게 보드 좌표 지면에 낙하 그림자
        if (forced)
        {
            UpdateFallingShadow();
            return;
        }

        // v13-1: 보드에 놓인 돌은 접지 그림자, 공중 돌은 기존 낙하 그림자로 분기.
        if (stone != null && stone.CurrentState == Stone.State.OnBoard)
        {
            UpdateContactShadow();
            return;
        }
        UpdateFallingShadow();
    }

    /// <summary>OnBoard: 접지 그림자 — 보이는 돌 지름에 비례하는 납작 타원 (v27).</summary>
    private void UpdateContactShadow()
    {
        Vector3 p = transform.position;
        float dia = stoneRenderer != null ? stoneRenderer.bounds.size.x : 0.5f;
        shadowObj.transform.localScale = new Vector3(dia * ContactDiaMul, dia * ContactDiaMul * ContactFlatten, 1f);
        shadowObj.transform.position = new Vector3(p.x, p.y + dia * ContactYOffMul, ShadowZ);

        shadowRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", new Color(0f, 0f, 0f, ContactAlpha * cutMul));
        mpb.SetColor("_Color", new Color(0f, 0f, 0f, ContactAlpha * cutMul));
        shadowRenderer.SetPropertyBlock(mpb);
    }

    /// <summary>InAir/Bouncing: 높이에 따라 크기/투명도가 변하는 낙하 그림자 (중심선 투영).</summary>
    private void UpdateFallingShadow()
    {
        float stoneX, shadowY, heightAbove;
        float dia = stoneRenderer != null ? stoneRenderer.bounds.size.x : 0.5f;
        bool legacy = !(stone != null && stone.HasBoardMotion);

        if (!legacy)
        {
            // v17: 돌이 자기 보드 좌표와 높이를 안다 → 그림자는 그 좌표의 지면에 정확히 찍힌다.
            // 이것이 "그림자 = 조준선"의 근거다. 돌이 아무리 높아도 그림자는 착지 지점에 머문다.
            var ground = BoardSpace.ToScreen(stone.BoardPos, 0f);
            stoneX = ground.x;
            shadowY = ground.y;
            heightAbove = stone.Height * BoardSpace.HeightScale;
        }
        else
        {
            // 구 경로(뿌리기·5단 등 아직 안 옮긴 곳): 사다리꼴 v=0.5 중심선으로 근사.
            // ⚠️ 깊이를 0.5로 고정하므로 실제 착지 지점과 어긋난다. 옮겨오면 이 분기는 사라진다.
            stoneX = transform.position.x;
            shadowY = ComputeShadowY(stoneX);
            heightAbove = transform.position.y - shadowY;
            // M2: 중심선보다 앞(화면 아래)으로 내려온 돌 — 높이가 음수라 중심선에 가장 진하게 찍히던 것을
            // 돌 발밑(접지 그림자 자리)으로 붙인다. 보드 앞변 아래로 나가면 아래에서 숨긴다.
            if (heightAbove < 0f)
            {
                shadowY = transform.position.y + dia * ContactYOffMul;
                heightAbove = 0f;
            }
        }

        float normalizedH = Mathf.Clamp01(heightAbove / HeightNormMax);

        // 크기: 높이 낮을수록 작게. v30: 지면(h=0)에서 접지 그림자와 정확히 같은 크기·납작비로
        // 수렴시켜 OnBoard 전환 프레임에 그림자가 튀지 않는다.
        float scale = Mathf.Lerp(dia * ContactDiaMul, ScaleAtPeak, normalizedH);
        shadowObj.transform.localScale = new Vector3(scale, scale * ContactFlatten, 1f);

        // 위치: 돌 X, 사다리꼴 내부 Y + 0.01f, Z = ShadowZ (돌보다 뒤)
        shadowObj.transform.position = new Vector3(
            stoneX,
            shadowY + 0.01f,
            ShadowZ
        );

        // 투명도: 높이 낮을수록 진하게
        float alpha = Mathf.Lerp(AlphaAtGround, AlphaTtPeak, normalizedH) * cutMul;
        // M2-S3: 구 경로(물리로 떨어지는 튕긴 돌)가 보드 앞변 아래로 내려가면 숨긴다 — 그 아래는 허공이다
        // (기획 v11 §6 "보드 아래로 내려가면 그림자가 없다". 판정선 = CatchSystem.CatchFailY와 같은 앞변).
        if (legacy && transform.position.y < BoardSpace.FrontScreenY) alpha = 0f;
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

    /// <summary>URP Unlit 알파 블렌드 머티리얼(없으면 Unlit/Transparent 폴백). 색·알파는 PropertyBlock의 _BaseColor/_Color로 준다.
    /// 그림자와 뿌리기 먼지(ScatterFx)가 같은 셰이더 경로를 쓰게 하나로 둔다 — 빌드 정합(셰이더 포함 여부)이 같이 따라간다.</summary>
    public static Material CreateTransparentUnlit(Texture2D tex)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
            shader = Shader.Find("Unlit/Transparent");   // URP Unlit 폴백

        var mat = new Material(shader);
        mat.mainTexture = tex;

        // 알파 블렌드 설정
        mat.SetFloat("_Surface", 1f);    // Transparent
        mat.SetFloat("_Blend", 0f);      // Alpha
        mat.SetFloat("_SrcBlend", 5f);   // SrcAlpha
        mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return mat;
    }

    /// <summary>중심 진하고 가장자리 투명한 원형 그라데이션 텍스처 생성.
    /// v18: <see cref="HandShadow"/>도 같은 그림자를 써야 "같은 바닥에 놓인 것"으로 읽히므로 공개한다.</summary>
    public static Texture2D CreateCircleGradientTexture(int size)
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
