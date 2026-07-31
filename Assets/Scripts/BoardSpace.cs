using UnityEngine;
using FiveStones.Core;

/// <summary>
/// v17 — 현재 스테이지의 <see cref="BoardGeometry"/> 제공자.
///
/// 기존 <see cref="BoardBounds"/>의 사다리꼴 quad로부터 투영 파라미터를 뽑아 만든다.
/// 따라서 **화면에 보이는 것은 지금과 완전히 동일**하고, 달라지는 것은
/// "로직이 화면 좌표 대신 보드 좌표로 생각한다"는 점뿐이다.
///
/// 논리 직사각형의 실제 치수(Width/Depth)는 밸런스 수치라 v17에서 재튜닝 대상이다.
/// 현재 값은 결정 6-2("가로로 넓게, 약 16:9")를 따른 시작점일 뿐이다.
/// </summary>
public static class BoardSpace
{
    /// <summary>논리 보드 가로 (보드 단위). ⚠️ 재튜닝 대상.</summary>
    public const float LogicalWidth = 16f;

    /// <summary>논리 보드 세로/깊이 (보드 단위). ⚠️ 재튜닝 대상.</summary>
    public const float LogicalDepth = 9f;

    /// <summary>화면 1유닛 = 높이 1유닛. ⚠️ 재튜닝 대상.</summary>
    public const float HeightScale = 1f;

    private static BoardGeometry cached;
    private static bool hasCached;

    /// <summary>현재 스테이지 기하. BoardBounds quad가 바뀌면 <see cref="Invalidate"/> 후 재생성된다.</summary>
    public static BoardGeometry Current
    {
        get
        {
            if (!hasCached) Rebuild();
            return cached;
        }
    }

    /// <summary>스테이지 전환/라이브 튜닝으로 보드가 바뀌면 호출. 다음 접근 시 재생성된다.</summary>
    public static void Invalidate() => hasCached = false;

    private static void Rebuild()
    {
        // quad 4점 → 투영 파라미터. QuadPoint는 quad가 없으면 MatRect AABB를 같은 규약으로 준다.
        Vector2 bl = BoardBounds.QuadPoint(0f, 0f);
        Vector2 br = BoardBounds.QuadPoint(1f, 0f);
        Vector2 fl = BoardBounds.QuadPoint(0f, 1f);
        Vector2 fr = BoardBounds.QuadPoint(1f, 1f);

        float backHalf  = Mathf.Abs(br.x - bl.x) * 0.5f;
        float frontHalf = Mathf.Abs(fr.x - fl.x) * 0.5f;
        float centerX   = (bl.x + br.x + fl.x + fr.x) * 0.25f;
        float backY     = (bl.y + br.y) * 0.5f;
        float frontY    = (fl.y + fr.y) * 0.5f;

        // 퇴화 방어: 아직 스테이지가 시작되지 않아 quad가 0이면 캐시하지 않는다.
        // (BoardBounds.HasQuad는 씬 init 시 false였다가 StartStage 후 true가 된다 — donts/game#19)
        if (backHalf < 0.01f || frontHalf < 0.01f || Mathf.Abs(frontY - backY) < 0.01f)
        {
            cached = new BoardGeometry(LogicalWidth, LogicalDepth, 1f, 2f, 0f, -1f, 0f, HeightScale);
            return; // hasCached를 세우지 않아 다음 프레임에 다시 시도
        }

        cached = new BoardGeometry(
            LogicalWidth, LogicalDepth,
            backHalf, frontHalf,
            backY, frontY,
            centerX, HeightScale);
        hasCached = true;
    }

    // ── 편의 변환 ────────────────────────────────────────────────────────────

    /// <summary>화면 좌표(월드 XY) → 보드 좌표. 지면 기준.</summary>
    public static Vector2 ToBoard(Vector2 screenXY) => Current.Unproject(screenXY);

    /// <summary>보드 좌표 + 높이 → 화면 좌표(월드 XY).</summary>
    public static Vector2 ToScreen(Vector2 boardPos, float height) => Current.Project(boardPos, height);

    /// <summary>보드 좌표를 직사각형 안으로 클램프.
    /// 손은 하늘(보드 뒷변 위)에 있을 수 있어 역투영이 v&lt;0을 낼 수 있다.
    /// "보드 위 어디에서 던졌는가" 같은 값은 반드시 보드 안이어야 하므로 여기서 걸러준다.</summary>
    public static Vector2 ClampToBoard(Vector2 boardPos)
    {
        float hw = LogicalWidth * 0.5f, hd = LogicalDepth * 0.5f;
        return new Vector2(Mathf.Clamp(boardPos.x, -hw, hw), Mathf.Clamp(boardPos.y, -hd, hd));
    }

    /// <summary>화면 y로부터 높이를 역산. 보드 좌표를 알고 있을 때만 정확하다
    /// (같은 화면 y라도 보드 앞/뒤에 따라 지면 높이가 다르기 때문).</summary>
    public static float HeightFromScreen(Vector2 boardPos, float screenY)
        => (screenY - Current.Project(boardPos, 0f).y) / HeightScale;
}
