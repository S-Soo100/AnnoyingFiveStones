using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// v19 (0825 피드백 6건) 수정 검증 메뉴. MCP execute_menu_item로 구동한다.
/// 각 항목이 콘솔에 [V19] PASS/FAIL 을 찍는다. 검증 끝나면 이 파일은 지워도 된다.
/// </summary>
public static class V19VerifyMenu
{
    private static void Report(string name, bool pass, string detail)
        => Debug.Log($"[V19] {(pass ? "PASS" : "FAIL")} — {name} ({detail})");

    // ── 1. 나이 파생 (Play, 세션 생성 후) ───────────────────────────────────
    [MenuItem("Tools/검증 v19/1. 나이 파생")]
    public static void VerifyAge()
    {
        var s = GameSession.Instance;
        if (s == null) { Debug.LogError("[V19] GameSession 없음 (Play + 게임 시작 후)"); return; }

        int savedAge = s.CurrentAge, savedLoop = s.CurrentLoop, savedStage = s.CurrentStageInLoop;

        s.CurrentLoop = 1; s.CurrentAge = 10; s.CurrentStageInLoop = 1;
        s.OnStageComplete(1);
        Report("1단 클리어 → 11살", s.CurrentAge == 11, $"age={s.CurrentAge}");
        s.OnStageComplete(2);
        Report("2단 클리어 → 12살", s.CurrentAge == 12, $"age={s.CurrentAge}");
        s.OnFail();
        Report("실패 → 루프 시작 나이 10살", s.CurrentAge == 10 && s.CurrentStageInLoop == 1,
               $"age={s.CurrentAge} stage={s.CurrentStageInLoop}");
        s.OnStageComplete(1);
        Report("재도전 1단 → 11살 (이중가산 없음)", s.CurrentAge == 11, $"age={s.CurrentAge}");
        s.OnStageComplete(5);
        Report("5단 클리어 → 15살 + 루프 2", s.CurrentAge == 15 && s.CurrentLoop == 2,
               $"age={s.CurrentAge} loop={s.CurrentLoop}");
        s.OnStageComplete(1);
        Report("루프2 1단 → 16살", s.CurrentAge == 16, $"age={s.CurrentAge}");

        s.CurrentLoop = 10; s.CurrentAge = 55; s.CurrentStageInLoop = 1;
        s.OnStageComplete(5);
        Report("루프10 5단 → 60살 게임클리어", s.CurrentAge == 60 && s.IsGameClear && s.CurrentLoop == 10,
               $"age={s.CurrentAge} clear={s.IsGameClear} loop={s.CurrentLoop}");

        s.CurrentAge = savedAge; s.CurrentLoop = savedLoop; s.CurrentStageInLoop = savedStage;
        Debug.Log("[V19] 나이 검증 완료 — 세션 원복 (regressionCount만 +1 잔존, 테스트 세션이라 무해)");
    }

    // ── 2. 받기 경계 확장 (Play, 뿌리기 이후) ───────────────────────────────
    [MenuItem("Tools/검증 v19/2. 받기 경계 (뿌리기 후)")]
    public static void VerifyCatchBoundary()
    {
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        var cs = Object.FindFirstObjectByType<CatchSystem>();
        var gm = GameManager.Instance;
        if (hand == null || cs == null || gm == null || gm.Stones == null || gm.Stones.Length == 0)
        { Debug.LogError("[V19] 손/CatchSystem/돌 없음 (게임 시작 + 뿌리기 후)"); return; }
        hand.StartCoroutine(CoCatchBoundary(hand, cs, gm));
    }

