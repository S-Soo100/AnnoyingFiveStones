using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 6 [35살] 움직이는 방해물 기믹.
/// 고정 장애물 2~3개 + 이동 장애물 1~2개 스폰.
/// Obstacle 컴포넌트가 손과의 거리 기반 충돌을 자체 처리.
/// </summary>
public class ObstacleGimmick : StageGimmick
{
    private List<GameObject> obstacles = new List<GameObject>();

    public override void OnStageStart(int stageInLoop)
    {
        obstacles.Clear();
        SpawnObstacles();
    }

    private void SpawnObstacles()
    {
        var board = gameManager?.BoardTransform;
        float cx = board != null ? board.position.x : 0f;
        float cy = board != null ? board.position.y : 0f;
        float halfW = 3.8f;
        float halfH = 3.0f;

        // ─── 고정 장애물 2~3개: 보드를 가로지르는 긴 박스 ───
        int staticCount = Random.Range(2, 4);
        for (int i = 0; i < staticCount; i++)
        {
            bool horizontal = (i % 2 == 0); // 교대로 가로/세로
            float posX = horizontal ? cx : cx + Random.Range(-halfW * 0.5f, halfW * 0.5f);
            float posY = horizontal ? cy + Random.Range(-halfH * 0.6f, halfH * 0.6f) : cy;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Obstacle_Static_{i}";
            go.transform.position = new Vector3(posX, posY, 0f);
            go.transform.localScale = horizontal
                ? new Vector3(halfW * 1.8f, 0.2f, 0.15f)  // 가로형 긴 박스
                : new Vector3(0.2f, halfH * 1.8f, 0.15f); // 세로형 긴 박스

            // 머티리얼 색상 (갈색)
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.55f, 0.35f, 0.15f);

            // 원래 Collider 제거 (물리 충돌 불필요, 거리 기반 감지 사용)
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var obs = go.AddComponent<Obstacle>();
            obs.type = Obstacle.ObstacleType.Static;
            obs.hitRadius = horizontal ? 0.25f : 0.25f;
            obs.onlyCheckOnBoard = true;

            obstacles.Add(go);
        }

        // ─── 이동 장애물 1~2개: 작은 원형, 왕복 이동 ───
        int movingCount = Random.Range(1, 3);
        for (int i = 0; i < movingCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Obstacle_Moving_{i}";
            go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            // 머티리얼 색상 (회색)
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.5f, 0.5f, 0.5f);

            // Collider 제거 (거리 기반 감지 사용)
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // 왕복 경로 설정
            bool horizontal = (i % 2 == 0);
            Vector3 startPos, endPos;
            if (horizontal)
            {
                float rowY = cy + Random.Range(-halfH * 0.4f, halfH * 0.4f);
                startPos = new Vector3(cx - halfW * 0.8f, rowY, 0f);
                endPos   = new Vector3(cx + halfW * 0.8f, rowY, 0f);
            }
            else
            {
                float colX = cx + Random.Range(-halfW * 0.4f, halfW * 0.4f);
                startPos = new Vector3(colX, cy - halfH * 0.8f, 0f);
                endPos   = new Vector3(colX, cy + halfH * 0.8f, 0f);
            }

            go.transform.position = startPos;

            var obs = go.AddComponent<Obstacle>();
            obs.type = Obstacle.ObstacleType.Moving;
            obs.startPos = startPos;
            obs.endPos = endPos;
            obs.moveSpeed = Random.Range(0.5f, 1.0f);
            obs.hitRadius = 0.35f;
            obs.onlyCheckOnBoard = true;

            obstacles.Add(go);
        }

        Debug.Log($"[ObstacleGimmick] Spawned {staticCount} static + {movingCount} moving obstacles.");
    }

    public override void OnStageEnd()
    {
        foreach (var go in obstacles)
        {
            if (go != null)
                Object.Destroy(go);
        }
        obstacles.Clear();
        Debug.Log("[ObstacleGimmick] Stage ended: obstacles destroyed.");
    }
}
