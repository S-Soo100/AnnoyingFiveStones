using UnityEngine;

/// <summary>
/// Quad 메시의 윗변을 좁혀서 사다리꼴로 만든다.
/// topNarrow = 0이면 직사각형, 0.1이면 윗변이 10% 좁아짐.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class TrapezoidQuad : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f)]
    private float topNarrow = 0.38f; // 윗변 좁힘 비율 (38% = v8-2d, 이미지 모드 매트 perspective와 톤 맞춤)

    /// <summary>윗변(뒤) 좁힘 비율. 윗변 너비 = 아랫변 너비 × (1 - TopNarrow).
    /// ScatterSystem이 보이는 사다리꼴 꼭짓점을 계산할 때 사용.</summary>
    public float TopNarrow => topNarrow;

    private void Start()
    {
        var meshFilter = GetComponent<MeshFilter>();
        var mesh = Instantiate(meshFilter.sharedMesh);
        meshFilter.mesh = mesh;

        var verts = mesh.vertices;
        // Quad 기본 정점: (-0.5,-0.5,0), (0.5,-0.5,0), (-0.5,0.5,0), (0.5,0.5,0)
        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].y > 0f) // 윗변 정점
            {
                verts[i].x *= (1f - topNarrow);
            }
        }
        mesh.vertices = verts;
        mesh.RecalculateBounds();
    }
}
