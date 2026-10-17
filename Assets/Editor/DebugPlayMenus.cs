using UnityEditor;
using UnityEngine;

/// <summary>
/// MCP(execute_menu_item)로 플레이 중 디버그 훅을 부르기 위한 메뉴.
///
/// 왜 필요한가: 게임 안의 TEST 패널(DebugHUD, IMGUI)은 에디터 앱이 비활성일 때
/// 합성 클릭을 받지 못한다(2026-10-17 직접 플레이 검증 — 게임플레이 입력(Input System)은
/// 받는데 IMGUI 버튼만 무반응). MCP 메뉴 호출은 앱 활성 여부와 무관하게 동작한다.
/// </summary>
public static class DebugPlayMenus
{
    /// <summary>TEST 패널의 "다음 스테이지로 (+5살)"와 동일 — CLEAR 연출을 정상 경유한다.
    /// ⚠️ TEST 패널 버튼과 달리 **전환 중이면 세션도 건드리지 않는다** — 먼저 세션만 +4단
    /// 올려놓고 gm 쪽이 무시되면 나이/단이 어긋난 채 남는다(직접 플레이 중 실측한 꼬임).</summary>
    [MenuItem("Tools/디버그: 현재 루프 클리어 (+5살)")]
    private static void CompleteCurrentLoop()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[DebugPlayMenus] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        var session = GameSession.Instance;
        if (gm == null || session == null) { Debug.LogWarning("[DebugPlayMenus] GameManager/GameSession 없음"); return; }
        if (gm.IsTransitioning) { Debug.LogWarning("[DebugPlayMenus] 전환 중 — 무시(세션 불변)"); return; }

        for (int i = 1; i <= 4; i++) session.OnStageComplete(i); // 1~4단은 세션에만 반영 (TEST 패널과 동일)
        gm.DebugCompleteCurrentLoop();                           // 5단은 정상 클리어 흐름 (ShowClear 경유)
        SidePanelUI.Instance?.Refresh();
        AgeSaturationController.Instance?.UpdateSaturation(session.CurrentAge);
    }

    /// <summary>CLEAR 연출을 6초로 늘린다 — 스크린샷 캡처 지연(~1.5s)보다 길게. Play 재시작 시 원복.</summary>
    [MenuItem("Tools/디버그: CLEAR 연출 6초로 (캡처용)")]
    private static void StretchClearDuration()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[DebugPlayMenus] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        if (gm == null) return;
        var f = typeof(GameManager).GetField("clearDuration",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) { f.SetValue(gm, 6f); Debug.Log("[DebugPlayMenus] clearDuration=6"); }
        else Debug.LogWarning("[DebugPlayMenus] clearDuration 필드 없음");
    }
}
