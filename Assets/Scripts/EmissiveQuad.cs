using UnityEngine;

/// <summary>
/// Lit 셰이더 Quad에 자체발광(Emission)을 활성화하여 조명 없이도 색상이 보이게 한다.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class EmissiveQuad : MonoBehaviour
{
    [SerializeField] private Color emissionColor = Color.white;

    private void Start()
    {
        var renderer = GetComponent<MeshRenderer>();
        var mat = renderer.material;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }
}
