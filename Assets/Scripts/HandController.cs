using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 손 컨트롤러: 커서 추종 + 자동 줍기 판정
/// 기획서: 팔(검은 사각형) + 손바닥(노란 원). 커서를 따라다니며 범위 안 돌 자동 줍기.
/// 줍기 완료 시 제자리에서 받기 모드 전환 → 플레이어가 직접 이동.
/// </summary>
public partial class HandController : MonoBehaviour
{
    [Header("Board Bounds")]
    [SerializeField] private Vector2 boardMin = new Vector2(-4f, -9f);
    [SerializeField] private Vector2 boardMax = new Vector2(4f, -1f); // v11: y는 BoardBounds.SkyFloorY로 이전. x는 미사용.

    [Header("Throw Settings")]
    [SerializeField] private float throwPeakY = 8f;        // 최고점 Y (하늘 영역 상단)
    [SerializeField] private float throwUpDuration = 0.8f;
    [SerializeField] private float throwDownDuration = 1.0f;
    private float throwDownDurationOverride = -1f;
    public enum ThrowDownCurveMode { EaseIn, Linear, EaseOut }
    private ThrowDownCurveMode throwDownCurveMode = ThrowDownCurveMode.EaseIn;
    private float maxMoveSpeed = -1f;      // 음수 = 무제한
    private float moveSmoothFactor = -1f;  // 음수 = 즉시 추종
    private HandGhostPool ghostPool;

    [Header("Catch Settings")]
    [SerializeField] private float catchAreaY = 2f;         // 받기 영역 Y (하늘/보드 경계)

    private Camera mainCamera;
    private HandModelBuilder handModel;
    private InputAction pointerAction;
    private InputAction clickAction;
    private Coroutine fingerFoldCoroutine;

    private List<Stone> pickedStones = new List<Stone>();
    private Stone throwStone;
    private bool isOnBoard;
    private bool isCatchMode;
    private bool isHolding;  // 클릭 Hold 상태
    private bool suppressCursorFollow; // 뿌리기 토스 중 커서추종 OFF (손 position을 코루틴이 직접 제어)

    // === 5단 꺾기 전용 ===
    [Header("Stage 5 Settings")]
    // v17: 5단 받기 판정을 보드 좌표 거리로 옮기면서 이 값도 **보드 단위**가 됐다.
    //   화면 단위 1.8은 보드 앞쪽에서 1.8, 뒤쪽에서 3.1에 해당해 사실상 관대함이 달랐다.
    //   사용자 테스트에서 "5단 아주 안정적"이라 체감값을 유지하는 쪽으로 1.8을 그대로 둔다.
    [SerializeField] private float stage5CatchRadius = 1.8f;    // 5개 동시 캐치 반경 (보드 단위, 손등 받기 시 2배)
    [SerializeField] private float stage5BackhandScaleMultiplier = 2f; // 손등 받기 시 손 크기 배율
    // v17: 화면 단위 → **보드 단위**. 보드 반너비가 8이므로 같은 숫자라도 의미가 달라진다
    //   (구 화면 1.2 ≈ 보드 1.2 수준으로 비슷하나, 깊이에 따라 화면 폭이 달라지던 문제가 사라진다).
    [SerializeField] private float stage5SpreadRange = 1.4f;     // 돌 퍼짐 범위 (보드 단위)
    [SerializeField] private float stage5MinSpacing = 0.35f;     // 돌 최소 간격 (보드 단위)
    [SerializeField] private float stage5MissThreshold = 3.5f;   // catchAreaY - 이 값 아래로 지나가면 실패 (보드 근처까지 허용)

    private bool stage5ClickPending;
    private bool stage5CatchActive;                            // 슬라이드 인 완료 후 true — LateUpdate Y 고정 + X 추종 트리거
    private Coroutine stage5Coroutine;
    private Coroutine throwCoroutine;
    private Coroutine flickCoroutine;

    [Header("Stage 5 Height Settings")]
    [SerializeField] private float stage5HeightStep = 0.4f;   // 돌 간 높이 간격 (부드럽게 모임)

    [Header("Stage 5 Gauge")]
    [SerializeField] private float stage5GaugeSpeed = 1.5f;
    [SerializeField] private float stage5MinPeakY = 5f;
    [SerializeField] private float stage5MaxPeakY = 10f;
    [SerializeField] private float stage5FistGrabRadius = 0.8f;  // 한붓그리기 시 개별 돌 캐치 반경
    [SerializeField] private float stage5BounceForce = 5f;

    private float stage5GaugeValue;
    private bool stage5GaugeActive;
    private bool stage5GaugeGoingUp = true;
    private bool stage5GaugeWaiting;  // Press 대기 중 (게이지 아직 안 시작)
    private bool stage5GaugePending;  // Release로 게이지 확정됨

    public List<Stone> PickedStones => pickedStones;
    public Stone ThrowStone => throwStone;
    public bool IsOnBoard => isOnBoard;
    public bool IsHolding => isHolding;
    public bool IsCatchMode => isCatchMode;
    /// <summary>5단 시퀀스 진행 중인가. 5단은 받기 판정 규칙이 따로라 공용 처리에서 제외할 때 쓴다.</summary>
    public bool IsStage5 => stage5Coroutine != null;

    private void Awake()
    {
        mainCamera = Camera.main;

        // 3D 손 모델 생성
        var modelBuilder = gameObject.AddComponent<HandModelBuilder>();
        modelBuilder.Build();
        handModel = modelBuilder;

        // Compound collider용 Rigidbody (kinematic — 커서 추종은 transform.position으로)
        var rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 손을 AirLayer(8)에 배치 — InAir/Bouncing 돌(같은 레이어)과 충돌 가능
        // Default(0) 레이어(보드 위 돌)과는 충돌 안 함
        SetLayerRecursive(gameObject, Stone.AirLayer);

        pointerAction = new InputAction("Pointer", InputActionType.Value);
        pointerAction.AddBinding("<Mouse>/position");
        pointerAction.AddBinding("<Touchscreen>/primaryTouch/position");

        clickAction = new InputAction("Click", InputActionType.Button);
        clickAction.AddBinding("<Mouse>/leftButton");
        clickAction.AddBinding("<Touchscreen>/primaryTouch/press");
    }

    private void OnEnable()
    {
        pointerAction.Enable();
        clickAction.Enable();
        clickAction.performed += OnClick;
        clickAction.started += OnClickStarted;
        clickAction.canceled += OnClickCanceled;
    }

