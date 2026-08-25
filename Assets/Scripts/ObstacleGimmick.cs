using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 5 방해물 기믹.
/// 자(ruler) 1개 — 보드를 비스듬히 양단
/// 볼펜 1개 — 가장자리에서 대각선 침범
/// 지우개 2개 — 모서리에서 안쪽 침범
/// 굴러다니는 공 2개 — 보드 내부 왕복
/// </summary>
public class ObstacleGimmick : StageGimmick
{
    private List<GameObject> obstacles = new List<GameObject>();
    private List<Obstacle> movingBalls = new List<Obstacle>();

    // 자(ruler) 월드 좌표 양 끝점 — OnScatterComplete에서 돌 겹침 보정용
    private Vector2 rulerWorldA;
    private Vector2 rulerWorldB;
    private float rulerSafeRadius = 1.2f; // 돌이 자에서 돌 1개 이상 거리 떨어져야 함

    public override void OnStageStart(int stageInLoop)
    {
        obstacles.Clear();
        movingBalls.Clear();
        SpawnObstacles();
    }

    private void SpawnObstacles()
    {
        // v10: BoardBounds 기준으로 영역 계산 (구 하드코딩 halfW=4.8 halfH=3.05 교체)
        // 중심: quad centroid. 폴백: BoardTransform
        float cx, cy;
        if (BoardBounds.HasQuad)
        {
            Vector2 c = BoardBounds.QuadPoint(0.5f, 0.5f);
            cx = c.x; cy = c.y;
        }
        else
        {
            var board = gameManager?.BoardTransform;
            cx = board != null ? board.position.x : 0f;
            cy = board != null ? board.position.y : 0f;
        }
        // halfW: MatRect.width(=AABB 앞폭)를 그대로 쓰면 너무 넓으므로 0.65 근사
        // 새 보드 앞폭 16.1 → MatRect.width*0.5*0.65 ≈ 5.23
        Rect r = BoardBounds.MatRect;
        float halfW = r.width  * 0.5f * 0.65f;
        // halfH: MatRect.height(=깊이 3.15) * 0.5 * 0.85 ≈ 1.34
        float halfH = r.height * 0.5f * 0.85f;

        SpawnRuler(cx, cy, halfW, halfH);
        SpawnPen(cx, cy, halfW, halfH);
        SpawnEraser(cx, cy, halfW, halfH);
        SpawnBalls(cx, cy, halfW, halfH);

        Debug.Log($"[ObstacleGimmick] Spawned ruler×1 + pen×1 + eraser×2 + ball×2. cx={cx:F2} cy={cy:F2} halfW={halfW:F2} halfH={halfH:F2}");
    }

    // ─── 자(ruler) ×1 — Cube, 보드를 비스듬히 양단 ──────────────────────────
    private void SpawnRuler(float cx, float cy, float halfW, float halfH)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Obstacle_Ruler";
        go.transform.localScale = new Vector3(halfW * 1.1f, 0.35f, 0.2f); // v10: 새 보드 halfW에 비례 (≈5.75)

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = CreateLitMaterial(new Color(0.85f, 0.72f, 0.45f));

        float posX = cx + halfW * 0.15f;
        float posY = cy + halfH * 0.15f;
        go.transform.position = new Vector3(posX, posY, -0.1f);
        float rulerAngle = Random.Range(25f, 40f);
        go.transform.rotation = Quaternion.Euler(15f, 0f, rulerAngle);

        var obs = go.AddComponent<Obstacle>();
        obs.type = Obstacle.ObstacleType.Static;
        obs.shape = ObstacleShape.Elongated;
        obs.localEndA = new Vector3(-0.5f, 0f, 0f);
        obs.localEndB = new Vector3( 0.5f, 0f, 0f);
        obs.hitRadius = 0.35f;

        // 월드 좌표 양 끝점 저장 (돌 겹침 보정용)
        rulerWorldA = go.transform.TransformPoint(obs.localEndA);
        rulerWorldB = go.transform.TransformPoint(obs.localEndB);

