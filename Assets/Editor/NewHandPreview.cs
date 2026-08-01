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
    private const string FbxPath = "Assets/Resources/Models/Hand.fbx";
    private const string PreviewName = "__NewHandPreview";

    /// <summary>화면에서 손이 차지할 **세로 길이**(손끝~손목, 월드 단위).
    /// ⚠️ 모델의 긴 축(원본 X)이 회전 후 화면 세로가 되므로, bounds.x를 이 값에 맞춘다.
    /// 1.3에서 시작했으나 "좀 더 크게" 피드백으로 상향.</summary>
    private const float TargetWidth = 2.09f;

    /// <summary>보드 위 어디에 놓아볼지 (BoardSpace 앞뒤 중간).</summary>
    private static Vector3 PreviewPos => new Vector3(0f, (BoardSpace.BackScreenY + BoardSpace.FrontScreenY) * 0.5f, -0.5f);

    /// <summary>정면 카메라에 맞는 회전 — **실측으로 확정**.
    ///
    /// ⚠️ 축을 한 번 거꾸로 읽어 손가락을 잘라먹은 적이 있다. 실루엣만 보면 팔뚝과 손가락이
    /// 둘 다 "여러 갈래"로 보여 구분이 안 된다. **정점 밀도**가 결정적이다 —
    /// 손가락은 마디마다 링이 있어 정점이 몰리고(377개), 팔뚝은 성긴 튜브다(182개).
    /// → 손가락은 **+X**, 손바닥 법선은 +Y(누운 자세).
    ///
    /// 카메라는 정면(+Z)이므로 손바닥 법선 → -Z, 손가락 → +Y로 돌린다.
    /// ⚠️ 계산으로 후보를 좁힌 뒤 **실제 렌더로 확정**했다. 모델의 축 방향 가정이 몇 번 틀려서
    ///    수식만으로는 결론이 안 났다 — 최종 확인은 눈으로 해야 한다.</summary>
    public static readonly Vector3 FrontFacingEuler = new Vector3(0f, 90f, -90f);

    /// <summary>손등이 카메라를 보는 회전 (5단 손등 받기용).</summary>
    public static readonly Vector3 BackhandEuler = new Vector3(0f, -90f, -90f);

    [MenuItem("Tools/New Hand/Preview (정면)")]
    public static void Preview() => Spawn(FrontFacingEuler);

    [MenuItem("Tools/New Hand/Preview — 원본 회전 (비교용)")]
    public static void PreviewRaw() => Spawn(Vector3.zero);

    /// <summary>접힘 확인용 — 게임을 켜지 않고 굽힘 방향만 눈으로 검증한다.
    /// 접기 로직(HandRig)은 런타임과 **같은 코드**를 쓰므로, 여기서 자연스러우면 게임에서도 같다.</summary>
    [MenuItem("Tools/New Hand/Preview — 접힘 (주먹)")]
    public static void PreviewFist() => SpawnFolded(1f);

    [MenuItem("Tools/New Hand/Preview — 반쯤 접힘")]
    public static void PreviewHalf() => SpawnFolded(0.5f);

    /// <summary>손가락 순서(엄지→소지) 매핑 검증용 — 검지 하나만 펴진 게 보여야 한다.</summary>
    [MenuItem("Tools/New Hand/Preview — 검지 가리키기")]
    public static void PreviewPoint()
        => SpawnPose(new[] { 1f, 0f, 1f, 1f, 1f }, "검지 가리키기");

    private static void SpawnFolded(float amount)
        => SpawnPose(new[] { amount, amount, amount, amount, amount }, $"접힘 {amount:P0}");

    private static void SpawnPose(float[] folds, string label)
    {
        Spawn(FrontFacingEuler);
        var go = GameObject.Find(PreviewName);
        if (go == null) return;
        var rig = HandRig.BuildFromBones(go.transform);
        if (rig == null) { Debug.LogError("[NewHandPreview] 리그 구성 실패"); return; }
        for (int i = 0; i < HandRig.FingerCount; i++) rig.SetFold(i, folds[i]);
        Debug.Log($"[NewHandPreview] {label} 적용 (마디 {rig.JointCount}개)");
    }

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