    private void OnDisable()
    {
        clickAction.performed -= OnClick;
        clickAction.started -= OnClickStarted;
        clickAction.canceled -= OnClickCanceled;
        pointerAction.Disable();
        clickAction.Disable();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        var phase = GameManager.Instance.CurrentPhase;

        // 위치 기반 모드 전환: PickStones 중 손 Y 위치로 받기/줍기 자동 전환
        if (phase == GameManager.GamePhase.PickStones)
        {
            // catch mode가 아닐 때만 위치 갱신 (catch mode는 LateUpdate에서 처리)
            if (!isCatchMode && !suppressCursorFollow) UpdatePosition();

            // v11-fix2: 하늘 경계 = BoardBounds.SkyFloorY (=-2.45, 보드 메시 상단)
            // 히스테리시스 0.2 unit: 한 번 catch 모드면 (SkyFloor - 0.2 = -2.65) 아래로 가야 해제 → chattering 방지
            float skyEnter = BoardBounds.SkyFloorY;
            float skyExit  = BoardBounds.SkyFloorY - 0.2f;
            bool inSky = isCatchMode
                ? transform.position.y > skyExit
                : transform.position.y > skyEnter;
            if (inSky && !isCatchMode)
            {
                SetCatchMode(true);
                handModel?.SetCollidersEnabled(true);
            }
            else if (!inSky && isCatchMode)
            {
                SetCatchMode(false);
                handModel?.SetCollidersEnabled(false);
            }
        }
        else if (!isCatchMode && !suppressCursorFollow)
        {
            UpdatePosition();
        }

        // 보드 모드에서만 줍기 판정
        if (!isCatchMode && isHolding)
        {
            if (phase == GameManager.GamePhase.PickThrowStone)
            {
                TryHoldPickThrowStone();
            }
            else if (phase == GameManager.GamePhase.PickStones)
            {
                TryHoldPickStones();
            }
        }

        // Stage 5 게이지: Press(꾹 누름)로 시작, Release(놓음)로 확정
        if (stage5GaugeWaiting)
        {
            bool isPressed = clickAction.IsPressed();

            if (isPressed && !stage5GaugeActive)
            {
                // Press 시작 → 게이지 왕복 시작
                stage5GaugeActive = true;
                stage5GaugeValue = 0f;
                stage5GaugeGoingUp = true;
                GaugeBarUI.Instance?.Show();
            }

            if (stage5GaugeActive)
            {
                // 게이지 왕복
                if (stage5GaugeGoingUp)
                {
                    stage5GaugeValue += stage5GaugeSpeed * Time.deltaTime;
                    if (stage5GaugeValue >= 1f) { stage5GaugeValue = 1f; stage5GaugeGoingUp = false; }
                }
                else
                {
                    stage5GaugeValue -= stage5GaugeSpeed * Time.deltaTime;
                    if (stage5GaugeValue <= 0f) { stage5GaugeValue = 0f; stage5GaugeGoingUp = true; }
                }
                GaugeBarUI.Instance?.SetValue(stage5GaugeValue);
            }

            if (!isPressed && stage5GaugeActive)
            {
                // Release → 게이지 확정
                stage5GaugeActive = false;
                stage5GaugePending = true;
                GaugeBarUI.Instance?.Hide();
            }
        }
    }

    private void UpdatePosition()
    {
        Vector2 screenPos = pointerAction.ReadValue<Vector2>();
        Vector3 targetPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        targetPos.z = -0.5f;

        Vector3 finalPos = targetPos;
        if (moveSmoothFactor > 0f)
            finalPos = Vector3.Lerp(transform.position, targetPos, moveSmoothFactor * Time.deltaTime);
        if (maxMoveSpeed > 0f)
            finalPos = Vector3.MoveTowards(transform.position, finalPos, maxMoveSpeed * Time.deltaTime);
        finalPos.z = -0.5f;

        transform.position = finalPos;
        ApplyBoardPerspective(finalPos);
        handModel?.SyncHitboxPosition(finalPos);
        ghostPool?.OnHandMoved(finalPos);
    }

    /// <summary>보드 위 손 크기를 돌과 **같은 원근**으로 맞춘다.
    ///
    /// 손만 화면 크기로 고정돼 있으면, 뒤쪽에서 손 그림은 그대로인데 판정(보드 단위)은 좁아져
    /// "분명히 손 안인데 안 잡히는" 어긋남이 생긴다. 돌이 이미 원근을 따르므로 손도 따라야
    /// 보이는 것과 판정이 같아진다.</summary>
    private void ApplyBoardPerspective(Vector3 handPos)
    {
        if (handModel == null) return;
        Vector2 board = BoardSpace.ToBoard(new Vector2(handPos.x, handPos.y));
        // 손은 보드 밖(하늘·아래 여백)으로도 나가므로 깊이를 보드 범위로 가둔다.
        // 안 그러면 외삽으로 손이 0에 수렴하거나 과장되게 커진다.
        board.y = Mathf.Clamp(board.y, -BoardSpace.LogicalDepth * 0.5f, BoardSpace.LogicalDepth * 0.5f);
        handModel.SetPerspectiveScale(BoardSpace.Current.PerspectiveScale(board, 0f));
    }

    // === Hold + Bounds 줍기 입력 ===

    private void OnClickStarted(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsPaused) return;
        if (GameManager.Instance.IsTransitioning) return;