        obstacles.Add(go);
    }

    // ─── 볼펜 ×1 — Cylinder, 가장자리에서 대각선 침범 ───────────────────────
    private void SpawnPen(float cx, float cy, float halfW, float halfH)
    {
        int side = Random.Range(0, 4); // 상하좌우 중 랜덤 1변

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Obstacle_Pen";
        go.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = CreateLitMaterial(new Color(0.2f, 0.2f, 0.6f));

        float posX, posY, angle;
        switch (side)
        {
            case 0: // 상
                posX = cx + Random.Range(-halfW * 0.5f, halfW * 0.5f);
                posY = cy + halfH;
                angle = Random.Range(210f, 240f);
                break;
            case 1: // 하
                posX = cx + Random.Range(-halfW * 0.5f, halfW * 0.5f);
                posY = cy - halfH;
                angle = Random.Range(30f, 60f);
                break;
            case 2: // 좌
                posX = cx - halfW;
                posY = cy + Random.Range(-halfH * 0.5f, halfH * 0.5f);
                angle = Random.Range(300f, 330f);
                break;
            default: // 우
                posX = cx + halfW;
                posY = cy + Random.Range(-halfH * 0.5f, halfH * 0.5f);
                angle = Random.Range(120f, 150f);
                break;
        }

        go.transform.position = new Vector3(posX, posY, 0f);
        go.transform.rotation = Quaternion.Euler(20f, 0f, angle);

        var obs = go.AddComponent<Obstacle>();
        obs.type = Obstacle.ObstacleType.Static;
        obs.shape = ObstacleShape.Elongated;
        obs.localEndA = new Vector3(0f, -1f, 0f);
        obs.localEndB = new Vector3(0f,  1f, 0f);
        obs.hitRadius = 0.3f;

        obstacles.Add(go);
    }

    // ─── 지우개 ×2 — Cube, 모서리에서 안쪽 침범 ──────────────────────────────
    private void SpawnEraser(float cx, float cy, float halfW, float halfH)
    {
        int[] corners = { 0, 1, 2, 3 };
        for (int i = 0; i < 2; i++)
        {
            int j = Random.Range(i, 4);
            int tmp = corners[i]; corners[i] = corners[j]; corners[j] = tmp;
        }

        for (int i = 0; i < 2; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Obstacle_Eraser_{i}";
            go.transform.localScale = new Vector3(1.8f, 1.0f, 0.6f);

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateLitMaterial(new Color(0.9f, 0.85f, 0.75f));

            float posX, posY;
            switch (corners[i])
            {
                case 0: posX = cx - halfW + 0.5f; posY = cy + halfH - 0.3f; break;
                case 1: posX = cx + halfW - 0.5f; posY = cy + halfH - 0.3f; break;
                case 2: posX = cx - halfW + 0.5f; posY = cy - halfH + 0.3f; break;
                default: posX = cx + halfW - 0.5f; posY = cy - halfH + 0.3f; break;
            }

            go.transform.position = new Vector3(posX, posY, 0f);
            go.transform.rotation = Quaternion.Euler(25f, 0f, Random.Range(-15f, 15f));

            var obs = go.AddComponent<Obstacle>();
            obs.type = Obstacle.ObstacleType.Static;
            obs.shape = ObstacleShape.Point;
            obs.hitRadius = 0.8f;

            obstacles.Add(go);
        }
    }

    // ─── 굴러다니는 공 ×2 — Sphere, 보드 내부 왕복 ──────────────────────────
    private void SpawnBalls(float cx, float cy, float halfW, float halfH)
    {
        // v7-3: 6단 BallSpeedMultiplier 적용 (다른 스테이지는 1.0으로 효과 없음)
        var cfg = StageConfig.Get(GameManager.Instance.CurrentStage);
        float ballSpeedMul = cfg.BallSpeedMultiplier;

        Color[] ballColors = {
            new Color(0.8f, 0.2f, 0.2f),
            new Color(0.2f, 0.4f, 0.8f),
        };

        for (int i = 0; i < 2; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Obstacle_Ball_{i}";
            go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateLitMaterial(ballColors[i]);

            Vector3 startPos, endPos;
            BuildBallPath(cx, cy, halfW, halfH, i, out startPos, out endPos);
            go.transform.position = startPos;

            var obs = go.AddComponent<Obstacle>();
            obs.type = Obstacle.ObstacleType.Moving;
            obs.shape = ObstacleShape.Point;
            obs.startPos = startPos;
            obs.endPos = endPos;
            // i=0(빨강·가로) ×2.0, i=1(파랑·세로) ×1.5
            float fastBonus = (i == 1) ? 1.5f : 2.0f;
            obs.moveSpeed = Random.Range(0.6f, 1.0f) * ballSpeedMul * fastBonus; // v7-3: 6단 1.4x 적용
            obs.hitRadius = 0.35f;

            movingBalls.Add(obs);
            obstacles.Add(go);
        }
    }

    private void BuildBallPath(float cx, float cy, float halfW, float halfH,
                               int index, out Vector3 startPos, out Vector3 endPos)
    {
        if (index == 0)
        {
            float rowY = cy + Random.Range(-halfH * 0.5f, halfH * 0.5f);
            startPos = new Vector3(cx - halfW * 0.7f, rowY, 0f);
            endPos   = new Vector3(cx + halfW * 0.7f, rowY, 0f);
        }
        else
        {
            float colX = cx + Random.Range(-halfW * 0.5f, halfW * 0.5f);
            startPos = new Vector3(colX, cy - halfH * 0.7f, 0f);
            endPos   = new Vector3(colX, cy + halfH * 0.7f, 0f);
        }
    }

    // ─── 산란 완료 후: 자(ruler)와 겹치는 돌을 수직 방향으로 밀어냄 ─────────
    public override void OnScatterComplete(Stone[] activeStones)
    {
        if (activeStones == null) return;

        foreach (var stone in activeStones)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;

            Vector2 stonePos = new Vector2(stone.transform.position.x, stone.transform.position.y);
            float dist = DistanceToSegment(stonePos, rulerWorldA, rulerWorldB, out Vector2 closest);

            if (dist < rulerSafeRadius)
            {
                // 자에서 수직 방향으로 밀어냄
                Vector2 pushDir = (stonePos - closest);
                if (pushDir.sqrMagnitude < 0.001f)
                    pushDir = Vector2.up; // 정확히 위에 있으면 위로 밀기
                pushDir = pushDir.normalized;

                Vector2 newPos = closest + pushDir * rulerSafeRadius;

                // v19(0825 피드백): 다른 돌 위로 텔레포트하면 콜라이더 겹침 → 물리 사출로
                // 돌이 밀려다닌다. 겹치면 같은 방향으로 조금씩 더 밀어 빈자리를 찾는다.
                for (int step = 0; step < 3 && OverlapsOtherStone(newPos, stone, activeStones); step++)
                    newPos += pushDir * 0.5f;

                // v19: transform 직접 이동은 v17 보드좌표(BoardPos)를 낡게 만들어
                // "보이는 자리에서 안 집히는" 원인이 됐다 → SetBoardMotion으로 이동해
                // 위치·BoardPos·원근 크기를 함께 갱신한다. 보드 밖으로는 밀지 않는다(낙 방지).
                Vector2 board = BoardSpace.ToBoard(newPos);
                float hw = BoardSpace.LogicalWidth * 0.5f - 0.6f;
                float hd = BoardSpace.LogicalDepth * 0.5f - 0.6f;
                board.x = Mathf.Clamp(board.x, -hw, hw);
                board.y = Mathf.Clamp(board.y, -hd, hd);
                // 안착 돌은 SettleStone이 kinematic으로 고정한 뒤라 velocity 설정이 경고를 낸다.
                if (!stone.Rb.isKinematic) stone.Rb.linearVelocity = Vector3.zero;
                stone.SetBoardMotion(board, 0f);
                Debug.Log($"[ObstacleGimmick] Stone {stone.StoneIndex} nudged away from ruler (dist was {dist:F2})");
            }
        }
    }

    /// <summary>후보 위치가 다른 OnBoard 돌과 겹치는가 (콜라이더 사출 방지용, 화면 좌표 거리).</summary>
    private static bool OverlapsOtherStone(Vector2 pos, Stone self, Stone[] stones)
    {
        foreach (var other in stones)
        {
            if (other == null || other == self) continue;
            if (other.CurrentState != Stone.State.OnBoard) continue;
            Vector2 op = new Vector2(other.transform.position.x, other.transform.position.y);
            if (Vector2.Distance(pos, op) < 0.8f) return true;
        }
        return false;
    }

    /// <summary>2D 점과 선분 사이의 최단 거리 + 최근접점 반환</summary>
    private float DistanceToSegment(Vector2 point, Vector2 segA, Vector2 segB, out Vector2 closest)
    {
        Vector2 ab = segB - segA;
        float sqrLen = ab.sqrMagnitude;
        if (sqrLen < 0.0001f) { closest = segA; return Vector2.Distance(point, segA); }
        float t = Mathf.Clamp01(Vector2.Dot(point - segA, ab) / sqrLen);
        closest = segA + t * ab;
        return Vector2.Distance(point, closest);
    }

    public override void OnStageEnd()
    {
        foreach (var go in obstacles)
        {
            if (go != null)
                Object.Destroy(go);
        }
        obstacles.Clear();
        movingBalls.Clear();
        Debug.Log("[ObstacleGimmick] Stage ended: obstacles destroyed.");
    }

    // CreatePrimitive는 Built-in Standard 셰이더를 기본값으로 쓰는데, URP 빌드에서 strip되어 핑크로 렌더링됨.
    // 런타임 생성 머테리얼은 URP Lit 셰이더로 명시 교체 필수.
    private static Material CreateLitMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        return mat;
    }
}
