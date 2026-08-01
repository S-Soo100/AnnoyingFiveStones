using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 손가락 접기를 **손가락별로 전부 렌더해서 눈으로 검증**하는 도구 (v17).
///
/// 왜 필요한가: 접히는 방향은 수식으로 확신할 수 없다(이 프로젝트에서 반복 실패한 지점).
/// 손가락 하나씩 접어 정면·측면 두 시점에서 찍어 놓고 비교해야 어느 손가락이 어느 방향으로
/// 잘못 도는지 특정할 수 있다. Play를 켜지 않고 에디터에서 바로 돈다.
///
/// 접기 로직은 런타임과 **같은 코드**(HandRig)를 쓴다 — 여기서 자연스러우면 게임에서도 같다.
/// 결과: Screenshots/handfold/*.png
/// </summary>
public static class HandFoldContactSheet
{
    private const string FbxPath = "Assets/Resources/Models/Hand.fbx";
    private const string TempName = "__HandFoldShot";
    private const string OutDir = "Screenshots/handfold";
    private const int ShotSize = 512;

    /// <summary>손 크기 — NewHandPreview와 동일해야 게임과 같은 비율로 보인다.</summary>
    private const float TargetWidth = 2.09f;

    private struct Pose { public string name; public float[] folds; }

    private static Pose[] Poses => new[]
    {
        P("0_open",   0, 0, 0, 0, 0),
        P("1_thumb",  1, 0, 0, 0, 0),
        P("2_index",  0, 1, 0, 0, 0),
        P("3_middle", 0, 0, 1, 0, 0),
        P("4_ring",   0, 0, 0, 1, 0),
        P("5_pinky",  0, 0, 0, 0, 1),
        P("6_half",   .5f, .5f, .5f, .5f, .5f),
        P("7_fist",   1, 1, 1, 1, 1),
        // 게임에서 실제로 쓰는 포즈 — 커서 호버(HandPose.PointIndex / PointMiddle)
        P("8_point_index",  1, 0, 1, 1, 1),
        P("9_point_middle", 1, 1, 0, 1, 1),
    };

    private static Pose P(string n, float a, float b, float c, float d, float e)
        => new Pose { name = n, folds = new[] { a, b, c, d, e } };

    [MenuItem("Tools/New Hand/손가락 접기 전수 촬영")]
    public static void Shoot()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), OutDir);
        Directory.CreateDirectory(dir);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null) { Debug.LogError($"[HandFoldContactSheet] FBX 없음: {FbxPath}"); return; }

        int shots = 0;
        foreach (var pose in Poses)
        {
            var go = SpawnHand(prefab);
            var rig = HandRig.BuildFromBones(go.transform);
            if (rig == null) { Debug.LogError("[HandFoldContactSheet] 리그 구성 실패"); Object.DestroyImmediate(go); return; }
            for (int i = 0; i < HandRig.FingerCount; i++) rig.SetFold(i, pose.folds[i]);

            // 정면 = 게임에서 보이는 그대로. 측면 = **접히는 방향**을 판별하는 시점
            // (정면만 보면 앞으로 마는지 뒤로 마는지 구분이 안 된다).
            Render(go.transform.position, front: true, Path.Combine(dir, $"{pose.name}_front.png"));
            Render(go.transform.position, front: false, Path.Combine(dir, $"{pose.name}_side.png"));
            shots += 2;

            Object.DestroyImmediate(go);
        }
        Debug.Log($"[HandFoldContactSheet] {shots}장 저장: {dir}");
    }

    /// <summary>줍기 판정 원이 **보이는 손바닥**과 겹치는지 확인하는 컷.
    /// 판정 중심·반경을 게임과 같은 방식(HandRig)으로 구해 그대로 그린다.</summary>
    [MenuItem("Tools/New Hand/줍기 판정 확인 촬영")]
    public static void ShootPickRadius()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), OutDir);
        Directory.CreateDirectory(dir);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (prefab == null) { Debug.LogError($"[HandFoldContactSheet] FBX 없음: {FbxPath}"); return; }

        var go = SpawnHand(prefab);
        var rig = HandRig.BuildFromBones(go.transform);
        if (rig == null) { Debug.LogError("[HandFoldContactSheet] 리그 구성 실패"); Object.DestroyImmediate(go); return; }

        var circle = MakeCircle(rig.PalmCenter, rig.PalmRadius, new Color(0.2f, 1f, 1f));
        Render(go.transform.position, front: true, Path.Combine(dir, "pick_radius.png"));
        Debug.Log($"[HandFoldContactSheet] 손바닥 중심 {rig.PalmCenter} / 반경 {rig.PalmRadius:F3} → pick_radius.png");

        Object.DestroyImmediate(circle);
        Object.DestroyImmediate(go);
    }

    private static GameObject MakeCircle(Vector3 center, float radius, Color color)
    {
        var go = new GameObject("__PickCircle") { hideFlags = HideFlags.HideAndDontSave };
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.widthMultiplier = 0.02f;
        lr.alignment = LineAlignment.View;
        const int n = 48;
        lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, center.z - 1f));
        }
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        lr.material = mat;
        lr.startColor = lr.endColor = color;
        return go;
    }

    private static GameObject SpawnHand(GameObject prefab)
    {
        // ⚠️ PrefabUtility.InstantiatePrefab 금지 — 프리팹 인스턴스는 DontSave 플래그를 줘도
        //    씬에 저장된다(NewHandPreview 주석 참고). 평범한 복제로 만든다.
        var go = Object.Instantiate(prefab);
        go.name = TempName;
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.rotation = Quaternion.Euler(NewHandPreview.FrontFacingEuler);
        go.transform.position = Vector3.zero;

        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.localScale *= TargetWidth / Mathf.Max(b.size.x, 0.0001f);
            go.transform.position = Vector3.zero;
        }
        return go;
    }

    /// <summary>임시 카메라로 손만 크게 담아 PNG로 저장. 씬 카메라 설정을 건드리지 않는다.</summary>
    private static void Render(Vector3 center, bool front, string path)
    {
        var camGo = new GameObject("__HandShotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.18f, 0.20f);
        cam.cullingMask = ~0;

        if (front)
        {
            camGo.transform.position = center + new Vector3(0f, 0.3f, -6f);
            camGo.transform.rotation = Quaternion.identity;
        }
        else
        {
            // 엄지 반대쪽(소지 쪽)에서 본다 — 손날 실루엣이라 말리는 방향이 가장 잘 보인다.
            camGo.transform.position = center + new Vector3(6f, 0.3f, 0f);
            camGo.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        }

        var rt = new RenderTexture(ShotSize, ShotSize, 24);
        cam.targetTexture = rt;
        cam.Render();

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(ShotSize, ShotSize, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, ShotSize, ShotSize), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        File.WriteAllBytes(path, tex.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
    }
}
