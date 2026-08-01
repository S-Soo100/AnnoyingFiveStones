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

    [MenuItem("Tools/테스트: 5단으로")]
    public static void GoStage5()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogError("[GameTestMenu] GameManager 없음"); return; }
        gm.StartStage(5);
        Debug.Log("[GameTestMenu] 5단 시작");
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

    /// <summary>받기 판정과 그려진 손이 맞는지 보기 — 앞/중앙/뒤 세 깊이를 각각 확인해야 한다
    /// (판정이 보드 단위라 깊이마다 화면상 크기가 다르다).</summary>
    // ⚠️ 보드 y는 **+가 앞(가까움), -가 뒤(멀리)**다. BoardSpace.Project가 v=0을 뒷변으로 잡는다.
    [MenuItem("Tools/테스트: 받기 모드 (보드 앞)")]
    public static void CatchFront() => ForceCatch(new Vector2(0f, BoardSpace.LogicalDepth * 0.5f), "앞");

    [MenuItem("Tools/테스트: 받기 모드 (보드 중앙)")]
    public static void CatchMid() => ForceCatch(Vector2.zero, "중앙");

    [MenuItem("Tools/테스트: 받기 모드 (보드 뒤)")]
    public static void CatchBack() => ForceCatch(new Vector2(0f, -BoardSpace.LogicalDepth * 0.5f), "뒤");

    private static void ForceCatch(Vector2 boardPos, string label)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        if (hand == null) { Debug.LogError("[GameTestMenu] 손 없음"); return; }
        hand.DebugForceCatchMode(boardPos);
        Debug.Log($"[GameTestMenu] 받기 모드({label}) — 손바닥원 {hand.DebugCatchPalmScreenRadius:F2} / " +
                  $"손전체원 {hand.DebugCatchHandScreenRadius:F2}");
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
