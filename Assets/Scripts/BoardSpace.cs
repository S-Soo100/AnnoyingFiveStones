using UnityEngine;
using FiveStones.Core;

/// <summary>
/// v17 — **전 스테이지 공통** 보드 공간.
///
/// 기획서 v11 §2: "보드는 배경에서 분리된 별도 오브젝트다. 배경 이미지는 분위기 전용이며
/// 플레이면이 아니다. 보드는 코드가 그리는 '바닥에 깔린 돗자리'로, 배경이 무엇이든 그 위에 놓인다."
///
/// ⚠️ **이 값은 스테이지에 따라 바뀌지 않는다.** 그것이 이 재설계의 핵심이다.
/// 이전에는 배경 아트(책상/돗자리/마우스패드/병상)마다 BoardQuad를 손으로 재서 맞췄고,
/// 배경을 갈 때마다 다시 재야 했다 — v11·v12·v14·v16 버그가 전부 여기서 나왔다.
/// 이제 배경이 무엇이든 노는 자리는 하나다. 1단에서 맞춘 감각이 10단까지 그대로 간다.
///
/// - **Board Space (논리)**: 완전한 직사각형. 모든 게임 로직이 여기서만 돈다.
/// - **Screen Space (표현)**: 아래 상수로 정의된 사다리꼴. 렌더링에만 쓴다.
/// </summary>
public static class BoardSpace
{
    // ── 논리 (판정) — 스테이지 무관 ──────────────────────────────────────────

    /// <summary>논리 보드 가로. 결정 6-2 "가로로 넓게, 약 16:9". ⚠️ 재튜닝 대상.</summary>
    public const float LogicalWidth = 16f;

    /// <summary>논리 보드 세로/깊이. ⚠️ 재튜닝 대상.</summary>
    public const float LogicalDepth = 9f;

    /// <summary>화면 1유닛 = 높이 1유닛. ⚠️ 재튜닝 대상.</summary>
    public const float HeightScale = 1f;

    // ── 표현 (사다리꼴) — 스테이지 무관 ──────────────────────────────────────
    // 카메라: ortho 7, 중심 y=-1.5 → 화면 y는 +5.5 ~ -8.5 (14유닛).
    //
    // v17-b (사용자 피드백 "좀더 납작하고 좀더 사다리꼴"):
    //   납작: 깊이 4.80 → 3.60. 뒷변을 내려서 줄였다(앞변 고정) → 하늘이 그만큼 넓어진다.
    //   사다리꼴: 뒷변 반폭 4.40 → 3.20. 앞변 대비 비율 0.611 → 0.444로 원근이 강해진다.
    //   ⚠️ 이 때문에 하늘:보드가 결정 6-3(54:34)에서 62:26으로 바뀐다. 사용자 지시 우선.
    //
    // ⚠️ 전부 재튜닝 대상. 뒷변 반폭을 바꾸면 Cloth의 사다리꼴 메시도 자동으로 따라온다
    //    (TrapezoidQuad.NarrowFromBoardSpace) — 손으로 맞추지 말 것.

    // v17-c (A안): 배경에 그려진 면들의 원근을 실측한 결과 0.57~0.80 구간이었다
    //   (교실책상 0.79 / 30살 패드 0.80 / 25살 패드 ~0.75 / 20살 테이블 0.57).
    //   v17-b의 0.444는 어느 배경보다도 강해 변이 눈에 띄게 벌어졌다 → 0.583으로 타협.
    //   구간의 아래쪽(가장 사다리꼴다운 20살 테이블)에 맞춰 "더 사다리꼴" 요청도 최대한 살린다.
    // v17-d: 좌우 확대. 배경에 그려진 면들의 앞변 반폭 실측 구간(6.5~8.2)의 위쪽에 맞춰
    //   7.20 → 8.00 (+11%). 사다리꼴 비율(0.583)은 유지 — 모양은 그대로 두고 폭만 키운다.
    //   화면 가로 반폭이 12.44(ortho 7 × 16:9)라 여유는 아직 많다.
    // v19(0825 피드백): "보드 맨 앞과 뒤에서 돌 크기 차이가 너무 남 — 훨씬 덜 나도록".
    //   돌·손 크기 차이는 사다리꼴 비율(뒷변폭/앞변폭)과 **정확히 같은 값**이다
    //   (BoardGeometryTests가 "물체 축소율 = 보드 폭 축소율"을 잠가 놓았다 — 따로 풀면
    //   '보이는 손 안인데 안 잡히는' v17 이전 버그 클래스가 되살아난다).
    //   → 비율 자체를 0.583 → 0.78로 올린다: 크기 차이 1.71배 → 1.28배.
    //   0.78은 배경 실측 구간(0.57~0.80)의 위쪽 끝(교실책상 0.79 / 30살 패드 0.80)이라
    //   배경과의 원근 정합도 유지된다. 보드가 덜 사다리꼴로 보이는 것은 이 결정의 대가.
    public const float BackScreenY   = -3.30f;
    public const float FrontScreenY  = -6.90f;
    public const float BackHalfWidth =  6.24f;  // 앞변 대비 0.78
    public const float FrontHalfWidth = 8.00f;
    public const float CenterScreenX =  0f;

    private static readonly BoardGeometry geometry = new BoardGeometry(
        LogicalWidth, LogicalDepth,
        BackHalfWidth, FrontHalfWidth,
        BackScreenY, FrontScreenY,
        CenterScreenX, HeightScale);

    /// <summary>전 스테이지 공통 기하.</summary>
    public static BoardGeometry Current => geometry;

    /// <summary>기존 <see cref="BoardBounds"/>에 넘길 사다리꼴 4꼭짓점 [BL, BR, FL, FR].
    /// 낙 판정·Flee·뿌리기 등 기존 소비자들이 전부 이 하나의 보드를 쓰게 된다.</summary>
    public static Vector2[] UnifiedQuad => new[]
    {
        new Vector2(CenterScreenX - BackHalfWidth,  BackScreenY),   // BL
        new Vector2(CenterScreenX + BackHalfWidth,  BackScreenY),   // BR
        new Vector2(CenterScreenX - FrontHalfWidth, FrontScreenY),  // FL
        new Vector2(CenterScreenX + FrontHalfWidth, FrontScreenY),  // FR
    };

    /// <summary>보드가 고정이라 무효화할 캐시가 없다. 기존 호출부 호환용 no-op.</summary>
    public static void Invalidate() { }

    // ── 편의 변환 ────────────────────────────────────────────────────────────

    /// <summary>화면 좌표(월드 XY) → 보드 좌표. 지면 기준.</summary>
    public static Vector2 ToBoard(Vector2 screenXY) => geometry.Unproject(screenXY);

    /// <summary>보드 좌표 + 높이 → 화면 좌표(월드 XY).</summary>
    public static Vector2 ToScreen(Vector2 boardPos, float height) => geometry.Project(boardPos, height);

    /// <summary>보드 좌표를 직사각형 안으로 클램프.
    /// 손은 하늘(보드 뒷변 위)에 있을 수 있어 역투영이 v&lt;0을 낼 수 있다.</summary>
    public static Vector2 ClampToBoard(Vector2 boardPos)
    {
        float hw = LogicalWidth * 0.5f, hd = LogicalDepth * 0.5f;
        return new Vector2(Mathf.Clamp(boardPos.x, -hw, hw), Mathf.Clamp(boardPos.y, -hd, hd));
    }
}
