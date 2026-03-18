using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테스트 로거 싱글톤. 게임 이벤트를 수집하고 세션 데이터를 관리.
/// 에디터/빌드 모두 동일하게 동작.
/// </summary>
public class TestLogger : MonoBehaviour
{
    public static TestLogger Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float positionSnapshotInterval = 0.5f; // 위치 스냅샷 간격 (초)
    [SerializeField] private bool logPositionSnapshots = true;

    private TestSession session;
    private float lastSnapshotTime;

    // 단계별 시도 추적
    private StageAttempt currentAttempt;
    private Dictionary<int, int> stageAttemptCounts = new();

    public TestSession Session => session;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        session = new TestSession();
        session.Initialize();
        Debug.Log($"[TestLogger] Session started: {session.sessionId}");
    }

    private void Update()
    {
        if (!logPositionSnapshots) return;
        if (GameManager.Instance == null) return;

        if (Time.time - lastSnapshotTime >= positionSnapshotInterval)
        {
            lastSnapshotTime = Time.time;
            LogPositionSnapshot();
        }
    }

    private void OnApplicationQuit()
    {
        FinishAndExport();
    }

    private void OnApplicationPause(bool paused)
    {
        // 모바일: 백그라운드 진입 시 저장
        if (paused) FinishAndExport();
    }

    // === Public API: 게임 코드에서 호출 ===

    public void LogPhaseChange(string fromPhase, string toPhase)
    {
        Log(TestEvent.Category.PhaseChange, "phase_change",
            $"from={fromPhase} to={toPhase}");
    }

    public void LogStageChange(int stage)
    {
        session.highestStageReached = Mathf.Max(session.highestStageReached, stage);
        Log(TestEvent.Category.StageChange, "stage_change",
            $"stage={stage}");
    }

    public void LogStoneState(int stoneIndex, string state, Vector3 position)
    {
        Log(TestEvent.Category.StoneState, "stone_state",
            $"stone={stoneIndex} state={state} pos=({position.x:F2},{position.y:F2},{position.z:F2})");
    }

    public void LogInput(string action, Vector2 screenPos, Vector3 worldPos)
    {
        Log(TestEvent.Category.Input, action,
            $"screen=({screenPos.x:F0},{screenPos.y:F0}) world=({worldPos.x:F2},{worldPos.y:F2})");
    }

    public void LogScatter(float gaugePower, float gaugeValue)
    {
        Log(TestEvent.Category.Scatter, "scatter",
            $"power={gaugePower:F2} gauge={gaugeValue:F2}");

        if (currentAttempt != null)
            currentAttempt.scatterPower = gaugePower;
    }

    public void LogCatch(bool success, float remainingTime)
    {
        Log(TestEvent.Category.Catch, success ? "catch_success" : "catch_fail",
            $"remaining_time={remainingTime:F2}");

        if (currentAttempt != null)
            currentAttempt.catchTimeRemaining = remainingTime;
    }

    public void LogFailure(string reason)
    {
        session.totalFailures++;
        if (!session.failureReasons.ContainsKey(reason))
            session.failureReasons[reason] = 0;
        session.failureReasons[reason]++;

        Log(TestEvent.Category.Failure, "failure", $"reason={reason}");

        if (currentAttempt != null)
        {
            currentAttempt.succeeded = false;
            currentAttempt.failReason = reason;
            currentAttempt.endTime = Time.time;
            session.stageAttempts.Add(currentAttempt);
            currentAttempt = null;
        }
    }

    public void LogPhysics(string eventName, string detail)
    {
        Log(TestEvent.Category.Physics, eventName, detail);
    }

    public void BeginStageAttempt(int stage)
    {
        session.totalAttempts++;
        if (!stageAttemptCounts.ContainsKey(stage))
            stageAttemptCounts[stage] = 0;
        stageAttemptCounts[stage]++;

        currentAttempt = new StageAttempt
        {
            stage = stage,
            attemptNumber = stageAttemptCounts[stage],
            startTime = Time.time
        };
    }

    public void CompleteStageAttempt()
    {
        if (currentAttempt == null) return;
        currentAttempt.succeeded = true;
        currentAttempt.endTime = Time.time;
        session.stageAttempts.Add(currentAttempt);
        currentAttempt = null;
    }

    // === Internal ===

    private void Log(TestEvent.Category category, string name, string detail)
    {
        var evt = new TestEvent(category, name, detail);
        session.events.Add(evt);
    }

    private void LogPositionSnapshot()
    {
        var gm = GameManager.Instance;
        if (gm?.Stones == null) return;

        // 돌 위치
        foreach (var stone in gm.Stones)
        {
            if (stone == null) continue;
            var p = stone.transform.position;
            Log(TestEvent.Category.StonePosition, "snapshot",
                $"stone={stone.StoneIndex} state={stone.CurrentState} pos=({p.x:F2},{p.y:F2},{p.z:F2})");
        }

        // 손 위치
        var hand = FindFirstObjectByType<HandController>();
        if (hand != null)
        {
            var hp = hand.transform.position;
            Log(TestEvent.Category.HandPosition, "snapshot",
                $"pos=({hp.x:F2},{hp.y:F2},{hp.z:F2}) onBoard={hand.IsOnBoard}");
        }
    }

    private void FinishAndExport()
    {
        if (session == null) return;
        session.Finish();
        TestReportWriter.WriteReport(session);
        Debug.Log($"[TestLogger] Report exported for session {session.sessionId}");
    }
}
