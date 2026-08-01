using UnityEditor;
using UnityEngine;

/// <summary>
/// Play 모드를 메뉴로 토글 — MCP에서 실행하기 위한 개발용 도구.
///
/// Unity 6에는 "Edit/Play" 메뉴 경로가 없어 MCP의 execute_menu_item으로 재생을 켤 수 없다.
/// 시각 검증은 Play에서 해야 하는 경우가 많아(donts/game#21) 진입 수단이 필요하다.
/// </summary>
public static class PlayModeToggle
{
    [MenuItem("Tools/Play 시작")]
    public static void Play()
    {
        if (EditorApplication.isPlaying) { Debug.Log("[PlayModeToggle] 이미 Play 중"); return; }
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Play 정지")]
    public static void Stop()
    {
        if (!EditorApplication.isPlaying) { Debug.Log("[PlayModeToggle] Play 중 아님"); return; }
        EditorApplication.isPlaying = false;
    }
}
