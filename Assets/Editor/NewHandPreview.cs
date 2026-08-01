using UnityEngine;
using UnityEditor;

/// <summary>
/// 새 손 모델(FBX)을 **게임에 연결하지 않고** 화면에만 띄워 크기·방향을 가늠하는 도구.
///
/// 교체는 손가락 접기·손등 뒤집기·판정 반경까지 건드리는 작업이라 리스크가 있다.
/// 먼저 "이 게임 화면에서 어떻게 보이는지"만 확인하고 판단하기 위한 단계.
///
/// 씬에 저장되지 않도록 프리뷰 오브젝트에 DontSave 플래그를 준다.
/// </summary>
public static class NewHandPreview
{
    private const string FbxPath = "Assets/Models/Hand/Hand.fbx";
    private const string PreviewName = "__NewHandPreview";

    /// <summary>화면에서 손이 차지할 **세로 길이**(손끝~손목, 월드 단위).
    /// ⚠️ 모델의 긴 축(원본 X)이 회전 후 화면 세로가 되므로, bounds.x를 이 값에 맞춘다.
    /// 1.3에서 시작했으나 "좀 더 크게" 피드백으로 상향.</summary>
    private const float TargetWidth = 1.9f;

    /// <summary>보드 위 어디에 놓아볼지 (BoardSpace 앞뒤 중간).</summary>
    private static Vector3 PreviewPos => new Vector3(0f, (BoardSpace.BackScreenY + BoardSpace.FrontScreenY) * 0.5f, -0.5f);

    /// <summary>정면 카메라에 맞는 회전 — **실측으로 확정**.
    ///
    /// 원본 모델은 손바닥이 위(+Y)를 보고 손가락이 -X로 뻗은 채 누워 있다.
    /// 우리 카메라(정면, +Z 방향)에 맞추려면 손바닥 법선을 -Z로, 손가락을 +Y로 돌려야 한다.
    /// Unity의 오일러 적용 순서(Ry·Rx·Rz) 때문에 눈대중으로는 안 맞아서,
    /// 두 조건을 만족하는 각을 계산해 얻었다. (0,90,-90) / (±180,-90,90) 셋이 해.</summary>
    public static readonly Vector3 FrontFacingEuler = new Vector3(0f, 90f, -90f);

    [MenuItem("Tools/New Hand/Preview (정면)")]
    public static void Preview() => Spawn(FrontFacingEuler);

    [MenuItem("Tools/New Hand/Preview — 원본 회전 (비교용)")]
    public static void PreviewRaw() => Spawn(Vector3.zero);

    [MenuItem("Tools/New Hand/Remove")]
    public static void Remove()
    {
        var go = GameObject.Find(PreviewName);
        while (go != null) { Object.DestroyImmediate(go); go = GameObject.Find(PreviewName); }
        Debug.Log("[NewHandPreview] 제거 완료");
    }

    private static void Spawn(Vector3 euler)
    {
        Remove(); // 중복 방지

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null) { Debug.LogError($"[NewHandPreview] FBX 없음: {FbxPath}"); return; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = PreviewName;
        go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild; // 씬 오염 방지
        go.transform.rotation = Quaternion.Euler(euler);
        go.transform.position = PreviewPos;

        // 렌더러 bounds로 실제 크기를 재서 목표 폭에 맞춘다 (FBX 단위를 추측하지 않기 위해).
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { Debug.LogError("[NewHandPreview] Renderer 없음"); return; }

        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float widest = Mathf.Max(b.size.x, 0.0001f);
        float k = TargetWidth / widest;
        go.transform.localScale = go.transform.localScale * k;
        go.transform.position = PreviewPos; // 스케일 후 재배치

        Debug.Log($"[NewHandPreview] 원본 bounds={b.size} → 배율 {k:F3} 적용, 목표 폭 {TargetWidth}, 회전 {euler}");
        Selection.activeGameObject = go;
    }
}
