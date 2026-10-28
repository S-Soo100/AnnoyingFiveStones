using UnityEngine;

/// <summary>
/// 뿌리기 게이지 왕복 중 "돌이 흩어질 범위"를 보드 위에 원(ring)으로 실시간 표시.
/// 순수 시각 레이어 — 낙/산개/손 로직은 전혀 건드리지 않는다.
/// ★좌표: ScatterSystem.DoScatter와 "동일한" BoardBounds 4꼭짓점 + TrapPoint(LerpUnclamped)로
///   원을 사다리꼴 보드에 매핑 → 링이 테이블 밖으로 넘치면 낙 경고색으로 전환.
/// ★z: 링은 돌(z=0) 바로 뒤·매트 앞 → RingZ=0.03 (안 보이면 실행 확인 후 조정).
/// </summary>
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

    // ── M1-S4 링 재스타일 (계획서 §5-7) ────────────────────────────────────────
    // "디버그 선처럼 보인다"(분석서 §2) → 보드에 눌러 붙은 **반투명 원판 + 점선 테두리**.
    //   원판: 정점색 = 밴드색, 알파 0.14±0.04가 1.2Hz로 숨쉰다(반경은 숨쉬지 않는다 — T1).
    //   점선: 28개, 듀티 0.6, 화면 폭 0.05, 알파 0.90.
    //   스윗 가이드: 선 2개 → 2.5~4.0 사이 옅은 흰 고리 면 1개(알파 0.07).
    // 메시 1개를 Awake에서 MarkDynamic으로 만들고 정점·색 배열을 미리 할당한다. 삼각형은 한 번만 설정
    // (토폴로지 고정) → UpdateRing은 정점·색만 덮어쓴다. 프레임당 할당 0.
    // 그리는 순서 = 삼각형 순서(한 메시·한 머티리얼): 원판 → 가이드 고리 → 점선.
    private const float DiscAlpha = 0.14f, DiscAlphaBreath = 0.04f, DiscBreathHz = 1.2f;
    private const int DashCount = 28;
    private const float DashDuty = 0.6f, DashWidth = 0.05f, DashAlpha = 0.90f;
    private const int DashSubdiv = 4;                 // 점선 하나를 몇 조각으로 휘게 그릴지
    private const float GuideAlpha = 0.07f;

    // 정점 배치: [원판 중심 1][원판 테두리 SEG][가이드 안 SEG][가이드 밖 SEG][점선 DashCount×(DashSubdiv+1)×2]
    private const int DiscCenterV = 0;
    private const int DiscRimV = 1;
    private const int GuideInV = DiscRimV + SEG;
    private const int GuideOutV = GuideInV + SEG;
    private const int DashV = GuideOutV + SEG;
    private const int DashPts = DashSubdiv + 1;
    private const int VertCount = DashV + DashCount * DashPts * 2;

    private Mesh mesh;
    private MeshRenderer mr;
    private Vector3[] verts;
    private Color32[] colors;
    private readonly Vector2[] dashScreen = new Vector2[DashPts]; // 점선 한 개의 화면 좌표 (재사용 버퍼)

    private void Awake()
    {
        // 정점은 월드 좌표로 쓴다 → 트랜스폼은 원점·무회전·단위 스케일이어야 한다.
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        verts = new Vector3[VertCount];
        colors = new Color32[VertCount];
        mesh = new Mesh { name = "ScatterRangeMesh" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.colors32 = colors;
        mesh.triangles = BuildTriangles();
        // 첫 UpdateRing 전 컬링 방지용 넉넉한 경계 (이후 정점 대입이 실제 경계로 다시 계산한다)
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(200f, 200f, 10f));

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        mr = gameObject.AddComponent<MeshRenderer>();
        // 정점색·알파 블렌드가 가장 안정적인 셰이더(구 LineRenderer와 같은 것)
        mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.enabled = false; // 시작은 숨김
    }

    private static int[] BuildTriangles()
    {
        int tri = SEG * 3 + SEG * 6 + DashCount * DashSubdiv * 6;
        var t = new int[tri];
        int k = 0;
        // 원판 (부채꼴)
        for (int i = 0; i < SEG; i++)
        {
            t[k++] = DiscCenterV; t[k++] = DiscRimV + (i + 1) % SEG; t[k++] = DiscRimV + i;
        }
        // 가이드 고리 면
        for (int i = 0; i < SEG; i++)
        {
            int i1 = (i + 1) % SEG;
            int a = GuideInV + i, b = GuideInV + i1, c = GuideOutV + i, d = GuideOutV + i1;
            t[k++] = a; t[k++] = c; t[k++] = b;
            t[k++] = b; t[k++] = c; t[k++] = d;
        }
        // 점선 (조각마다 사각형 하나)
        for (int dsh = 0; dsh < DashCount; dsh++)
        {
            int baseV = DashV + dsh * DashPts * 2;
            for (int j = 0; j < DashSubdiv; j++)
            {
                int a = baseV + j * 2, b = a + 1, c = a + 2, d = a + 3; // a,b = j번 점의 안/밖, c,d = j+1번
                t[k++] = a; t[k++] = c; t[k++] = b;
                t[k++] = b; t[k++] = c; t[k++] = d;
            }
        }
        return t;
    }

    /// <summary>게이지 시작 시 표시. 여러 번 호출돼도 무해.</summary>
    public void Show()
    {
        if (mr != null) mr.enabled = true;
    }

    /// <summary>게이지 종료/리셋 시 숨김. 여러 번 호출돼도 무해.</summary>
    public void Hide()
    {
        if (mr != null) mr.enabled = false;
    }

    /// <summary>검증 로그용 — 현재 표시 상태 요약.</summary>
    public string DebugSummary =>
        mr == null ? "없음" : $"enabled={mr.enabled} disc={colors[DiscCenterV]} dash={colors[DashV]} v0={verts[DiscRimV]:F2}";

    /// <summary>게이지 값에 대응하는 산개 반경(**보드 단위**)으로 링을 갱신.
    /// centerBoard = 산개 중심(커서/손 위치의 보드 좌표).
    /// 2026-10-28: 모양은 화면 타원(아래 본문 주석), 위험색은 보드 좌표의 실제 착지 원으로 계산한다.</summary>
    public void UpdateRing(float radiusBoard, Vector2 centerBoard)
    {
        // 사용자 결정(2026-10-28): 링은 **화면에서 깔끔한 타원**으로 그린다.
        //   보드 투영(BoardGeometry.Project)은 가로만 원근으로 넓히고 깊이는 균일하게 눌러서, 보드 위 진짜 원이
        //   화면에선 위가 좁고 아래가 넓은 달걀이 된다. 그 달걀이 "찌그러져 보인다"는 피드백.
        //   타원 = 중심은 원 중심의 화면 위치, 가로 반지름은 그 깊이의 화면 폭, 세로 반지름은 깊이 압축 그대로
        //   (원의 위·아래·좌·우 끝점은 실제 착지 범위와 일치). 대각선 방향은 실제 착지 경계와 최대 ~0.4 화면 단위
        //   어긋난다(위쪽은 넓게, 아래쪽은 좁게) — 감수한 트레이드오프.
        //   ⚠️ 낙 위험색(outside)은 계속 **실제 착지 원(보드 좌표)** 으로 센다 — 색이 말하는 정보는 정확하다.
        Vector2 cw = BoardSpace.ToScreen(centerBoard, 0f);
        float halfWAtCenter = Mathf.LerpUnclamped(BoardSpace.BackHalfWidth, BoardSpace.FrontHalfWidth,
                                                  centerBoard.y / BoardSpace.LogicalDepth + 0.5f);
        float sx = halfWAtCenter / (BoardSpace.LogicalWidth * 0.5f);                          // 보드 1 → 화면 가로
        float sy = (BoardSpace.BackScreenY - BoardSpace.FrontScreenY) / BoardSpace.LogicalDepth; // 보드 1 → 화면 세로

        // 1) 원판 테두리 SEG개 점 배치 + 낙 위험(보드 밖) 카운트. (구 링과 같은 48점·같은 판정)
        int outside = 0;
        for (int i = 0; i < SEG; i++)
        {
            float theta = 2f * Mathf.PI * i / SEG;
            Vector2 bp = centerBoard + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radiusBoard;
            if (!BoardSpace.Current.Contains(bp)) outside++;

            Vector2 world = Ellipse(cw, radiusBoard * sx, radiusBoard * sy, theta);
            verts[DiscRimV + i] = new Vector3(world.x, world.y, RingZ);
        }
        verts[DiscCenterV] = new Vector3(cw.x, cw.y, RingZ);

        float dangerFrac = (float)outside / SEG;

        // 2) 3밴드 색. 실제 넘침은 coral 강조. (색 규칙 불변)
        Color c = BandColor(radiusBoard);
        if (dangerFrac > 0f) c = Color.Lerp(c, coral, Mathf.Clamp01(dangerFrac * 3f)); // 실제 넘침 강조

        // 3) 스윗 밴드 고리 면 (중심도 커서 따라 이동 — 원판과 동일 원점).
        for (int i = 0; i < SEG; i++)
        {
            float theta = 2f * Mathf.PI * i / SEG;
            Vector2 wi = Ellipse(cw, GuideInnerBoard * sx, GuideInnerBoard * sy, theta);
            Vector2 wo = Ellipse(cw, GuideOuterBoard * sx, GuideOuterBoard * sy, theta);
            verts[GuideInV + i] = new Vector3(wi.x, wi.y, RingZ);
            verts[GuideOutV + i] = new Vector3(wo.x, wo.y, RingZ);
        }

        // 4) 점선 — 보드 원 위의 호를 화면으로 투영한 뒤 **화면에서** 폭을 준다(원근에 눌려 얇아지지 않게).
        float step = 2f * Mathf.PI / DashCount;
        for (int dsh = 0; dsh < DashCount; dsh++)
        {
            float a0 = step * dsh;
            for (int j = 0; j < DashPts; j++)
            {
                float a = a0 + step * DashDuty * j / DashSubdiv;
                dashScreen[j] = Ellipse(cw, radiusBoard * sx, radiusBoard * sy, a);
            }
            int baseV = DashV + dsh * DashPts * 2;
            for (int j = 0; j < DashPts; j++)
            {
                Vector2 tan = dashScreen[Mathf.Min(j + 1, DashPts - 1)] - dashScreen[Mathf.Max(j - 1, 0)];
                Vector2 nrm = tan.sqrMagnitude > 1e-10f ? new Vector2(-tan.y, tan.x).normalized : Vector2.up;
                Vector2 o = nrm * (DashWidth * 0.5f);
                verts[baseV + j * 2]     = new Vector3(dashScreen[j].x - o.x, dashScreen[j].y - o.y, RingZ);
                verts[baseV + j * 2 + 1] = new Vector3(dashScreen[j].x + o.x, dashScreen[j].y + o.y, RingZ);
            }
        }

        // 5) 색 — 원판은 알파만 숨쉰다. 반경은 그대로.
        float breath = DiscAlpha + DiscAlphaBreath * Mathf.Sin(2f * Mathf.PI * DiscBreathHz * Time.time);
        Color32 disc = WithAlpha(c, breath);
        Color32 dash = WithAlpha(c, DashAlpha);
        Color32 guide = WithAlpha(Color.white, GuideAlpha);
        for (int i = DiscCenterV; i < GuideInV; i++) colors[i] = disc;
        for (int i = GuideInV; i < DashV; i++) colors[i] = guide;
        for (int i = DashV; i < VertCount; i++) colors[i] = dash;

        mesh.vertices = verts;     // 같은 크기 배열 덮어쓰기 — 관리 힙 할당 없음
        mesh.colors32 = colors;
    }

    /// <summary>화면 타원 위의 점. 보드 +y(앞)가 화면 아래이므로 sin 항은 빼서 원과 같은 방향으로 돈다.</summary>
    private static Vector2 Ellipse(Vector2 c, float rx, float ry, float theta)
        => new Vector2(c.x + rx * Mathf.Cos(theta), c.y - ry * Mathf.Sin(theta));

    private static Color32 WithAlpha(Color c, float a)
    {
        c.a = Mathf.Clamp01(a);
        return c;
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
