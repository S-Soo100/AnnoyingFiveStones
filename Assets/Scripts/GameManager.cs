using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public enum GamePhase
    {
        Scatter,        // 뿌리기 (게이지 조절 → 돌 산개)
        PickThrowStone, // 던질 돌 고르기 (커서로 1개 자동 줍기)
        Throw,          // 던지기 (클릭/탭 → 돌이 하늘로)
        PickStones,     // 바닥 돌 줍기 (커서 자동 줍기)
        Catch,          // 떨어지는 돌 받기
        Stage5Throw,    // 5단: 5개 돌 동시 던지기 대기 (클릭으로 시작)
        Stage5Catch,    // 5단: 5개 돌 동시 받기 (손등 또는 손바닥)
        StageComplete,  // 단계 클리어
        Failed          // 실패 → 1단 리셋
    }

    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private int currentStage = 1;
    [SerializeField] private GamePhase currentPhase = GamePhase.Scatter;

    [Header("References (auto-resolved at runtime)")]
    private Stone[] stones;
    private Transform boardTransform;
    private ScatterSystem scatterSystem;
    private HandController handController;
    private CatchSystem catchSystem;

    [Header("Settings")]
    [SerializeField] private float baseHangTime = 5f; // 기본 체공 시간

    [Header("Transition Timing")]
    [SerializeField] private float stageIntroDuration = 1.2f;
    [SerializeField] private float stage5IntroDuration = 2.0f;
    [SerializeField] private float clearDuration = 1.5f;
    [SerializeField] private float failDuration = 1.5f;

    private int stage5Step; // 0 = 1차(손등받기), 1 = 2차(손바닥받기)
    private bool isTransitioning;
    private bool isAllClear;
    private string lastFailReason = "";
    private Coroutine transitionCoroutine;

    // 연출 타이밍 (DebugHUD에서 읽음)
    private float transitionStartTime;
    private string transitionType = ""; // "stage_intro", "clear", "fail", "all_clear"

    public int CurrentStage => currentStage;
    public GamePhase CurrentPhase => currentPhase;
    public Stone[] Stones => stones;
    public Transform BoardTransform => boardTransform;
    public int Stage5Step => stage5Step;
    public bool IsTransitioning => isTransitioning;
    public bool IsAllClear => isAllClear;
    public string LastFailReason => lastFailReason;
    public float TransitionStartTime => transitionStartTime;
    public string TransitionType => transitionType;

    /// <summary>
    /// 현재 단계에서 한 번에 주워야 할 돌 수
    /// </summary>
    public int RequiredPickCount
    {
        get
        {
            return currentStage switch
            {
                1 => 1,
                2 => 2,
                3 => 3, // 3단은 3+1 또는 1+3 (첫 회차 기준 3)
                4 => 4,
                _ => 0
            };
        }
    }

    /// <summary>
    /// 현재 단계의 체공 시간
    /// </summary>
    public float HangTime
    {
        get
        {
            return currentStage switch
            {
                1 => baseHangTime * 0.8f,  // 짧게
                2 => baseHangTime,
                3 => baseHangTime,
                4 => baseHangTime * 1.3f,  // 길게
                5 => baseHangTime,
                _ => baseHangTime
            };
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 런타임 참조 자동 해결
        stones = FindObjectsByType<Stone>(FindObjectsSortMode.None);
        boardTransform = GameObject.Find("Cloth")?.transform;
        scatterSystem = GetComponent<ScatterSystem>();
        handController = FindFirstObjectByType<HandController>();
        catchSystem = FindFirstObjectByType<CatchSystem>();

        // Stone 인덱스 초기화
        for (int i = 0; i < stones.Length; i++)
        {
            stones[i].Initialize(i);
        }

        StartStage(1);
    }

    public void SetFailReason(string reason)
    {
        lastFailReason = reason;
    }

    public void StartStage(int stage)
    {
        // 진행 중인 전환 코루틴 중단
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        currentStage = stage;
        isAllClear = false;

        // 모든 돌 활성화 + 초기 상태 복원
        ResetAllStones();

        TestLogger.Instance?.LogStageChange(stage);
        TestLogger.Instance?.BeginStageAttempt(stage);

        // 시작 연출 → 딜레이 후 실제 게임 진입
        transitionCoroutine = StartCoroutine(DoStageIntro(stage));
    }

    private IEnumerator DoStageIntro(int stage)
    {
        isTransitioning = true;
        transitionType = "stage_intro";
        transitionStartTime = Time.time;

        // 사운드: 단계 인트로
        if (stage == 5)
            AudioManager.Instance?.PlayStage5Intro();
        else
            AudioManager.Instance?.PlayStageIntro();

        float duration = stage == 5 ? stage5IntroDuration : stageIntroDuration;
        yield return new WaitForSeconds(duration);

        isTransitioning = false;
        transitionType = "";
        transitionCoroutine = null;

        // 실제 게임 시작
        if (stage <= 4)
        {
            SetPhase(GamePhase.Scatter);
        }
        else if (stage == 5)
        {
            stage5Step = 0;

            foreach (var stone in stones)
            {
                stone.SetState(Stone.State.InHand);
                stone.Rb.isKinematic = true;
                stone.Rb.useGravity = false;
                stone.transform.SetParent(handController.transform);
                stone.transform.localPosition = Vector3.zero;
            }

            Debug.Log("[GameManager] Stage 5 (꺾기) started! Click to toss all 5 stones.");
            SetPhase(GamePhase.Stage5Throw);
        }

        Debug.Log($"[GameManager] Stage {stage} started. Phase: {currentPhase}");
    }

    public void SetPhase(GamePhase phase)
    {
        var prevPhase = currentPhase;
        currentPhase = phase;
        TestLogger.Instance?.LogPhaseChange(prevPhase.ToString(), phase.ToString());
        Debug.Log($"[GameManager] Phase changed to: {phase}");

        switch (phase)
        {
            case GamePhase.Scatter:
                handController.ResetHand();
                scatterSystem.BeginScatter();
                break;
            case GamePhase.Catch:
                // BeginCatch는 HandController.DoThrow에서 직접 호출
                break;
            case GamePhase.Stage5Throw:
                handController.BeginStage5Throw();
                break;
            case GamePhase.Stage5Catch:
                // 코루틴이 이미 진행 중 — 별도 처리 불필요
                break;
            case GamePhase.Failed:
                OnFailed();
                break;
            case GamePhase.StageComplete:
                OnStageComplete();
                break;
        }
    }

    private void OnFailed()
    {
        Debug.Log("[GameManager] FAILED! Resetting to Stage 1.");
        ResetAllStones();
        transitionCoroutine = StartCoroutine(DoFailTransition());
    }

    private IEnumerator DoFailTransition()
    {
        isTransitioning = true;
        transitionType = "fail";
        transitionStartTime = Time.time;

        AudioManager.Instance?.PlayFail();

        yield return new WaitForSeconds(failDuration);

        isTransitioning = false;
        transitionType = "";
        transitionCoroutine = null;

        StartStage(1);
    }

    private void ResetAllStones()
    {
        handController.ResetHand();
        foreach (var stone in stones)
        {
            stone.gameObject.SetActive(true); // 비활성화된 돌 복원
            stone.transform.SetParent(null);
            stone.SetState(Stone.State.OnBoard);
            stone.Rb.linearVelocity = Vector3.zero;
            stone.Rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 5단 내부 단계 진행: 손등 받기 성공 → 2차 던지기로
    /// </summary>
    public void AdvanceStage5Step()
    {
        stage5Step = 1;
        Debug.Log("[GameManager] Stage 5 step advanced to palm catch.");
    }

    private void OnStageComplete()
    {
        TestLogger.Instance?.CompleteStageAttempt();

        int nextStage = currentStage + 1;
        if (nextStage > 5)
        {
            Debug.Log("[GameManager] ALL STAGES CLEARED!");
            transitionCoroutine = StartCoroutine(DoAllClearTransition());
            return;
        }
        Debug.Log($"[GameManager] Stage {currentStage} complete! Moving to stage {nextStage}.");
        transitionCoroutine = StartCoroutine(DoClearTransition(nextStage));
    }

    private IEnumerator DoClearTransition(int nextStage)
    {
        isTransitioning = true;
        transitionType = "clear";
        transitionStartTime = Time.time;

        AudioManager.Instance?.PlayStageClear();

        yield return new WaitForSeconds(clearDuration);

        isTransitioning = false;
        transitionType = "";
        transitionCoroutine = null;

        StartStage(nextStage);
    }

    private IEnumerator DoAllClearTransition()
    {
        isTransitioning = true;
        isAllClear = true;
        transitionType = "all_clear";
        transitionStartTime = Time.time;

        AudioManager.Instance?.PlayAllClear();

        // ALL CLEAR는 무한 대기 — 탭으로 재시작 (DebugHUD 또는 Update에서 처리)
        yield return null;
        // isTransitioning은 true로 유지
    }

    private InputAction allClearClickAction;

    private void OnEnable()
    {
        allClearClickAction = new InputAction("AllClearClick", InputActionType.Button);
        allClearClickAction.AddBinding("<Mouse>/leftButton");
        allClearClickAction.AddBinding("<Touchscreen>/primaryTouch/press");
        allClearClickAction.performed += OnAllClearClick;
        allClearClickAction.Enable();
    }

    private void OnDisable()
    {
        allClearClickAction.performed -= OnAllClearClick;
        allClearClickAction.Disable();
    }

    private void OnAllClearClick(InputAction.CallbackContext ctx)
    {
        if (!isAllClear) return;

        isAllClear = false;
        isTransitioning = false;
        transitionType = "";
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }
        StartStage(1);
    }
}
