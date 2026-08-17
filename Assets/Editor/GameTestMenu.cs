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

    /// <summary>게임 시작 연출을 실제 경로 그대로 태우면서 프레임을 연속 촬영한다.
    /// "0.1초 깜빡" 같은 건 프레임을 늘어놓고 봐야 원인을 특정할 수 있다.</summary>
    [MenuItem("Tools/테스트: 게임 시작 연출 촬영")]
    public static void CaptureIntro()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (TitleScreenUI.Instance == null || GameManager.Instance == null)
        {
            Debug.LogError("[GameTestMenu] 타이틀/GameManager 없음 (타이틀 화면에서 실행)");
            return;
        }
        IntroFrameCapture.Begin(60, 1); // 매 프레임 — 0.1초짜리 깜빡임을 놓치지 않으려면 간격을 두면 안 된다
        TitleScreenUI.Instance.DebugStartGame(); // "게임 시작" 버튼과 같은 경로
        Debug.Log("[GameTestMenu] 게임 시작 연출 촬영 시작");
    }

    /// <summary>지금부터 60프레임을 연속 촬영. 임의 구간(대화 종료 → 스테이지 진입 등) 검사용.</summary>
    [MenuItem("Tools/테스트: 프레임 촬영 시작")]
    public static void CaptureFrames()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        IntroFrameCapture.Begin(60, 1);
        Debug.Log("[GameTestMenu] 프레임 촬영 시작");
    }

    /// <summary>대화 종료 → 스테이지 진입 구간을 촬영. 배경이 늦게 들어오는지 등을 프레임으로 본다.</summary>
    [MenuItem("Tools/테스트: 스테이지 진입 촬영")]
    public static void CaptureStageEnter()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        IntroFrameCapture.Begin(120, 1);
        SkipDialogue();
    }

    [MenuItem("Tools/테스트: 5단으로")]
    public static void GoStage5() => GoStage(5);

    private static void GoStage(int stage)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogError("[GameTestMenu] GameManager 없음"); return; }
        gm.StartStage(stage);
        Debug.Log($"[GameTestMenu] {stage}단 시작");
    }

    // ── 나이(배경) 이동 ───────────────────────────────────────────────────────
    // ⚠️ 배경·보드는 **단(stage)이 아니라 회차(loop)** 로 정해진다.
    //    GameManager.StartStage는 StageConfig.Get(session.CurrentLoop)을 읽는다.
    //    단 1~5는 한 회차 안의 단계(5단=꺾기)이고, 회차가 바뀌어야 나이·배경이 바뀐다.
    //    나이 = 10 + (회차-1)×5.
    // 배경/보드 좌표 작업은 나이를 계속 오가며 눈으로 대조해야 한다(donts/game#21).

    [MenuItem("Tools/테스트: 나이 → 35살 (6회차)")]
    public static void GoAge35() => GoLoop(6);

    [MenuItem("Tools/테스트: 나이 → 40살 (7회차)")]
    public static void GoAge40() => GoLoop(7);

    [MenuItem("Tools/테스트: 나이 → 20살 (3회차)")]
    public static void GoAge20() => GoLoop(3);

    private static void GoLoop(int loop)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        var session = GameSession.Instance;
        if (gm == null || session == null)
        {
            Debug.LogError($"[GameTestMenu] GM={gm != null} Session={session != null}");
            return;
        }

        int age = 10 + (loop - 1) * 5;
        session.CurrentLoop = loop;
        session.CurrentAge  = age;
        gm.StartStage(1);   // 새 회차의 1단부터 — 배경/BGM이 여기서 갱신된다
        Debug.Log($"[GameTestMenu] {loop}회차 = {age}살 진입");
    }

    /// <summary>게이지를 원하는 값으로 띄워둔다. 실제 뿌리기는 누르는 동안만 보여서
    /// 화면 대조(시안 vs 게임)를 할 틈이 없다.</summary>
    [MenuItem("Tools/테스트: 게이지 보이기 (67%)")]
    public static void ShowGauge() => ShowGaugeAt(0.67f);

    /// <summary>퍼센트 글자가 **초록 채움 위**일 때와 **회색 홈 위**일 때 둘 다 읽히는지
    /// 봐야 한다 — 배경 밝기가 정반대라 한쪽만 보고 정하면 다른 쪽에서 묻힌다.</summary>
    [MenuItem("Tools/테스트: 게이지 보이기 (25%)")]
    public static void ShowGaugeLow() => ShowGaugeAt(0.25f);

    private static void ShowGaugeAt(float v)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var g = GaugeBarUI.Instance;
        if (g == null) { Debug.LogError("[GameTestMenu] GaugeBarUI 없음"); return; }
        g.Show();
        g.SetValue(v);
        Debug.Log($"[GameTestMenu] 게이지 표시 ({v:0.00})");
    }

    /// <summary>영문에서 글자가 상자를 넘치는지 보려면 언어를 바꿔가며 같은 화면을 찍어야 한다.</summary>
    [MenuItem("Tools/테스트: 언어 전환")]
    public static void ToggleLanguage()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        LocalizationManager.Toggle();
        Debug.Log("[GameTestMenu] 언어 전환");
    }

    [MenuItem("Tools/테스트: 일시정지 창")]
    public static void ShowPause()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (PauseMenuUI.Instance == null) { Debug.LogError("[GameTestMenu] PauseMenuUI 없음"); return; }
        PauseMenuUI.Instance.Toggle();
    }

    [MenuItem("Tools/테스트: 경고 창")]
    public static void ShowQuitConfirm()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (PauseMenuUI.Instance == null) { Debug.LogError("[GameTestMenu] PauseMenuUI 없음"); return; }
        PauseMenuUI.Instance.DebugShowQuitConfirm();
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
