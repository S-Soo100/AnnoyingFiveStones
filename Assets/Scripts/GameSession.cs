using UnityEngine;

/// <summary>
/// 씬 간 유지되는 게임 세션 데이터.
/// DontDestroyOnLoad 싱글턴. GameManager.Start()에서 런타임 자동 생성.
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("Player Info")]
    public string PlayerName = "Player";
    public bool IsTestPlay = false;

    [Header("Game State")]
    [SerializeField] private int currentAge = 10;
    [SerializeField] private int currentLoop = 1;
    [SerializeField] private int currentStageInLoop = 1;
    [SerializeField] private float elapsedTime = 0f;
    [SerializeField] private bool isRecordMode = false;
    [SerializeField] private int regressionCount = 0;

    // 공개 프로퍼티
    public int CurrentAge
    {
        get => currentAge;
        set => currentAge = value;
    }

    public int CurrentLoop
    {
        get => currentLoop;
        set => currentLoop = value;
    }

    public int CurrentStageInLoop
    {
        get => currentStageInLoop;
        set => currentStageInLoop = value;
    }

    public float ElapsedTime
    {
        get => elapsedTime;
        set => elapsedTime = value;
    }

    public bool IsRecordMode
    {
        get => isRecordMode;
        set => isRecordMode = value;
    }

    public int RegressionCount => regressionCount;

    /// <summary>60살 도달 시 게임 클리어 (Stage 10 Monochrome 완료 후)</summary>
    public bool IsGameClear => currentAge >= 60;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 단계 클리어 처리. 나이+1/단, 5단 완료 시 루프 증가.
    /// 순서: age 갱신 → IsGameClear 체크 → false면 5단 시 loop++, stageInLoop=1
    /// </summary>
    public void OnStageComplete(int completedStage)
    {
        // v19(0825 피드백): 1단마다 1살 — 나이는 "루프 시작 나이 + 클리어한 단 수"로 파생한다.
        // 누적(+1)이 아니라 파생인 이유: 실패 후 재도전하면 같은 단을 다시 깨는데,
        // 누적이면 그때마다 나이를 또 먹어 60살 클리어 판정이 조기 발동한다.
        // 5단 완료 시 base+5 = 다음 루프 시작 나이(StageConfig.Age)와 정확히 이어진다.
        currentAge = LoopBaseAge(currentLoop) + completedStage;
        currentStageInLoop = completedStage;

        // IsGameClear (age >= 60) 이면 루프/단계 변경 없음 (게임 종료)
        if (!IsGameClear && completedStage == 5)
        {
            currentLoop++;
            currentStageInLoop = 1;
        }

        Debug.Log($"[GameSession] Stage {completedStage} complete. Age={currentAge}, Loop={currentLoop}, Stage={currentStageInLoop}, Clear={IsGameClear}");
    }

    /// <summary>
    /// 실패 처리. 나이/루프 유지, 해당 루프 1단 리셋.
    /// </summary>
    public void OnFail()
    {
        regressionCount++;
        currentStageInLoop = 1;
        // v19: 회귀는 이번 루프의 시작 나이로 되돌린다 (단수 파생 구조와 일치).
        currentAge = LoopBaseAge(currentLoop);
        Debug.Log($"[GameSession] Failed. Age={currentAge}, Loop={currentLoop} (loop unchanged). Reset to stage 1. Regression={regressionCount}");
    }

    /// <summary>루프 시작 나이 — SOT는 StageConfig.Age (10, 15, …, 55).</summary>
    private static int LoopBaseAge(int loop)
    {
        var cfg = StageConfig.Get(loop);
        return cfg != null ? cfg.Age : 10 + (loop - 1) * 5;
    }

    /// <summary>
    /// 전체 초기화 (ALL CLEAR 후 재시작).
    /// </summary>
    public void ResetAll()
    {
        PlayerName = "Player";
        IsTestPlay = false;
        currentAge = 10;
        currentLoop = 1;
        currentStageInLoop = 1;
        elapsedTime = 0f;
        isRecordMode = false;
        regressionCount = 0;
        Debug.Log("[GameSession] ResetAll called.");
    }
}
