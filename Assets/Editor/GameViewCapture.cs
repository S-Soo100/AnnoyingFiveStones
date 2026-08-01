using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 게임 뷰를 PNG로 저장하는 개발용 도구 (v17).
///
/// 왜 필요한가: 이 프로젝트는 시각/판정 문제를 "실행 중 게임을 직접 확인"해서 잡아야 하는데
/// (donts/game#21), 셸의 screencapture는 화면 녹화 권한이 없어 쓸 수 없다.
/// MCP로 이 메뉴를 실행하면 오케스트레이터가 결과 이미지를 직접 확인·전달할 수 있다.
///
/// Play 모드에서 실행해야 게임 화면이 찍힌다.
/// </summary>
public static class GameViewCapture
{
    private const string OutDir = "Screenshots";

    [MenuItem("Tools/Capture Game View")]
    public static void Capture()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), OutDir);
        Directory.CreateDirectory(dir);

        // 고정 파일명 — 매번 덮어써야 오케스트레이터가 경로를 알고 읽을 수 있다.
        var path = Path.Combine(dir, "gameview.png");
        if (File.Exists(path)) File.Delete(path);

        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[GameViewCapture] 저장 요청: {path} (다음 프레임에 기록됨)");
    }

    /// <summary>Play를 켜지 않고 즉시 저장 — 카메라를 RenderTexture에 직접 그린다.
    /// ScreenCapture는 게임 뷰가 한 프레임 그려져야 파일이 생기는데, Edit 모드에서는
    /// 게임 뷰가 갱신되지 않을 수 있어 파일이 안 생긴다. 이 경로는 그 의존이 없다.
    /// (Screen Space Overlay UI는 안 찍힌다 — 3D 오브젝트 확인용)</summary>
    [MenuItem("Tools/Capture Game View (즉시)")]
    public static void CaptureNow()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[GameViewCapture] MainCamera 없음"); return; }

        const int W = 1280, H = 720;
        var rt = new RenderTexture(W, H, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;

        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        var dir = Path.Combine(Directory.GetCurrentDirectory(), OutDir);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "gameview.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Debug.Log($"[GameViewCapture] 저장 완료: {path}");
    }
}