    private static IEnumerator CoCatchBoundary(HandController hand, CatchSystem cs, GameManager gm)
    {
        gm.SetPhase(GameManager.GamePhase.PickStones);
        cs.BeginCatch(gm.Stones[0]);
        yield return null;

        // A. 캐치 중 + 보드 중간 높이(-5, 뒷변 -3.3 아래) → v19에선 받기 모드 ON
        hand.DebugPlaceHand(new Vector3(0f, -5f, -0.5f));
        yield return null; yield return null;
        Report("낙하 중 보드 중간(-5)에서 받기 모드", hand.IsCatchMode, $"isCatchMode={hand.IsCatchMode}");

        // B. 보드 앞변(-6.9) - 히스테리시스(0.2) 아래(-7.3) → 받기 모드 OFF
        hand.DebugPlaceHand(new Vector3(0f, -7.3f, -0.5f));
        yield return null; yield return null;
        Report("앞변 아래(-7.3)에서 받기 해제", !hand.IsCatchMode, $"isCatchMode={hand.IsCatchMode}");

        // C. 캐치 종료 후 -5 → 경계가 뒷변으로 원복, 받기 모드 OFF
        cs.StopCatch();
        hand.DebugPlaceHand(new Vector3(0f, -5f, -0.5f));
        yield return null; yield return null;
        Report("캐치 종료 후 -5에서 줍기 모드 유지", !hand.IsCatchMode, $"isCatchMode={hand.IsCatchMode}");

        // D. 캐치 아닐 때 하늘(-2)에선 여전히 받기 모드 (기존 동작 보존)
        hand.DebugPlaceHand(new Vector3(0f, -2f, -0.5f));
        yield return null; yield return null;
        Report("하늘(-2)에서 받기 모드 (기존 동작)", hand.IsCatchMode, $"isCatchMode={hand.IsCatchMode}");

        Debug.Log("[V19] 받기 경계 검증 완료 — 페이즈가 틀어졌으니 이후 다른 검증은 Play 재시작 후 권장");
    }

    // ── 3. 줍기 = 보이는 위치 (Play, 뿌리기 후) ────────────────────────────
    [MenuItem("Tools/검증 v19/3. 줍기 밀림 판정 (뿌리기 후)")]
    public static void VerifyPickAfterDrift()
    {
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        var gm = GameManager.Instance;
        if (hand == null || gm == null || gm.Stones == null)
        { Debug.LogError("[V19] 손/돌 없음 (게임 시작 + 뿌리기 후)"); return; }

        Stone target = null;
        foreach (var st in gm.Stones)
            if (st != null && st.gameObject.activeSelf && st.CurrentState == Stone.State.OnBoard) { target = st; break; }
        if (target == null) { Debug.LogError("[V19] OnBoard 돌 없음 (뿌리기 후 실행)"); return; }

        // 물리 밀림 시뮬레이션: transform만 옮기고 BoardPos는 그대로 둔다 (v17의 낡은 값 상황 재현)
        Vector2 staleBoardPos = target.BoardPos;
        target.transform.position += new Vector3(0.8f, 0.3f, 0f);
        Vector2 visualBoard = BoardSpace.ToBoard(target.transform.position);
        float gapBoard = Vector2.Distance(staleBoardPos, visualBoard);

        // 손바닥 중심이 정확히 돌 위에 오도록 2단 배치 (루트-손바닥 오프셋 보정)
        hand.DebugPlaceHand(target.transform.position);
        Vector3 offset = hand.DebugPalmCenter - hand.transform.position;
        hand.DebugPlaceHand(target.transform.position - offset);

        bool picked = hand.DebugIsStoneUnderHand(target);
        Report("밀린 돌을 보이는 자리에서 집기", picked,
               $"BoardPos 괴리={gapBoard:F2} (보드단위), 구 판정이면 반경 초과였음");
    }

