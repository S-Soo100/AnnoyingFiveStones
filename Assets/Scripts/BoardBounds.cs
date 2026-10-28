using UnityEngine;

/// <summary>
/// 매트(보드) 영역의 단일 진실 공급원(SOT).
/// 기존 5+곳 하드코딩(Cloth, ScatterSystem.boardSize, FleeGimmick, MonochromeGimmick, ObstacleGimmick, GameManager.SafeZone)을 통합 대체.
/// 단, v7-2 범위에서는 신규 호출처(GameManager 전역 낙 판정 + FleeMovement)에서만 사용. 기존 5곳은 회귀 위험으로 그대로 둠.
/// v9: quad override 추가 — Stage 2 사다리꼴 플레이 영역 지원.
/// </summary>
public static class BoardBounds
{
    private static Rect cachedRect;
    private static bool cached;
    private static Rect? overrideRect;
    private static Vector2[] overrideQuad; // v9: 사다리꼴 4꼭짓점 [BL, BR, FL, FR]

    /// <summary>quad override가 유효한지 여부.</summary>
    public static bool HasQuad => overrideQuad != null && overrideQuad.Length == 4;

    /// <summary>"하늘" 영역의 하한 Y. 이 위는 outside 판정 면제 + 손 받기 모드 전환.
    /// v11-fix3: 사다리꼴 뒷변 y(-3.95)로 통일. SetQuadOverride 시 자동 동기화됨.
    /// 이전 v11-fix2(-2.45)는 Cloth bounds.max.y(보드 메시 상단)와 맞췄으나
    /// 시각적 사다리꼴 윗변(-3.95)보다 1.5 unit 위 → "하늘" 진입이 보드 통과처럼 보이는 데드존 발생.</summary>
    public static float SkyFloorY { get; private set; } = -3.95f;

    /// <summary>SkyFloorY를 stage별로 override (현재 모든 stage 공통 -3.95 기본값 사용).</summary>
    public static void SetSkyFloor(float y) { SkyFloorY = y; }

    /// <summary>SkyFloorY를 기본값(-3.95f)으로 복원.</summary>
    public static void ClearSkyFloor() { SkyFloorY = -3.95f; }

