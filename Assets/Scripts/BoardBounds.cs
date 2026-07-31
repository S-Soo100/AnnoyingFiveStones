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
    public static bool IsOutsideMat(Vector2 pos, float marginAbsolute = 0.2f)
    {
        if (HasQuad)
        {
            // Y 상한: 사다리꼴 뒷변과 SkyFloor 중 위 값. 이 위는 "하늘"이라 outside 면제.
            Vector2 centroidForSky = (overrideQuad[0] + overrideQuad[1] + overrideQuad[2] + overrideQuad[3]) * 0.25f;
            Vector2 e0s = ExpandCorner(overrideQuad[0], centroidForSky, marginAbsolute);
            Vector2 e1s = ExpandCorner(overrideQuad[1], centroidForSky, marginAbsolute);
            float backY = Mathf.Max(e0s.y, e1s.y);
            float skyFloor = Mathf.Max(backY, SkyFloorY);
            if (pos.y > skyFloor) return false; // 위로 올라간 돌 보호 (하늘 영역)

            return IsOutsideQuadInterior(pos, marginAbsolute);
        }

        var r = MatRect;
        return pos.x < r.xMin - marginAbsolute || pos.x > r.xMax + marginAbsolute
            || pos.y < r.yMin - marginAbsolute; // Y 상한은 무시 (던지기 중 위로 올라간 돌 보호)
    }

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
    /// 보드 면 위 이동(예: Flee)용 엄격 판정에 사용.</summary>
    private static bool IsOutsideQuadInterior(Vector2 pos, float marginAbsolute)
    {
        // 확장 quad: 각 꼭짓점을 centroid 기준으로 margin만큼 바깥으로 밀어냄
        Vector2 centroid = (overrideQuad[0] + overrideQuad[1] + overrideQuad[2] + overrideQuad[3]) * 0.25f;
        Vector2 e0 = ExpandCorner(overrideQuad[0], centroid, marginAbsolute); // BL
        Vector2 e1 = ExpandCorner(overrideQuad[1], centroid, marginAbsolute); // BR
        Vector2 e2 = ExpandCorner(overrideQuad[2], centroid, marginAbsolute); // FL
        Vector2 e3 = ExpandCorner(overrideQuad[3], centroid, marginAbsolute); // FR

        // 점-사각형 내부 판정: 둘레 순서 CCW BL→BR→FR→FL = e0→e1→e3→e2
        float cross0 = CrossEdge(e0, e1, pos);
        float cross1 = CrossEdge(e1, e3, pos);
        float cross2 = CrossEdge(e3, e2, pos);
        float cross3 = CrossEdge(e2, e0, pos);

        // 모두 같은 부호면 내부
        bool inside = (cross0 >= 0f && cross1 >= 0f && cross2 >= 0f && cross3 >= 0f)
                   || (cross0 <= 0f && cross1 <= 0f && cross2 <= 0f && cross3 <= 0f);
        return !inside;
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

    /// <summary>꼭짓점을 centroid 기준으로 margin만큼 바깥으로 밀어낸 확장 좌표. (할당 없는 헬퍼)</summary>
    private static Vector2 ExpandCorner(Vector2 corner, Vector2 centroid, float margin)
    {
        Vector2 dir = corner - centroid;
        float len = dir.magnitude;
        return centroid + (len > 0f ? dir / len : Vector2.zero) * (len + margin);
    }
}