        var phase = GameManager.Instance.CurrentPhase;
        if (phase == GameManager.GamePhase.PickThrowStone || phase == GameManager.GamePhase.PickStones)
        {
            isHolding = true;
            AnimateFingerFold(true); // 손가락 접힘
            // Collider는 켜지 않음 — 손바닥 중심 거리로 판정 (물리 밀어냄 방지)
        }
    }

    private void OnClickCanceled(InputAction.CallbackContext ctx)
    {
        if (!isHolding) return;
        isHolding = false;
        AnimateFingerFold(false); // 손가락 펼침

        if (GameManager.Instance == null) return;
        var phase = GameManager.Instance.CurrentPhase;

        // PickThrowStone: Hold 해제 시 돌 1개 잡혔으면 던지기로
        if (phase == GameManager.GamePhase.PickThrowStone)
        {
            if (throwStone != null)
            {
                // 1개 잡힘 → Throw 페이즈 전환
                GameManager.Instance.SetPhase(GameManager.GamePhase.Throw);
            }
            // 0개: 아무 일 없음 (다시 Hold 가능)
        }
        // PickStones: 받기 모드 전환은 위치 기반 (Update에서 Y 체크)
        // Hold 해제 시 별도 처리 없음 — 하늘로 올라가면 자동 전환
    }

    // === Hold + Bounds 줍기 (Phase C) ===

    /// <summary>던질 돌 1개 잡기: 돌 중심이 손바닥 안에 있으면 줍기</summary>
    private void TryHoldPickThrowStone()
    {
        if (throwStone != null) return;
        if (GameManager.Instance.IsTransitioning) return;
        if (handModel == null) return;

        var allStones = GameManager.Instance.Stones;
        if (allStones == null) return;

        int coveredCount = 0;
        Stone coveredStone = null;

        foreach (var stone in allStones)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;

            // v17: 화면 Bounds → 보드 좌표 거리 (IsStoneUnderHand 주석 참고)
            if (IsStoneUnderHand(stone))
            {
                coveredCount++;
                coveredStone = stone;
            }
        }

        if (coveredCount == 1)
        {
            // v12b: 기믹이 던지는 돌을 지정하면 검증 (Stage 4 순서대로 잡기: 검정 돌만 허용)
            var throwGimmick = GameManager.Instance.CurrentGimmick;
            if (throwGimmick != null && !throwGimmick.ValidateThrowPick(coveredStone))
            {
                isHolding = false;
                AnimateFingerFold(false);
                AudioManager.Instance?.PlayPickExcess();
                TestLogger.Instance?.LogFailure("wrong_throw_stone");
                GameManager.Instance.SetFailReason("던지는 돌(검정)부터 집어야 한다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                return;
            }

            throwStone = coveredStone;
            throwStone.SetState(Stone.State.InHand);
            throwStone.transform.SetParent(transform);
            throwStone.transform.localPosition = Vector3.zero;
            AudioManager.Instance?.PlayStonePickThrow();
            TestLogger.Instance?.LogStoneState(throwStone.StoneIndex, "picked_as_throw", throwStone.transform.position);
            // 1개 잡기 성공 → 자동으로 던져짐
            isHolding = false;
            AnimateFingerFold(false);
            GameManager.Instance.SetPhase(GameManager.GamePhase.Throw);
        }
        else if (coveredCount >= 2)
        {
            isHolding = false;
            AnimateFingerFold(false);
            AudioManager.Instance?.PlayPickExcess();
            TestLogger.Instance?.LogFailure("pick_throw_excess");
            GameManager.Instance.SetFailReason("돌을 너무 많이 집었다!");
            GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
        }
    }

    /// <summary>바닥 돌 줍기: Hold 중 손바닥이 돌을 100% 덮으면 줍기</summary>
    private void TryHoldPickStones()
    {
        if (GameManager.Instance.IsTransitioning) return;
        if (handModel == null) return;

        var allStones = GameManager.Instance.Stones;
        if (allStones == null) return;
        int required = GameManager.Instance.RequiredPickCount;

        foreach (var stone in allStones)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;
            if (pickedStones.Contains(stone)) continue;

            // v17: 화면 Bounds → 보드 좌표 거리 (IsStoneUnderHand 주석 참고)
            if (IsStoneUnderHand(stone))
            {
                // v4: 기믹 ValidatePick — Add 전에 먼저 확인
                if (GameManager.Instance != null)
                {
                    var gimmick = GameManager.Instance.CurrentGimmick;
                    if (gimmick != null && !gimmick.ValidatePick(stone))
                    {
                        // 기믹이 거부한 돌 — 즉시 실패 (가짜 돌 줍기 등)
                        isHolding = false;
                        AnimateFingerFold(false);
                        AudioManager.Instance?.PlayPickExcess();
                        TestLogger.Instance?.LogFailure("gimmick_invalid_pick");
                        GameManager.Instance.SetFailReason("잘못된 돌을 집었다!");
                        GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                        return;
                    }
                }

                pickedStones.Add(stone);
                stone.SetState(Stone.State.InHand);
                stone.transform.SetParent(transform);
                stone.transform.localPosition = Vector3.up * 0.3f * pickedStones.Count;
                AudioManager.Instance?.PlayStonePick(pickedStones.Count);
                TestLogger.Instance?.LogStoneState(stone.StoneIndex, "hold_picked", stone.transform.position);

                // v4: 기믹에 줍기 알림 (FleeGimmick 등 첫 줍기 트리거)
                GameManager.Instance?.NotifyStonePicked(stone);

                // 3단 첫 줍기: 1 or 3 허용 (2는 받기 시 CatchSystem이 검증)
                // 줍는 도중 2를 거쳐야 3에 도달하므로, 여기서는 4+ 초과만 차단
                if (required < 0)
                {
                    if (pickedStones.Count >= 4)
                    {
                        isHolding = false;
                        AnimateFingerFold(false);
                        AudioManager.Instance?.PlayPickExcess();
                        TestLogger.Instance?.LogFailure($"stage3_pick_excess_{pickedStones.Count}");
                        GameManager.Instance.SetFailReason("돌을 너무 많이 집었다!");
                        GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                        return;
                    }
                }
                else
                {
                    // 초과 → 즉시 실패
                    if (pickedStones.Count > required)
                    {
                        isHolding = false;
                        AnimateFingerFold(false);
                        AudioManager.Instance?.PlayPickExcess();
                        TestLogger.Instance?.LogFailure($"pick_excess_{pickedStones.Count}_of_{required}");
                        GameManager.Instance.SetFailReason("돌을 너무 많이 집었다!");
                        GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                        return;
                    }
                }

                // 받기 모드 전환은 위치 기반 (Update에서 Y 위치 체크)
                // count 기반 전환 제거 — 플레이어가 하늘로 올라가면 자동 전환
            }
        }
    }

    // === 던지기 ===

    private void OnClick(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsPaused) return; // [P4]
        if (GameManager.Instance.IsTransitioning) return;
        var phase = GameManager.Instance.CurrentPhase;

        if (phase == GameManager.GamePhase.Throw && throwStone != null)
        {
            throwCoroutine = StartCoroutine(DoThrow());
        }
        else if (phase == GameManager.GamePhase.Stage5Catch)
        {
            stage5ClickPending = true;
        }
    }

    private IEnumerator DoThrow()
    {
        var stone = throwStone;
        stone.transform.SetParent(null);

        // 물리 끄고 직접 제어
        stone.Rb.isKinematic = true;
        stone.Rb.useGravity = false;

        // 시작 위치: 보드 영역 (Caught 상태에서 하늘에 있을 수 있으므로 보정)
        var cloth = GameObject.Find("Cloth");
        float boardY = cloth != null ? cloth.transform.position.y : -5f;
        stone.transform.position = new Vector3(transform.position.x, boardY, 0f);

        float startX = stone.transform.position.x;
        float startY = stone.transform.position.y;

        // ── v17: 던진 돌은 보드 좌표 고정 + 높이(h)만 변한다 ──────────────────
        // 화면 y 하나로 "보드 위 위치"와 "높이"를 겸하던 것이 불공평의 근원이었다.
        // boardPos를 던지는 순간 확정하고, 이후 h만 애니메이션한다.
        // → 그림자(= Project(boardPos, 0))가 움직이지 않으므로 조준선이 된다.
        //
        // ⚠️ 보드 좌표는 **손의 위치**에서 뽑는다. 위에서 stone을 옮긴 Cloth의 y(boardY)는
        //    보드 전체에 공통인 고정 화면 y라, 그걸 역투영하면 손이 앞쪽에 있어도
        //    항상 같은 깊이로 해석된다(= 그림자가 엉뚱한 곳에 생김).
        // 던지는 순간 손이 이미 하늘(보드 뒷변 위)에 있을 수 있으므로 보드 안으로 클램프한다.
        // (클램프 없으면 v<0이 되어 그림자가 보드 밖 위쪽에 생긴다 — 실측 로그 y=-2.9 사례)
        Vector2 throwBoardPos = BoardSpace.ClampToBoard(
            BoardSpace.ToBoard(new Vector2(transform.position.x, transform.position.y)));
        Vector2 groundScreen  = BoardSpace.ToScreen(throwBoardPos, 0f);
        float peakHeight      = Mathf.Max(0.1f, throwPeakY - groundScreen.y);

        // 이 던지기의 받기 판정은 이 깊이에서 일어난다 → 손도 그 깊이 크기로 그린다.
        catchDepthBoard = throwBoardPos;
        if (isCatchMode) ApplyCatchVisualScale();

        // 출발점도 그 보드 좌표의 지면으로 맞춘다 (그림자와 발밑이 어긋나지 않게).
        stone.SetBoardMotion(throwBoardPos, 0f);
        startX = groundScreen.x;
        startY = groundScreen.y;

        AudioManager.Instance?.PlayThrowUp();
        TestLogger.Instance?.LogStoneState(stone.StoneIndex, "thrown_up", stone.transform.position);
        Debug.Log($"[Hand] Threw stone {stone.StoneIndex} from y={startY:F1} to peak y={throwPeakY}");

        // v4: 기믹에 던지기 시작 알림 (ColorSelectGimmick 등 추가 돌 스폰)
        GameManager.Instance?.NotifyThrowStart(stone);

        // 줍기 단계 시작 (올라가는 동안 바닥 돌 줍기)
        GameManager.Instance.SetPhase(GameManager.GamePhase.PickStones);

        // === 올라가기 (EaseOut) ===
        float elapsed = 0f;
        while (elapsed < throwUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwUpDuration);
            float eased = 1f - (1f - t) * (1f - t);
            // v17: 화면 y가 아니라 높이(h)를 보간하고, 화면 위치는 투영으로 얻는다.
            float h = Mathf.Lerp(0f, peakHeight, eased);
            stone.SetBoardMotion(throwBoardPos, h);
            yield return null;
        }

        // 최고점 사운드
        AudioManager.Instance?.PlayThrowPeak();

        // Catch 판정 시작
        var catchSystem = FindFirstObjectByType<CatchSystem>();
        catchSystem?.BeginCatch(stone);

        // 받기 모드 Collider 활성화는 위치 기반 (Update에서 Y 체크로 자동 전환)

        // 돌을 InAir 상태로 전환 (Collider 활성, 물리 낙하 준비)
        stone.SetState(Stone.State.InAir);
        // SetState(InAir)가 isKinematic=false로 설정하므로 다시 true로 복원
        // catchAreaY 도달 전까지는 코루틴이 위치 직접 제어
        stone.Rb.isKinematic = true;
        stone.Rb.useGravity = false;

        // 낙하 사운드
        AudioManager.Instance?.PlayThrowDown();

        // === 내려오기 (EaseIn — 가속 낙하) ===
        // catchAreaY 위: 코루틴이 위치 직접 제어 (isKinematic=true)
        // catchAreaY 도달: isKinematic=false로 전환 → 물리 엔진이 위치 관리 → Collider 판정 가능
        // v6-1: catchAreaY = 받기 모드 전환 기준선 (더 이상 물리 전환점 아님 → 실제론 여전히 물리 전환점, Collision 판정 필수)
        float effectiveDownDuration = throwDownDurationOverride > 0f ? throwDownDurationOverride : throwDownDuration;

        // v6-1: 낙하 목표 Y를 boardSurfaceY 기준으로 (코루틴 타임아웃 대신 CatchSystem이 판정)
        float fallTargetY = catchSystem != null ? catchSystem.BoardSurfaceY : startY;

        elapsed = 0f;
        while (elapsed < effectiveDownDuration)
        {
            if (catchSystem != null && !catchSystem.IsCatchPhase)
            {
                Debug.Log("[Hand] Stone caught or fell (CatchSystem handled)!");
                SetCatchMode(false);
                handModel?.SetCollidersEnabled(false);
                throwCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / effectiveDownDuration);
            float eased = throwDownCurveMode switch
            {
                ThrowDownCurveMode.EaseIn  => t * t,
                ThrowDownCurveMode.Linear  => t,
                ThrowDownCurveMode.EaseOut => 1f - (1f - t) * (1f - t),
                _ => t * t
            };
            // v17: 화면 y가 아니라 높이(h)를 보간한다. 보드 좌표는 고정.
            float h = Mathf.Lerp(peakHeight, 0f, eased);
            stone.SetBoardMotion(throwBoardPos, h);
            var p = new Vector2(stone.transform.position.x, stone.transform.position.y);

            // ── v17 받기 판정 ────────────────────────────────────────────────
            // 물리 충돌(콜라이더 겹침)을 폐기하고 **보드 좌표 거리**로 판정한다.
            // 구 방식은 2.5D 평면 물리라 결국 "화면 겹침"이었고, 1px만 어긋나도
            // "분명히 손 위인데" 못 받았다(= 진단 3 불공평의 직접 원인).
            //
            // 떨어지는 돌이 손의 화면 높이를 통과하는 순간 판정한다.
            // (손이 하늘로 들려 있으므로 "특정 h"가 아니라 "손 높이 통과"가 시각적으로 맞다)
            if (IsAtCatchMoment(p.y))
            {
                var verdict = JudgeBoardCatch(throwBoardPos);
                if (verdict != BoardCatchVerdict.Miss)
                {
                    if (verdict == BoardCatchVerdict.Palm)
                    {
                        catchSystem?.OnPalmCatch(stone);
                    }
                    else // Finger — 가장자리로 받아 튕김 (기획 3-5 유지)
                    {
                        // 손 중심에서 멀어지는 방향으로 튕긴다. 손이 왼쪽에 있으면 오른쪽으로.
                        float awayX = Mathf.Sign(p.x - transform.position.x);
                        if (Mathf.Approximately(awayX, 0f)) awayX = 1f;
                        catchSystem?.OnFingerBounce(stone, new Vector3(awayX, 1f, 0f).normalized);
                    }
                    throwCoroutine = null;
                    yield break;
                }
                // Miss면 계속 내려간다 → 지면 도달 시 아래 실패 처리
            }

            yield return null;
        }

        // 코루틴 타임아웃: CatchSystem이 이미 처리했을 수도 있음 (중복 방지)
        if (catchSystem != null && !catchSystem.IsCatchPhase)
        {
            // CatchSystem이 이미 실패/성공 처리 완료 — 중복 호출 방지
            SetCatchMode(false);
            handModel?.SetCollidersEnabled(false);
            throwCoroutine = null;
            yield break;
        }

        // 못 받음 → 실패 (CatchSystem이 미처 감지 못한 경우 fallback)
        AudioManager.Instance?.PlayCatchFail();
        if (catchSystem != null) catchSystem.StopCatch();
        SetCatchMode(false);
        handModel?.SetCollidersEnabled(false);
        // 물리 전환 후 OnBoard 복귀 시 isKinematic 보정
        stone.Rb.isKinematic = true;
        stone.Rb.useGravity = false;
        stone.transform.position = new Vector3(startX, startY, 0f);
        stone.SetState(Stone.State.OnBoard);
        TestLogger.Instance?.LogFailure("catch_missed_landing");
        GameManager.Instance.SetFailReason("돌을 놓쳤다!");
        throwCoroutine = null;
        GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
    }

    // === v17 보드 좌표 받기 판정 ===

    /// <summary>손 전체 판정 반경 (보드 단위, 가로). 이 밖이면 못 받는다. ⚠️ 재튜닝 대상.</summary>
    private const float CatchHandRadius = 1.6f;

    /// <summary>손바닥(안전) 반경. 결정 6-15 "손바닥 : 손가락 = 절반씩" → 전체의 1/2. ⚠️ 재튜닝 대상.</summary>
    private const float CatchPalmRadius = CatchHandRadius * 0.5f;

    /// <summary>이번 받기 판정이 일어나는 보드 깊이. 던진 돌은 수직 낙하라 던질 때 확정된다.
    /// 기본값(0,0)=보드 중앙 — 5단처럼 돌이 여러 개인 경우에 쓴다.</summary>
    private Vector2 catchDepthBoard = Vector2.zero;

    /// <summary>받기 모드에서 손을 그릴 배율 — **손끝이 판정 경계에 오도록** 맞춘다.
    ///
    /// 왜: 받기 판정은 손 중심에서의 거리(1.6 보드 단위)인데 손은 기본 크기로 그려져,
    /// 손끝 바깥으로 지나간 돌이 잡히는 순간이 있었다. 난이도(판정 숫자)는 그대로 두고
    /// **그림을 판정에 맞춘다**. 두 값 중 하나가 바뀌어도 자동으로 따라온다.
    ///
    /// ⚠️ 깊이를 곱하는 이유: 판정은 보드 단위라 같은 1.6이라도 뒤쪽 돌에서는 화면상 더 좁다
    /// (뒤가 좁은 사다리꼴). 앞쪽 기준으로만 맞추면 뒤쪽에서 "손 안인데 못 받는" 반대 어긋남이 생긴다.
    /// 돌이 떨어질 깊이의 원근 배율을 곱해야 모든 깊이에서 손끝 = 판정 경계가 된다.
    ///
    /// ⚠️ **5단은 제외한다.** 5단은 자기 판정 반경(1.8, 손등이면 3.6)과 자기 손 크기 연출(루트 2배)을
    /// 이미 짝지어 갖고 있다. 여기서 1~4단 기준(1.6)으로 다시 키우면 5단 받기 감각이 바뀐다.
    /// (사용자 테스트에서 "5단 아주 안정적" — 건드리지 않는다)</summary>
    private void ApplyCatchVisualScale()
    {
        if (handModel == null) return;
        if (stage5Coroutine != null) { handModel.SetPerspectiveScale(1f); return; }

        // 배율 1에서 **지금 자세의** 화면 손끝 반경을 잰 뒤, 그 값이 판정 반경이 되도록 다시 건다.
        // 받기 자세는 손을 눕히므로(-60°) 정면 길이로 계산하면 실제보다 크게 나온다.
        handModel.SetPerspectiveScale(1f);
        float reach = handModel.CurrentScreenTipReach;
        if (reach < 0.01f) return;

        float target = CatchHandRadius * BoardSpace.Current.PerspectiveScale(catchDepthBoard, 0f);
        handModel.SetPerspectiveScale(target / reach);
    }

    /// <summary>디버그 표시용 — 받기 판정의 **화면 반경**(손바닥/손 전체).
    /// 판정은 보드 단위라 깊이에 따라 화면상 크기가 달라진다. 그 값을 그대로 노출한다.</summary>
    public float DebugCatchPalmScreenRadius
        => CatchPalmRadius * BoardSpace.Current.PerspectiveScale(catchDepthBoard, 0f);
    public float DebugCatchHandScreenRadius
        => CatchHandRadius * BoardSpace.Current.PerspectiveScale(catchDepthBoard, 0f);

    private enum BoardCatchVerdict { Miss, Finger, Palm }

    /// <summary>받기 판정 — **보드 가로 거리** 기준.
    ///
    /// ⚠️ 왜 2D 거리가 아니라 가로 거리인가:
    /// 이 게임은 받을 때 손을 **하늘로 들어올린다**(`inSky` = 손 y > SkyFloorY).
    /// 그래서 손의 화면 좌표를 지면으로 역투영하면 보드 뒤쪽(v&lt;0)이 나와 깊이를 알 수 없다.
    /// 반면 던진 돌은 **수직으로** 떨어지므로(boardPos 고정) 조준의 의미 있는 축은 가로다.
    /// → 손의 화면 x를 "돌과 같은 깊이"에서 해석해 보드 x를 얻고, 그 거리로 판정한다.
    ///
    /// 화면 겹침(구 물리 충돌)과 달리 **보드 단위 거리**라 돌이 앞에 있든 뒤에 있든 관대함이 같다.
    /// (깊이까지 맞추는 판정은 손을 하늘로 들지 않는 구조로 바꾼 뒤에 검토 — 미결)
    /// </summary>
    private BoardCatchVerdict JudgeBoardCatch(Vector2 stoneBoardPos)
    {
        float groundY = BoardSpace.ToScreen(stoneBoardPos, 0f).y;
        // 가로 기준점도 손바닥 중심 — 손 루트와 x가 어긋나면 "손바닥 한가운데로 받았는데 튕김"이 난다.
        float palmX = handModel != null ? handModel.GetPalmCenter().x : transform.position.x;
        Vector2 handBoard = BoardSpace.ToBoard(new Vector2(palmX, groundY));

        float dist = Mathf.Abs(handBoard.x - stoneBoardPos.x);
        if (dist <= CatchPalmRadius) return BoardCatchVerdict.Palm;
        if (dist <= CatchHandRadius) return BoardCatchVerdict.Finger;
        return BoardCatchVerdict.Miss;
    }

    /// <summary>받기 판정을 시작할 시점인가 — 떨어지는 돌이 손의 화면 높이를 지나는 순간.
    /// 손이 하늘에 있으므로 "돌이 h=0.6까지 내려왔을 때"가 아니라 "손 높이를 통과할 때"가
    /// 시각적으로 맞는 순간이다.</summary>
    private bool IsAtCatchMoment(float stoneScreenY)
        => isCatchMode && stoneScreenY <= transform.position.y;

    // === v17 줍기 판정 (보드 좌표) ===

    /// <summary>손이 돌을 덮는 반경 (보드 단위) — **보이는 손바닥에서 그대로 끌어온다**.
    ///
    /// 왜 보드 단위인가: 손의 줍기 범위가 **화면 크기로 고정**돼 있으면, 원근 때문에
    /// 뒤로 갈수록 좁게 그려지는 보드에서 같은 손이 더 넓은 보드 면적을 덮는다.
    /// 실측: 앞쪽 0.85 vs 뒤쪽 1.46(1.7배). 뿌리기 최소 간격이 1.05라
    /// **뒤쪽에서만 돌 두 개가 한꺼번에 잡혀 초과 줍기로 즉사**했다.
    /// 같은 조작인데 위치에 따라 죽고 사는 건 "몰라서 죽으면 안 된다" 원칙 위반이다.
    ///
    /// 그런데 보드 단위로만 두면 이번엔 **손 그림이 원근을 안 따라서** 판정과 어긋난다.
    /// → 손도 돌과 같은 원근 스케일로 그린다(UpdatePosition). 그러면 보드 단위 반경 하나가
    ///   모든 깊이에서 "보이는 손바닥"과 정확히 일치한다.
    /// 보드 앞변에서 월드 1 = 보드 1이므로 손바닥 반경(월드)을 그대로 보드 단위로 쓴다.</summary>
    private float PickRadiusBoard => handModel != null ? handModel.PalmRadiusBase : 0.5f;

