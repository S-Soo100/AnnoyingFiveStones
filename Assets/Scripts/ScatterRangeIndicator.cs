using UnityEngine;

/// <summary>
/// 뿌리기 게이지 왕복 중 "돌이 흩어질 범위"를 보드 위에 원(ring)으로 실시간 표시.
/// 순수 시각 레이어 — 낙/산개/손 로직은 전혀 건드리지 않는다.
/// ★좌표: ScatterSystem.DoScatter와 "동일한" BoardBounds 4꼭짓점 + TrapPoint(LerpUnclamped)로
///   원을 사다리꼴 보드에 매핑 → 링이 테이블 밖으로 넘치면 낙 경고색으로 전환.
/// ★z: 링은 돌(z=0) 바로 뒤·매트 앞 → RingZ=0.03 (안 보이면 실행 확인 후 조정).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ScatterRangeIndicator : MonoBehaviour
{
    private const int SEG = 48;
    private const float RingZ = 0.03f;

    // 스윗 밴드 정적 목표 도넛(항상 표시) — 유저가 겨냥할 안전 반경대.
    // v17: uv → 보드 단위. 보드 반깊이 4.5가 앞뒤 낙의 경계다.
    private const float GuideInnerBoard = 2.50f; // 뭉침 탈출 = 스윗 진입
    private const float GuideOuterBoard = 4.00f; // 스윗 끝 (이 위는 경계 경고)

    // 게이지바와 톤 통일한 팔레트.
    // v18: 스윗 색을 UI 시안의 초록으로 바꿨다. 링과 게이지바는 같은 순간에 같은 화면에 뜨고
    // 같은 정보를 말하므로, 스윗 색이 서로 다르면 플레이어가 둘을 다른 신호로 읽는다.
    private static readonly Color mint  = UISkin.SafeGreen;
    private static readonly Color amber = new Color(0.95f, 0.75f, 0.30f);
    private static readonly Color coral = new Color(0.95f, 0.40f, 0.35f);

    /// <summary>
    /// 뿌림 반경 → 위험 색. 뭉침(coral) → 스윗(mint) → 경계(amber) → 낙(coral).
    ///
    /// **낮은 쪽도 나쁘다** — 반경이 작으면 돌이 뭉쳐서 손바닥으로 하나만 집을 수가 없다.
    /// 그래서 이 곡선은 단조 증가가 아니라 U자다. 게이지바가 이 함수를 같이 쓴다
    /// (GaugeBarUI) — 링과 바가 각자 계산하면 언젠가 서로 다른 말을 하게 된다.
    /// </summary>
    public static Color BandColor(float radiusBoard)
    {
        if (radiusBoard < GuideInnerBoard)
            return Color.Lerp(coral, mint, Mathf.InverseLerp(1.6f, GuideInnerBoard, radiusBoard)); // 뭉침→스윗 진입
        if (radiusBoard <= 4.50f)
            return Color.Lerp(mint, amber, Mathf.InverseLerp(GuideOuterBoard, 4.50f, radiusBoard)); // 스윗, 경계 근처 앰버
        return coral; // 낙
    }

    private LineRenderer lr;
    private LineRenderer guideInner;
    private LineRenderer guideOuter;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = SEG;
        lr.widthMultiplier = 0.06f;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 0;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        // LineRenderer는 vertex-color·투명이 Sprites/Default에서 가장 안정적.
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.enabled = false; // 시작은 숨김

        // 스윗 밴드 정적 도넛 2개 (라이브 링과 동일 세팅, 더 얇고 흐린 회백색).
        guideInner = CreateGuide("ScatterGuideInner");
        guideOuter = CreateGuide("ScatterGuideOuter");
    }

    /// <summary>정적 가이드용 자식 LineRenderer 생성 (라이브 링과 동일 세팅, 얇고 흐림).</summary>
    private LineRenderer CreateGuide(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var g = go.AddComponent<LineRenderer>();
        g.useWorldSpace = true;
        g.loop = true;
        g.positionCount = SEG;
        g.widthMultiplier = 0.03f; // 라이브 링보다 얇게
        g.numCornerVertices = 4;
        g.numCapVertices = 0;
        g.alignment = LineAlignment.View;
        g.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.receiveShadows = false;
        g.material = new Material(Shader.Find("Sprites/Default"));
        var faint = new Color(1f, 1f, 1f, 0.25f);
        g.startColor = faint;
        g.endColor = faint;
        g.enabled = false; // 시작은 숨김
        return g;
    }

    /// <summary>게이지 시작 시 표시. 여러 번 호출돼도 무해.</summary>
    public void Show()
    {
        lr.enabled = true;
        if (guideInner != null) guideInner.enabled = true;
        if (guideOuter != null) guideOuter.enabled = true;
    }

    /// <summary>게이지 종료/리셋 시 숨김. 여러 번 호출돼도 무해.</summary>
    public void Hide()
    {
        if (lr != null) lr.enabled = false;
        if (guideInner != null) guideInner.enabled = false;
        if (guideOuter != null) guideOuter.enabled = false;
    }

    /// <summary>게이지 값에 대응하는 산개 반경(**보드 단위**)으로 링을 갱신.
    /// centerBoard = 산개 중심(커서/손 위치의 보드 좌표).
    /// v17: 보드 공간의 진짜 원을 그린다 → 화면에서는 원근에 눌린 타원으로 보이는데,
    /// 그게 "돗자리 위에 그린 원"의 올바른 모습이고 실제 산개와 정확히 일치한다.</summary>
    public void UpdateRing(float radiusBoard, Vector2 centerBoard)
    {
        // 1) 스윗 밴드 도넛 (중심도 커서 따라 이동 — 라이브 링과 동일 원점).
        BuildGuide(guideInner, GuideInnerBoard, centerBoard);
        BuildGuide(guideOuter, GuideOuterBoard, centerBoard);

        // 2) SEG개 점 배치 + 낙 위험(보드 밖) 카운트.
        int outside = 0;
        for (int i = 0; i < SEG; i++)
        {
            float theta = 2f * Mathf.PI * i / SEG;
            Vector2 bp = centerBoard + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radiusBoard;
            Vector2 world = BoardSpace.ToScreen(bp, 0f);
            lr.SetPosition(i, new Vector3(world.x, world.y, RingZ));

            if (!BoardSpace.Current.Contains(bp)) outside++;
        }

        float dangerFrac = (float)outside / SEG;

        // 3) 3밴드 색. 실제 넘침은 coral 강조.
        Color c = BandColor(radiusBoard);
        if (dangerFrac > 0f) c = Color.Lerp(c, coral, Mathf.Clamp01(dangerFrac * 3f)); // 실제 넘침 강조
        c.a = 0.85f;
        lr.startColor = c;
        lr.endColor = c;
    }

    /// <summary>정적 가이드 도넛을 반경 rUV로 보드 폴리곤에 배치. centerUV = 산개 중심(커서).</summary>
    private void BuildGuide(LineRenderer g, float rBoard, Vector2 centerBoard)
    {
        if (g == null) return;
        for (int i = 0; i < SEG; i++)
        {
            float theta = 2f * Mathf.PI * i / SEG;
            Vector2 bp = centerBoard + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * rBoard;
            Vector2 world = BoardSpace.ToScreen(bp, 0f);
            g.SetPosition(i, new Vector3(world.x, world.y, RingZ));
        }
    }

    /// <summary>보드 폴리곤 bilinear 매핑 (ScatterSystem.TrapPoint와 동일).
    /// LerpUnclamped라 uv>1이면 외삽 → 링이 보드 밖으로 넘침.</summary>
    private static Vector2 TrapPoint(float u, float v, Vector2 bl, Vector2 br, Vector2 fl, Vector2 fr)
    {
        Vector2 back  = Vector2.LerpUnclamped(bl, br, u);
        Vector2 front = Vector2.LerpUnclamped(fl, fr, u);
        return Vector2.LerpUnclamped(back, front, v);
    }
}
