using UnityEngine;

/// <summary>
/// Quad 메시의 윗변을 좁혀서 사다리꼴로 만든다.
/// topNarrow = 0이면 직사각형, 0.1이면 윗변이 10% 좁아짐.
///
/// ⚠️ v17: 이 값은 **씬에서 손으로 정하지 않는다.** <see cref="BoardSpace"/>의
/// 뒷변/앞변 반폭 비에서 자동으로 파생된다. 씬 값과 판정 폴리곤이 따로 놀면
/// "보이는 사다리꼴 ≠ 판정 사다리꼴"이 되어, v17이 없애려는 버그가 되살아난다.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class TrapezoidQuad : MonoBehaviour
{
    [SerializeField, Range(0f, 0.9f)]
    private float topNarrow = 0.38f; // 시작값일 뿐 — Start에서 BoardSpace 기준으로 덮어쓴다

    /// <summary>윗변(뒤) 좁힘 비율. 윗변 너비 = 아랫변 너비 × (1 - TopNarrow).
    /// ScatterSystem이 보이는 사다리꼴 꼭짓점을 계산할 때 사용.</summary>
    public float TopNarrow => topNarrow;

    /// <summary>BoardSpace가 정의하는 사다리꼴 비율. 뒷변이 좁을수록 원근이 강해 보인다.</summary>
    public static float NarrowFromBoardSpace
        => Mathf.Clamp01(1f - BoardSpace.BackHalfWidth / BoardSpace.FrontHalfWidth);

    private Mesh workMesh;
    private Vector3[] baseVerts;

    private void Start() => Rebuild(NarrowFromBoardSpace);

    /// <summary>지정한 비율로 메시를 다시 만든다. 라이브 튜닝에서도 호출 가능.</summary>
    public void Rebuild(float narrow)
    {
        var meshFilter = GetComponent<MeshFilter>();

        if (baseVerts == null)
        {
            // 원본 정점을 한 번만 보관 — 반복 호출해도 계속 좁아지지 않게.
            workMesh = Instantiate(meshFilter.sharedMesh);
            meshFilter.mesh = workMesh;
            baseVerts = (Vector3[])workMesh.vertices.Clone();
        }

        topNarrow = Mathf.Clamp01(narrow);

        var verts = (Vector3[])baseVerts.Clone();
        // Quad 기본 정점: (-0.5,-0.5,0), (0.5,-0.5,0), (-0.5,0.5,0), (0.5,0.5,0)
        for (int i = 0; i < verts.Length; i++)
        {
            if (verts[i].y > 0f) // 윗변(뒤) 정점
                verts[i].x *= (1f - topNarrow);
        }
        workMesh.vertices = verts;
        workMesh.RecalculateBounds();
    }
}
