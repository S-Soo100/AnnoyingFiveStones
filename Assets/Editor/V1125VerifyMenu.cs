using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2026-11-25 기획자 피드백 검증 메뉴. MCP execute_menu_item로 구동(Play 중).
/// 콘솔에 [V1125] PASS/FAIL 과 진단 로그를 찍는다.
/// </summary>
public static class V1125VerifyMenu
{
    private static IEnumerator routine;
    private static readonly List<string> logs = new List<string>();
    private static int pass, fail;

    private static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++; else fail++;
        Debug.Log($"[V1125] {(ok ? "PASS" : "FAIL")} — {name} ({detail})");
    }

    private static void Start(IEnumerator r)
    {
        if (!Application.isPlaying) { Debug.LogError("[V1125] Play 중에만 동작"); return; }
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
        if (msg.StartsWith("[V1125]")) return;
        if (msg.StartsWith("[GameManager]") || msg.StartsWith("[MonochromeGimmick]") || msg.StartsWith("[CatchSystem]")
            || msg.StartsWith("[Nak") || msg.StartsWith("[Hand] Airtime") || t == LogType.Error || t == LogType.Exception)
            logs.Add($"{Time.time:F2} {msg.Split('\n')[0]}");
    }

    // ── 0. 55살(저승사자) 분신 15개 직후 낙 재현 ──────────────────────────
    [MenuItem("Tools/검증 1125/0 저승사자 분신 낙 재현")]
    public static void RunReaper() => Start(Wrap(Reaper()));

    private static IEnumerator Wrap(IEnumerator body)
    {
        pass = fail = 0;
        logs.Clear();
        Application.logMessageReceived += OnLog;
        while (true)
        {
            bool more;
            try { more = body.MoveNext(); }
            catch (System.Exception e) { Report("예외", false, e.ToString()); break; }
            if (!more) break;
            yield return null;
        }
        Application.logMessageReceived -= OnLog;
        Hand?.ClearAllOverrides();
        Debug.Log($"[V1125] SUMMARY pass={pass} fail={fail}");
    }

    // 실제 순서: 15살(돌 18개)·35살(10개)에서 쓰고 버려진 풀 돌이 55살 분신으로 다시 나온다.
    [MenuItem("Tools/검증 1125/0b 15살 낙 뒤 저승사자 분신")]
    public static void RunReaperAfterUse() => Start(Wrap(ReaperAfterUse()));

    private static IEnumerator ReaperAfterUse()
    {
        for (int trial = 0; trial < 3; trial++)
        {
            // 15살 1단 — 세게 뿌려 여러 개가 보드 밖으로 낙(물리 낙하)하게 둔다
            var p0 = Prep(2, 1, 0.98f, 31 + trial); while (p0.MoveNext()) yield return null;
            var w0 = Wait(2.5f); while (w0.MoveNext()) yield return null;
            var pool = StonePool.Instance;
            var stale = Object.FindObjectsByType<Stone>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                              .Where(st => st.StoneIndex >= 5).OrderBy(st => st.StoneIndex).ToList();
            Debug.Log($"[V1125] AFTER15 trial={trial} phase={GM.CurrentPhase} " + string.Join(" ", stale.Take(18).Select(st =>
                $"#{st.StoneIndex}:{st.CurrentState}{(st.gameObject.activeSelf ? "" : "(off)")} v={st.Rb.linearVelocity.magnitude:F1} kin={st.Rb.isKinematic} g={st.Rb.useGravity} vis={st.GetComponent<Renderer>().enabled}")));

            var p = Prep(10, 1, 0.45f, 12); while (p.MoveNext()) yield return null;
            logs.Clear();
            var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
            yield return null;
            var add = OnBoard().Where(st => st.StoneIndex >= 5).ToList();
            Debug.Log($"[V1125] SPAWN trial={trial} " + string.Join(" ", add.Select(st =>
                $"#{st.StoneIndex} v={st.Rb.linearVelocity.magnitude:F1} kin={st.Rb.isKinematic} g={st.Rb.useGravity} vis={st.GetComponent<Renderer>().enabled} scale={st.transform.localScale.x:F2}")));
            var w = Wait(1.0f); while (w.MoveNext()) yield return null;
            foreach (var l in logs.Take(40)) Debug.Log($"[V1125] LOG {l}");
            bool nak = GM.IsNakPending || GM.CurrentPhase == GameManager.GamePhase.Failed;
            Report($"0b 15살 낙 뒤 분신 직후 1초 안에 낙 없음 trial={trial}", !nak, $"phase={GM.CurrentPhase} nak={GM.IsNakPending}");
            var w2 = Wait(4f); while (w2.MoveNext()) yield return null;
        }
    }

    // 쓸어담기(누른 뒤 2초) 중 타겟 → 다른 타겟으로 끌면 그 사이 다른 숫자 돌을 스치는가
    [MenuItem("Tools/검증 1125/0c 저승사자 타겟 사이 쓸기")]
    public static void RunReaperSweep() => Start(Wrap(ReaperSweep()));

    private static IEnumerator ReaperSweep()
    {
        int failed = 0, ok = 0;
        for (int trial = 0; trial < 6; trial++)
        {
            var p = Prep(10, 1, 0.45f, 11 + trial); while (p.MoveNext()) yield return null;
            var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
            yield return null;
            var gim = GM.CurrentGimmick;
            var board = OnBoard();
            var targets = board.Where(st => gim.ValidatePick(st)).ToList();
            // 손에서 가장 가까운 타겟 → 그 타겟에서 가장 가까운 다른 타겟
            Vector3 hand = Hand.DebugPalmCenter;
            var a = targets.OrderBy(st => Vector2.Distance(st.transform.position, hand)).First();
            var b = targets.Where(st => st != a).OrderBy(st => Vector2.Distance(st.transform.position, a.transform.position)).First();
            PlacePalm(a.transform.position); yield return null;
            Hand.DebugPress(); yield return null;
            var d = Drag(a.transform.position, b.transform.position, 12); while (d.MoveNext()) yield return null;
            yield return null;
            Hand.DebugRelease(); yield return null;
            bool fail = GM.CurrentPhase == GameManager.GamePhase.Failed;
            if (fail) failed++; else ok++;
            Debug.Log($"[V1125] SWEEP trial={trial} targets={targets.Count} dist={Vector2.Distance(a.transform.position, b.transform.position):F2} picked={Hand.PickedStones.Count} phase={GM.CurrentPhase} reason={(fail ? "fail" : "-")}");
            var w = Wait(3.5f); while (w.MoveNext()) yield return null;
        }
        Debug.Log($"[V1125] SWEEP RESULT 가장 가까운 타겟 둘 사이를 누른 채 끌기: 실패 {failed} / 성공 {ok}");
    }

    private static readonly float[] ReaperGauges = { 0.25f, 0.45f, 0.65f, 0.85f };

    private static IEnumerator Reaper()
    {
        for (int trial = 0; trial < 8; trial++)
        {
            int seed = 11 + trial;
            float gauge = ReaperGauges[trial % ReaperGauges.Length];
            var p = Prep(10, 1, gauge, seed); while (p.MoveNext()) yield return null;
            int before = OnBoard().Count;
            logs.Clear();
            var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
            // 분신 15개가 나온 직후 상태 — 낙 판정에 걸리는 돌 / 서로 겹친 돌 / 밀려나는 돌
            yield return null;
            var board = OnBoard();
            var start = board.ToDictionary(st => st, st => (Vector2)st.transform.position);
            var outs = board.Where(st => BoardBounds.IsNakInPlay((Vector2)st.transform.position)).ToList();
            int overlaps = 0; float minD = float.MaxValue;
            for (int a = 0; a < board.Count; a++)
                for (int b = a + 1; b < board.Count; b++)
                {
                    float d = Vector2.Distance(start[board[a]], start[board[b]]);
                    minD = Mathf.Min(minD, d);
                    if (d < 0.45f) overlaps++;
                }
            Debug.Log($"[V1125] DIAG trial={trial} seed={seed} g={gauge:F2} before={before} after={board.Count} nakInPlayNow={outs.Count} overlaps<0.45={overlaps} minDist={minD:F2} " +
                      string.Join(" ", outs.Select(st => $"#{st.StoneIndex}({st.transform.position.x:F2},{st.transform.position.y:F2}) kin={st.Rb.isKinematic}")));
            var w = Wait(1.0f); while (w.MoveNext()) yield return null;
            var moved = start.Where(kv => kv.Key != null && kv.Key.gameObject.activeSelf
                                          && Vector2.Distance(kv.Value, (Vector2)kv.Key.transform.position) > 0.05f)
                             .Select(kv => $"#{kv.Key.StoneIndex} {kv.Key.CurrentState} {Vector2.Distance(kv.Value, (Vector2)kv.Key.transform.position):F2} kin={kv.Key.Rb.isKinematic}");
            Debug.Log($"[V1125] MOVED trial={trial} " + string.Join(" | ", moved));
            foreach (var l in logs.Take(40)) Debug.Log($"[V1125] LOG {l}");
            bool nak = GM.IsNakPending || GM.CurrentPhase == GameManager.GamePhase.Failed;
            Report($"0 분신 직후 1초 안에 낙 없음 trial={trial} seed={seed} g={gauge:F2}", !nak, $"phase={GM.CurrentPhase} nak={GM.IsNakPending}");
            var w2 = Wait(4f); while (w2.MoveNext()) yield return null;
        }
    }

    // ── 1~3. 튕김 재포획 / 꺾기 높이 / 뿌리기 최소 누름 ─────────────────────
    [MenuItem("Tools/검증 1125/1 튕김·꺾기·뿌리기 전체")]
    public static void RunFixes() => Start(Wrap(Fixes()));

    private static IEnumerator Fixes()
    {
        IEnumerator[] tests = { Recatch(0), Recatch(1), Recatch(2), Stage5Height(), ScatterMinHold() };
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
            Hand?.ClearAllOverrides();
        }
    }

    /// <summary>mode 0 = 튕긴 돌이 내려올 때 손바닥을 밑에 댄다 → 받기 성공
    /// mode 1 = 손을 반대쪽으로 치운다 → 떨어져 실패
    /// mode 2 = 튕기는 순간 손바닥이 돌과 겹친 채 가만히 → 0.15초 안에 저절로 받기가 되면 안 된다</summary>
    private static IEnumerator Recatch(int mode)
    {
        string tag = mode == 0 ? "따라가 손바닥" : mode == 1 ? "손 치움" : "튕기는 순간 겹침";
        var p = Prep(1, 1); while (p.MoveNext()) yield return null;
        var th = ThrowFromPick(); while (th.MoveNext()) yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.PickStones) { Report($"1 준비 {tag}", false, $"phase={GM.CurrentPhase}"); yield break; }
        var stone = Hand.ThrowStone;
        var solo = SoloStones(OnBoard());
        if (solo.Count == 0) { Report($"1 준비 {tag}", false, "줍을 돌 없음"); yield break; }
        PlacePalm(solo[0].transform.position); yield return null;
        Hand.DebugPress(); yield return null; yield return null;
        Hand.DebugRelease(); yield return null;

        // 하늘로 — 돌에서 옆으로 3 비켜 서서(낙하 판정에 안 걸리게) 내려오기를 기다린다
        var cs = Object.FindFirstObjectByType<CatchSystem>();
        float skyY = BoardBounds.SkyFloorY + 1.5f;
        float t0 = Time.time;
        while (Time.time - t0 < 5f)
        {
            PlacePalm(new Vector3(stone.transform.position.x + 3f, skyY, 0f));
            if (cs.IsCatchPhase && stone.transform.position.y < skyY + 1.0f) break;
            yield return null;
        }
        if (!cs.IsCatchPhase) { Report($"1 준비 {tag}", false, $"받기 페이즈 아님 phase={GM.CurrentPhase}"); yield break; }

        // 손은 돌 오른쪽 3에 있다 → 왼쪽 위로 튕겨 손에서 멀어지게 (손 콜라이더와 계속 겹치면 물리가 돌을 날려 버린다)
        Vector3 handRest = Hand.transform.position;
        bool bounced = Hand.DebugFingerBounceNow(new Vector3(-1f, 1f, 0f));
        if (!bounced) { Report($"1 준비 {tag}", false, "튕김 실패"); yield break; }
        // 2026-11-16 허점 재현: 튕기는 그 프레임에 손바닥이 돌과 겹쳐 있다
        if (mode == 2) PlacePalm(stone.transform.position);
        float tb = Time.time;
        float nextLog = 0f;

        if (mode == 2)
        {
            while (Time.time - tb < 0.15f) yield return null;   // 손은 그 자리
            bool instant = GM.CurrentPhase == GameManager.GamePhase.Throw;
            Report($"1c 튕기는 순간 겹쳐도 저절로 받기 안 됨", !instant, $"phase={GM.CurrentPhase} state={stone.CurrentState}");
            var wx = Wait(3f); while (wx.MoveNext()) yield return null;
            yield break;
        }

        float maxVy = float.MinValue;
        bool placedUnder = false;
        while (Time.time - tb < 3f && GM.CurrentPhase == GameManager.GamePhase.PickStones && !GM.IsNakPending)
        {
            var sp = stone.transform.position;
            float vy = stone.Rb.linearVelocity.y;
            maxVy = Mathf.Max(maxVy, vy);
            if (Time.time - tb >= nextLog)
            {
                nextLog += 0.15f;
                Debug.Log($"[V1125] TRAJ {tag} t={Time.time - tb:F2} pos=({sp.x:F2},{sp.y:F2},{sp.z:F2}) v={stone.Rb.linearVelocity:F1} kin={stone.Rb.isKinematic} g={stone.Rb.useGravity} layer={stone.gameObject.layer} col={stone.GetComponent<Collider>().enabled} hand=({Hand.transform.position.x:F2},{Hand.transform.position.y:F2}) catchMode={Hand.IsCatchMode} dt={Time.deltaTime:F3}");
            }
            // 손은 튕긴 자리 오른쪽에 그대로 두고(따라가 붙어 있으면 손 콜라이더가 돌을 밀어낸다),
            // mode 0만 돌이 내려오기 시작하면 그 밑에 손바닥을 댄다
            // (플레이어처럼) 내려오기 시작하면 손을 같은 높이에서 옆으로 옮겨 돌 밑을 따라간다
            if (mode == 0 && (placedUnder || (vy < 0f && Time.time - tb > 0.25f)))
            {
                PlacePalm(new Vector3(sp.x, skyY, 0f));
                placedUnder = true;
            }
            else Hand.DebugPlaceHand(handRest);
            yield return null;
        }
        bool caught = GM.CurrentPhase == GameManager.GamePhase.Throw;
        string detail = $"phase={GM.CurrentPhase} nak={GM.IsNakPending} state={stone.CurrentState} maxVy={maxVy:F2} under={placedUnder} t={Time.time - tb:F2}";
        if (mode == 0) Report("1a 튕긴 돌을 손바닥으로 다시 받으면 성공", caught, detail);
        else Report("1b 튕긴 돌을 안 받으면 떨어져 실패", !caught && (GM.IsNakPending || GM.CurrentPhase == GameManager.GamePhase.Failed), detail);
        var w = Wait(4f); while (w.MoveNext()) yield return null;
    }

    /// <summary>5단 던지기: 게이지 0 / 0.5 / 1 모두 돌 전체가 화면 위 끝 안에 머문다.</summary>
    private static IEnumerator Stage5Height()
    {
        var cam = Camera.main;
        float top = cam.transform.position.y + cam.orthographicSize;
        float[] gauges = { 0f, 0.5f, 1f, 1f };
        foreach (float g in gauges)
        {
            var s = GameSession.Instance;
            s.CurrentLoop = 1; s.CurrentAge = 10;
            GM.StartStage(5);
            float t0 = Time.time;
            while (!Hand.DebugStage5Release(g) && Time.time - t0 < 8f) yield return null;
            if (Time.time - t0 >= 8f) { Report("2 5단 게이지 대기", false, $"phase={GM.CurrentPhase}"); yield break; }
            var stones = GM.Stones.Where(st => st != null && st.gameObject.activeSelf).ToList();
            float maxTop = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            float t1 = Time.time;
            while (Time.time - t1 < 1.6f)   // 올라가기 + 최고점
            {
                foreach (var st in stones)
                {
                    var b = st.GetComponent<Renderer>().bounds;
                    maxTop = Mathf.Max(maxTop, b.max.y);
                    minX = Mathf.Min(minX, b.center.x); maxX = Mathf.Max(maxX, b.center.x);
                }
                yield return null;
            }
            Report($"2 꺾기 게이지 {g:F1}: 모든 돌이 화면 안 (위 끝 {top:F2})", maxTop < top,
                   $"가장 높은 돌 윗변={maxTop:F2} 화면 좌우 폭={maxX - minX:F2}");
            var w = Wait(5f); while (w.MoveNext()) yield return null;
        }
    }

    /// <summary>뿌리기: 0.12초 누르고 떼면 뿌려진다 / 0.05초면 취소(재대기). 게이지 속도 씬 값 1.1.</summary>
    private static IEnumerator ScatterMinHold()
    {
        var scatter = Object.FindFirstObjectByType<ScatterSystem>();
        var so = new SerializedObject(scatter);
        float speed = so.FindProperty("gaugeSpeed").floatValue;
        Report("3 게이지 속도 씬 값 1.1", Mathf.Approximately(speed, 1.1f), $"gaugeSpeed={speed}");

        // 백그라운드 에디터는 한 프레임이 0.1초를 넘기도 해 0.05초 누름을 못 만든다 → 이 검사 동안만 프레임 시간을 0.01초로 고정
        float[] holds = { 0.05f, 0.12f };
        foreach (float hold in holds)
        {
            Time.captureDeltaTime = 0f;
            var s = GameSession.Instance;
            s.CurrentLoop = 1; s.CurrentAge = 10;
            GM.StartStage(1);
            var w = WaitScatterReady(); while (w.MoveNext()) yield return null;
            var wr = Wait(0.3f); while (wr.MoveNext()) yield return null;
            var c = BoardSpace.ToScreen(Vector2.zero, 0f);
            Hand.DebugPlaceHand(new Vector3(c.x, c.y, 0f));
            Time.captureDeltaTime = 0.01f;
            yield return null;
            scatter.DebugGaugeHold(0.11f);
            float t0 = Time.time;
            while (Time.time - t0 < hold - 0.001f) yield return null;
            scatter.DebugGaugeRelease();
            Debug.Log($"[V1125] HOLD 실제 누른 게임 시간 {Time.time - t0:F3}s");
            Time.captureDeltaTime = 0f;
            var w2 = Wait(1.6f); while (w2.MoveNext()) yield return null;
            bool scattered = GM.CurrentPhase != GameManager.GamePhase.Scatter;
            if (hold < 0.1f) Report($"3 {hold:F2}초 누름 → 취소(안 뿌려짐)", !scattered, $"phase={GM.CurrentPhase}");
            else Report($"3 {hold:F2}초 누름 → 뿌려짐", scattered, $"phase={GM.CurrentPhase}");
            if (!scattered) { scatter.DebugScatterNow(0.3f); var w3 = Wait(1.5f); while (w3.MoveNext()) yield return null; }
        }
    }

    // ── 공통 (V1115VerifyMenu와 같은 방식) ─────────────────────────────────
    private static GameManager GM => GameManager.Instance;
    private static HandController Hand => Object.FindFirstObjectByType<HandController>(FindObjectsInactive.Include);

    private static IEnumerator Wait(float sec) { float t0 = Time.time; while (Time.time - t0 < sec) yield return null; }

    private static IEnumerator WaitScatterReady()
    {
        float t0 = Time.time;
        while ((GM.CurrentPhase != GameManager.GamePhase.Scatter || GM.IsTransitioning) && Time.time - t0 < 10f) yield return null;
    }

    private static IEnumerator Prep(int loop, int stage, float gauge = 0.45f, int seed = 7)
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
        Object.FindFirstObjectByType<ScatterSystem>().DebugScatterSeeded(gauge, seed, new Vector2(0.5f, 0.5f));
        float t0 = Time.time;
        while (GM.CurrentPhase == GameManager.GamePhase.Scatter && Time.time - t0 < 6f) yield return null;
        yield return null;
    }

    private static List<Stone> OnBoard()
        => GM.Stones.Where(st => st != null && st.gameObject.activeSelf && st.CurrentState == Stone.State.OnBoard).ToList();

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

    private static IEnumerator Drag(Vector3 from, Vector3 to, int frames)
    {
        for (int i = 1; i <= frames; i++)
        {
            PlacePalm(Vector3.Lerp(from, to, i / (float)frames));
            yield return null;
        }
    }

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

    private static IEnumerator ThrowFromPick()
    {
        var solo = SoloStones(OnBoard());
        if (solo.Count == 0) { Report("던질 돌 찾기", false, "단독으로 덮이는 돌 없음"); yield break; }
        PlacePalm(solo[0].transform.position);
        yield return null;
        Hand.DebugPress(); yield return null; yield return null;
        Hand.DebugRelease(); yield return null;
        if (GM.CurrentPhase != GameManager.GamePhase.Throw) { Report("던질 돌 누르기 → Throw", false, $"phase={GM.CurrentPhase}"); yield break; }
        Hand.DebugClick(); yield return null;
    }
}