    /// <summary>매트 영역(XY 평면). quad 있으면 그 AABB, overrideRect 있으면 그 값, 없으면 Cloth.Renderer.bounds 기반.</summary>
    public static Rect MatRect
    {
        get
        {
            if (HasQuad)
            {
                // quad 4점의 AABB 반환
                float minX = overrideQuad[0].x, maxX = overrideQuad[0].x;
                float minY = overrideQuad[0].y, maxY = overrideQuad[0].y;
                for (int i = 1; i < 4; i++)
                {
                    if (overrideQuad[i].x < minX) minX = overrideQuad[i].x;
                    if (overrideQuad[i].x > maxX) maxX = overrideQuad[i].x;
                    if (overrideQuad[i].y < minY) minY = overrideQuad[i].y;
                    if (overrideQuad[i].y > maxY) maxY = overrideQuad[i].y;
                }
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            if (overrideRect.HasValue) return overrideRect.Value;
            if (!cached) Recompute();
            return cachedRect;
        }
    }

    /// <summary>안쪽 마진 적용된 영역. Flee 클램프, 스폰 위치 산정 등에 사용. 시그니처·동작 불변.</summary>
    public static Rect InnerRect(float marginPercent)
    {
        var r = MatRect;
        float mx = r.width * marginPercent;
        float my = r.height * marginPercent;
        return new Rect(r.x + mx, r.y + my, r.width - 2f * mx, r.height - 2f * my);
    }

    /// <summary>매트 밖 판정. quad 있으면 점-사각형(CCW: BL→BR→FR→FL) 판정, 없으면 AABB 판정.
    /// Y 상한 무시 규칙 계승: 던지기 중 위로 올라간 돌 보호.</summary>
    public static bool IsOutsideMat(Vector2 pos, float marginAbsolute = BoardSpace.GraceBandWidth)
    {
        if (HasQuad)
        {
            // Y 상한: 사다리꼴 뒷변(+균일 마진)과 SkyFloor 중 위 값. 이 위는 "하늘"이라 outside 면제.
            // v30: 구 ExpandCorner 확장은 변마다 실효 마진이 달랐다 — 뒷변은 margin 그대로 더한다.
            float backY = Mathf.Max(overrideQuad[0].y, overrideQuad[1].y) + marginAbsolute;
            float skyFloor = Mathf.Max(backY, SkyFloorY);
            if (pos.y > skyFloor) return false; // 위로 올라간 돌 보호 (하늘 영역)

            return IsOutsideQuadInterior(pos, marginAbsolute);
        }

        var r = MatRect;
        return pos.x < r.xMin - marginAbsolute || pos.x > r.xMax + marginAbsolute
            || pos.y < r.yMin - marginAbsolute; // Y 상한은 무시 (던지기 중 위로 올라간 돌 보호)
    }

    /// <summary>v25 — 유예 밴드 질의: 보드 경계 밖이지만 낙은 아닌 "가장자리" 구간.
    /// 시각 연출·통계·후속 기믹의 정보 훅 (판정 자체는 IsOutsideMat가 담당).
    /// v30: 낙 판정(IsOutsideMat)과 같은 경계를 공유한다 — 모든 위치가 보드/밴드/낙 셋 중
    /// 정확히 하나가 되도록. 하늘 면제 구간(뒷변 위 생존 돌)도 "보드 밖 생존"이므로 밴드로 분류.</summary>
    public static bool IsInGraceBand(Vector2 pos)
        => IsOutsideMatStrict(pos, 0f) && !IsOutsideMat(pos, BoardSpace.GraceBandWidth);

    /// <summary>매트 밖 엄격 판정 — SkyFloor 면제 없음. quad 있으면 사다리꼴 엄격 판정, 없으면 AABB(Y 상한 포함).
    /// 보드 면 위 이동(예: Stage 3 FleeMovement)에서 +Y로 새는 것을 막기 위해 사용.
    /// v12-fix: 던진 돌 보호용 SkyFloor 면제가 Flee 돌(보드 면 이동)에 잘못 적용되는 버그 차단.</summary>
    public static bool IsOutsideMatStrict(Vector2 pos, float marginAbsolute = 0f)
    {
        if (HasQuad)
            return IsOutsideQuadInterior(pos, marginAbsolute);

        var r = MatRect;
        return pos.x < r.xMin - marginAbsolute || pos.x > r.xMax + marginAbsolute
            || pos.y < r.yMin - marginAbsolute || pos.y > r.yMax + marginAbsolute;
    }

    /// <summary>사다리꼴 내부 판정만 수행. SkyFloor 면제 없음.
    /// v30 — 균일 마진: 구 방식(꼭짓점을 centroid 방사 방향으로 margin만큼 확장)은 변의 방향에 따라
    /// 실효 마진이 달랐다(마진 0.8에서 앞·뒷변 ~0.2-0.3, 측면 ~0.75 — 유예 밴드가 설계 폭이 안 됨).
    /// 이제 "다각형 둘레까지의 유클리드 최단 거리 ≤ margin"으로 판정해 어느 변에서나 폭이 같다.</summary>
    private static bool IsOutsideQuadInterior(Vector2 pos, float marginAbsolute)
    {
        // 점-사각형 내부 판정: 둘레 순서 CCW BL→BR→FR→FL
        float cross0 = CrossEdge(overrideQuad[0], overrideQuad[1], pos);
        float cross1 = CrossEdge(overrideQuad[1], overrideQuad[3], pos);
        float cross2 = CrossEdge(overrideQuad[3], overrideQuad[2], pos);
        float cross3 = CrossEdge(overrideQuad[2], overrideQuad[0], pos);
        bool inside = (cross0 >= 0f && cross1 >= 0f && cross2 >= 0f && cross3 >= 0f)
                   || (cross0 <= 0f && cross1 <= 0f && cross2 <= 0f && cross3 <= 0f);
        if (inside) return false;
        if (marginAbsolute <= 0f) return true;

        return DistanceToQuadBoundary(pos) > marginAbsolute;
    }

    /// <summary>점에서 사다리꼴 둘레(4변 선분)까지의 최단 거리. 외부 점의 마진 판정용.</summary>
    private static float DistanceToQuadBoundary(Vector2 pos)
    {
        float d = PointSegmentDistance(pos, overrideQuad[0], overrideQuad[1]);                  // 뒷변
        d = Mathf.Min(d, PointSegmentDistance(pos, overrideQuad[1], overrideQuad[3]));          // 우측
        d = Mathf.Min(d, PointSegmentDistance(pos, overrideQuad[3], overrideQuad[2]));          // 앞변
        return Mathf.Min(d, PointSegmentDistance(pos, overrideQuad[2], overrideQuad[0]));       // 좌측
    }

    private static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>bilinear 매핑. u,v∈[0,1]. u=0,v=0→BL / u=1,v=0→BR / u=0,v=1→FL / u=1,v=1→FR.
    /// quad 없으면 MatRect AABB를 사다리꼴 특수 케이스로 처리.</summary>
    public static Vector2 QuadPoint(float u, float v)
    {
        Vector2 bl, br, fl, fr;
        if (HasQuad)
        {
            bl = overrideQuad[0]; br = overrideQuad[1];
            fl = overrideQuad[2]; fr = overrideQuad[3];
        }
        else
        {
            var r = MatRect;
            bl = new Vector2(r.xMin, r.yMin);
            br = new Vector2(r.xMax, r.yMin);
            fl = new Vector2(r.xMin, r.yMax);
            fr = new Vector2(r.xMax, r.yMax);
        }
        // bilinear: Lerp(Lerp(BL,BR,u), Lerp(FL,FR,u), v)
        Vector2 back  = Vector2.Lerp(bl, br, u);
        Vector2 front = Vector2.Lerp(fl, fr, u);
        return Vector2.Lerp(back, front, v);
    }

    /// <summary>마진 적용 bilinear. u,v를 [margin, 1-margin]으로 리맵 후 QuadPoint 호출.</summary>
    public static Vector2 InnerQuadPoint(float u, float v, float margin)
    {
        float ru = Mathf.Lerp(margin, 1f - margin, u);
        float rv = Mathf.Lerp(margin, 1f - margin, v);
        return QuadPoint(ru, rv);
    }

    /// <summary>사다리꼴 4꼭짓점 override. [0]=BL, [1]=BR, [2]=FL, [3]=FR.
    /// SetQuadOverride 시 overrideRect는 자동 해제(상호 배타).
    /// v11-fix3: quad 등록 시 SkyFloorY를 사다리꼴 뒷변 y로 자동 동기화.</summary>
    public static void SetQuadOverride(Vector2[] quad)
    {
        overrideQuad = quad;
        overrideRect = null; // 상호 배타
        // v11-fix3: quad 등록 시 SkyFloorY를 사다리꼴 뒷변 y로 자동 동기화.
        //          이로써 "보이는 보드 바로 위 = 하늘"이 코드와 일치.
        if (quad != null && quad.Length == 4)
            SkyFloorY = Mathf.Max(quad[0].y, quad[1].y); // backY (BL.y, BR.y 중 큰 값)
        BoardSpace.Invalidate(); // v17: 보드가 바뀌면 BoardGeometry 재생성
    }

    /// <summary>플레이 영역을 명시적 Rect로 덮어쓴다(예: Stage 2 책상 wood). InnerRect/IsOutsideMat도 자동으로 이 영역 사용.
    /// SetOverride 시 overrideQuad는 자동 해제(상호 배타).</summary>
    public static void SetOverride(Rect rect)
    {
        overrideRect = rect;
        overrideQuad = null; // 상호 배타
        // v11-fix3 후보강 (Codex 리뷰): Rect override 시 SkyFloorY를 rect.yMax로 동기화
        // (이전엔 quad 시절 값 잔존 → API 일관성 결함)
        SkyFloorY = rect.yMax;
        BoardSpace.Invalidate(); // v17
    }

    /// <summary>override 해제 → Cloth.bounds 기반 복귀.</summary>
    public static void ClearOverride()
    {
        overrideRect = null;
        overrideQuad = null; // v9 추가
        SkyFloorY = -3.95f; // v11-fix3: quad 해제 시 폴백 복귀
        BoardSpace.Invalidate(); // v17
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

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────────

    /// <summary>변 e=(p1-p0), 점 d=(point-p0)의 2D cross product. 양수=CCW 내부 쪽.</summary>
    private static float CrossEdge(Vector2 p0, Vector2 p1, Vector2 point)
    {
        Vector2 e = p1 - p0;
        Vector2 d = point - p0;
        return e.x * d.y - e.y * d.x;
    }
}
