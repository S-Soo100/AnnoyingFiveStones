using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손 잔상 효과 풀. 이동 경로를 따라 반투명 Palm 복제본 생성 + 페이드 아웃.
/// Palm 실제 scale: (1.0, 0.8, 0.14). 잔상은 약 60% 크기인 (0.6, 0.48, 0.05).
/// </summary>
public class HandGhostPool : MonoBehaviour
{
    private const int MaxPool = 20;
    private const float SpawnDistanceThreshold = 0.15f; // 이 거리 이상 이동 시 스폰
    private const float FadeDuration = 0.4f;
    private const float StartAlpha = 0.45f;
    private const float GhostZ = -0.3f; // 손(-0.5f)보다 앞(카메라 쪽)

    // Palm 실제 scale (1.0, 0.8, 0.14) 대비 약 60%
    private static readonly Vector3 GhostScale = new Vector3(0.6f, 0.48f, 0.05f);

    // 손바닥 색: palmColor (1f, 0.85f, 0.6f) 참고
    private static readonly Color GhostBaseColor = new Color(0.95f, 0.85f, 0.5f, StartAlpha);

    private readonly List<GhostInstance> pool = new List<GhostInstance>();
    private Vector3 lastSpawnPos;
    private bool isActive = false;

    private class GhostInstance
    {
        public GameObject go;
        public MeshRenderer meshRenderer;
        public Material material;
        public float spawnTime;
        public bool alive;
    }

    public void Activate()
    {
        isActive = true;
        lastSpawnPos = Vector3.one * 9999f; // 첫 프레임에 바로 스폰되도록
    }

    public void OnHandMoved(Vector3 handPos)
    {
        if (!isActive) return;
        if (Vector3.Distance(handPos, lastSpawnPos) >= SpawnDistanceThreshold)
        {
            SpawnGhost(handPos);
            lastSpawnPos = handPos;
        }
    }

    private void SpawnGhost(Vector3 pos)
    {
        GhostInstance ghost = GetOrCreateGhost();
        ghost.go.transform.position = new Vector3(pos.x, pos.y, GhostZ);
        ghost.go.transform.localScale = GhostScale;
        ghost.spawnTime = Time.time;
        ghost.alive = true;
        ghost.go.SetActive(true);
        SetAlpha(ghost, StartAlpha);
    }

    private GhostInstance GetOrCreateGhost()
    {
        // 비활성 고스트 찾기
        foreach (var g in pool)
        {
            if (!g.alive)
                return g;
        }

        // 풀 가득 → 가장 오래된 것 재사용
        if (pool.Count >= MaxPool)
        {
            GhostInstance oldest = pool[0];
            float oldestTime = float.MaxValue;
            foreach (var g in pool)
            {
                if (g.spawnTime < oldestTime)
                {
                    oldestTime = g.spawnTime;
                    oldest = g;
                }
            }
            return oldest;
        }

        // 새로 생성
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "HandGhost";
        Destroy(go.GetComponent<Collider>()); // 물리 충돌 제거

        // URP 투명 머테리얼 — HandModelBuilder.SetRendererAlpha와 동일한 패턴
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = GhostBaseColor;
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;

        var meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.material = mat;

        var ghost = new GhostInstance
        {
            go = go,
            meshRenderer = meshRenderer,
            material = mat,
            spawnTime = Time.time,
            alive = false
        };
        pool.Add(ghost);
        return ghost;
    }

    private void SetAlpha(GhostInstance ghost, float alpha)
    {
        var color = ghost.material.color;
        color.a = alpha;
        ghost.material.color = color;
    }

    private void Update()
    {
        if (!isActive) return;

        foreach (var ghost in pool)
        {
            if (!ghost.alive) continue;

            float elapsed = Time.time - ghost.spawnTime;
            if (elapsed >= FadeDuration)
            {
                ghost.alive = false;
                ghost.go.SetActive(false);
                continue;
            }

            float alpha = Mathf.Lerp(StartAlpha, 0f, elapsed / FadeDuration);
            SetAlpha(ghost, alpha);
        }
    }

    public void Cleanup()
    {
        isActive = false;
        foreach (var ghost in pool)
        {
            ghost.alive = false;
            if (ghost.go != null)
                ghost.go.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        foreach (var ghost in pool)
        {
            if (ghost.go != null)
                Destroy(ghost.go);
        }
        pool.Clear();
    }
}
