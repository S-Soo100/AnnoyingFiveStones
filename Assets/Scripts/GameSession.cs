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
    [SerializeField] private int currentAge = 0;
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

    /// <summary>50살 도달 시 게임 클리어</summary>
    public bool IsGameClear => currentAge >= 50;

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
    /// 단계 클리어 처리. 나이++, 5단 완료 시 루프 증가.
    /// 순서: age++ → IsGameClear 체크 → false면 5단 시 loop++, stageInLoop=1
    /// </summary>
    public void OnStageComplete(int completedStage)
    {
        if (completedStage == 5) currentAge += 5;
        currentStageInLoop = completedStage;

        // IsGameClear (age >= 50) 이면 루프/단계 변경 없음 (게임 종료)
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
        Debug.Log($"[GameSession] Failed. Age={currentAge}, Loop={currentLoop} (unchanged). Reset to stage 1. Regression={regressionCount}");
    }

    /// <summary>
    /// 전체 초기화 (ALL CLEAR 후 재시작).
    /// </summary>
    public void ResetAll()
    {
        PlayerName = "Player";
        IsTestPlay = false;
        currentAge = 0;
        currentLoop = 1;
        currentStageInLoop = 1;
        elapsedTime = 0f;
        isRecordMode = false;
        regressionCount = 0;
        Debug.Log("[GameSession] ResetAll called.");
    }
}
