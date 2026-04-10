using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 6 [35살] 가장자리 침범 기믹.
/// 볼펜 2개(Elongated, 가장자리 대각선 침범)
/// 지우개 1개(Point, 모서리 침범)
/// 이동형 버그 1개(Point, 중앙 왕복)
/// </summary>
public class ObstacleGimmick : StageGimmick
{
    private List<GameObject> obstacles = new List<GameObject>();
    private Obstacle movingBug; // OnPickPhaseStart에서 경로 재설정용

    public override void OnStageStart(int stageInLoop)
    {
        obstacles.Clear();
        movingBug = null;
        SpawnObstacles();
    }

    private void SpawnObstacles()
    {
        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 4.0f;
        float halfH = 3.2f;

        SpawnPens(cx, cy, halfW, halfH);
        SpawnEraser(cx, cy, halfW, halfH);
        SpawnBug(cx, cy, halfW, halfH);

        Debug.Log("[ObstacleGimmick] Spawned pen×2 + eraser×2 + bug×1.");
    }

    // ─── 볼펜 ×2 — Cylinder, 가장자리에서 대각선 침범 ──────────────────────
    private void SpawnPens(float cx, float cy, float halfW, float halfH)
    {
        // 4변 중 중복 없이 2변 선택
        int[] allSides = { 0, 1, 2, 3 }; // 0=상, 1=하, 2=좌, 3=우
        // Fisher-Yates로 앞 2개 뽑기
        for (int i = 0; i < 2; i++)
        {
            int j = Random.Range(i, 4);
            int tmp = allSides[i]; allSides[i] = allSides[j]; allSides[j] = tmp;
        }

        for (int i = 0; i < 2; i++)
        {
            int side = allSides[i];
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = $"Obstacle_Pen_{i}";

            // Cylinder: 지름 0.3 (XZ), 길이 3 (Y 높이축)
            // X축 20도 기울여 원통 단면이 보이도록 입체감 부여
            go.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);

            // Collider 제거
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // 색상: 진한 파란
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.2f, 0.2f, 0.6f);

            // 변별 위치와 각도
            float posX, posY, angle;
            switch (side)
            {
                case 0: // 상: 위 변 → 아래 안쪽
                    posX = cx + Random.Range(-halfW * 0.5f, halfW * 0.5f);
                    posY = cy + halfH;
                    angle = Random.Range(210f, 240f);
                    break;
                case 1: // 하: 아래 변 → 위 안쪽
                    posX = cx + Random.Range(-halfW * 0.5f, halfW * 0.5f);
                    posY = cy - halfH;
                    angle = Random.Range(30f, 60f);
                    break;
                case 2: // 좌: 왼쪽 변 → 오른쪽 안쪽
                    posX = cx - halfW;
                    posY = cy + Random.Range(-halfH * 0.5f, halfH * 0.5f);
                    angle = Random.Range(300f, 330f);
                    break;
                default: // 3, 우: 오른쪽 변 → 왼쪽 안쪽
                    posX = cx + halfW;
                    posY = cy + Random.Range(-halfH * 0.5f, halfH * 0.5f);
                    angle = Random.Range(120f, 150f);
                    break;
            }

            go.transform.position = new Vector3(posX, posY, 0f);
            // X축 20도 기울여 원통 단면이 보이도록 입체감 부여
            go.transform.rotation = Quaternion.Euler(20f, 0f, angle);

            var obs = go.AddComponent<Obstacle>();
            obs.type = Obstacle.ObstacleType.Static;
            obs.shape = ObstacleShape.Elongated;
            // Cylinder 로컬 Y축: scale Y=1.5 → 로컬 ±1이 월드에서 ±1.5
            obs.localEndA = new Vector3(0f, -1f, 0f);
            obs.localEndB = new Vector3(0f,  1f, 0f);
            obs.hitRadius = 0.3f;

