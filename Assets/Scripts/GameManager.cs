using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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

    // 3단 서브라운드: 첫 줍기 결과에 따라 다음 필요 수량 결정
    // -1 = 아직 첫 줍기 안 함, 1 or 3 = 첫 줍기에서 주운 수
    private int stage3FirstPickCount = -1;

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

    // v4: storyMents 딕셔너리 제거 → StageConfig.Get(stageNumber).StoryMent 로 대체

    // v4: 기믹 필드
    private StageGimmick currentGimmick;
    private StageConfig currentStageConfig;

    private int stage5Step; // 0 = 1차(손등받기), 1 = 2차(손바닥받기)
    private bool isTransitioning;
    private bool isAllClear;
    private bool isPaused;                // [P4]
    private bool isInTitleScreen = true;
    private string lastFailReason = "";
    private Coroutine transitionCoroutine;
    private InputAction escAction;        // [P4]

    public int CurrentStage => currentStage;
    public GamePhase CurrentPhase => currentPhase;
    public Stone[] Stones => stones;
    public StageGimmick CurrentGimmick => currentGimmick;

    /// <summary>StonePool에서 활성 돌을 다시 가져와 stones 갱신</summary>
    public void RefreshStones()
    {
        stones = StonePool.Instance != null ? StonePool.Instance.ActiveStones : stones;
    }

    /// <summary>던질 돌이 공중에 올라갈 때 기믹에 알림 (ColorSelectGimmick 등 추가 돌 스폰용)</summary>
    public void NotifyThrowStart(Stone thrownStone)
    {
        currentGimmick?.OnThrowStart(thrownStone);
        RefreshStones();
    }

    /// <summary>돌을 주울 때 기믹에 알림 (FleeGimmick 등 첫 줍기 트리거용)</summary>
    public void NotifyStonePicked(Stone stone)
    {
        currentGimmick?.OnStonePicked(stone);
    }

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
            if (currentStage == 3)
            {
                // 3단: 첫 줍기 = 1 or 3 자유, 두 번째 = 나머지 (합계 4)
                if (stage3FirstPickCount < 0)
                    return -1; // 아직 미정 → HandController에서 1 또는 3 허용
                else
                    return 4 - stage3FirstPickCount; // 첫 줍기의 보수
            }
            return currentStage switch
            {
                1 => 1,
                2 => 2,
                4 => 4,
                _ => 0
            };
        }
    }

    /// <summary>3단 첫 줍기 결과 기록 (CatchSystem에서 호출)</summary>
    public void SetStage3FirstPick(int count)
    {
        stage3FirstPickCount = count;
        Debug.Log($"[GameManager] Stage 3 first pick: {count} → next required: {4 - count}");
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
        boardTransform = GameObject.Find("Cloth")?.transform;
        scatterSystem = GetComponent<ScatterSystem>();
        handController = FindFirstObjectByType<HandController>();
        catchSystem = FindFirstObjectByType<CatchSystem>();

        // StonePool 자동 생성 + 초기화
        var existingStones = FindObjectsByType<Stone>(FindObjectsSortMode.None);
        if (StonePool.Instance == null)
            new GameObject("StonePool").AddComponent<StonePool>();
        StonePool.Instance.Initialize(existingStones);

        // 기본 5개 활성화 (인덱스 초기화는 StonePool.Initialize에서 처리됨)
        stones = StonePool.Instance.Activate(5);

        // 보드 하단 벽 생성 (돌이 아래로 굴러 떨어지는 것 방지)
        CreateBoardBottomWall();

        // [P1] GameSession 자동 생성/참조
        if (GameSession.Instance == null)
            new GameObject("GameSession").AddComponent<GameSession>();
        session = GameSession.Instance;

        // [P1] SidePanelUI 자동 생성/참조
        if (SidePanelUI.Instance == null)
            new GameObject("SidePanelUI").AddComponent<SidePanelUI>();

        // [v4] ScreenManager 자동 생성
        if (ScreenManager.Instance == null)
            new GameObject("ScreenManager").AddComponent<ScreenManager>();

        // [P4] PauseMenuUI 자동 생성
        if (PauseMenuUI.Instance == null)
            new GameObject("PauseMenuUI").AddComponent<PauseMenuUI>();

        // [Online] Supabase / NameInputUI / GraveyardUI 자동 생성
        if (SupabaseManager.Instance == null)
            new GameObject("SupabaseManager").AddComponent<SupabaseManager>();
        if (NameInputUI.Instance == null)
            new GameObject("NameInputUI").AddComponent<NameInputUI>();
        if (GraveyardUI.Instance == null)
            new GameObject("GraveyardUI").AddComponent<GraveyardUI>();

        // [Phase B] AgeSaturationController 자동 생성
        if (AgeSaturationController.Instance == null)
            new GameObject("AgeSaturationController").AddComponent<AgeSaturationController>();

        // [Phase B] StoryMentUI 자동 생성
        if (StoryMentUI.Instance == null)
            new GameObject("StoryMentUI").AddComponent<StoryMentUI>();

        // [튜토리얼] TutorialUI 자동 생성
        if (TutorialUI.Instance == null)
            new GameObject("TutorialUI").AddComponent<TutorialUI>();

        // [커서] HandCursorUI 자동 생성
        if (HandCursorUI.Instance == null)
            new GameObject("HandCursorUI").AddComponent<HandCursorUI>();

        // 타이틀 화면에서 손 숨김 (1프레임 뒤 — 다른 Start()에서 FindFirstObjectByType 완료 후)
        StartCoroutine(HideHandNextFrame());

        // 타이틀 화면 표시 — 시작 버튼 누르면 게임 시작
        if (TitleScreenUI.Instance == null)
            new GameObject("TitleScreenUI").AddComponent<TitleScreenUI>();
        TitleScreenUI.Instance?.Show();
        HandCursorUI.Instance?.SetActive(true); // 타이틀에서 손 커서 활성화
    }

    private void Update()
    {
        // [P1] 경과 시간 갱신: 전환 중이 아닐 때만
        if (session != null && !isTransitioning && !isAllClear && !isInTitleScreen)
        {
            session.ElapsedTime += Time.deltaTime;
        }

        // 상시 장외 감시: PickThrowStone/PickStones 중 OnBoard 돌이 보드 밖이면 실패
        if (!isTransitioning && !isAllClear && !isInTitleScreen && boardTransform != null)
        {
            var phase = currentPhase;
            if (phase == GamePhase.PickThrowStone || phase == GamePhase.PickStones || phase == GamePhase.Throw)
            {
                CheckOnBoardStonesOutOfBounds();
            }
        }

        // v4: 기믹 프레임 업데이트
        if (!isTransitioning && !isAllClear && !isInTitleScreen)
            currentGimmick?.OnUpdate();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugStageJump();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void DebugStageJump()
    {
        if (Keyboard.current == null) return;
        if (isInTitleScreen) return;
        if (isTransitioning) return;
        if (isPaused) return;
        if (StoryMentUI.Instance != null && StoryMentUI.Instance.IsShowing) return;

        // 숫자키 1~9: 스테이지 1~9, 0: 스테이지 10
        for (int i = 0; i <= 9; i++)
        {
            KeyControl key = i switch
            {
                0 => Keyboard.current.digit0Key,
                1 => Keyboard.current.digit1Key,
                2 => Keyboard.current.digit2Key,
                3 => Keyboard.current.digit3Key,
                4 => Keyboard.current.digit4Key,
                5 => Keyboard.current.digit5Key,
                6 => Keyboard.current.digit6Key,
                7 => Keyboard.current.digit7Key,
                8 => Keyboard.current.digit8Key,
                9 => Keyboard.current.digit9Key,
                _ => null
            };
            if (key != null && key.wasPressedThisFrame)
            {
                int targetLoop = i == 0 ? 10 : i;
                var config = StageConfig.Get(targetLoop);
                if (session != null)
                {
                    session.CurrentLoop = targetLoop;
                    session.CurrentAge = config.Age;
                    session.CurrentStageInLoop = 1;
                }
                Debug.Log($"[DEBUG] Jump to Loop {targetLoop} ({config.StageName}), Age={config.Age}");
                StartStage(1);
                return;
            }
        }

        // +/-: 현재 루프 내 단(1~5) 변경
        bool plusPressed = Keyboard.current.equalsKey.wasPressedThisFrame ||
                           Keyboard.current.numpadPlusKey.wasPressedThisFrame;
        bool minusPressed = Keyboard.current.minusKey.wasPressedThisFrame ||
                            Keyboard.current.numpadMinusKey.wasPressedThisFrame;

        if (plusPressed)
        {
            int nextStage = Mathf.Clamp(currentStage + 1, 1, 5);
            Debug.Log($"[DEBUG] Stage jump +1 → Stage {nextStage}");
            StartStage(nextStage);
        }
        else if (minusPressed)
        {
            int prevStage = Mathf.Clamp(currentStage - 1, 1, 5);
            Debug.Log($"[DEBUG] Stage jump -1 → Stage {prevStage}");
            StartStage(prevStage);
        }
    }
#endif

    private void CheckOnBoardStonesOutOfBounds()
    {
        if (stones == null) return;
        Vector2 boardCenter = new Vector2(boardTransform.position.x, boardTransform.position.y);
        Vector2 halfSize = new Vector2(4f, 3.2f); // boardSize / 2

        foreach (var stone in stones)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;
            if (!stone.gameObject.activeSelf) continue;

            Vector2 pos = new Vector2(stone.transform.position.x, stone.transform.position.y);
            bool outX = pos.x < boardCenter.x - halfSize.x - 0.5f || pos.x > boardCenter.x + halfSize.x + 0.5f;
            bool outY = pos.y < boardCenter.y - halfSize.y - 0.5f || pos.y > boardCenter.y + halfSize.y + 0.5f;

            if (outX || outY)
            {
                Debug.Log($"[GameManager] Stone {stone.StoneIndex} out of bounds during play at ({pos.x:F1},{pos.y:F1})");
                SetFailReason("낙!");
                SetPhase(GamePhase.Failed);
                return;
            }
        }
    }

    public void SetFailReason(string reason)
    {
        lastFailReason = reason;
    }

    public void StartGameFromTitle()
    {
        isInTitleScreen = false;
        if (session != null)
        {
            session.PlayerName = "Player";
            // IsTestPlay는 TitleScreenUI.OnModeSelected에서 이미 설정됨
        }
        // [Phase B] 타이틀에서 시작 시 채도 초기화
        AgeSaturationController.Instance?.ResetSaturation();

        // 첫 플레이 튜토리얼
        if (PlayerPrefs.GetInt("TutorialSeen", 0) == 0)
        {
            isTransitioning = true;
            TutorialUI.Instance?.Show(() =>
            {
                PlayerPrefs.SetInt("TutorialSeen", 1);
                PlayerPrefs.Save();
                isTransitioning = false;
                StartGameAfterTutorial();
            });
        }
        else
        {
            StartGameAfterTutorial();
        }
    }

    private void StartGameAfterTutorial()
    {
        // v4: 첫 스테이지 StoryMent 표시 후 게임 시작
        var config = StageConfig.Get(session != null ? session.CurrentLoop : 1);
        string ment = config.StoryMent;
        if (!string.IsNullOrEmpty(ment))
        {
            isTransitioning = true;
            StoryMentUI.Instance?.Show(ment, () =>
            {
                isTransitioning = false;
                HandCursorUI.Instance?.SetActive(false);
                handController?.gameObject.SetActive(true);
                StartStage(1);
            });
        }
        else
        {
            HandCursorUI.Instance?.SetActive(false);
            handController?.gameObject.SetActive(true);
            StartStage(1);
        }
    }

    public void StartStage(int stage)
    {
        isInTitleScreen = false; // 어떤 경로로든 스테이지 시작 시 타이틀 아님

        // Hand가 비활성이면 활성화 (타이틀 복귀 후 PauseMenu 초기화 등 경로 안전장치)
        if (handController != null && !handController.gameObject.activeSelf)
            handController.gameObject.SetActive(true);

        // 진행 중인 전환 코루틴 중단
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        currentStage = stage;
        isAllClear = false;

        // v4: 스테이지 설정 로드 + 기믹 생성
        currentStageConfig = StageConfig.Get(session != null ? session.CurrentLoop : 1);
        currentGimmick?.OnStageEnd(); // 이전 기믹 정리
        // 5단 꺾기에서는 기믹 비활성화
        if (stage == 5)
            currentGimmick = null;
        else
            currentGimmick = StageGimmick.Create(currentStageConfig.Gimmick, this);

        stage3FirstPickCount = -1; // 3단 서브라운드 리셋

        // [P1] 세션 단계 갱신 + 사이드 패널 반영
        if (session != null)
            session.CurrentStageInLoop = stage;
        SidePanelUI.Instance?.Refresh();

        // 게이지 강제 리셋 (스킵 등으로 Scatter 중간에 전환 시)
        scatterSystem.ResetGauge();

        // 모든 돌 활성화 + 초기 상태 복원
        ResetAllStones();
        currentGimmick?.OnStageStart(stage); // ResetAllStones 이후 호출 — 색 배정이 리셋에 덮이지 않도록

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
            case GamePhase.PickStones:
                currentGimmick?.OnPickPhaseStart(); // v4: 기믹 훅
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
            GamePhase.PickStones => "[ 돌을 단계에 맞게 주우세요 ]",
            GamePhase.Catch => "[ 커서를 움직여 돌을 받으세요! ]",
            GamePhase.Stage5Throw => stage5Step switch
            {
                0 => "[ 게이지에 맞춰 클릭! 손바닥 던지기 ]",
                2 => "[ 게이지에 맞춰 클릭! 손등 던지기 ]",
                _ => "[ 클릭하여 던지기! ]"
            },
            GamePhase.Stage5Catch => stage5Step switch
            {
                1 => "[ 손등으로 5개 모두 받기! ]",
                3 => "[ 타이밍에 맞춰 클릭! 낚아채기! ]",
                _ => "[ 돌을 받으세요! ]"
            },
            _ => null
        };

        if (guide != null)
            GameUI.Instance.UpdateGuideText(guide);
        else
            GameUI.Instance.HideGuideText();
    }

    private void OnFailed()
    {
        if (isAllClear) return; // ALL CLEAR 이후 실패 무시
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

        // 회귀 연출: Fade Out → "인생을 다시 시작합니다" → Fade In
        GameUI.Instance?.ShowRegressionTransition();
        yield return new WaitForSeconds(2.5f); // 0.5 fade out + 1.5 text + 0.5 fade in

        isTransitioning = false;
        transitionCoroutine = null;

        StartStage(1);
    }

    /// <summary>보드 하단에 보이지 않는 벽 생성 (돌이 아래로 굴러 떨어지는 것만 방지)</summary>
    private void CreateBoardBottomWall()
    {
        if (boardTransform == null) return;

        float boardCenterX = boardTransform.position.x;
        float boardBottomY = boardTransform.position.y - 3.2f; // boardSize.y / 2

        var wallGo = new GameObject("BoardBottomWall");
        wallGo.transform.position = new Vector3(boardCenterX, boardBottomY - 0.25f, 0f);
        var col = wallGo.AddComponent<BoxCollider>();
        col.size = new Vector3(10f, 0.5f, 2f); // 넓고 얇은 벽
        // Rigidbody 없음 = Static Collider (움직이지 않음)
    }

    private static readonly Vector3 StoneOriginalScale = new Vector3(0.3f, 0.3f, 0.3f);

    private void ResetAllStones()
    {
        handController.ResetHand();

        // v4: 스테이지별 돌 개수 설정 (5단 꺾기는 항상 5개)
        int stoneCount = (currentStage == 5) ? 5
            : (currentStageConfig != null ? currentStageConfig.TotalStones : 5);
        stones = StonePool.Instance != null ? StonePool.Instance.Activate(stoneCount) : stones;

        foreach (var stone in stones)
        {
            stone.transform.SetParent(null);
            stone.transform.localScale = StoneOriginalScale; // 5단 스케일 변동 복원
            stone.SetState(Stone.State.OnBoard);
            stone.Rb.linearVelocity = Vector3.zero;
            stone.Rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 5단 내부 단계 진행: 각 단계 완료 시 step++ (0→1→2→3)
    /// </summary>
    public void AdvanceStage5Step()
    {
        stage5Step++;
        Debug.Log($"[GameManager] Stage 5 step advanced to {stage5Step}.");
    }

    private void OnStageComplete()
    {
        TestLogger.Instance?.CompleteStageAttempt();

        // [P1] 세션 상태 갱신 (나이++, 5단이면 루프++)
        session?.OnStageComplete(currentStage);
        // [Phase B] 채도 갱신
        AgeSaturationController.Instance?.UpdateSaturation(session != null ? session.CurrentAge : 0);
        SidePanelUI.Instance?.Refresh();

        currentGimmick?.OnStageEnd(); // v4: 기믹 정리

        int nextStage = currentStage + 1;
        if (nextStage > 5)
        {
            // v4: 55살(10스테이지 5단) 완료 시 게임 클리어
            if (session != null && session.IsGameClear)
            {
                Debug.Log("[GameManager] GAME CLEAR! Age 55 reached!");
                transitionCoroutine = StartCoroutine(DoAllClearTransition());
                return;
            }

            // v4: 5단 클리어 + 아직 55살 미만 → 다음 스테이지 번호로 멘트 가져오기
            Debug.Log($"[GameManager] Stage {session?.CurrentLoop} started! (Age={session?.CurrentAge})");
            int nextLoopNum = session != null ? session.CurrentLoop : 1;
            var nextConfig = StageConfig.Get(nextLoopNum);
            if (nextConfig != null && !string.IsNullOrEmpty(nextConfig.StoryMent))
            {
                transitionCoroutine = StartCoroutine(DoClearThenMent(nextConfig.StoryMent));
            }
            else
            {
                transitionCoroutine = StartCoroutine(DoClearTransition(1));
            }
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

    private IEnumerator HideHandNextFrame()
    {
        yield return null; // 다른 스크립트의 Start() 완료 대기
        handController?.gameObject.SetActive(false);
    }

    private IEnumerator DoClearThenMent(string ment)
    {
        isTransitioning = true;

        AudioManager.Instance?.PlayStageClear();
        GameUI.Instance?.ShowClear();

        yield return new WaitForSeconds(clearDuration);

        // 멘트 표시 → 탭 완료 시 다음 루프
        bool mentDone = false;
        StoryMentUI.Instance?.Show(ment, () => mentDone = true);
        yield return new WaitUntil(() => mentDone);

        isTransitioning = false;
        transitionCoroutine = null;

        StartStage(1);
    }

    private IEnumerator DoAllClearTransition()
    {
        isTransitioning = true;
        isAllClear = true;

        // 값을 즉시 캡처 (이후 session이 리셋되어도 안전)
        float clearTime = session != null ? session.ElapsedTime : 0f;
        int regressionCount = session != null ? session.RegressionCount : 0;

        AudioManager.Instance?.PlayAllClear();
        GameUI.Instance?.ShowAllClear();

        // 놀림 메시지 5초 표시 후 이름 입력으로 전환
        yield return new WaitForSeconds(5f);

        GameUI.Instance?.HideOverlay();

        // 이름 입력 팝업 → 이름 확정 후 레코드 저장 → 묘지 파노라마
        bool nameConfirmed = false;
        NameInputUI.Instance?.Show((name, isTestPlay) =>
        {
            session.PlayerName = name;
            session.IsTestPlay = isTestPlay;
            nameConfirmed = true;
        });

        yield return new WaitUntil(() => nameConfirmed);

        string playerName = session != null ? session.PlayerName : "Player";
        bool testPlay = session != null && session.IsTestPlay;

        if (!testPlay)
        {
            SupabaseManager.Instance?.PostRecord(playerName, clearTime, regressionCount, success =>
            {
                if (!success)
                    Debug.LogWarning("[GameManager] PostRecord failed — continuing without record upload.");
            });
        }

        handController?.gameObject.SetActive(false);
        HandCursorUI.Instance?.SetActive(true);
        GraveyardUI.Instance?.Show(clearTime, playerName, regressionCount, testPlay);

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
        // 타이틀 화면에서는 ESC 무시
        if (isInTitleScreen) return;

        // ALL CLEAR 화면에서는 ESC 무시
        if (isAllClear) return;

        // [Online] 이름 입력 중에는 ESC 무시
        if (NameInputUI.Instance != null && NameInputUI.Instance.IsOpen) return;

        PauseMenuUI.Instance?.Toggle();
    }

    // [P4] 외부에서 isPaused 설정 (PauseMenuUI 전용)
    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    private void OnAllClearClick(InputAction.CallbackContext ctx)
    {
        if (isInTitleScreen) return;
        if (!isAllClear) return;
        if (isPaused) return; // [P4] 일시정지 중 클릭 무시

        // [Phase B] 멘트 표시 중 클릭 무시
        if (StoryMentUI.Instance != null && StoryMentUI.Instance.IsShowing) return;

        // GraveyardUI가 활성 상태면 자체 처리
        if (GraveyardUI.Instance != null && GraveyardUI.Instance.IsShowing) return;

        RestartGame();
    }

    public void RestartGame()
    {
        isAllClear = false;
        isTransitioning = false;
        GameUI.Instance?.HideOverlay();
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        GraveyardUI.Instance?.Hide();

        // 세션 전체 초기화 (이름 입력은 엔딩 후)
        session?.ResetAll();
        // [Phase B] 채도 리셋
        AgeSaturationController.Instance?.ResetSaturation();
        SidePanelUI.Instance?.Refresh();

        isInTitleScreen = true;
        handController?.gameObject.SetActive(false);
        HandCursorUI.Instance?.SetActive(true);
        TitleScreenUI.Instance?.Show();
    }
}