    // ── 4. 손크기 측정 (Play) ───────────────────────────────────────────────
    [MenuItem("Tools/검증 v19/4. 손크기 접힘 측정")]
    public static void VerifyHandScale()
    {
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        var model = hand != null ? hand.GetComponent<HandModelBuilder>() : null;
        if (model == null || model.Rig == null) { Debug.LogError("[V19] 손 모델/리그 없음 (Play + 게임 시작 후)"); return; }

        // 시나리오 1: 손가락 접힌 채 받기 진입 (버그 재현 조건)
        for (int i = 0; i < HandRig.FingerCount; i++) model.Rig.SetFold(i, 1f);
        hand.DebugForceCatchMode(Vector2.zero);
        float reachFromFolded = model.CurrentScreenTipReach;
        bool unfolded = true;
        for (int i = 0; i < HandRig.FingerCount; i++) unfolded &= model.Rig.GetFold(i) < 0.01f;
        Report("받기 진입 시 손가락 자동 펼침", unfolded, $"folds cleared={unfolded}");

        // 시나리오 2: 펼친 채 진입 — 결과 배율(=손끝 거리)이 시나리오 1과 같아야 함
        model.Rig.ResetAll();
        hand.DebugForceCatchMode(Vector2.zero);
        float reachFromOpen = model.CurrentScreenTipReach;
        bool same = Mathf.Abs(reachFromFolded - reachFromOpen) < 0.01f;
        Report("접힘/펼침 진입 손크기 동일", same,
               $"folded진입={reachFromFolded:F3} open진입={reachFromOpen:F3}");
    }

    // ── 5. 돌 사출/드리프트 감시 (Play, 뿌리기 후 — 35살 권장) ──────────────
    [MenuItem("Tools/검증 v19/5. 돌 사출 감시 1.5초 (뿌리기 후)")]
    public static void VerifyNoEjection()
    {
        var gm = GameManager.Instance;
        var hand = Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);
        if (gm == null || gm.Stones == null || hand == null) { Debug.LogError("[V19] GM/돌 없음"); return; }
        hand.StartCoroutine(CoWatchDrift(gm));
    }

    private static IEnumerator CoWatchDrift(GameManager gm)
    {
        var tracked = new List<Stone>();
        var start = new List<Vector3>();
        foreach (var st in gm.Stones)
            if (st != null && st.gameObject.activeSelf && st.CurrentState == Stone.State.OnBoard)
            { tracked.Add(st); start.Add(st.transform.position); }
        if (tracked.Count == 0) { Debug.LogError("[V19] OnBoard 돌 없음 (뿌리기 후 실행)"); yield break; }

        yield return new WaitForSeconds(1.5f);

        float maxDrift = 0f; int maxIdx = -1;
        for (int i = 0; i < tracked.Count; i++)
        {
            if (tracked[i] == null || tracked[i].CurrentState != Stone.State.OnBoard) continue;
            float d = Vector3.Distance(start[i], tracked[i].transform.position);
            if (d > maxDrift) { maxDrift = d; maxIdx = tracked[i].StoneIndex; }
        }
        Report("안착 돌 1.5초 드리프트 없음", maxDrift < 0.05f,
               $"돌 {tracked.Count}개, 최대 이동={maxDrift:F3} (돌#{maxIdx})");
    }

    // ── 6. 원근 배율 (Edit/Play 무관) ───────────────────────────────────────
    [MenuItem("Tools/검증 v19/6. 원근 배율")]
    public static void VerifyPerspective()
    {
        float back = BoardSpace.Current.PerspectiveScale(new Vector2(0f, -BoardSpace.LogicalDepth * 0.5f), 0f);
        float front = BoardSpace.Current.PerspectiveScale(new Vector2(0f, BoardSpace.LogicalDepth * 0.5f), 0f);
        float ratio = front / back;
        Report("원근 배율 0.78 / 크기차 1.28배", Mathf.Abs(back - 0.78f) < 0.001f && Mathf.Abs(front - 1f) < 0.001f,
               $"back={back:F3} front={front:F3} 크기차={ratio:F2}배 (기존 1.71배)");
    }

    // ── 화면 캡처 1장 (Play) ────────────────────────────────────────────────
    [MenuItem("Tools/검증 v19/화면 캡처 1장")]
    public static void CaptureOne()
    {
        if (!Application.isPlaying) { Debug.LogError("[V19] Play 중에만 동작"); return; }
        IntroFrameCapture.Begin(1, 1);
        Debug.Log("[V19] 캡처 → Screenshots/intro/000.png");
    }
}