            obstacles.Add(go);
        }
    }

    // ─── 지우개 ×2 — Cube, 모서리에서 안쪽 침범 ──────────────────────────
    private void SpawnEraser(float cx, float cy, float halfW, float halfH)
    {
        // 4모서리 중 중복 없이 2곳 선택
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
            // 두께(Z) 0.6으로 입체감 부여
            go.transform.localScale = new Vector3(1.8f, 1.0f, 0.6f);

            // Collider 제거
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // 색상: 크림색
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.9f, 0.85f, 0.75f);

            float posX, posY;
            switch (corners[i])
            {
                case 0: posX = cx - halfW + 0.5f; posY = cy + halfH - 0.3f; break; // 좌상
                case 1: posX = cx + halfW - 0.5f; posY = cy + halfH - 0.3f; break; // 우상
                case 2: posX = cx - halfW + 0.5f; posY = cy - halfH + 0.3f; break; // 좌하
                default: posX = cx + halfW - 0.5f; posY = cy - halfH + 0.3f; break; // 우하
            }

            go.transform.position = new Vector3(posX, posY, 0f);
            // X축 25도 기울여 윗면이 보이도록 입체감 부여
            go.transform.rotation = Quaternion.Euler(25f, 0f, Random.Range(-15f, 15f));

            var obs = go.AddComponent<Obstacle>();
            obs.type = Obstacle.ObstacleType.Static;
            obs.shape = ObstacleShape.Point;
            obs.hitRadius = 0.8f;

            obstacles.Add(go);
        }
    }

    // ─── 이동형 버그 ×1 — Sphere, 중앙 왕복 ──────────────────────────────
    private void SpawnBug(float cx, float cy, float halfW, float halfH)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Obstacle_Bug";
        go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        // Collider 제거
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // 색상: 어두운 갈색
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.4f, 0.35f, 0.3f);

        Vector3 startPos, endPos;
        BuildBugPath(cx, cy, halfW, halfH, out startPos, out endPos);

        go.transform.position = startPos;

        var obs = go.AddComponent<Obstacle>();
        obs.type = Obstacle.ObstacleType.Moving;
        obs.shape = ObstacleShape.Point;
        obs.startPos = startPos;
        obs.endPos = endPos;
        obs.moveSpeed = Random.Range(0.5f, 0.8f);
        obs.hitRadius = 0.3f;

        movingBug = obs;
        obstacles.Add(go);
    }

    /// <summary>버그 경로 생성: 가로 또는 세로 랜덤, 보드 중앙 영역</summary>
    private void BuildBugPath(float cx, float cy, float halfW, float halfH,
                              out Vector3 startPos, out Vector3 endPos)
    {
        bool horizontal = Random.value > 0.5f;
        if (horizontal)
        {
            float rowY = cy + Random.Range(-halfH * 0.6f, halfH * 0.6f);
            startPos = new Vector3(cx - halfW * 0.6f, rowY, 0f);
            endPos   = new Vector3(cx + halfW * 0.6f, rowY, 0f);
        }
        else
        {
            float colX = cx + Random.Range(-halfW * 0.6f, halfW * 0.6f);
            startPos = new Vector3(colX, cy - halfH * 0.6f, 0f);
            endPos   = new Vector3(colX, cy + halfH * 0.6f, 0f);
        }
    }

    /// <summary>줍기 단계 시작 시 버그 경로를 새로 랜덤 설정</summary>
    public override void OnPickPhaseStart()
    {
        if (movingBug == null) return;

        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 4.0f;
        float halfH = 3.2f;

        Vector3 newStart, newEnd;
        BuildBugPath(cx, cy, halfW, halfH, out newStart, out newEnd);

        movingBug.startPos = newStart;
        movingBug.endPos = newEnd;
        // moveProgress 리셋: Obstacle.Update에서 자연스럽게 반영됨
        Debug.Log("[ObstacleGimmick] Bug path refreshed for pick phase.");
    }

    public override void OnStageEnd()
    {
        foreach (var go in obstacles)
        {
            if (go != null)
                Object.Destroy(go);
        }
        obstacles.Clear();
        movingBug = null;
        Debug.Log("[ObstacleGimmick] Stage ended: obstacles destroyed.");
    }
}
