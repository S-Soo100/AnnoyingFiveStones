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
}
