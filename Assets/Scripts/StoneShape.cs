using UnityEngine;

/// <summary>
/// Sphere 메시를 런타임에 변형하여 공기돌 형태로 만든다.
/// 20면체 주사위처럼 통통하고 각이 살짝 있는 형태.
/// 바닥이 평평해서 잘 굴러가지 않음.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class StoneShape : MonoBehaviour
{
    [Header("Shape — 통통한 다면체")]
    [SerializeField] private float flattenY = 0.85f;       // Y축 (1=구, 0.85=약간만 눌림)
    [SerializeField] private float stretchX = 1.05f;        // X축 약간 늘림
    [SerializeField] private float stretchZ = 0.95f;        // Z축 약간 줄임

    [Header("Surface — 다면체 느낌")]
    [SerializeField] private float facetStrength = 0.04f;   // 각진 느낌 강도
    [SerializeField] private float facetScale = 5f;          // 면의 크기
    [SerializeField] private float noiseStrength = 0.02f;    // 미세한 울퉁불퉁

    [Header("Randomness")]
    [SerializeField] private float shapeVariation = 0.08f;

    private void Start()
    {
        DeformMesh();
    }

    private void DeformMesh()
    {
        var meshFilter = GetComponent<MeshFilter>();
        var mesh = Instantiate(meshFilter.sharedMesh);
        meshFilter.mesh = mesh;

        var vertices = mesh.vertices;
        float seed = GetInstanceID() * 0.1f;

        float fy = flattenY + Random.Range(-shapeVariation, shapeVariation);
        float sx = stretchX + Random.Range(-shapeVariation, shapeVariation);
        float sz = stretchZ + Random.Range(-shapeVariation, shapeVariation);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            Vector3 dir = v.normalized;

            // 1) 통통한 기본 형태 (약간만 눌림)
            v.x *= sx;
            v.y *= fy;
            v.z *= sz;

            // 2) 다면체 느낌: 큰 스케일 노이즈로 면 생성
            float facetNoise = Mathf.PerlinNoise(
                (dir.x + seed) * facetScale,
                (dir.y + dir.z + seed) * facetScale
            ) - 0.5f;
            v += dir * facetNoise * facetStrength;

            // 3) 미세한 표면 디테일
            float fineNoise = Mathf.PerlinNoise(
                (v.x + seed + 50f) * 8f,
                (v.y + v.z + seed + 50f) * 8f
            ) - 0.5f;
            v += dir * fineNoise * noiseStrength;

            // 4) 바닥면 평평하게 (굴러가지 않도록)
            if (v.y < -0.15f)
            {
                v.y = Mathf.Lerp(v.y, -0.35f * fy, 0.6f);
            }

            vertices[i] = v;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var sphereCol = GetComponent<SphereCollider>();
        if (sphereCol != null)
        {
            sphereCol.radius = 0.48f;
            sphereCol.center = new Vector3(0f, -0.01f, 0f);
        }
    }
}
