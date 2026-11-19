using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-11-15 플레이 피드백 4건 검증 메뉴. MCP execute_menu_item로 구동(Play 중).
/// 콘솔에 [V1115] PASS/FAIL 을 찍는다. 누르기·떼기는 HandController.DebugPress/Release로
/// 실제 입력 콜백을 그대로 태운다. 검증 끝나면 이 파일은 지워도 된다.
/// </summary>
public static class V1115VerifyMenu
{
    private static IEnumerator routine;
    private static readonly List<string> logs = new List<string>();
    private static int pass, fail;

    private static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++; else fail++;
        Debug.Log($"[V1115] {(ok ? "PASS" : "FAIL")} — {name} ({detail})");
    }

    [MenuItem("Tools/검증 1115/전체 실행")]
    public static void RunAll() => Start(All());

    private static void Start(IEnumerator r)
    {
        if (!Application.isPlaying) { Debug.LogError("[V1115] Play 중에만 동작"); return; }
        // MCP로 몰면 에디터가 백그라운드 → runInBackground=false면 게임 루프가 안 돈다 (런타임 값만, 설정 파일 불변)
        Application.runInBackground = true;
        if (routine != null) EditorApplication.update -= Tick;
        routine = r;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (routine == null || !Application.isPlaying || !routine.MoveNext())
        {
            EditorApplication.update -= Tick;
            routine = null;
        }
    }

    private static void OnLog(string msg, string st, LogType t)
    {
        if (msg.StartsWith("[Hand] Airtime") || msg.StartsWith("[GravityGimmick] Thrown") || msg.StartsWith("[SpotlightGimmick]"))
            logs.Add(msg);
    }

    private static IEnumerator All()
    {
        pass = fail = 0;
        Application.logMessageReceived += OnLog;
        IEnumerator[] tests = { TestPressPick(), TestSkyPress(), TestHoldCatchZone(), TestAirtime(), TestFlee(), TestSpotlight() };
        foreach (var t in tests)
        {
            while (true)
            {
                bool more;
                try { more = t.MoveNext(); }
                catch (System.Exception e) { Report("예외", false, e.Message); break; }
                if (!more) break;
                yield return null;
            }
            // 테스트가 중간에 yield break로 끝나도 검증용 오버라이드(낙하 시간 등)가 다음 테스트로 새지 않게
            // (StartStage는 오버라이드를 지우지 않는다 — 단 완료·RestartGame만 지운다)
            Hand?.ClearAllOverrides();
        }
        Application.logMessageReceived -= OnLog;
        Debug.Log($"[V1115] SUMMARY pass={pass} fail={fail}");
    }

    // ── 2026-11-16 받기 흔들림(A4) — 옆으로 흐르는 돌을 따라가면 받고, 가만히 있으면 흐른 만큼 판정 ──
    [MenuItem("Tools/검증 1115/1116 받기 흔들림")]
    public static void RunDrift() => Start(DriftAll());

    private static IEnumerator DriftAll()
    {
        pass = fail = 0;
        Application.logMessageReceived += OnLog;
        // 검증 뿌리기는 Random을 시드 7로 고정한다 → 던지기 직전에 시드를 바꿔 흐름·속도를 다양하게 뽑는다
        for (int sd = 1; sd <= 6; sd++)
        {
            // 0 = 돌 따라감, 1 = 가만히(보드 뒷변 위 1.5), 2 = 가만히 + 손을 정점 바로 아래로 높이(받기 판정을 최대한 일찍)
            for (int mode = 0; mode <= 2; mode++)
            {
                var t = DriftCase(sd, mode, shoot: sd == 1 && mode == 0);
                while (true)
                {
                    bool more;
                    try { more = t.MoveNext(); }
                    catch (System.Exception e) { Report("흔들림 예외", false, e.Message); break; }
                    if (!more) break;
                    yield return null;
                }
                Hand?.ClearAllOverrides();
            }
        }
        Application.logMessageReceived -= OnLog;
        Debug.Log($"[V1115] DRIFT SUMMARY pass={pass} fail={fail}");
    }

    private static IEnumerator DriftCase(int seed, int mode, bool shoot)
    {
        bool follow = mode == 0;
        string tag = $"seed={seed} {(mode == 0 ? "따라감" : mode == 1 ? "가만히" : "가만히·높이")}";
        var p = Prep(1, 1); while (p.MoveNext()) yield return null;
        Random.InitState(1000 + seed);
        logs.Clear();
        var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.PickStones) { Report($"6 준비 {tag}", false, $"phase={GM.CurrentPhase}"); yield break; }
        var stone = Hand.ThrowStone;
        // 던진 자리 = DoThrow의 throwBoardPos (손 transform → 보드 클램프). 던진 뒤 손은 움직이지 않았다.
        Vector2 throwBoard = BoardSpace.ClampToBoard(BoardSpace.ToBoard((Vector2)Hand.transform.position));

        // 1단 목표 1개 줍기
        var solo = SoloStones(OnBoard());
        if (solo.Count == 0) { Report($"6 준비 {tag}", false, "줍을 돌 없음"); yield break; }
        PlacePalm(solo[0].transform.position); yield return null;
        Hand.DebugPress(); yield return null; yield return null;
        Hand.DebugRelease(); yield return null;
        int picked = Hand.PickedStones.Count;

        // 하늘로: 받기 경계(보드 뒷변) 위 1.5
        float skyY = BoardBounds.SkyFloorY + 1.5f;
        float t0 = Time.time;
        bool shot1 = false, shot2 = false;
        float peakY = float.MinValue;
        var cs = Object.FindFirstObjectByType<CatchSystem>();
        while (GM.CurrentPhase == GameManager.GamePhase.PickStones && !GM.IsNakPending && Time.time - t0 < 6f)
        {
            float x = follow
                ? stone.transform.position.x                                   // 눈에 보이는 돌(= 그림자) x를 따라간다
                : BoardSpace.ToScreen(throwBoard, 0f).x;                       // 던진 자리 위에 그대로
            peakY = Mathf.Max(peakY, stone.transform.position.y);
            float y = (mode == 2 && cs != null && cs.IsCatchPhase) ? peakY - 0.3f : skyY;   // 정점 직후 판정되게
            PlacePalm(new Vector3(x, y, 0f));
            if (shoot && !shot1 && Time.time - t0 > 0.3f) { Shot("V1116_rise.png"); shot1 = true; }
            if (shoot && !shot2 && (Object.FindFirstObjectByType<CatchSystem>()?.IsCatchPhase ?? false)
                && stone.transform.position.y < skyY + 2.5f) { Shot("V1116_fall.png"); shot2 = true; }
            yield return null;
        }

        string a = logs.FirstOrDefault(l => l.StartsWith("[Hand] Airtime")) ?? "";
        var md = System.Text.RegularExpressions.Regex.Match(a, @"drift=([+-][0-9.]+)");
        float drift = md.Success ? float.Parse(md.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : float.NaN;
        bool caught = GM.CurrentPhase == GameManager.GamePhase.Throw;
        string detail = $"drift={drift:+0.00;-0.00} picked={picked} phase={GM.CurrentPhase} nak={GM.IsNakPending} | {a}";

        if (follow)
            Report($"6a 흐르는 돌 따라가면 받음 {tag}", caught, detail);
        else
        {
            // 흐름은 정점에서 끝나므로 판정 자리 = 최종 자리(손 높이 무관). 손바닥 반경 0.8 근처(0.7~0.9)는 손바닥 중심 오차로 생략
            float d = Mathf.Abs(drift);
            if (d < 0.7f) Report($"6b 가만히 + 흐름 {d:F2}<0.8 → 손바닥 받음 {tag}", caught, detail);
            // 못 받음 = 실제로 떨어져 실패 단계(낙 대기/Failed)까지 갔는가 — 튕긴 돌이 손 위에 얹혀 멈추는 것도 실패로 잡는다
            else if (d > 0.9f) Report($"6b 가만히 + 흐름 {d:F2}>0.8 → 손가락 튕김 → 떨어져 실패 {tag}",
                                      !caught && (GM.IsNakPending || GM.CurrentPhase == GameManager.GamePhase.Failed), detail);
            else Debug.Log($"[V1115] SKIP — 6b 경계 흐름 {d:F2} {tag} ({detail})");
        }
        var w = Wait(4.5f); while (w.MoveNext()) yield return null;
    }

    // ── 2026-11-19 돌 자세·앞뒤 크기 비교 촬영 (판정 아님 — 사용자와 고르기 위한 미리보기) ──
    [MenuItem("Tools/검증 1115/1119 돌 자세·크기 비교 촬영")]
    public static void RunPosePreview() => Start(PosePreview());

    private static readonly Vector2[] PreviewPos =
        { new Vector2(-3f, 3.8f), new Vector2(3f, 3.8f), new Vector2(0f, 0f), new Vector2(-3f, -3.8f), new Vector2(3f, -3.8f) };
    // 자세 B: X 기울기(윗면이 보이는 정도) ±25 / Y 방향 아무렇게나 / Z 기울기 ±8 (옆으로 눕지 않게 작게)
    private static readonly Vector3[] PoseB =
        { new Vector3(-20f, 40f, 5f), new Vector3(15f, 200f, -7f), new Vector3(-10f, 110f, 3f), new Vector3(25f, 300f, -4f), new Vector3(-25f, 160f, 8f) };
    // 자세 C: 같은 방향 무작위 + 기울기 절반 이하(X ±12 / Z ±4) — B가 원통이 세워진 듯 보여서
    private static readonly Vector3[] PoseC =
        { new Vector3(-10f, 40f, 3f), new Vector3(8f, 200f, -4f), new Vector3(-5f, 110f, 2f), new Vector3(12f, 300f, -2f), new Vector3(-12f, 160f, 4f) };

    private static IEnumerator PosePreview()
    {
        // 시작 멘트("클릭하여 계속")가 떠 있으면 먼저 닫는다 — Hide도 페이드아웃 끝에 콜백(StartStage(1))을 부르므로
        // 배치 **전에** 닫고 그 재시작이 끝날 때까지 기다린다(배치 후에 닫으면 판이 새로 시작돼 돌이 사라졌다).
        var ment = StoryMentUI.Instance;
        if (ment != null && ment.IsShowing)
        {
            ment.Hide();
            float tm = Time.time;
            while (ment.IsShowing && Time.time - tm < 3f) yield return null;
            var w0 = Wait(1f); while (w0.MoveNext()) yield return null;
        }
        var p = Prep(1, 1); while (p.MoveNext()) yield return null;
        GameUI.Instance?.HideGuideText();
        Hand.DebugPlaceHand(new Vector3(9f, 3f, 0f));   // 손은 화면 오른쪽 위로 치운다
        var stones = OnBoard().Take(5).ToList();
        float orig = Stone.DepthSizeStrength;
        var cases = new (string name, int pose, float strength)[]
            { ("A_now", 0, 1f), ("B_pose", 1, 1f), ("C_pose", 2, 1f), ("C_size07", 2, 0.7f), ("C_size05", 2, 0.5f) };
        foreach (var c in cases)
        {
            Stone.DepthSizeStrength = c.strength;
            for (int i = 0; i < stones.Count; i++)
            {
                stones[i].SetBoardMotion(PreviewPos[i], 0f);
                stones[i].transform.rotation = c.pose == 1 ? Quaternion.Euler(PoseB[i]) : c.pose == 2 ? Quaternion.Euler(PoseC[i]) : Quaternion.identity;
            }
            yield return null; yield return null;   // 그림자(Update)가 새 크기를 따라오게
            // 실측: 화면상 돌 지름(렌더러 bounds) — 앞 2개 / 가운데 / 뒤 2개
            string sizes = string.Join(" ", stones.Select((st, k) =>
                $"{(PreviewPos[k].y > 0 ? "앞" : PreviewPos[k].y < 0 ? "뒤" : "중")}{st.GetComponent<Renderer>().bounds.size.x:F3}"));
            Debug.Log($"[V1115] SIZE {c.name} strength={c.strength:F1} phase={GM.CurrentPhase} {sizes}");
            Shot($"V1119_{c.name}.png");
            yield return null; yield return null;
        }
        Stone.DepthSizeStrength = orig;
        // 실제 뿌리기 착지 결과(손으로 배치한 게 아니라 게임 코드가 놓은 자세·크기)
        var p2 = Prep(1, 1); while (p2.MoveNext()) yield return null;
        GameUI.Instance?.HideGuideText();
        Hand.DebugPlaceHand(new Vector3(9f, 3f, 0f));
        var w2 = Wait(0.5f); while (w2.MoveNext()) yield return null;
        string poses = string.Join(" ", OnBoard().Select(st => $"({st.transform.eulerAngles.x:F0},{st.transform.eulerAngles.y:F0},{st.transform.eulerAngles.z:F0})"));
        Debug.Log($"[V1115] REAL strength={Stone.DepthSizeStrength:F1} poses={poses}");
        Shot("V1119_real_scatter.png");
        yield return null; yield return null;
        Debug.Log("[V1115] PREVIEW DONE");
    }

    /// <summary>게임 뷰 캡처 → 프로젝트 Temp/ (git 무시, 에디터 종료 시 정리).</summary>
    private static void Shot(string name)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", name));
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[V1115] SHOT {path}");
    }

    // ── 공통 ─────────────────────────────────────────────────────────────
    private static GameManager GM => GameManager.Instance;
    private static HandController Hand => Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);

    private static IEnumerator Wait(float sec) { float t0 = Time.time; while (Time.time - t0 < sec) yield return null; }

    private static IEnumerator WaitScatterReady()
    {
        float t0 = Time.time;
        while ((GM.CurrentPhase != GameManager.GamePhase.Scatter || GM.IsTransitioning) && Time.time - t0 < 10f) yield return null;
    }

    /// <summary>나이(loop)·단(stage)으로 가서 시드 뿌리기 → PickThrowStone까지.</summary>
    private static IEnumerator Prep(int loop, int stage)
    {
        var s = GameSession.Instance;
        s.CurrentLoop = loop;
        s.CurrentAge = 10 + (loop - 1) * 5;
        GM.StartStage(1);
        var w = WaitScatterReady(); while (w.MoveNext()) yield return null;
        if (stage > 1)
        {
            GM.StartStage(stage);
            w = WaitScatterReady(); while (w.MoveNext()) yield return null;
        }
        var c = BoardSpace.ToScreen(Vector2.zero, 0f);
        Hand.DebugPlaceHand(new Vector3(c.x, c.y, 0f));
        Object.FindFirstObjectByType<ScatterSystem>().DebugScatterSeeded(0.45f, 7, new Vector2(0.5f, 0.5f));
        float t0 = Time.time;
        while (GM.CurrentPhase == GameManager.GamePhase.Scatter && Time.time - t0 < 6f) yield return null;
        yield return null;
    }

    private static List<Stone> OnBoard()
        => GM.Stones.Where(st => st != null && st.gameObject.activeSelf && st.CurrentState == Stone.State.OnBoard).ToList();

    /// <summary>손바닥 중심을 target(화면 좌표)에 맞춘다 — 판정 중심은 손 transform이 아니라 손바닥이다.</summary>
    private static void PlacePalm(Vector3 target)
    {
        var h = Hand;
        for (int i = 0; i < 3; i++)
        {
            Vector3 off = h.DebugPalmCenter - h.transform.position;
            h.DebugPlaceHand(new Vector3(target.x - off.x, target.y - off.y, 0f));
        }
    }

    private static int CountUnder(List<Stone> stones) => stones.Count(st => Hand.DebugIsStoneUnderHand(st));

    /// <summary>손바닥을 올렸을 때 그 돌 하나만 덮이는 돌.</summary>
    private static List<Stone> SoloStones(List<Stone> stones)
    {
        var r = new List<Stone>();
        foreach (var st in stones)
        {
            PlacePalm(st.transform.position);
            if (Hand.DebugIsStoneUnderHand(st) && CountUnder(stones) == 1) r.Add(st);
        }
        return r;
    }

    /// <summary>보드 안에서 손 아래 돌이 하나도 없는 지점.</summary>
    private static bool FindEmpty(List<Stone> stones, out Vector3 p)
    {
        for (float y = -3.5f; y <= 3.5f; y += 0.5f)
            for (float x = -6f; x <= 6f; x += 0.5f)
            {
                var s = BoardSpace.ToScreen(new Vector2(x, y), 0f);
                p = new Vector3(s.x, s.y, 0f);
                PlacePalm(p);
                if (CountUnder(stones) == 0) return true;
            }
        p = Vector3.zero;
        return false;
    }

    /// <summary>누른 채(또는 안 누른 채) from→to로 n프레임에 걸쳐 손을 끈다.</summary>
    private static IEnumerator Drag(Vector3 from, Vector3 to, int frames)
    {
        for (int i = 1; i <= frames; i++)
        {
            PlacePalm(Vector3.Lerp(from, to, i / (float)frames));
            yield return null;
        }
    }

    private static IEnumerator ThrowFromPick()
    {
        // 던질 돌: 손바닥에 하나만 덮이는 돌 위에서 누른다 → 실제 경로로 Throw 전환 → 클릭으로 던지기
        var board = OnBoard();
        var solo = SoloStones(board);
        if (solo.Count == 0) { Report("던질 돌 찾기", false, "단독으로 덮이는 돌 없음"); yield break; }
        PlacePalm(solo[0].transform.position);
        yield return null;
        Hand.DebugPress(); yield return null; yield return null;
        Hand.DebugRelease(); yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.Throw) { Report("던질 돌 누르기 → Throw", false, $"phase={GM.CurrentPhase}"); yield break; }
        Hand.DebugClick(); yield return null;
    }

    // ── 1. 누르는 순간 집기 (10살 2단) ───────────────────────────────────
    private static IEnumerator TestPressPick()
    {
        var p = Prep(1, 2); while (p.MoveNext()) yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.PickThrowStone) { Report("1 준비", false, $"phase={GM.CurrentPhase}"); yield break; }

        // 1-0 던질 돌 고르기: 빈 곳에서 누른 채 돌 위로 끌면 안 집혀야 한다
        var board = OnBoard();
        var solo = SoloStones(board);
        if (solo.Count == 0 || !FindEmpty(board, out var empty0)) { Report("1-0 준비", false, $"solo={solo.Count}"); yield break; }
        PlacePalm(empty0); yield return null;
        Hand.DebugPress(); yield return null;
        var d = Drag(empty0, solo[0].transform.position, 12); while (d.MoveNext()) yield return null;
        yield return null;
        Report("1-0 던질 돌: 빈 곳에서 누른 채 돌 위로 끌기 → 안 집힘",
               GM.CurrentPhase == GameManager.GamePhase.PickThrowStone && Hand.ThrowStone == null,
               $"phase={GM.CurrentPhase} throwStone={(Hand.ThrowStone != null)}");
        Hand.DebugRelease(); yield return null;

        var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        Report("1-1 던질 돌: 돌 위에서 누르기 → 던지기 → 줍기 단계",
               GM.CurrentPhase == GameManager.GamePhase.PickStones, $"phase={GM.CurrentPhase}");
        if (GM.CurrentPhase != GameManager.GamePhase.PickStones) yield break;

        board = OnBoard();
        solo = SoloStones(board);
        if (solo.Count < 2 || !FindEmpty(board, out var empty)) { Report("1 줍기 준비", false, $"solo={solo.Count}"); yield break; }
        Stone a = solo[0], b = solo[1];

        // 1-2 쓸기(2초 안): 빈 곳에서 누른 채 A → B 를 지나간다 → 2개 (2026-11-15 2차: 누른 뒤 2초 쓸어담기)
        PlacePalm(empty); yield return null;
        Hand.DebugPress(); yield return null;
        d = Drag(empty, a.transform.position, 4); while (d.MoveNext()) yield return null;
        d = Drag(a.transform.position, b.transform.position, 4); while (d.MoveNext()) yield return null;
        int afterSweep = Hand.PickedStones.Count;
        Hand.DebugRelease(); yield return null;
        Report("1-2 누른 뒤 2초 안에 돌 2개 위로 쓸기 → 2개", afterSweep == 2,
               $"picked={afterSweep} phase={GM.CurrentPhase}");
        var w = Wait(4.5f); while (w.MoveNext()) yield return null; // 받기 실패 → 리셋 대기

        // 1-3~1-5는 2초 기다린 뒤에도 줍기 단계가 남아 있어야 한다 → 이 던지기만 낙하를 늘린다(검증 전용, 끝나면 해제)
        //   백그라운드 에디터 ~10fps에선 4단 체공 3.38초로도 1-5 전에 떨어졌다 (1차 실행 t=3.50s Failed)
        p = Prep(1, 4); while (p.MoveNext()) yield return null;
        // 2026-11-16: 4단 배율 1.15 + 낙하 랜덤(×0.77~1.0) → 4.5 × 1.15 × 0.77 ≈ 3.98초 이상 낙하 (구 3 × 1.35 = 4.05초와 비슷하게)
        Hand.SetThrowDownDurationOverride(4.5f);
        th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.PickStones) { Report("1-3 준비", false, $"phase={GM.CurrentPhase}"); yield break; }
        float tThrow = Time.time;
        board = OnBoard();
        solo = SoloStones(board);
        if (solo.Count < 2 || !FindEmpty(board, out var empty2)) { Report("1-3 준비", false, $"solo={solo.Count}"); yield break; }
        a = solo[0]; b = solo[1];

        // 1-3 빈 곳에서 누른 채 2초 넘게 버틴 뒤 A 위로 끌기 → 0개
        PlacePalm(empty2); yield return null;
        Hand.DebugPress(); yield return null;
        w = Wait(2.1f); while (w.MoveNext()) { PlacePalm(empty2); yield return null; }
        d = Drag(empty2, a.transform.position, 3); while (d.MoveNext()) yield return null;
        yield return null;
        int afterExpire = Hand.PickedStones.Count;
        bool aUnder = Hand.DebugIsStoneUnderHand(a);
        Hand.DebugRelease(); yield return null;
        Report("1-3 누른 채 2초 지난 뒤 돌 위로 끌기 → 안 집힘", afterExpire == 0 && aUnder,
               $"picked={afterExpire} A가손아래={aUnder} phase={GM.CurrentPhase} t={Time.time - tThrow:F2}s");

        // 1-4 떼고 A 위에서 다시 누르기 → 1개
        PlacePalm(a.transform.position); yield return null;
        Hand.DebugPress(); yield return null; yield return null;
        int afterPressA = Hand.PickedStones.Count;
        Report("1-4 떼고 돌 A 위에서 다시 누르기 → 1개", afterPressA == 1, $"picked={afterPressA} t={Time.time - tThrow:F2}s");

        // 1-5 그 누름 그대로 B 위로 끌기 → 2개 (다시 누르면 쓸기 시간 새로 시작)
        d = Drag(Hand.DebugPalmCenter, b.transform.position, 3); while (d.MoveNext()) yield return null;
        yield return null;
        int afterDragB = Hand.PickedStones.Count;
        Hand.DebugRelease(); yield return null;
        Report("1-5 다시 누른 채 돌 B 위로 끌기 → 2개", afterDragB == 2,
               $"picked={afterDragB} phase={GM.CurrentPhase} t={Time.time - tThrow:F2}s");
        w = Wait(5.5f); while (w.MoveNext()) yield return null; // 받기 실패 → 리셋 대기
    }

    // ── 1-6. 하늘에서 누른 채 보드로 끌고 내려오기 → 안 집힘 ─────────────
    private static IEnumerator TestSkyPress()
    {
        var p = Prep(1, 1); while (p.MoveNext()) yield return null;
        var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.PickStones) { Report("1-6 준비", false, $"phase={GM.CurrentPhase}"); yield break; }
        var board = OnBoard();
        var target = board[0].transform.position;
        var sky = new Vector3(target.x, BoardBounds.SkyFloorY + 1.5f, 0f);
        PlacePalm(sky); yield return null; yield return null;
        bool wasCatch = Hand.IsCatchMode;
        Hand.DebugPress(); yield return null;
        var d = Drag(sky, target, 12); while (d.MoveNext()) yield return null;
        yield return null;
        int picked = Hand.PickedStones.Count;
        Hand.DebugRelease(); yield return null;
        Report("1-6 하늘(받기 모드)에서 누른 채 보드 돌 위로 내려오기 → 안 집힘", wasCatch && picked == 0,
               $"catchModeAtPress={wasCatch} picked={picked}");
        var w = Wait(4f); while (w.MoveNext()) yield return null;
    }

    // ── 1-7. 쓸기 시간이 끝난 홀드는 받기 영역을 좁히지 않는다 (리뷰 지적) ──
    private static IEnumerator TestHoldCatchZone()
    {
        var p = Prep(1, 1); while (p.MoveNext()) yield return null;
        Hand.SetThrowDownDurationOverride(4f); // 낙하 3.1~4초(11-16 랜덤 ×0.77~1.0) — 2초 쓸기 시간이 끝난 뒤에도 낙하 중이게
        var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.PickStones) { Report("1-7 준비", false, $"phase={GM.CurrentPhase}"); yield break; }
        float tThrow = Time.time;
        var board = OnBoard();
        if (!FindEmpty(board, out var empty)) { Report("1-7 준비", false, "빈 곳 없음"); yield break; }
        var catchSys = Object.FindFirstObjectByType<CatchSystem>();

        // 보드 빈 곳(받기 확장 영역 = 보드 앞변~뒷변 사이)에서 누르고 계속 누르고 있는다
        PlacePalm(empty); yield return null;
        Hand.DebugPress();
        float tPress = Time.time;   // 누른 그 프레임 기준 — 쓸기 끝 = tPress + 2.0 (yield 뒤에 재면 dt만큼 늦게 잡힌다)
        yield return null;
        bool catchInWindow = true, sawFallInWindow = false;
        string firstCatchInfo = null;
        // 2026-11-16: 한계 체크를 yield **뒤**에 한다. 앞에서만 보면 yield 뒤 샘플이 1.9초를 넘어(백그라운드 ~10fps)
        //   쓸기 끝(2.0초) 이후 받기 모드 전환(= 1-7b의 정상 동작)을 1-7a 실패로 잡았다(실측 첫 catchMode t=2.00s).
        while (true)
        {
            PlacePalm(empty); yield return null;
            if (Time.time - tPress >= 1.9f) break;
            if (catchSys != null && catchSys.IsCatchPhase) { sawFallInWindow = true; catchInWindow = Hand.IsCatchMode; }
            if (Hand.IsCatchMode && firstCatchInfo == null)
                firstCatchInfo = $"첫 catchMode t={Time.time - tPress:F2}s handY={Hand.transform.position.y:F2} " +
                                 $"skyFloor={BoardBounds.SkyFloorY:F2} front={BoardSpace.FrontScreenY:F2} hold={Hand.IsHolding} catchPhase={catchSys?.IsCatchPhase}";
        }
        var w = Wait(0.4f); while (w.MoveNext()) { PlacePalm(empty); yield return null; }
        bool fallingAfter = catchSys != null && catchSys.IsCatchPhase;
        bool catchAfter = Hand.IsCatchMode;
        bool stillHolding = Hand.IsHolding;
        Hand.DebugRelease(); yield return null;
        Report("1-7a 쓸기 시간 안(낙하 중) 홀드 → 보드 모드 유지(쓸기 계속)", sawFallInWindow && !catchInWindow,
               $"낙하관측={sawFallInWindow} catchMode={catchInWindow} {firstCatchInfo ?? "(창 안 catchMode 없음)"}");
        Report("1-7b 쓸기 시간 지난 홀드(낙하 중) → 받기 모드(보드 앞쪽까지 받기 영역)", fallingAfter && stillHolding && catchAfter,
               $"낙하중={fallingAfter} 누르는중={stillHolding} catchMode={catchAfter} t={Time.time - tThrow:F2}s");
        w = Wait(4.5f); while (w.MoveNext()) yield return null;
    }

    // ── 2·3. 단별 체공 + 40살 중력 기준값 ────────────────────────────────
    private static IEnumerator TestAirtime()
    {
        // 2026-11-16 밸런스 A1·A2: 배율 1.0/1.05/1.1/1.15, 기본 up 0.85 / down 1.3. down은 랜덤 전 기준값이다.
        float[] mul = { 1f, 1.05f, 1.1f, 1.15f };
        for (int stage = 1; stage <= 4; stage++)
        {
            var p = Prep(1, stage); while (p.MoveNext()) yield return null;
            logs.Clear();
            var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
            // 체공 로그는 꼭대기 도달 뒤(내려오기 시작)에 찍힌다
            float t0 = Time.time;
            while (!logs.Any(l => l.StartsWith("[Hand] Airtime")) && Time.time - t0 < 3f) yield return null;
            float tPeak = Time.time - t0;
            string line = (logs.FirstOrDefault(l => l.StartsWith("[Hand] Airtime")) ?? "(없음)") + $" | 측정 상승≈{tPeak:F2}s";
            string expect = $"x{mul[stage - 1]:F2}: up={0.85f * mul[stage - 1]:F2}s down={1.3f * mul[stage - 1]:F2}s";
            Report($"2 체공 10살 {stage}단", line.Contains(expect), $"{line} / 기대 {expect}");
            Report($"2r 낙하 랜덤·옆 흐름 범위 10살 {stage}단", FallAndDriftInRange(line, 1.3f * mul[stage - 1]), line);
            var w = Wait(4.5f); while (w.MoveNext()) yield return null;
        }

        // 40살: 무게별 override = 1.3 × (0.5/1.0/1.8), 그 위에 1단 배율 1.0 (2026-11-16 A2로 기본 1.5→1.3)
        var seen = new HashSet<string>();
        for (int tryN = 0; tryN < 4 && seen.Count < 2; tryN++)
        {
            var p = Prep(7, 1); while (p.MoveNext()) yield return null;
            logs.Clear();
            var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
            float t0 = Time.time;
            while (!logs.Any(l => l.StartsWith("[Hand] Airtime")) && Time.time - t0 < 3f) yield return null;
            string g = logs.FirstOrDefault(l => l.StartsWith("[GravityGimmick] Thrown")) ?? "";
            string a = logs.FirstOrDefault(l => l.StartsWith("[Hand] Airtime")) ?? "";
            string weight = g.Contains("weight=Heavy") ? "Heavy" : g.Contains("weight=Light") ? "Light" : g.Contains("weight=Normal") ? "Normal" : "?";
            float expDown = weight == "Heavy" ? 0.65f : weight == "Light" ? 2.34f : 1.3f;
            if (!seen.Contains(weight))
            {
                seen.Add(weight);
                Report($"3 40살 중력 {weight}", a.Contains($"down={expDown:F2}s"), $"{g} | {a} / 기대 down={expDown:F2}s");
            }
            var w = Wait(4f); while (w.MoveNext()) yield return null;
        }
    }

    /// <summary>2026-11-16 — Airtime 로그의 fall이 down × [1/1.3, 1] 안이고, drift가 0 또는 ±0.6~1.4 안인가.
    /// (drift 0은 50살 시야 제한 / 가장자리 양쪽 막힘에서만 — 10살 검증에선 0.6 이상이어야 정상)</summary>
    private static bool FallAndDriftInRange(string line, float expDown)
    {
        var mf = System.Text.RegularExpressions.Regex.Match(line, @"fall=([0-9.]+)s");
        var md = System.Text.RegularExpressions.Regex.Match(line, @"drift=([+-][0-9.]+)");
        if (!mf.Success || !md.Success) return false;
        float fall = float.Parse(mf.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        float drift = Mathf.Abs(float.Parse(md.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        bool fallOk = fall >= expDown / 1.3f - 0.011f && fall <= expDown + 0.011f;
        bool driftOk = drift >= 0.6f - 0.011f && drift <= 1.4f + 0.011f;
        return fallOk && driftOk;
    }

    // ── 4. 20살 도망 공기 ────────────────────────────────────────────────
    private static IEnumerator TestFlee()
    {
        var p = Prep(3, 4); while (p.MoveNext()) yield return null;
        var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.PickStones) { Report("4 준비", false, $"phase={GM.CurrentPhase}"); yield break; }

        var stones = OnBoard();
        var last = stones.ToDictionary(s => s, s => (Vector2)s.transform.position);
        var moveRun = stones.ToDictionary(s => s, s => 0f);     // 진행 중 이동 구간 길이
        var firstRun = stones.ToDictionary(s => s, s => -1f);   // 첫 이동 구간 길이(끝난 것만)
        var speeds = new List<float>();
        const float dt = 0.1f;
        float t0 = Time.time, tPrev = Time.time;
        while (GM.CurrentPhase == GameManager.GamePhase.PickStones && Time.time - t0 < 3.3f)
        {
            yield return null;
            if (Time.time - tPrev < dt) continue;
            float step = Time.time - tPrev; tPrev = Time.time;
            foreach (var s in stones)
            {
                if (s == null || s.CurrentState != Stone.State.OnBoard) continue;
                Vector2 now = s.transform.position;
                float v = Vector2.Distance(now, last[s]) / step;
                last[s] = now;
                if (v > 0.3f) { speeds.Add(v); moveRun[s] += step; }
                else if (moveRun[s] > 0f) { if (firstRun[s] < 0f) firstRun[s] = moveRun[s]; moveRun[s] = 0f; }
            }
        }
        speeds.Sort();
        float med = speeds.Count > 0 ? speeds[speeds.Count / 2] : 0f;
        float p90 = speeds.Count > 0 ? speeds[(int)(speeds.Count * 0.9f)] : 0f;
        var runs = firstRun.Values.Where(v => v > 0f).ToList();
        Report("4 도망 속도 (설정 1.5~3, 이전 3~6)", speeds.Count > 0 && med <= 3.2f && p90 <= 3.6f,
               $"samples={speeds.Count} median={med:F2} p90={p90:F2} max={(speeds.Count > 0 ? speeds[speeds.Count - 1] : 0):F2}");
        Report("4 첫 도망 구간 길이 (설정 1.0~1.5초, 이전 1.5~2)", runs.Count > 0 && runs.All(r => r >= 0.85f && r <= 1.65f),
               $"runs=[{string.Join(", ", runs.Select(r => r.ToString("F1")))}] (0.1초 샘플링)");
        var w = Wait(4f); while (w.MoveNext()) yield return null;
    }

    // ── 5. 50살 시야 제한 ────────────────────────────────────────────────
    private static IEnumerator TestSpotlight()
    {
        var p = Prep(9, 1); while (p.MoveNext()) yield return null;
        var go = GameObject.Find("SpotlightOverlay");
        if (go == null) { Report("5 준비", false, "SpotlightOverlay 없음"); yield break; }
        var mat = go.GetComponent<MeshRenderer>().sharedMaterial;

        var w = Wait(3.0f); while (w.MoveNext()) yield return null; // 이전 동작이면 2.5초에 완전 어둠
        float before = mat.GetFloat("_DarknessAlpha");
        Capture("v1115-spotlight-before-throw.png");
        Report("5-1 뿌리기 후 3초, 던지기 전 → 밝음", before < 0.01f && GM.CurrentPhase == GameManager.GamePhase.PickThrowStone,
               $"alpha={before:F2} phase={GM.CurrentPhase} (이전 동작이면 1.00)");

        var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        float tThrow = Time.time;
        // 매 프레임 (경과, 알파)를 기록 — 백그라운드 저fps라 특정 시점 1점 샘플은 못 믿는다
        var samples = new List<string>();
        float firstFull = -1f, firstPartial = -1f;
        while (Time.time - tThrow < 0.8f)
        {
            float a = mat.GetFloat("_DarknessAlpha"), e = Time.time - tThrow;
            samples.Add($"{e:F2}:{a:F2}");
            if (a > 0.01f && a < 0.99f && firstPartial < 0f) firstPartial = e;
            if (a > 0.99f && firstFull < 0f) firstFull = e;
            yield return null;
        }
        Capture("v1115-spotlight-after-throw.png");
        // 페이드는 Time.time 기준 0.3초 선형 — 알파가 경과/0.3과 맞는지로 본다
        Report("5-2 던지기 순간부터 어둠 시작, 0.3초 페이드로 완전 어둠",
               firstFull > 0f && firstFull <= 0.3f + 0.15f,
               $"firstPartial={firstPartial:F2}s firstFull={firstFull:F2}s samples=[{string.Join(" ", samples.Take(10))}]");
        w = Wait(4f); while (w.MoveNext()) yield return null;
    }

    private static void Capture(string file)
    {
        var cam = Camera.main;
        if (cam == null) return;
        const int W = 1280, H = 720;
        var rt = new RenderTexture(W, H, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prevActive;
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/v1115");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
