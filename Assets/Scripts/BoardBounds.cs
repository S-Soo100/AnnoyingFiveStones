using UnityEngine;

/// <summary>
/// 매트(보드) 영역의 단일 진실 공급원(SOT).
/// 기존 5+곳 하드코딩(Cloth, ScatterSystem.boardSize, FleeGimmick, MonochromeGimmick, ObstacleGimmick, GameManager.SafeZone)을 통합 대체.
/// 단, v7-2 범위에서는 신규 호출처(GameManager 전역 낙 판정 + FleeMovement)에서만 사용. 기존 5곳은 회귀 위험으로 그대로 둠.
/// </summary>
public static class BoardBounds
{
    private static Rect cachedRect;
    private static bool cached;

    /// <summary>매트 영역(XY 평면). Cloth GameObject의 Renderer.bounds 기반.</summary>
    public static Rect MatRect { get { if (!cached) Recompute(); return cachedRect; } }

    /// <summary>안쪽 마진 적용된 영역. Flee 클램프, 스폰 위치 산정 등에 사용.</summary>
    public static Rect InnerRect(float marginPercent)
    {
        var r = MatRect;
        float mx = r.width * marginPercent;
        float my = r.height * marginPercent;
        return new Rect(r.x + mx, r.y + my, r.width - 2f * mx, r.height - 2f * my);
    }

    /// <summary>매트 밖 판정. 사용자 결정: 마진 0.2 (절충).</summary>
    public static bool IsOutsideMat(Vector2 pos, float marginAbsolute = 0.2f)
    {
        var r = MatRect;
        return pos.x < r.xMin - marginAbsolute || pos.x > r.xMax + marginAbsolute
            || pos.y < r.yMin - marginAbsolute; // Y 상한은 무시 (던지기 중 위로 올라간 돌 보호)
    }

    /// <summary>매트가 변경되면(예: Cloth 위치 변경) 호출. 일반적으로 자동 캐싱 사용.</summary>
    public static void Recompute()
    {
        var cloth = GameObject.Find("Cloth");
        if (cloth == null) { Debug.LogError("[BoardBounds] Cloth GameObject not found!"); cached = false; return; }
        var rd = cloth.GetComponent<Renderer>();
        if (rd == null) { Debug.LogError("[BoardBounds] Cloth has no Renderer!"); cached = false; return; }
        var b = rd.bounds;
        cachedRect = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
        cached = true;
    }
}