#if UNITY_EDITOR
    /// <summary>에디터 검증 전용 — 손을 지정 위치에 놓고 커서추종을 멈춘다.
    /// "판정 원이 그려진 손바닥과 겹치는가"를 스크린샷으로 확인하기 위한 것.</summary>
    public void DebugPlaceHand(Vector3 worldPos)
    {
        suppressCursorFollow = true;
        transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);
        ApplyBoardPerspective(transform.position);
        handModel?.SyncHitboxPosition(transform.position);
    }

    /// <summary>에디터 검증 전용 — 줍기 판정 결과를 그대로 노출.</summary>
    public bool DebugIsStoneUnderHand(Stone stone) => IsStoneUnderHand(stone);

    /// <summary>에디터 검증 전용 — 손바닥 기준점(판정 중심).</summary>
    public Vector3 DebugPalmCenter => handModel != null ? handModel.GetPalmCenter() : transform.position;

    /// <summary>에디터 검증 전용 — 받기 모드를 강제로 켜고 손을 하늘에 세운다.
    /// "그려진 손끝이 판정 경계에 오는가"를 스크린샷으로 확인하기 위한 것.</summary>
    public void DebugForceCatchMode(Vector2 stoneBoardPos)
    {
        catchDepthBoard = stoneBoardPos;
        suppressCursorFollow = true;
        var ground = BoardSpace.ToScreen(stoneBoardPos, 0f);
        transform.position = new Vector3(ground.x, BoardBounds.SkyFloorY + 1.5f, -0.5f);
        SetCatchMode(true);
        ApplyCatchVisualScale(); // 회전이 적용된 뒤 다시 재야 자세 반영값이 나온다
        handModel?.SyncHitboxPosition(transform.position);
    }
