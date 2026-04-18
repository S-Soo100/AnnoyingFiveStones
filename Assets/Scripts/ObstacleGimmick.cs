using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 6 [35살] 방해물 기믹.
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
        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 4.8f;
        float halfH = 3.05f;

        SpawnRuler(cx, cy, halfW, halfH);
        SpawnPen(cx, cy, halfW, halfH);
        SpawnEraser(cx, cy, halfW, halfH);
        SpawnBalls(cx, cy, halfW, halfH);

        Debug.Log("[ObstacleGimmick] Spawned ruler×1 + pen×1 + eraser×2 + ball×2.");
    }

    // ─── 자(ruler) ×1 — Cube, 보드를 비스듬히 양단 ──────────────────────────
    private void SpawnRuler(float cx, float cy, float halfW, float halfH)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Obstacle_Ruler";
        go.transform.localScale = new Vector3(8f, 0.35f, 0.2f);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.85f, 0.72f, 0.45f);

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
            renderer.material.color = new Color(0.2f, 0.2f, 0.6f);

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
                renderer.material.color = new Color(0.9f, 0.85f, 0.75f);

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
                renderer.material.color = ballColors[i];

            Vector3 startPos, endPos;
            BuildBallPath(cx, cy, halfW, halfH, i, out startPos, out endPos);
            go.transform.position = startPos;

            var obs = go.AddComponent<Obstacle>();
            obs.type = Obstacle.ObstacleType.Moving;
            obs.shape = ObstacleShape.Point;
            obs.startPos = startPos;
            obs.endPos = endPos;
            obs.moveSpeed = Random.Range(0.6f, 1.0f);
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
                stone.transform.position = new Vector3(newPos.x, newPos.y, 0f);
                stone.Rb.linearVelocity = Vector3.zero;
                Debug.Log($"[ObstacleGimmick] Stone {stone.StoneIndex} nudged away from ruler (dist was {dist:F2})");
            }
        }
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

    public override void OnPickPhaseStart()
    {
        if (movingBalls.Count == 0) return;

        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 4.8f;
        float halfH = 3.05f;

        for (int i = 0; i < movingBalls.Count; i++)
        {
            if (movingBalls[i] == null) continue;
            Vector3 newStart, newEnd;
            BuildBallPath(cx, cy, halfW, halfH, i, out newStart, out newEnd);
            movingBalls[i].startPos = newStart;
            movingBalls[i].endPos = newEnd;
        }
        Debug.Log("[ObstacleGimmick] Ball paths refreshed for pick phase.");
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
}
