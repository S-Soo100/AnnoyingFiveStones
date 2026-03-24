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
    private GameSession session;

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
    private bool isPaused;                // [P4]
    private string lastFailReason = "";
    private Coroutine transitionCoroutine;
    private InputAction escAction;        // [P4]

    public int CurrentStage => currentStage;
    public GamePhase CurrentPhase => currentPhase;
    public Stone[] Stones => stones;
    public Transform BoardTransform => boardTransform;
    public int Stage5Step => stage5Step;
    public bool IsTransitioning => isTransitioning;
    public bool IsAllClear => isAllClear;
    public bool IsPaused => isPaused;     // [P4]
    public string LastFailReason => lastFailReason;

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

        // [P1] GameSession 자동 생성/참조
        if (GameSession.Instance == null)
            new GameObject("GameSession").AddComponent<GameSession>();
        session = GameSession.Instance;

        // [P1] SidePanelUI 자동 생성/참조
        if (SidePanelUI.Instance == null)
            new GameObject("SidePanelUI").AddComponent<SidePanelUI>();

        // [P4] 창모드 설정
        Screen.SetResolution(1280, 720, false);

        // [P4] PauseMenuUI 자동 생성
        if (PauseMenuUI.Instance == null)
            new GameObject("PauseMenuUI").AddComponent<PauseMenuUI>();

        StartStage(1);
    }

    private void Update()
    {
        // [P1] 경과 시간 갱신: 전환 중이 아닐 때만
        if (session != null && !isTransitioning && !isAllClear)
        {
            session.ElapsedTime += Time.deltaTime;
        }
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

        // [P1] 세션 단계 갱신 + 사이드 패널 반영
        if (session != null)
            session.CurrentStageInLoop = stage;
        SidePanelUI.Instance?.Refresh();

        // 게이지 강제 리셋 (스킵 등으로 Scatter 중간에 전환 시)
        scatterSystem.ResetGauge();

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

        // 사운드: 단계 인트로
        if (stage == 5)
            AudioManager.Instance?.PlayStage5Intro();
        else
            AudioManager.Instance?.PlayStageIntro();

        // UI 연출 시작
        GameUI.Instance?.ShowStageIntro(stage);
        GameUI.Instance?.UpdateProgressDots(stage);

        float duration = stage == 5 ? stage5IntroDuration : stageIntroDuration;
        yield return new WaitForSeconds(duration);

        isTransitioning = false;
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

        // UI 가이드 텍스트 푸시
        PushGuideText(phase);

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

    private void PushGuideText(GamePhase phase)
    {
        if (GameUI.Instance == null) return;

        string guide = phase switch
        {
            GamePhase.Scatter => "[ 꾹 눌러서 게이지 조절, 놓으면 뿌리기 ]",
            GamePhase.PickThrowStone => "[ 커서를 돌 위로 이동 ]",
            GamePhase.Throw => "[ 클릭하여 던지기 ]",
            GamePhase.PickStones => $"[ 돌 {RequiredPickCount}개를 주우세요 ]",
            GamePhase.Catch => "[ 커서를 움직여 돌을 받으세요! ]",
            GamePhase.Stage5Throw => "[ 클릭하여 5개 모두 던지기! ]",
            GamePhase.Stage5Catch => stage5Step == 0
                ? "[ 손등으로 5개 모두 받기! ]"
                : "[ 뒤집어서 손바닥으로 받기! ]",
            _ => null
        };

        if (guide != null)
            GameUI.Instance.UpdateGuideText(guide);
        else
            GameUI.Instance.HideGuideText();
    }

    private void OnFailed()
    {
        Debug.Log("[GameManager] FAILED! Resetting to Stage 1.");

        // [P1] 세션: 해당 루프 1단으로 리셋 (나이/루프 유지)
        session?.OnFail();
        SidePanelUI.Instance?.Refresh();

        ResetAllStones();
        transitionCoroutine = StartCoroutine(DoFailTransition());
    }

    private IEnumerator DoFailTransition()
    {
        isTransitioning = true;

        AudioManager.Instance?.PlayFail();
        GameUI.Instance?.ShowFail(lastFailReason);

        yield return new WaitForSeconds(failDuration);

        isTransitioning = false;
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

        // [P1] 세션 상태 갱신 (나이++, 5단이면 루프++)
        session?.OnStageComplete(currentStage);
        SidePanelUI.Instance?.Refresh();

        int nextStage = currentStage + 1;
        if (nextStage > 5)
        {
            // [P1] 50살(10루프 5단) 완료 시 게임 클리어
            if (session != null && session.IsGameClear)
            {
                Debug.Log("[GameManager] GAME CLEAR! Age 50 reached!");
                transitionCoroutine = StartCoroutine(DoAllClearTransition());
                return;
            }

            // [P1] 5단 클리어 + 아직 50살 미만 → 다음 루프 1단 시작
            Debug.Log($"[GameManager] Loop {session?.CurrentLoop} started! (Age={session?.CurrentAge})");
            transitionCoroutine = StartCoroutine(DoClearTransition(1));
            return;
        }

        Debug.Log($"[GameManager] Stage {currentStage} complete! Moving to stage {nextStage}.");
        transitionCoroutine = StartCoroutine(DoClearTransition(nextStage));
    }

    private IEnumerator DoClearTransition(int nextStage)
    {
        isTransitioning = true;

        AudioManager.Instance?.PlayStageClear();
        GameUI.Instance?.ShowClear();

        yield return new WaitForSeconds(clearDuration);

        isTransitioning = false;
        transitionCoroutine = null;

        StartStage(nextStage);
    }

    private IEnumerator DoAllClearTransition()
    {
        isTransitioning = true;
        isAllClear = true;

        AudioManager.Instance?.PlayAllClear();
        GameUI.Instance?.ShowAllClear();

        // ALL CLEAR는 무한 대기 — 탭으로 재시작 (OnAllClearClick에서 처리)
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

        // [P4] ESC 입력
        escAction = new InputAction("Escape", InputActionType.Button);
        escAction.AddBinding("<Keyboard>/escape");
        escAction.performed += OnEscPressed;
        escAction.Enable();
    }

    private void OnDisable()
    {
        allClearClickAction.performed -= OnAllClearClick;
        allClearClickAction.Disable();

        // [P4]
        if (escAction != null)
        {
            escAction.performed -= OnEscPressed;
            escAction.Disable();
        }
    }

    // [P4] ESC 콜백
    private void OnEscPressed(InputAction.CallbackContext ctx)
    {
        // ALL CLEAR 화면에서는 ESC 무시
        if (isAllClear) return;

        PauseMenuUI.Instance?.Toggle();
    }

    // [P4] 외부에서 isPaused 설정 (PauseMenuUI 전용)
    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    private void OnAllClearClick(InputAction.CallbackContext ctx)
    {
        if (!isAllClear) return;
        if (isPaused) return; // [P4] 일시정지 중 클릭 무시

        isAllClear = false;
        isTransitioning = false;
        GameUI.Instance?.HideOverlay();
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        // [P1] 세션 전체 초기화
        session?.ResetAll();
        SidePanelUI.Instance?.Refresh();

        StartStage(1);
    }
}