#endif

    /// <summary>손이 이 돌을 덮고 있는가 — 보드 좌표 거리로 판정.
    /// 기준점은 손 루트가 아니라 **손바닥 중심**이다(손 루트는 손가락까지 포함한 중앙이라 위로 치우친다).</summary>
    private bool IsStoneUnderHand(Stone stone)
    {
        Vector3 palm = handModel != null ? handModel.GetPalmCenter() : transform.position;
        Vector2 handBoard = BoardSpace.ToBoard(new Vector2(palm.x, palm.y));
        Vector2 stoneBoard = stone.HasBoardMotion
            ? stone.BoardPos
            : BoardSpace.ToBoard(new Vector2(stone.transform.position.x, stone.transform.position.y));
        return Vector2.Distance(handBoard, stoneBoard) <= PickRadiusBoard;
    }

    // === Catch 모드: 좌우 이동 ===

    private void LateUpdate()
    {
        if (!isCatchMode) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return; // [P4]
        // 코루틴이 손 위치를 직접 몰고 있으면(뿌리기 토스 등) 커서 추종이 덮어쓰면 안 된다.
        if (suppressCursorFollow) return;

        // 5단 받기: stage5CatchActive일 때 XY 자유 추종
        if (stage5CatchActive)
        {
            Vector2 screenPos = pointerAction.ReadValue<Vector2>();
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
            transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);
            handModel?.SyncHitboxPosition(transform.position);
            return;
        }

        // 1~4단 받기: X/Y 모두 커서 추종, Y 하한 = boardMin.y
        Vector2 screenPos2 = pointerAction.ReadValue<Vector2>();
        Vector3 targetPos2 = mainCamera.ScreenToWorldPoint(new Vector3(screenPos2.x, screenPos2.y, 10f));
        float clampedY = Mathf.Max(targetPos2.y, boardMin.y);
        Vector3 targetCatch = new Vector3(targetPos2.x, clampedY, -0.5f);

        Vector3 finalCatch = targetCatch;
        if (moveSmoothFactor > 0f)
            finalCatch = Vector3.Lerp(transform.position, targetCatch, moveSmoothFactor * Time.deltaTime);
        if (maxMoveSpeed > 0f)
            finalCatch = Vector3.MoveTowards(transform.position, finalCatch, maxMoveSpeed * Time.deltaTime);
        finalCatch.z = -0.5f;

        transform.position = finalCatch;
        handModel?.SyncHitboxPosition(finalCatch);
        ghostPool?.OnHandMoved(finalCatch);
    }

    // === 5단 꺾기 ===

    // v6-1: 5단 내 boardSurfaceY (DoStage5Sequence 시작 시 캐시)
    private float stage5BoardSurfaceY = -8.2f;


    /// <summary>
    /// 캐치 성공 후: picked 클리어, throwStone 유지 (같은 돌로 다시 던지기).
    /// 던지기 돌은 손에 붙어있는 상태로 Throw 페이즈 직행.
    /// </summary>
    public void ClearPickedButKeepThrow()
    {
        pickedStones.Clear();
        SetCatchMode(false);
        // throwStone은 그대로 유지 (Caught 상태, 손에 부착)
    }

    /// <summary>
    /// 전체 리셋용 (실패/새 스테이지): throwStone도 null로 비움.
    /// </summary>
    public void ClearPickedOnly()
    {
        pickedStones.Clear();
        throwStone = null;
        SetCatchMode(false);
    }

    /// <summary>
    /// HandHitbox에서 전달되는 충돌 이벤트.
    /// Palm → 손바닥 안착, Finger → 튕김 처리.
    /// </summary>
    public void OnStoneHit(Stone stone, HandHitbox.HitZone zone, Collision collision)
    {
        // 받기 모드가 아니면 무시
        if (!isCatchMode) return;
        // 이미 잡힌 돌이면 무시
        if (stone.CurrentState == Stone.State.Caught || stone.CurrentState == Stone.State.InHand) return;

        var catchSystem = FindFirstObjectByType<CatchSystem>();
        if (catchSystem == null || !catchSystem.IsCatchPhase) return;

        if (zone == HandHitbox.HitZone.Palm)
        {
            // 손바닥 안착
            catchSystem.OnPalmCatch(stone);
        }
        else if (zone == HandHitbox.HitZone.Finger)
        {
            // 손가락 튕김 — contacts가 있을 때만 반사 벡터 계산
            Vector3 reflectDir;
            if (collision.contacts.Length > 0)
            {
                reflectDir = Vector3.Reflect(
                    collision.relativeVelocity.normalized,
                    collision.contacts[0].normal
                );
            }
            else
            {
                reflectDir = Vector3.up; // fallback: 위로 튕김
            }
            catchSystem.OnFingerBounce(stone, reflectDir);
        }
    }

    /// <summary>받기 모드 전환: 시각 회전 + 물리 hitbox 재배치</summary>
    private void SetCatchMode(bool catching)
    {
        isCatchMode = catching;
        if (catching)
        {
            // 시각: 옆에서 본 손 (손가락 왼쪽, 손바닥 틸트)
            transform.localEulerAngles = new Vector3(-60f, 0f, 90f);
            // 받기 모드: 불투명
            handModel?.SetVisualAlpha(1f);
            // 받기는 하늘에서 일어나 보드 깊이가 없다 → 원근 대신 **판정 반경에 맞춘 크기**로 그린다.
            ApplyCatchVisualScale();
        }
        else
        {
            transform.localEulerAngles = Vector3.zero;
            // 줍기 모드: 반투명
            handModel?.SetVisualAlpha(0.35f);
        }
    }

    /// <summary>
    /// 특정 포즈로 손가락 애니메이션 (외부에서 호출 가능).
    /// HandPose와 동일한 포즈를 3D 손에 적용.
    /// </summary>
    public void SetHandPose(HandPose pose)
    {
        // 손가락별 접힘량 (0 = 펼침, 1 = 접힘). 순서는 **엄지→소지**.
        float[] targets;
        switch (pose)
        {
            case HandPose.PointIndex:
                targets = new float[] { 1f, 0f, 1f, 1f, 1f };   // 검지만 펼침
                break;
            case HandPose.PointMiddle:
                targets = new float[] { 1f, 1f, 0f, 1f, 1f };   // 중지만 펼침
                break;
            default: // Open
                targets = new float[] { 0f, 0f, 0f, 0f, 0f };
                break;
        }
        StartFingerFold(targets);
    }

    /// <summary>손가락 접힘/펼침 애니메이션</summary>
    /// <summary>
    /// 뿌리기 던지는 순간 손목 flick 연출 (시각 전용).
    /// UpdatePosition이 position만 덮으므로 rotation은 유지됨 → 손 루트 rotation만 사용.
    /// 손목을 z축으로 살짝 꺾었다가 identity로 EaseOut 복귀. 완료 시 반드시 identity 복원.
    /// </summary>
    public void PlayScatterFlick()
    {
        if (flickCoroutine != null) StopCoroutine(flickCoroutine);
        flickCoroutine = StartCoroutine(DoScatterThrow());
    }

    /// <summary>
    /// 뿌리기 던지기 안무: 윈드업(뒤/위 젖힘) → 캐스트(앞/아래 스냅, 손가락 펼침) → 팔로스루(커서 복귀).
    /// 캐스트 끝(≈0.22s)에 ScatterSystem.DoScatter가 돌을 detach+토스 시작 → 타이밍 정렬.
    /// suppressCursorFollow로 토스 동안 손 position을 코루틴이 직접 제어.
    /// </summary>
    private IEnumerator DoScatterThrow()
    {
        suppressCursorFollow = true;          // 토스 동안 커서추종 OFF (손 position 제어)
        Vector3 P0 = transform.position;      // 릴리스 시작점(=현재 커서 위치)
        float biasX = Mathf.Clamp((0f - P0.x) * 0.15f, -0.4f, 0.4f); // 보드중심 쪽 살짝
        Vector3 castPos = P0 + new Vector3(biasX, -0.35f, 0f); // 앞/아래로 살짝

        AnimateFingerFold(false);
        // v16: 윈드업 제거 — 돌은 이미 날아감(DoScatter 즉시 토스). 놓는 즉시 캐스트 스냅 → 팔로스루 복귀.
        yield return LerpHand(P0, castPos, 0f, -22f, 0.10f, easeOut: false);
        Vector3 cursorNow = ReadCursorWorld();
        yield return LerpHand(castPos, cursorNow, -22f, 0f, 0.15f, easeOut: true);

        transform.rotation = Quaternion.identity;
        suppressCursorFollow = false;         // 커서추종 재개
        flickCoroutine = null;
    }

    /// <summary>손 position/손목회전(z)을 dur 동안 보간. z는 -0.5 유지. easeOut=1-(1-t)², else t².</summary>
    private IEnumerator LerpHand(Vector3 from, Vector3 to, float rotZfrom, float rotZto, float dur, bool easeOut)
    {
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float e = easeOut ? 1f - (1f - t) * (1f - t) : t * t;
            Vector3 p = Vector3.Lerp(from, to, e);
            p.z = -0.5f;
            transform.position = p;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(rotZfrom, rotZto, e));
            handModel?.SyncHitboxPosition(transform.position);
            yield return null;
        }
        Vector3 end = to;
        end.z = -0.5f;
        transform.position = end;
        transform.rotation = Quaternion.Euler(0f, 0f, rotZto);
        handModel?.SyncHitboxPosition(transform.position);
    }

    /// <summary>현재 커서의 월드 좌표(z=-0.5). UpdatePosition과 동일 방식.</summary>
    private Vector3 ReadCursorWorld()
    {
        Vector2 screenPos = pointerAction.ReadValue<Vector2>();
        Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        world.z = -0.5f;
        return world;
    }

    /// <summary>다섯 손가락을 한꺼번에 접거나(true) 편다(false). 줍기 피드백의 기본 동작.</summary>
    private void AnimateFingerFold(bool fold)
    {
        float v = fold ? 1f : 0f;
        StartFingerFold(new[] { v, v, v, v, v });
    }

    /// <summary>손가락별 접힘량(0~1)으로 부드럽게 이동. 굽는 축·마디별 각도는 HandRig가 안다.</summary>
    private void StartFingerFold(float[] targets)
    {
        if (handModel?.Rig == null) return;
        if (fingerFoldCoroutine != null) StopCoroutine(fingerFoldCoroutine);
        fingerFoldCoroutine = StartCoroutine(DoFingerFold(targets));
    }

    private IEnumerator DoFingerFold(float[] targets)
    {
        var rig = handModel?.Rig;
        if (rig == null) yield break;

        const float duration = 0.1f;
        float[] start = new float[HandRig.FingerCount];
        for (int i = 0; i < HandRig.FingerCount; i++) start[i] = rig.GetFold(i);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < HandRig.FingerCount; i++)
                rig.SetFold(i, Mathf.Lerp(start[i], targets[i], t));
            yield return null;
        }
        for (int i = 0; i < HandRig.FingerCount; i++) rig.SetFold(i, targets[i]);

        fingerFoldCoroutine = null;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    /// <summary>
    /// 전체 리셋 (실패/새 스테이지 시작 시)
    /// </summary>
    /// <summary>기믹용: 던지기 낙하 시간 오버라이드. 음수면 기본값 사용.</summary>
    public void SetThrowDownDurationOverride(float value) { throwDownDurationOverride = value; }

    /// <summary>기믹용: 낙하 커브 모드 오버라이드. EaseIn=기본(무거움), Linear=중간, EaseOut=가벼움.</summary>
    public void SetThrowDownCurveMode(ThrowDownCurveMode mode) { throwDownCurveMode = mode; }

    /// <summary>기믹용: 손 최대 이동 속도 오버라이드. 음수면 무제한.</summary>
    public void SetMoveSpeedOverride(float speed) { maxMoveSpeed = speed; }
    /// <summary>기믹용: 손 이동 감쇠 계수 오버라이드. 음수면 즉시 추종.</summary>
    public void SetMoveSmoothOverride(float factor) { moveSmoothFactor = factor; }
    /// <summary>기믹용: 잔상 풀 연결. null이면 잔상 없음.</summary>
    public void SetGhostPool(HandGhostPool pool) { ghostPool = pool; }

    /// <summary>모든 기믹 오버라이드 해제. 스테이지 완료/재시작 시 호출.</summary>
    public void ClearAllOverrides()
    {
        throwDownDurationOverride = -1f;
        throwDownCurveMode = ThrowDownCurveMode.EaseIn;
        maxMoveSpeed = -1f;
        moveSmoothFactor = -1f;
        if (ghostPool != null)
        {
            ghostPool.Cleanup();
            ghostPool = null;
        }
    }

    public void ResetHand()
    {
        // 5단 코루틴이 실행 중이면 중단
        if (stage5Coroutine != null)
        {
            StopCoroutine(stage5Coroutine);
            stage5Coroutine = null;
        }
        // 던지기 코루틴이 실행 중이면 중단
        if (throwCoroutine != null)
        {
            StopCoroutine(throwCoroutine);
            throwCoroutine = null;
        }
        stage5ClickPending = false;
        stage5CatchActive = false;
        stage5GaugeActive = false;
        stage5GaugeWaiting = false;
        stage5GaugePending = false;
        GaugeBarUI.Instance?.Hide();

        if (fingerFoldCoroutine != null)
        {
            StopCoroutine(fingerFoldCoroutine);
            fingerFoldCoroutine = null;
        }
        // 뿌리기 flick 코루틴 정리 + 손 기울기 복원 (기운 채 남지 않도록)
        if (flickCoroutine != null)
        {
            StopCoroutine(flickCoroutine);
            flickCoroutine = null;
        }
        transform.rotation = Quaternion.identity;
        suppressCursorFollow = false; // 커서추종 복원 (토스 중 리셋 안전망)
        // 손가락 펼침 상태로 즉시 복원
        handModel?.Rig?.ResetAll();
        catchDepthBoard = Vector2.zero; // 다음 던지기 전까지는 보드 중앙 기준

        SetCatchMode(false);
        isHolding = false;
        handModel?.SetCollidersEnabled(false);

        foreach (var stone in pickedStones)
        {
            if (stone != null)
                stone.transform.SetParent(null);
        }
        pickedStones.Clear();

        if (throwStone != null)
        {
            throwStone.transform.SetParent(null);
            throwStone = null;
        }
    }
}
