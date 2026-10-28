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

    // M1-S0: 뿌리기 검증은 10개 나이 전부를 돌아야 한다(계획서 §12).
    [MenuItem("Tools/테스트: 나이 → 10살 (1회차)")]
    public static void GoAge10() => GoLoop(1);

    [MenuItem("Tools/테스트: 나이 → 15살 (2회차)")]
    public static void GoAge15() => GoLoop(2);

    [MenuItem("Tools/테스트: 나이 → 25살 (4회차)")]
    public static void GoAge25() => GoLoop(4);

    [MenuItem("Tools/테스트: 나이 → 30살 (5회차)")]
    public static void GoAge30() => GoLoop(5);

    [MenuItem("Tools/테스트: 나이 → 45살 (8회차)")]
    public static void GoAge45() => GoLoop(8);

    [MenuItem("Tools/테스트: 나이 → 50살 (9회차)")]
    public static void GoAge50() => GoLoop(9);

    [MenuItem("Tools/테스트: 나이 → 55살 (10회차)")]
    public static void GoAge55() => GoLoop(10);

    // ── M1-S0 뿌리기 기준값 캡처 ──────────────────────────────────────────────
    // 10회차 × 게이지 4 × seed 20의 목표(targets, isNak)를 파일로 남긴다.
    // 목표 계산은 시드 고정 + 중심 uv 고정(0.5,0.5)이라 돌 수(n)만 회차마다 다르다.
    // 같은 캡처를 두 번 돌려 문자열이 같은지(결정성)도 함께 확인한다.
    private static readonly float[] BaselineGauges = { 0.10f, 0.45f, 0.67f, 0.95f };
    private const int BaselineSeeds = 20;
    // 중심 (0.5,0.5)만으로는 게이지 0.95(반경 최대 4.27)도 반깊이+마진(4.7) 안이라 isNak가 전부 False다.
    // 앞·오른쪽으로 치우친 중심을 하나 더 넣어 낙 판정도 기준값에 들어가게 한다.
    private static readonly Vector2[] BaselineCenters = { new Vector2(0.5f, 0.5f), new Vector2(0.75f, 0.78f) };

    [MenuItem("Tools/테스트: 뿌리기 기준값 캡처 (before)")]
    public static void CaptureScatterBaselineBefore() => CaptureScatterBaseline("Logs/scatter-baseline-before.txt");

    [MenuItem("Tools/테스트: 뿌리기 기준값 캡처 (after)")]
    public static void CaptureScatterBaselineAfter() => CaptureScatterBaseline("Logs/scatter-baseline-after.txt");

    private static void CaptureScatterBaseline(string relPath)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        var session = GameSession.Instance;
        var scatter = Object.FindFirstObjectByType<ScatterSystem>();
        if (gm == null || session == null || scatter == null)
        {
            Debug.LogError($"[GameTestMenu] GM={gm != null} Session={session != null} Scatter={scatter != null}");
            return;
        }

        int origLoop = session.CurrentLoop;
        string run1 = RunBaselineOnce(gm, session, scatter);
        string run2 = RunBaselineOnce(gm, session, scatter);
        bool deterministic = run1 == run2;

        string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), relPath);
        System.IO.File.WriteAllText(path, run1);
        int lines = run1.Split('\n').Length - 1;
        GoLoop(origLoop < 1 ? 1 : origLoop);   // 원래 나이로 복귀
        Debug.Log($"[GameTestMenu] 뿌리기 기준값 캡처 완료 → {relPath} ({lines}줄, 결정성={deterministic})");
    }

    // ── M1 체크포인트 청취용 (T3 음계 A/B, D6 마무리음) ─────────────────────────
    [MenuItem("Tools/테스트: 착지음 음계 A↔B 전환")]
    public static void ToggleScatterNoteSet()
    {
        AudioManager.ActiveNoteSet = AudioManager.ActiveNoteSet == AudioManager.ScatterNoteSet.B_Narrow
            ? AudioManager.ScatterNoteSet.A_Pentatonic : AudioManager.ScatterNoteSet.B_Narrow;
        Debug.Log($"[GameTestMenu] 착지음 음계 = {AudioManager.ActiveNoteSet}");
    }

    [MenuItem("Tools/테스트: 마무리음 켜기↔끄기")]
    public static void ToggleScatterFinishCue()
    {
        AudioManager.FinishCueEnabled = !AudioManager.FinishCueEnabled;
        Debug.Log($"[GameTestMenu] 마무리음 = {AudioManager.FinishCueEnabled}");
    }

    // ── M1-S7 뿌리기 낙 강제 (계획서 §7 GameTestMenu, §13 M1 매트릭스) ─────────────
    // 현재 나이에서 손을 보드 중앙에 두고, 돌 0..k-1의 목표를 보드 좌표로 덮어써 뿌린다.
    // 보드 좌표: +y = 앞, -x = 왼쪽. 반폭 8 / 반깊이 4.5 (BoardSpace.LogicalWidth/Depth의 절반).
    private static float HW => BoardSpace.LogicalWidth * 0.5f;
    private static float HD => BoardSpace.LogicalDepth * 0.5f;

    [MenuItem("Tools/테스트 낙/앞 1.0")]   public static void NakFront10() => ForceNak(new Vector2(0f, HD + 1.0f));
    [MenuItem("Tools/테스트 낙/뒤 1.0")]   public static void NakBack10()  => ForceNak(new Vector2(0f, -HD - 1.0f));
    [MenuItem("Tools/테스트 낙/좌 1.0")]   public static void NakLeft10()  => ForceNak(new Vector2(-HW - 1.0f, 0f));
    [MenuItem("Tools/테스트 낙/우 1.0")]   public static void NakRight10() => ForceNak(new Vector2(HW + 1.0f, 0f));
    [MenuItem("Tools/테스트 낙/앞 0.05")]  public static void NakFront005() => ForceNak(new Vector2(0f, HD + 0.05f));
    [MenuItem("Tools/테스트 낙/뒤 0.05")]  public static void NakBack005()  => ForceNak(new Vector2(0f, -HD - 0.05f));
    [MenuItem("Tools/테스트 낙/좌 0.05")]  public static void NakLeft005()  => ForceNak(new Vector2(-HW - 0.05f, 0f));
    [MenuItem("Tools/테스트 낙/우 0.05")]  public static void NakRight005() => ForceNak(new Vector2(HW + 0.05f, 0f));

    [MenuItem("Tools/테스트 낙/동시 3개 (앞·좌·우 1.0)")]
    public static void NakTriple() => ForceNak(new Vector2(0f, HD + 1.0f), new Vector2(-HW - 1.0f, 0f), new Vector2(HW + 1.0f, 0f));

    [MenuItem("Tools/테스트 낙/모서리 우1.0×앞0.1 (25살용)")]
    public static void NakCornerRightFront() => ForceNak(new Vector2(HW + 1.0f, HD + 0.1f));

    [MenuItem("Tools/테스트 낙/우 +0.25 (Flat 경계 바로 너머)")]
    public static void NakRight025() => ForceNak(new Vector2(HW + 0.25f, 0f));

    [MenuItem("Tools/테스트 낙/앞 -0.01 (Ledge 경계 바로 안쪽, 안전)")]
    public static void NakFrontInside() => ForceNak(new Vector2(0f, HD - 0.01f));

    /// <summary>낙 대기 중 일시정지(F7): 대기 0.4초 시점에 일시정지 → 실시간 2초 → 재개. 실패는 게임 시간 기준으로
    /// 남은 만큼만 기다렸다 1회. 이어서 실패 직후 돌 복구 상태(렌더러·콜라이더·z)를 로그로 남긴다.</summary>
    [MenuItem("Tools/테스트 낙/검증: 대기 중 일시정지")]
    public static void VerifyNakPause() => StartEditorRoutine(NakPauseRoutine());

    /// <summary>낙 대기 중 스테이지 점프(F8): 대기 0.4초 시점에 20살로 점프 → 이전 실패가 발생하지 않아야 한다.</summary>
    [MenuItem("Tools/테스트 낙/검증: 대기 중 스테이지 점프")]
    public static void VerifyNakJump() => StartEditorRoutine(NakJumpRoutine());

    private static System.Collections.IEnumerator WaitGame(float sec)
    {
        float t0 = Time.time;
        while (Time.time - t0 < sec) yield return null;
    }

    private static System.Collections.IEnumerator NakPauseRoutine()
    {
        var gm = GameManager.Instance;
        GoLoop(1);
        float t0 = Time.time;
        while ((gm.CurrentPhase != GameManager.GamePhase.Scatter || gm.IsTransitioning) && Time.time - t0 < 8f) yield return null;
        NakTriple();
        float real0 = Time.realtimeSinceStartup, game0 = Time.time;
        var w = WaitGame(0.4f); while (w.MoveNext()) yield return null;
        PauseMenuUI.Instance.Toggle();
        float pr = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - pr < 2f) yield return null;
        Debug.Log($"[NakVerify] paused 2s real: pending={gm.IsNakPending} phase={gm.CurrentPhase} timeScale={Time.timeScale}");
        PauseMenuUI.Instance.Toggle();
        t0 = Time.realtimeSinceStartup;
        while (gm.CurrentPhase == GameManager.GamePhase.Scatter && Time.realtimeSinceStartup - t0 < 6f) yield return null;
        Debug.Log($"[NakVerify] after resume: phase={gm.CurrentPhase} game={Time.time - game0:F3}s real={Time.realtimeSinceStartup - real0:F3}s");
        yield return null;
        LogStoneRestore("after Failed");
    }

    private static System.Collections.IEnumerator NakJumpRoutine()
    {
        var gm = GameManager.Instance;
        GoLoop(1);
        float t0 = Time.time;
        while ((gm.CurrentPhase != GameManager.GamePhase.Scatter || gm.IsTransitioning) && Time.time - t0 < 8f) yield return null;
        NakTriple();
        var w = WaitGame(0.4f); while (w.MoveNext()) yield return null;
        GoLoop(3);
        Debug.Log($"[NakVerify] jumped: pending={gm.IsNakPending} phase={gm.CurrentPhase}");
        LogStoneRestore("after jump");
        w = WaitGame(2.5f); while (w.MoveNext()) yield return null;
        Debug.Log($"[NakVerify] 2.5s after jump: phase={gm.CurrentPhase} reason='{gm.LastFailReason}'");
    }

    private static void LogStoneRestore(string tag)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var s in GameManager.Instance.Stones)
        {
            var c = s.GetComponent<Collider>();
            sb.Append($"{s.StoneIndex}:{s.CurrentState} rend={s.IsRendererVisible} col={(c != null && c.enabled)} z={s.transform.position.z:F2} | ");
        }
        Debug.Log($"[NakVerify] stones {tag}: {sb}");
    }

    // ── M2 플레이 중 낙 (계획서 §12 M2) ──────────────────────────────────────────

    /// <summary>현재 나이에서 시드 뿌리기(낙 없음)로 PickThrowStone까지 간다.</summary>
    private static System.Collections.IEnumerator PrepareInPlay(int loop)
    {
        var gm = GameManager.Instance;
        GoLoop(loop);
        float t0 = Time.time;
        while ((gm.CurrentPhase != GameManager.GamePhase.Scatter || gm.IsTransitioning) && Time.time - t0 < 8f) yield return null;
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        var c = BoardSpace.ToScreen(Vector2.zero, 0f);
        hand.DebugPlaceHand(new Vector3(c.x, c.y, 0f));
        Object.FindFirstObjectByType<ScatterSystem>().DebugScatterSeeded(0.45f, 7, new Vector2(0.5f, 0.5f));
        t0 = Time.time;
        while (gm.CurrentPhase == GameManager.GamePhase.Scatter && Time.time - t0 < 5f) yield return null;
    }

    private struct InPlayCase { public int Loop; public FiveStones.Core.BoardEdge Edge; public float Over; public bool ExpectNak; }

    /// <summary>M2-S1 완료 확인: 판정 사례 + 스테이지별 "전 변 Flat이면 IsOutsideMat과 같다" 격자 비교(실제 BoardBounds).</summary>
    [MenuItem("Tools/테스트 낙(플레이)/검증: S1 판정")]
    public static void VerifyInPlayJudge() => StartEditorRoutine(VerifyInPlayJudgeRoutine());

    private static System.Collections.IEnumerator VerifyInPlayJudgeRoutine()
    {
        var gm = GameManager.Instance;
        // 1) 격자 비교 — 스테이지마다 quad가 다르다
        var allFlat = new FiveStones.Core.BoardEdges(FiveStones.Core.EdgeKind.Flat, FiveStones.Core.EdgeKind.Flat,
                                                     FiveStones.Core.EdgeKind.Flat, FiveStones.Core.EdgeKind.Flat);
        for (int loop = 1; loop <= 10; loop++)
        {
            GoLoop(loop);
            yield return null;
            int n = 0, mismatch = 0, nakAllFlat = 0, nakReal = 0;
            for (float x = -13f; x <= 13f; x += 0.1f)
                for (float y = -9f; y <= 1f; y += 0.1f)
                {
                    var p = new Vector2(x, y);
                    bool a = BoardBounds.IsNakInPlay(p, allFlat), b = BoardBounds.IsOutsideMat(p, BoardSpace.GraceBandWidth);
                    if (a != b) mismatch++;
                    if (a) nakAllFlat++;
                    if (BoardBounds.IsNakInPlay(p)) nakReal++;
                    n++;
                }
            Debug.Log($"[InPlayVerify] grid loop={loop} edges={BoardSpace.Edges} samples={n} allFlat-vs-IsOutsideMat mismatch={mismatch} " +
                      $"nak(allFlat)={nakAllFlat} nak(real edges)={nakReal}");
        }

        // 2) 사례 (계획서 §12 M2-S1 완료 확인 + 경계)
        var F = FiveStones.Core.BoardEdge.Front; var B = FiveStones.Core.BoardEdge.Back;
        var Lf = FiveStones.Core.BoardEdge.Left; var R = FiveStones.Core.BoardEdge.Right;
        var cases = new[]
        {
            new InPlayCase { Loop = 1, Edge = F,  Over = 0.05f, ExpectNak = true },
            new InPlayCase { Loop = 1, Edge = F,  Over = 0.01f, ExpectNak = false },
            new InPlayCase { Loop = 1, Edge = Lf, Over = 0.05f, ExpectNak = true },
            new InPlayCase { Loop = 1, Edge = B,  Over = 0.05f, ExpectNak = true },   // D9-b A
            new InPlayCase { Loop = 4, Edge = F,  Over = 0.05f, ExpectNak = true },
            new InPlayCase { Loop = 4, Edge = R,  Over = 0.5f,  ExpectNak = false },
            new InPlayCase { Loop = 7, Edge = R,  Over = 0.5f,  ExpectNak = false },
            new InPlayCase { Loop = 7, Edge = R,  Over = 0.9f,  ExpectNak = true },
            new InPlayCase { Loop = 7, Edge = B,  Over = 0.5f,  ExpectNak = false },  // Flat 뒷변 유예
            new InPlayCase { Loop = 7, Edge = B,  Over = 2.0f,  ExpectNak = false },  // Flat 뒷변 하늘 면제 유지
        };
        int bad = 0;
        foreach (var cs in cases)
        {
            var prep = PrepareInPlay(cs.Loop); while (prep.MoveNext()) yield return prep.Current;
            if (gm.CurrentPhase != GameManager.GamePhase.PickThrowStone)
            { Debug.LogError($"[InPlayVerify] loop={cs.Loop} 준비 실패 phase={gm.CurrentPhase}"); bad++; continue; }
            float t0 = Time.time;
            gm.DebugForceInPlayNak(cs.Edge, cs.Over);
            while (Time.time - t0 < 1.6f && gm.CurrentPhase != GameManager.GamePhase.Failed) yield return null;
            bool nak = gm.CurrentPhase == GameManager.GamePhase.Failed;
            bool ok = nak == cs.ExpectNak;
            if (!ok) bad++;
            Debug.Log($"[InPlayVerify] case age={10 + (cs.Loop - 1) * 5} edge={cs.Edge} over={cs.Over:F2} expectNak={cs.ExpectNak} " +
                      $"nak={nak} after={Time.time - t0:F3}s reason='{(nak ? gm.LastFailReason : "-")}' ok={ok}");
        }
        Debug.Log($"[InPlayVerify] SUMMARY cases={cases.Length} bad={bad}");
        GoLoop(1);
    }

    private static void ForceNak(params Vector2[] targets)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var gm = GameManager.Instance;
        var scatter = Object.FindFirstObjectByType<ScatterSystem>();
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        if (gm == null || scatter == null || hand == null) { Debug.LogError("[GameTestMenu] 준비 안 됨"); return; }
        if (gm.IsNakPending) { Debug.LogError("[GameTestMenu] 낙 대기 중"); return; }
        if (gm.CurrentPhase != GameManager.GamePhase.Scatter) gm.SetPhase(GameManager.GamePhase.Scatter);
        var c = BoardSpace.ToScreen(Vector2.zero, 0f);
        hand.DebugPlaceHand(new Vector3(c.x, c.y, 0f));
        Debug.Log($"[GameTestMenu] 낙 강제 {targets.Length}개: {string.Join(" ", System.Array.ConvertAll(targets, t => t.ToString("F2")))} edges={BoardSpace.Edges}");
        scatter.DebugScatterForcedNak(0.45f, 31, new Vector2(0.5f, 0.5f), targets);
    }

    // ── M1 뿌리기 검증 (계획서 §12 S1 완료 확인) ────────────────────────────────
    // 10나이 × 게이지(0.1/0.45/0.95) 실제 뿌리기를 연속으로 돌려 돌별 diff·총 시간·출발 간격 합을 요약한다.
    // 여러 프레임에 걸치므로 EditorApplication.update로 한 단계씩 진행한다(메뉴는 즉시 반환).
    private static System.Collections.IEnumerator editorRoutine;

    [MenuItem("Tools/테스트: 뿌리기 검증 (10나이 × 게이지 3, 중심)")]
    public static void VerifyScatterCenter() =>
        StartEditorRoutine(VerifyScatterRoutine(new[] { 0.10f, 0.45f, 0.95f }, new Vector2(0.5f, 0.5f), 7));

    [MenuItem("Tools/테스트: 뿌리기 검증 (10나이, 앞오른쪽 g0.95 — 낙 포함)")]
    public static void VerifyScatterNak() =>
        StartEditorRoutine(VerifyScatterRoutine(new[] { 0.95f }, new Vector2(0.75f, 0.78f), 3));

    private static void StartEditorRoutine(System.Collections.IEnumerator r)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (editorRoutine != null) EditorApplication.update -= TickEditorRoutine;
        editorRoutine = r;
        EditorApplication.update += TickEditorRoutine;
    }

    private static void TickEditorRoutine()
    {
        if (editorRoutine == null || !Application.isPlaying || !editorRoutine.MoveNext())
        {
            EditorApplication.update -= TickEditorRoutine;
            editorRoutine = null;
        }
    }

    private static System.Collections.IEnumerator VerifyScatterRoutine(float[] gauges, Vector2 center, int seed)
    {
        var gm = GameManager.Instance;
        var session = GameSession.Instance;
        var scatter = Object.FindFirstObjectByType<ScatterSystem>();
        if (gm == null || session == null || scatter == null) { Debug.LogError("[ScatterVerify] 준비 안 됨"); yield break; }

        int origLoop = Mathf.Max(1, session.CurrentLoop);
        int runs = 0, bad = 0;
        float maxTotal = 0f, maxDiff = 0f, maxStagger = 0f, maxDrift = 0f;
        for (int loop = 1; loop <= 10; loop++)
        {
            foreach (float g in gauges)
            {
                session.CurrentLoop = loop;
                session.CurrentAge = 10 + (loop - 1) * 5;
                gm.StartStage(1);
                float t0 = Time.time;
                while ((gm.CurrentPhase != GameManager.GamePhase.Scatter || gm.IsTransitioning) && Time.time - t0 < 8f)
                    yield return null;
                if (gm.CurrentPhase != GameManager.GamePhase.Scatter)
                {
                    Debug.LogError($"[ScatterVerify] loop={loop} Scatter 진입 실패 ({gm.CurrentPhase})");
                    bad++; continue;
                }
                yield return null; yield return null;

                var scattered = (Stone[])gm.Stones.Clone();
                scatter.DebugScatterSeeded(g, seed, center);
                t0 = Time.time;
                while (gm.CurrentPhase == GameManager.GamePhase.Scatter && Time.time - t0 < 6f)
                    yield return null;
                var phase = gm.CurrentPhase;

                // 완료 직후 위치 → 0.3초 뒤 위치: 완료 뒤 아무 코루틴도 돌을 움직이지 않아야 한다(F2·Obstacle nudge 유지)
                var before = new Vector3[scattered.Length];
                for (int i = 0; i < scattered.Length; i++) before[i] = scattered[i].transform.position;
                t0 = Time.time;
                while (Time.time - t0 < 0.3f) yield return null;
                float drift = 0f;
                if (phase == GameManager.GamePhase.PickThrowStone)
                    for (int i = 0; i < scattered.Length; i++)
                        drift = Mathf.Max(drift, (scattered[i].transform.position - before[i]).magnitude);

                runs++;
                bool ok = phase == GameManager.GamePhase.PickThrowStone
                    ? scatter.DebugLastTotal > 0f && scatter.DebugLastTotal <= 1.20f && scatter.DebugLastMaxDiff < 1e-4f
                      && scatter.DebugLastStaggerSum <= 0.18f + 1e-5f && drift < 1e-4f
                    : phase == GameManager.GamePhase.Failed;
                if (!ok) bad++;
                if (phase == GameManager.GamePhase.PickThrowStone)
                {
                    maxTotal = Mathf.Max(maxTotal, scatter.DebugLastTotal);
                    maxDiff = Mathf.Max(maxDiff, scatter.DebugLastMaxDiff);
                    maxDrift = Mathf.Max(maxDrift, drift);
                }
                maxStagger = Mathf.Max(maxStagger, scatter.DebugLastStaggerSum);
                Debug.Log($"[ScatterVerify] age={session.CurrentAge} g={g:F2} n={scattered.Length} phase={phase} " +
                          $"total={scatter.DebugLastTotal:F3} maxDiff={scatter.DebugLastMaxDiff:F4} " +
                          $"stagger={scatter.DebugLastStaggerSum:F3} drift={drift:F4} ok={ok}");
            }
        }
        Debug.Log($"[ScatterVerify] SUMMARY runs={runs} bad={bad} maxTotal={maxTotal:F3} maxDiff={maxDiff:F4} " +
                  $"maxStagger={maxStagger:F3} maxDrift={maxDrift:F4}");
        GoLoop(origLoop);
    }

    private static string RunBaselineOnce(GameManager gm, GameSession session, ScatterSystem scatter)
    {
        var sb = new System.Text.StringBuilder();
        for (int loop = 1; loop <= 10; loop++)
        {
            session.CurrentLoop = loop;
            session.CurrentAge = 10 + (loop - 1) * 5;
            gm.StartStage(1);   // 동기: 돌 활성화(회차별 개수)·기믹 OnStageStart까지 이 호출 안에서 끝난다
            foreach (var c in BaselineCenters)
                foreach (float g in BaselineGauges)
                    for (int seed = 1; seed <= BaselineSeeds; seed++)
                        scatter.DebugScatterSeeded(g, seed, c, planOnly: true, sink: sb);
        }
        return sb.ToString();
    }

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

    /// <summary>뭉침 구간 — 채움이 coral로 경고해야 한다.</summary>
    [MenuItem("Tools/테스트: 게이지 보이기 (10%)")]
    public static void ShowGaugeTight() => ShowGaugeAt(0.10f);

    [MenuItem("Tools/테스트: 게이지 보이기 (100%)")]
    public static void ShowGaugeFull() => ShowGaugeAt(1.00f);

    /// <summary>게이지바와 **보드 위 링**을 같은 값으로 함께 띄운다.
    /// 둘은 같은 순간 같은 화면에 뜨므로, 색이 어긋나는지는 따로 보면 알 수 없다.
    /// (링은 원래 누르고 있는 동안만 갱신돼서 캡처할 틈이 없다.)</summary>
    private static void ShowGaugeAt(float v)
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        var g = GaugeBarUI.Instance;
        if (g == null) { Debug.LogError("[GameTestMenu] GaugeBarUI 없음"); return; }
        g.Show();
        g.SetValue(v);

        var ring = Object.FindFirstObjectByType<ScatterRangeIndicator>(FindObjectsInactive.Include);
        if (ring != null)
        {
            ring.Show();
            ring.UpdateRing(ScatterSystem.RadiusBoard(v), Vector2.zero);
        }
        // M1-S4: 링이 LineRenderer → 메시(원판+점선)로 바뀌었다
        Debug.Log($"[GameTestMenu] 게이지+링 표시 ({v:0.00}, 반경 {ScatterSystem.RadiusBoard(v):0.00}) / " +
                  $"링 {(ring != null ? ring.DebugSummary : "없음")}");
    }

    /// <summary>영문에서 글자가 상자를 넘치는지 보려면 언어를 바꿔가며 같은 화면을 찍어야 한다.</summary>
    [MenuItem("Tools/테스트: 언어 전환")]
    public static void ToggleLanguage()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        LocalizationManager.Toggle();
        Debug.Log("[GameTestMenu] 언어 전환");
    }

    /// <summary>홈의 설정 창. 일시정지 창과 **같은 부품**을 쓰는지 눈으로 대조해야 한다.</summary>
    [MenuItem("Tools/테스트: 설정 창(홈)")]
    public static void ShowSettings()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        SettingsPopupUI.EnsureInstance().Open();
    }

    /// <summary>판 없이 뜨는 글자(CLEAR!·N단)의 외곽선 확인용.
    /// 촬영을 같이 걸어야 한다 — 연출이 짧아 따로 부르면 놓친다.</summary>
    [MenuItem("Tools/테스트: CLEAR! 표시")]
    public static void ShowClear()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (GameUI.Instance == null) { Debug.LogError("[GameTestMenu] GameUI 없음"); return; }
        IntroFrameCapture.Begin(60, 4);
        GameUI.Instance.ShowClear();
    }

    [MenuItem("Tools/테스트: 단 인트로 표시")]
    public static void ShowStageIntro()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (GameUI.Instance == null) { Debug.LogError("[GameTestMenu] GameUI 없음"); return; }
        IntroFrameCapture.Begin(60, 4);
        GameUI.Instance.ShowStageIntro(3);
    }

    /// <summary>시안 마지막 두 화면. 올클리어까지 가지 않고도 대조할 수 있어야 한다.</summary>
    [MenuItem("Tools/테스트: 생 종료 화면")]
    public static void ShowLifeEnd()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (GameUI.Instance == null) { Debug.LogError("[GameTestMenu] GameUI 없음"); return; }
        // 촬영과 연출을 **한 번의 메뉴 실행 안에서** 같이 건다.
        // MCP 호출 간격(수 초)이 연출 길이(4.6초)보다 길어서 따로 부르면 매번 놓친다.
        IntroFrameCapture.Begin(60, 6);   // 6프레임마다 60장 ≈ 6초 구간
        GameUI.Instance.ShowLifeEnd(() => Debug.Log("[GameTestMenu] 생 종료 끝"));
    }

    [MenuItem("Tools/테스트: 엔딩 화면")]
    public static void ShowEndingJoke()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (GameUI.Instance == null) { Debug.LogError("[GameTestMenu] GameUI 없음"); return; }
        IntroFrameCapture.Begin(60, 6);
        GameUI.Instance.ShowEndingJoke(() => Debug.Log("[GameTestMenu] 엔딩 끝"));
    }

    [MenuItem("Tools/테스트: 기록 저장 창")]
    public static void ShowNameInput()
    {
        if (!Application.isPlaying) { Debug.LogError("[GameTestMenu] Play 중에만 동작"); return; }
        if (NameInputUI.Instance == null) { Debug.LogError("[GameTestMenu] NameInputUI 없음"); return; }
        NameInputUI.Instance.Show(182f, (n, t) => Debug.Log($"[GameTestMenu] 저장: {n} (연습={t})"));
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
