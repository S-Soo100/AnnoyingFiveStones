using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 5 방해물 기믹.
/// 자(ruler) 1개 — 보드 중심을 비스듬히 양단 (기울기 방향 랜덤)
/// 볼펜 1개 · 지우개 2개 — 보드 4분면에 하나씩 (자가 지나는 칸 하나는 자가 맡는다)
/// 굴러다니는 공 2개 — 보드 내부 왕복
///
/// 2026-10-30 기획 피드백 "왼쪽이 비어 장애물이 역할을 못 한다": 배치를 화면 AABB(MatRect) 근사에서
/// **보드 좌표(BoardSpace)** 로 옮겼다. 구 방식은 자가 늘 중심 오른쪽·우상향이라 왼쪽이 비었고,
/// 사각형 모서리 기준이라 뒤쪽 지우개가 사다리꼴 밖(매트 밖)에 떴다. 개수·크기·판정 반경은 그대로다.
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

    // 보드 좌표 배치 상수 (보드 단위: x ±8, y ±4.5, +y=앞)
    private const float RulerHalfLen = 4.2f;                    // 자 반길이
    private const float RulerAngleMin = 35f, RulerAngleMax = 55f; // 보드 기준 기울기 (깊이는 화면에서 눌려 화면각은 더 완만)
    private const float ItemXMin = 2.5f, ItemXMax = 5.5f;       // 4분면 안 볼펜·지우개 |x|
    private const float ItemYMin = 1.4f, ItemYMax = 2.6f;       // 4분면 안 볼펜·지우개 |y|
    private const float RulerClearance = 2.0f;                  // 자가 지나는 칸의 물건은 자에서 이만큼 띄운다

    private void SpawnObstacles()
    {
        // 자: 중심 근처를 지나고 기울기 부호가 랜덤 → 지나는 대각선 두 칸이 매번 바뀐다.
        float sign = Random.value < 0.5f ? 1f : -1f;
        float a = sign * Random.Range(RulerAngleMin, RulerAngleMax) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 rc = new Vector2(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.6f));
        Vector2 rulerBoardA = rc - dir * RulerHalfLen;
        Vector2 rulerBoardB = rc + dir * RulerHalfLen;
        SpawnRuler(rulerBoardA, rulerBoardB);

        // 4분면 (x부호, y부호). 자는 dir 방향 칸과 그 반대 칸을 지난다.
        var crossed = new[] { new Vector2(1f, sign), new Vector2(-1f, -sign) };
        var free    = new[] { new Vector2(1f, -sign), new Vector2(-1f, sign) };
        // 볼펜·지우개 3개 → 자가 안 지나는 두 칸 + 자가 지나는 칸 하나. 어떤 물건이 어느 칸에 갈지는 셔플.
        var quads = new[] { free[0], free[1], crossed[Random.Range(0, 2)] };
        var items = new[] { 0, 1, 2 }; // 0=볼펜, 1·2=지우개
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        int eraserIdx = 0;
        for (int k = 0; k < 3; k++)
        {
            Vector2 p = QuadrantPoint(quads[k], rulerBoardA, rulerBoardB);
            if (items[k] == 0) SpawnPen(p);
            else SpawnEraser(p, eraserIdx++);
        }

        SpawnBalls();

        Debug.Log($"[ObstacleGimmick] Spawned ruler×1 + pen×1 + eraser×2 + ball×2 (board). " +
                  $"ruler {rulerBoardA:F1}→{rulerBoardB:F1}, quads=[{quads[0]},{quads[1]},{quads[2]}] items=[{items[0]},{items[1]},{items[2]}]");
    }

    /// <summary>4분면 안 랜덤 점(보드 좌표). 자와 가까우면 자에서 수직으로 밀어낸다.</summary>
    private static Vector2 QuadrantPoint(Vector2 quadSign, Vector2 rulerA, Vector2 rulerB)
    {
        Vector2 p = new Vector2(quadSign.x * Random.Range(ItemXMin, ItemXMax),
                                quadSign.y * Random.Range(ItemYMin, ItemYMax));
        float d = DistanceToSegment(p, rulerA, rulerB, out Vector2 closest);
        if (d < RulerClearance)
        {
            Vector2 away = p - closest;
            if (away.sqrMagnitude < 0.0001f) away = new Vector2(-(rulerB - rulerA).y, (rulerB - rulerA).x);
            p = closest + away.normalized * RulerClearance;
            // 같은 칸·보드 안에 남긴다 (칸 경계 0.8, 바깥 한계는 Item 최대값)
            p.x = quadSign.x * Mathf.Clamp(p.x * quadSign.x, 0.8f, ItemXMax);
            p.y = quadSign.y * Mathf.Clamp(p.y * quadSign.y, 0.8f, ItemYMax);
        }
        return p;
    }

    /// <summary>보드 좌표 → 지면 월드 위치 (z 지정).</summary>
    private static Vector3 BoardToWorld(Vector2 board, float z)
    {
        Vector2 s = BoardSpace.ToScreen(board, 0f);
        return new Vector3(s.x, s.y, z);
    }

    // ─── 자(ruler) ×1 — Cube, 보드를 비스듬히 양단 ──────────────────────────
    private void SpawnRuler(Vector2 boardA, Vector2 boardB)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Obstacle_Ruler";

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = CreateLitMaterial(new Color(0.85f, 0.72f, 0.45f));

        // 보드 양 끝점을 화면에 투영 → 그 사이를 잇는 큐브 (로컬 x ±0.5 = 양 끝)
        Vector3 wa = BoardToWorld(boardA, -0.1f);
        Vector3 wb = BoardToWorld(boardB, -0.1f);
        Vector3 delta = wb - wa;
        float rulerAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        go.transform.localScale = new Vector3(new Vector2(delta.x, delta.y).magnitude, 0.35f, 0.2f);
        go.transform.position = (wa + wb) * 0.5f;
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

    // ─── 볼펜 ×1 — Cylinder, 4분면 한 칸에 비스듬히 ─────────────────────────
    private void SpawnPen(Vector2 board)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Obstacle_Pen";
        go.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = CreateLitMaterial(new Color(0.2f, 0.2f, 0.6f));

        // Cylinder 길이 축 = 로컬 Y(화면 세로). 90°±35° 회전 → 가로에 가깝게 눕혀 보드 뒷변 위로 삐져나가지 않게.
        float angle = 90f + Random.Range(-35f, 35f);
        go.transform.position = BoardToWorld(board, 0f);
        go.transform.rotation = Quaternion.Euler(20f, 0f, angle);

        var obs = go.AddComponent<Obstacle>();
        obs.type = Obstacle.ObstacleType.Static;
        obs.shape = ObstacleShape.Elongated;
        obs.localEndA = new Vector3(0f, -1f, 0f);
        obs.localEndB = new Vector3(0f,  1f, 0f);
        obs.hitRadius = 0.3f;

        obstacles.Add(go);
    }

    // ─── 지우개 — Cube, 4분면 한 칸에 ───────────────────────────────────────
    private void SpawnEraser(Vector2 board, int index)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Obstacle_Eraser_{index}";
        go.transform.localScale = new Vector3(1.8f, 1.0f, 0.6f);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = CreateLitMaterial(new Color(0.9f, 0.85f, 0.75f));

        go.transform.position = BoardToWorld(board, 0f);
        go.transform.rotation = Quaternion.Euler(25f, 0f, Random.Range(-15f, 15f));

        var obs = go.AddComponent<Obstacle>();
        obs.type = Obstacle.ObstacleType.Static;
        obs.shape = ObstacleShape.Point;
        obs.hitRadius = 0.8f;

        obstacles.Add(go);
    }

    // ─── 굴러다니는 공 ×2 — Sphere, 보드 내부 왕복 ──────────────────────────
    private void SpawnBalls()
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
            BuildBallPath(i, out startPos, out endPos);
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

    /// <summary>공 왕복 경로 — 보드 좌표 직선을 투영(사다리꼴에서도 직선으로 남는다).
    /// 0=가로(좌↔우), 1=세로(뒤↔앞). 범위는 구 화면 근사(±0.7×halfW/halfH)와 비슷하게 — 공 난이도는 그대로.</summary>
    private static void BuildBallPath(int index, out Vector3 startPos, out Vector3 endPos)
    {
        if (index == 0)
        {
            float rowY = Random.Range(-2.0f, 2.0f);
            startPos = BoardToWorld(new Vector2(-4.8f, rowY), 0f);
            endPos   = BoardToWorld(new Vector2( 4.8f, rowY), 0f);
        }
        else
        {
            float colX = Random.Range(-4.0f, 4.0f);
            startPos = BoardToWorld(new Vector2(colX, -2.8f), 0f);
            endPos   = BoardToWorld(new Vector2(colX,  2.8f), 0f);
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
    private static float DistanceToSegment(Vector2 point, Vector2 segA, Vector2 segB, out Vector2 closest)
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
