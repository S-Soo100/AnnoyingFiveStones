using UnityEditor;
using UnityEngine;

/// <summary>
/// Play 중 게임 상태를 손으로 조작하기 위한 개발용 메뉴.
///
/// 왜 필요한가: MCP로 에디터를 조작할 때는 마우스가 게임 뷰 밖에 있어 클릭·커서 이동이 안 된다.
/// 그래서 "타이틀에서 게임 시작", "손을 돌 위에 올리기" 같은 최소 조작을 코드로 대신한다.
/// 시각/판정 검증은 반드시 실행 중 화면으로 확인해야 하기 때문(donts/game#21).
/// </summary>
public static class GameTestMenu
{
    [MenuItem("Tools/테스트: 게임 시작 (타이틀 건너뛰기)")]
    public static void StartGame()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogError("[GameTestMenu] GameManager 없음"); return; }
        TitleScreenUI.Instance?.Hide(null);
        gm.StartGameFromTitle();
        Debug.Log("[GameTestMenu] 게임 시작");
    }

    [MenuItem("Tools/테스트: 대화 넘기기")]
    public static void SkipDialogue()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        bool any = false;
        // Hide()는 페이드아웃 후 pendingCallback을 부르므로 클릭과 같은 경로로 진행된다.
        if (StoryMentUI.Instance != null && StoryMentUI.Instance.IsShowing) { StoryMentUI.Instance.Hide(); any = true; }
        if (TutorialUI.Instance != null) { TutorialUI.Instance.Hide(); any = true; }
        Debug.Log($"[GameTestMenu] 대화 넘김: {any}");
    }

    [MenuItem("Tools/테스트: 돌 뿌리기")]
    public static void Scatter()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var scatter = Object.FindFirstObjectByType<ScatterSystem>();
        if (scatter == null) { Debug.LogError("[GameTestMenu] ScatterSystem 없음"); return; }
        scatter.BeginScatter();
        scatter.DebugScatterNow(0.67f); // 사용자 플레이테스트에서 5개가 안정적으로 안착한 게이지
        Debug.Log("[GameTestMenu] 뿌리기 실행 (게이지 0.67)");
    }

    [MenuItem("Tools/테스트: 손을 보드 돌 위로")]
    public static void PlaceHandOnStone()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        // 손은 페이즈에 따라 비활성일 수 있어 Include 필요.
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        var gm = GameManager.Instance;
        if (hand == null || gm == null || gm.Stones == null)
        {
            Debug.LogError($"[GameTestMenu] 손={hand != null} GM={gm != null} 돌목록={(gm != null && gm.Stones != null ? gm.Stones.Length.ToString() : "null")}");
            return;
        }

        Stone target = null;
        foreach (var s in gm.Stones)
            if (s != null && s.CurrentState == Stone.State.OnBoard) { target = s; break; }
        if (target == null) { Debug.LogError("[GameTestMenu] 보드 위 돌이 없음 (뿌리기 이후에 실행)"); return; }

        hand.DebugPlaceHand(target.transform.position);
        Debug.Log($"[GameTestMenu] 손 배치 → 돌 #{target.StoneIndex} {target.transform.position} / " +
                  $"손바닥 {hand.DebugPalmCenter} / 판정 {hand.DebugIsStoneUnderHand(target)}");
    }
}
