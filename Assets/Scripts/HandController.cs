using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 손 컨트롤러: 커서 추종 + 자동 줍기 판정
/// 기획서: 팔(검은 사각형) + 손바닥(노란 원). 커서를 따라다니며 범위 안 돌 자동 줍기.
/// 줍기 완료 시 제자리에서 받기 모드 전환 → 플레이어가 직접 이동.
/// </summary>
public class HandController : MonoBehaviour
{
    [Header("Board Bounds")]
    [SerializeField] private Vector2 boardMin = new Vector2(-4f, -9f);
    [SerializeField] private Vector2 boardMax = new Vector2(4f, -1f);

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

    // === 5단 꺾기 전용 ===
    [Header("Stage 5 Settings")]
    [SerializeField] private float stage5CatchRadius = 1.8f;    // 5개 동시 캐치 반경 (기본값, 손등 받기 시 2배)
    [SerializeField] private float stage5BackhandScaleMultiplier = 2f; // 손등 받기 시 손 크기 배율
    [SerializeField] private float stage5SpreadRange = 1.2f;     // 돌 퍼짐 범위 (카시오페이아급 밀집)
    [SerializeField] private float stage5MinSpacing = 0.3f;      // 돌 최소 간격
    [SerializeField] private float stage5MissThreshold = 3.5f;   // catchAreaY - 이 값 아래로 지나가면 실패 (보드 근처까지 허용)

    private bool stage5ClickPending;
    private bool stage5CatchActive;                            // 슬라이드 인 완료 후 true — LateUpdate Y 고정 + X 추종 트리거
    private Coroutine stage5Coroutine;
    private Coroutine throwCoroutine;

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
            if (!isCatchMode) UpdatePosition();

            bool inSky = transform.position.y > boardMax.y;
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
        else if (!isCatchMode)
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
        handModel?.SyncHitboxPosition(finalPos);
        ghostPool?.OnHandMoved(finalPos);
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
            // Collider는 켜지 않음 — GetPalmPickupBounds()로 판정 (물리 밀어냄 방지)
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

        Bounds palmBounds = handModel.GetPalmPickupBounds();
        var allStones = GameManager.Instance.Stones;
        if (allStones == null) return;

        int coveredCount = 0;
        Stone coveredStone = null;

        foreach (var stone in allStones)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;

            // 돌 중심점이 손바닥 Bounds 안에 있는지 판정
            if (palmBounds.Contains(stone.transform.position))
            {
                coveredCount++;
                coveredStone = stone;
            }
        }

        if (coveredCount == 1)
        {
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

        Bounds palmBounds = handModel.GetPalmPickupBounds();
        var allStones = GameManager.Instance.Stones;
        if (allStones == null) return;
        int required = GameManager.Instance.RequiredPickCount;

        foreach (var stone in allStones)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;
            if (pickedStones.Contains(stone)) continue;

            // 돌 중심점이 손바닥 Bounds 안에 있는지 판정
            if (palmBounds.Contains(stone.transform.position))
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
            float y = Mathf.Lerp(startY, throwPeakY, eased);
            stone.transform.position = new Vector3(startX, y, 0f);
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
            // v6-1: Lerp 목표를 fallTargetY(보드 표면)로 변경
            float y = Mathf.Lerp(throwPeakY, fallTargetY, eased);

            // catchAreaY 도달: 코루틴 제어에서 물리 낙하로 전환
            // (Collision 판정을 위해 isKinematic=false 전환 유지)
            if (y <= catchAreaY && stone.Rb.isKinematic)
            {
                stone.Rb.isKinematic = false;
                stone.Rb.useGravity = true;
                // 커브별 미분값으로 전환 순간 속도 계산 (속도 점프 방지)
                // EaseIn: d/dt(t²) = 2t, Linear: d/dt(t) = 1, EaseOut: d/dt(1-(1-t)²) = 2(1-t)
                float derivative = throwDownCurveMode switch
                {
                    ThrowDownCurveMode.EaseIn  => 2f * t,
                    ThrowDownCurveMode.Linear  => 1f,
                    ThrowDownCurveMode.EaseOut => 2f * (1f - t),
                    _ => 2f * t
                };
                float instantSpeed = derivative / effectiveDownDuration * (throwPeakY - fallTargetY);
                stone.Rb.linearVelocity = new Vector3(0f, -instantSpeed, 0f);
                Debug.Log($"[Hand] Stone {stone.StoneIndex} switched to physics at y={y:F2}, speed={instantSpeed:F2}");
            }

            // isKinematic일 때만 코루틴이 위치 직접 제어
            if (stone.Rb.isKinematic)
            {
                stone.transform.position = new Vector3(startX, y, 0f);
            }
            // isKinematic=false면 물리 엔진이 위치 관리

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

    // === Catch 모드: 좌우 이동 ===

    private void LateUpdate()
    {
        if (!isCatchMode) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return; // [P4]

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
    /// GameManager에서 호출: 5단 시퀀스 시작 (코루틴이 이미 실행 중이면 중복 시작 방지)
    /// </summary>
    public void BeginStage5Throw()
    {
        if (stage5Coroutine != null) return; // 이미 실행 중 — 3단계 SetPhase 재호출 방지
        stage5ClickPending = false;
        stage5Coroutine = StartCoroutine(DoStage5Sequence());
    }

    /// <summary>
    /// 5단 꺾기 전체 시퀀스 (4단계):
    /// [1단계] 손바닥 던지기 (게이지) → [2단계] 손등 받기 → [3단계] 손등 던지기 (게이지) → [4단계] 주먹 낚아채기
    /// </summary>
    private IEnumerator DoStage5Sequence()
    {
        var gm = GameManager.Instance;
        var allStones = gm.Stones;
        int count = allStones.Length;

        // v6-1: boardSurfaceY 캐시 (DoStage5Catch/FistGrab에서 멤버 변수로 참조)
        var catchSys = FindFirstObjectByType<CatchSystem>();
        stage5BoardSurfaceY = catchSys != null ? catchSys.BoardSurfaceY : -8.2f;

        // ============ [1단계] 손바닥 던지기 ============
        // SetPhase(Stage5Throw)는 GameManager.DoStageIntro에서 이미 호출됨
        // 돌도 DoStageIntro에서 InHand + SetParent(handController)로 설정됨
        SetCatchMode(false);

        // 손 위치 세팅 (돌은 hand 자식이므로 따라감)
        transform.position = new Vector3(0f, catchAreaY - 2f, -0.5f);

        // Press/Release 게이지 대기
        stage5GaugeWaiting = true;
        stage5GaugeActive = false;
        stage5GaugePending = false;
        yield return new WaitUntil(() => stage5GaugePending);
        stage5GaugeWaiting = false;
        stage5GaugePending = false;

        // 게이지 값으로 높이 결정
        float peakY1 = Mathf.Lerp(stage5MinPeakY, stage5MaxPeakY, stage5GaugeValue);

        // 1차 던지기 (X 퍼짐)
        AudioManager.Instance?.PlayStage5Toss();
        yield return DoStage5Toss(allStones, count, peakY1, true);

        // ============ [2단계] 손등으로 받기 ============
        gm.AdvanceStage5Step(); // step 0→1
        SetCatchMode(false);
        stage5CatchActive = false;
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        bool catch1Success = false;
        yield return DoStage5Catch(allStones, count, success => catch1Success = success);

        if (!catch1Success)
        {
            stage5Coroutine = null;
            yield break;
        }

        // 돌을 손에 부착
        Debug.Log("[Stage5] Back-hand catch SUCCESS!");
        for (int i = 0; i < count; i++)
        {
            allStones[i].SetState(Stone.State.Caught);
            allStones[i].transform.SetParent(transform);
            allStones[i].transform.localPosition = new Vector3((i - 2) * 0.3f, 0f, 0f);
        }

        // ============ [3단계] 손등 던지기 ============
        gm.AdvanceStage5Step(); // step 1→2
        gm.SetPhase(GameManager.GamePhase.Stage5Throw);
        SetCatchMode(false);
        stage5CatchActive = false;

        yield return new WaitForSeconds(0.3f);

        // Press/Release 게이지 대기
        stage5GaugeWaiting = true;
        stage5GaugeActive = false;
        stage5GaugePending = false;
        yield return new WaitUntil(() => stage5GaugePending);
        stage5GaugeWaiting = false;
        stage5GaugePending = false;

        float peakY2 = Mathf.Lerp(stage5MinPeakY, stage5MaxPeakY, stage5GaugeValue);

        // 2차 던지기 (X 고정, 수직)
        AudioManager.Instance?.PlayStage5Toss();
        yield return DoStage5Toss(allStones, count, peakY2, false);

        // ============ [4단계] 최종 낚아채기 ============
        gm.AdvanceStage5Step(); // step 2→3
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        bool grabSuccess = false;
        yield return DoStage5FistGrab(allStones, count, success => grabSuccess = success);

        if (!grabSuccess)
        {
            stage5Coroutine = null;
            yield break;
        }

        // 성공! 돌 정리
        Debug.Log("[Stage5] Fist grab SUCCESS! ALL STAGES CLEARED!");
        for (int i = 0; i < count; i++)
        {
            allStones[i].SetState(Stone.State.Caught);
            allStones[i].transform.SetParent(null);
            allStones[i].gameObject.SetActive(false);
        }

        stage5Coroutine = null;
        gm.SetPhase(GameManager.GamePhase.StageComplete);
    }

    /// <summary>
    /// 5개 돌을 동시에 하늘로 던지는 코루틴.
    /// spreadX=true: GenerateSpreadPositions로 X 퍼짐 (1단계)
    /// spreadX=false: 각 돌의 현재 X 위치 유지 — 수직 던지기 (3단계)
    /// </summary>
    private IEnumerator DoStage5Toss(Stone[] stones, int count, float peakY, bool spreadX)
    {
        float[] targetX = spreadX ? GenerateSpreadPositions(count) : new float[count];
        float[] startX = new float[count];
        float[] startY = new float[count]; // 각 돌의 실제 시작 world position
        float[] peakOffset = new float[count];

        for (int i = 0; i < count; i++)
        {
            // SetParent(null) 전에 world position 읽기
            startX[i] = stones[i].transform.position.x;
            startY[i] = stones[i].transform.position.y;

            if (!spreadX) targetX[i] = startX[i]; // X 고정: 현재 위치 유지

            peakOffset[i] = Random.Range(-stage5HeightStep, stage5HeightStep);

            stones[i].transform.SetParent(null);
            stones[i].SetState(Stone.State.InAir); // layer=AirLayer, col=true
            stones[i].Rb.isKinematic = true;       // SetState 후 덮어쓰기 (코루틴 위치 제어용)
            stones[i].Rb.useGravity = false;
        }

        // 올라가기 (EaseOut)
        float elapsed = 0f;
        while (elapsed < throwUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwUpDuration);
            float eased = 1f - (1f - t) * (1f - t); // EaseOut

            for (int i = 0; i < count; i++)
            {
                float peak = peakY + peakOffset[i];
                float y = Mathf.Lerp(startY[i], peak, eased); // 각 돌의 실제 시작 Y
                float x = spreadX
                    ? Mathf.Lerp(startX[i], targetX[i], eased) // 손 위치에서 퍼짐
                    : targetX[i];
                stones[i].transform.position = new Vector3(x, y, 0f);
            }
            yield return null;
        }

        // 최고점 고정
        for (int i = 0; i < count; i++)
        {
            stones[i].transform.position = new Vector3(
                targetX[i], peakY + peakOffset[i], 0f);
        }

        Debug.Log($"[Stage5] Toss complete — stones at peak. peakY={peakY:F1}, spreadX={spreadX}");
    }

    /// <summary>
    /// 5개 돌의 하강 + 캐치 판정 코루틴.
    /// 플레이어는 커서 좌우 이동으로 손을 움직여 받는다.
    /// </summary>
    private IEnumerator RestoreHandScale(Vector3 originalScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * (2f - t); // EaseOut
            transform.localScale = Vector3.Lerp(startScale, originalScale, eased);
            yield return null;
        }
        transform.localScale = originalScale;
    }

    private IEnumerator DoStage5Catch(Stone[] stones, int count, System.Action<bool> onResult)
    {
        // === 슬라이드 인 (0.3초) ===
        // isCatchMode=false 상태 (호출 전 설정), LateUpdate 무동작
        stage5CatchActive = false;

        float slideStartX = 8f;       // 화면 오른쪽 밖 (boardMax.x=4 + 여유)
        float slideEndX = 0f;          // 화면 중앙
        float slideDuration = 0.3f;

        transform.position = new Vector3(slideStartX, catchAreaY, -0.5f);

        float slideElapsed = 0f;
        while (slideElapsed < slideDuration)
        {
            slideElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(slideElapsed / slideDuration);
            float eased = t * (2f - t);  // EaseOut 감속 도착
            float x = Mathf.Lerp(slideStartX, slideEndX, eased);
            transform.position = new Vector3(x, catchAreaY, -0.5f);
            yield return null;
        }
        transform.position = new Vector3(slideEndX, catchAreaY, -0.5f);

        // 슬라이드 인 완료 — 이제부터 LateUpdate가 X축 조작 처리
        SetCatchMode(true);
        stage5CatchActive = true;

        // === 손등 받기: 손 크기 2배로 확대 (0.3초 보간) ===
        float originalCatchRadius = stage5CatchRadius;
        float scaleDuration = 0.3f;
        float scaleElapsed = 0f;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * stage5BackhandScaleMultiplier;
        float targetRadius = originalCatchRadius * stage5BackhandScaleMultiplier;

        while (scaleElapsed < scaleDuration)
        {
            scaleElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(scaleElapsed / scaleDuration);
            float eased = t * (2f - t); // EaseOut
            transform.localScale = Vector3.Lerp(originalScale, targetScale, eased);
            stage5CatchRadius = Mathf.Lerp(originalCatchRadius, targetRadius, eased);
            yield return null;
        }
        transform.localScale = targetScale;
        stage5CatchRadius = targetRadius;

        // === 독립 낙하 시간 계산 ===
        bool[] caught = new bool[count];
        int caughtCount = 0;

        float[] stoneStartY = new float[count];
        float[] stoneX = new float[count];
        for (int i = 0; i < count; i++)
        {
            stoneStartY[i] = stones[i].transform.position.y;
            stoneX[i] = stones[i].transform.position.x;
        }

        float baseFallDuration = throwDownDuration * 1.2f;

        // 최고점/최저점 계산 (독립 낙하 시간 정규화용)
        float maxStartY = stoneStartY[0];
        float minStartY = stoneStartY[0];
        for (int i = 1; i < count; i++)
        {
            if (stoneStartY[i] > maxStartY) maxStartY = stoneStartY[i];
            if (stoneStartY[i] < minStartY) minStartY = stoneStartY[i];
        }

        float[] stoneElapsed = new float[count];
        float[] downDuration = new float[count];
        float maxDownDuration = 0f;

        for (int i = 0; i < count; i++)
        {
            float normalizedH = (maxStartY > minStartY)
                ? (stoneStartY[i] - minStartY) / (maxStartY - minStartY)
                : 0f;
            downDuration[i] = baseFallDuration + normalizedH * baseFallDuration;
            if (downDuration[i] > maxDownDuration) maxDownDuration = downDuration[i];
            Debug.Log($"[Stage5] Stone {stones[i].StoneIndex}: startY={stoneStartY[i]:F1}, normalizedH={normalizedH:F2}, downDuration={downDuration[i]:F2}s");
        }

        // v6-1: landY를 boardSurfaceY 기준으로 통일 (기존: catchAreaY - stage5MissThreshold)
        float landY = stage5BoardSurfaceY; // 보드 표면까지 내려오면 놓침

        // === 독립 낙하 루프 (EaseIn — 가속) ===
        float globalElapsed = 0f;
        while (globalElapsed < maxDownDuration)
        {
            globalElapsed += Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                if (caught[i]) continue;

                stoneElapsed[i] += Time.deltaTime;
                float t = Mathf.Clamp01(stoneElapsed[i] / downDuration[i]);
                float eased = t * t;  // EaseIn 가속 낙하
                float y = Mathf.Lerp(stoneStartY[i], landY - 1f, eased);
                stones[i].transform.position = new Vector3(stoneX[i], y, 0f);

                // 캐치 판정: catchAreaY 범위에 들어왔을 때 X 거리 체크
                if (y <= catchAreaY + 0.5f && y >= catchAreaY - 0.8f)
                {
                    float distX = Mathf.Abs(stoneX[i] - transform.position.x);
                    if (distX <= stage5CatchRadius)
                    {
                        caught[i] = true;
                        caughtCount++;
                        AudioManager.Instance?.PlayStage5CatchStone(caughtCount);
                        stones[i].SetState(Stone.State.Caught);
                        stones[i].Rb.isKinematic = true;
                        // 손에 부착 — 손이 움직이면 돌도 함께 이동 (그릇 안 담긴 느낌)
                        stones[i].transform.SetParent(transform);
                        stones[i].transform.localPosition = new Vector3((caughtCount - 1) * 0.25f - 0.5f, 0.1f, 0f);
                        Debug.Log($"[Stage5] Caught stone {stones[i].StoneIndex}! ({caughtCount}/{count})");
                    }
                }

                // 놓침 판정: 손 아래로 지나감
                if (y < landY && !caught[i])
                {
                    Debug.Log($"[Stage5] MISSED stone {stones[i].StoneIndex}!");
                    TestLogger.Instance?.LogFailure($"stage5_miss_stone_{stones[i].StoneIndex}");
                    stage5CatchActive = false;
                    // 실패 시 즉시 원래 크기로 복원
                    transform.localScale = originalScale;
                    stage5CatchRadius = originalCatchRadius;
                    SetCatchMode(false);
                    onResult?.Invoke(false);
                    GameManager.Instance.SetFailReason("돌을 놓쳤다!");
                    GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                    yield break;
                }
            }

            // 모두 잡으면 조기 종료
            if (caughtCount >= count)
            {
                stage5CatchActive = false;
                // 손 크기 복원 (0.2초 보간)
                yield return RestoreHandScale(originalScale, 0.2f);
                stage5CatchRadius = originalCatchRadius;
                SetCatchMode(false);
                onResult?.Invoke(true);
                yield break;
            }

            yield return null;
        }

        // 시간 초과
        stage5CatchActive = false;
        // 시간 초과 시 즉시 원래 크기로 복원
        transform.localScale = originalScale;
        stage5CatchRadius = originalCatchRadius;
        SetCatchMode(false);
        if (caughtCount < count)
        {
            Debug.Log($"[Stage5] Time up! Only caught {caughtCount}/{count}");
            TestLogger.Instance?.LogFailure($"stage5_timeout_{caughtCount}_of_{count}");
            GameManager.Instance.SetFailReason("시간 초과!");
            onResult?.Invoke(false);
        }
        else
        {
            onResult?.Invoke(true);
        }
    }

    /// <summary>
    /// 4단계: 한붓그리기 낚아채기.
    /// 홀드(Press) + 드래그로 떨어지는 돌을 스쳐 지나가며 하나씩 낚아챔.
    /// Release 시 5개 모두 잡혔으면 성공, 아니면 실패.
    /// </summary>
    private IEnumerator DoStage5FistGrab(Stone[] stones, int count, System.Action<bool> callback)
    {
        // 슬라이드 인
        float slideStartX = 8f;
        float slideDuration = 0.3f;
        float slideElapsed = 0f;

        transform.position = new Vector3(slideStartX, catchAreaY, -0.5f);

        while (slideElapsed < slideDuration)
        {
            slideElapsed += Time.deltaTime;
            float t = slideElapsed / slideDuration;
            float eased = t * (2f - t);
            float x = Mathf.Lerp(slideStartX, 0f, eased);
            transform.position = new Vector3(x, catchAreaY, -0.5f);
            yield return null;
        }

        SetCatchMode(true);
        stage5CatchActive = true;

        // === 손바닥 받기: 손 크기 2배로 확대 (0.3초 보간) ===
        float originalGrabRadius = stage5FistGrabRadius;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * stage5BackhandScaleMultiplier;
        float targetGrabRadius = originalGrabRadius * stage5BackhandScaleMultiplier;
        {
            float scaleElapsed = 0f;
            float scaleDuration = 0.3f;
            while (scaleElapsed < scaleDuration)
            {
                scaleElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(scaleElapsed / scaleDuration);
                float eased = t * (2f - t);
                transform.localScale = Vector3.Lerp(originalScale, targetScale, eased);
                stage5FistGrabRadius = Mathf.Lerp(originalGrabRadius, targetGrabRadius, eased);
                yield return null;
            }
            transform.localScale = targetScale;
            stage5FistGrabRadius = targetGrabRadius;
        }

        // 각 돌의 낙하 시작 위치
        float[] stoneStartY = new float[count];
        float[] stoneX = new float[count];
        float[] downDuration = new float[count];
        float[] stoneElapsed = new float[count];
        bool[] caught = new bool[count];
        int caughtCount = 0;

        float minStartY = float.MaxValue, maxStartY = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            stoneStartY[i] = stones[i].transform.position.y;
            stoneX[i] = stones[i].transform.position.x;
            stoneElapsed[i] = 0f;
            caught[i] = false;
            if (stoneStartY[i] < minStartY) minStartY = stoneStartY[i];
            if (stoneStartY[i] > maxStartY) maxStartY = stoneStartY[i];
        }

        float baseFallDuration = 1.8f;
        float heightRange = maxStartY - minStartY;
        for (int i = 0; i < count; i++)
        {
            float normalizedH = heightRange > 0.01f
                ? (stoneStartY[i] - minStartY) / heightRange
                : 0f;
            downDuration[i] = baseFallDuration * (1f + normalizedH);
        }

        // v6-1: landY를 boardSurfaceY 기준으로 통일 (기존: catchAreaY - 3.5f)
        float landY = stage5BoardSurfaceY;
        float maxDownDuration = baseFallDuration * 3f;
        float globalElapsed = 0f;
        bool isGrabbing = false; // 홀드 중 (한붓그리기 활성)

        // 낙하 + 한붓그리기 루프
        while (globalElapsed < maxDownDuration)
        {
            globalElapsed += Time.deltaTime;
            bool anyReachedFloor = false;

            // 돌 위치 업데이트 (잡히지 않은 돌만)
            for (int i = 0; i < count; i++)
            {
                if (caught[i]) continue;
                stoneElapsed[i] += Time.deltaTime;
                float t = Mathf.Clamp01(stoneElapsed[i] / downDuration[i]);
                float y = Mathf.Lerp(stoneStartY[i], landY - 1f, t * t);
                stones[i].transform.position = new Vector3(stoneX[i], y, 0f);

                if (y <= landY)
                    anyReachedFloor = true;
            }

            // 홀드 감지: clickAction이 눌려있는지
            bool pressed = clickAction.IsPressed();

            if (pressed && !isGrabbing)
            {
                // Press 시작 → 한붓그리기 시작
                isGrabbing = true;
                AnimateFingerFold(true);
                Debug.Log("[Stage5] Fist grab started (hold)");
            }

            if (isGrabbing && pressed)
            {
                // 홀드 중 → 매 프레임 손 근처 돌 체크
                Vector2 handPos = new Vector2(transform.position.x, transform.position.y);
                for (int i = 0; i < count; i++)
                {
                    if (caught[i]) continue;
                    Vector2 stonePos = new Vector2(stones[i].transform.position.x, stones[i].transform.position.y);
                    float dist = Vector2.Distance(handPos, stonePos);
                    if (dist <= stage5FistGrabRadius)
                    {
                        caught[i] = true;
                        caughtCount++;
                        stones[i].Rb.isKinematic = true;
                        stones[i].SetState(Stone.State.Caught);
                        stones[i].transform.SetParent(transform);
                        stones[i].transform.localPosition = new Vector3((caughtCount - 3) * 0.15f, 0f, 0f);
                        AudioManager.Instance?.PlayStage5CatchStone(caughtCount);
                        Debug.Log($"[Stage5] Grabbed stone {stones[i].StoneIndex}! ({caughtCount}/{count})");
                    }
                }

                // 홀드 중 5개 모두 잡으면 즉시 성공
                if (caughtCount >= count)
                {
                    AudioManager.Instance?.PlayStageClear();
                    yield return new WaitForSeconds(0.5f);
                    stage5CatchActive = false;
                    yield return RestoreHandScale(originalScale, 0.2f);
                    stage5FistGrabRadius = originalGrabRadius;
                    SetCatchMode(false);
                    AnimateFingerFold(false);
                    callback?.Invoke(true);
                    yield break;
                }
            }

            if (!pressed && isGrabbing)
            {
                // Release → 한붓그리기 종료
                isGrabbing = false;
                Debug.Log($"[Stage5] Fist grab released: {caughtCount}/{count} caught");

                if (caughtCount >= count)
                {
                    // 전부 잡음 (Release와 동시)
                    AudioManager.Instance?.PlayStageClear();
                    yield return new WaitForSeconds(0.5f);
                    stage5CatchActive = false;
                    yield return RestoreHandScale(originalScale, 0.2f);
                    stage5FistGrabRadius = originalGrabRadius;
                    SetCatchMode(false);
                    AnimateFingerFold(false);
                    callback?.Invoke(true);
                    yield break;
                }
                else
                {
                    // 미완성 → 실패
                    yield return new WaitForSeconds(0.5f);
                    stage5CatchActive = false;
                    transform.localScale = originalScale;
                    stage5FistGrabRadius = originalGrabRadius;
                    SetCatchMode(false);
                    AnimateFingerFold(false);
                    GameManager.Instance.SetFailReason("돌을 놓쳤다!");
                    GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                    callback?.Invoke(false);
                    yield break;
                }
            }

            // 미입력 + 바닥 도달 = 실패
            if (anyReachedFloor && !isGrabbing)
            {
                stage5CatchActive = false;
                transform.localScale = originalScale;
                stage5FistGrabRadius = originalGrabRadius;
                SetCatchMode(false);
                AnimateFingerFold(false);
                AudioManager.Instance?.PlayCatchFail();
                GameManager.Instance.SetFailReason("돌을 놓쳤다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                callback?.Invoke(false);
                yield break;
            }

            yield return null;
        }

        // 타임아웃
        stage5CatchActive = false;
        transform.localScale = originalScale;
        stage5FistGrabRadius = originalGrabRadius;
        SetCatchMode(false);
        AnimateFingerFold(false);
        AudioManager.Instance?.PlayCatchFail();
        GameManager.Instance.SetFailReason("시간 초과!");
        GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
        callback?.Invoke(false);
    }

    /// <summary>
    /// 최소 간격을 보장하며 랜덤 X 위치를 생성
    /// </summary>
    private float[] GenerateSpreadPositions(int count)
    {
        float[] positions = new float[count];
        int maxAttempts = 50;

        for (int i = 0; i < count; i++)
        {
            bool valid = false;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float x = Random.Range(-stage5SpreadRange, stage5SpreadRange);
                bool tooClose = false;

                for (int j = 0; j < i; j++)
                {
                    if (Mathf.Abs(x - positions[j]) < stage5MinSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    positions[i] = x;
                    valid = true;
                    break;
                }
            }

            // 못 찾으면 균등 분배 fallback
            if (!valid)
            {
                positions[i] = Mathf.Lerp(-stage5SpreadRange, stage5SpreadRange,
                    (float)i / (count - 1));
            }
        }

        return positions;
    }

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
    /// X축 회전 접힘 각도: 양수 = 화면 안쪽으로 말림 (주먹 쥐기)
    private const float FingerFoldX = 90f;

    public void SetHandPose(HandPose pose)
    {
        if (handModel == null || handModel.Fingers == null) return;
        // X축 회전 각도 배열: 0 = 펼침, FingerFoldX = 접힘
        float[] targets;
        switch (pose)
        {
            case HandPose.PointIndex:
                // 검지(1)만 펼침, 나머지 접힘
                targets = new float[] { FingerFoldX, 0f, FingerFoldX, FingerFoldX, FingerFoldX };
                break;
            case HandPose.PointMiddle:
                // 중지(2)만 펼침, 나머지 접힘
                targets = new float[] { FingerFoldX, FingerFoldX, 0f, FingerFoldX, FingerFoldX };
                break;
            default: // Open
                targets = new float[] { 0f, 0f, 0f, 0f, 0f };
                break;
        }
        if (fingerFoldCoroutine != null) StopCoroutine(fingerFoldCoroutine);
        fingerFoldCoroutine = StartCoroutine(DoFingerFoldCustom(targets));
    }

    private IEnumerator DoFingerFoldCustom(float[] targetAngles)
    {
        if (handModel == null || handModel.Fingers == null) yield break;

        float duration = 0.1f;
        float elapsed = 0f;
        float[] startAngles = new float[handModel.Fingers.Length];
        for (int i = 0; i < handModel.Fingers.Length; i++)
        {
            startAngles[i] = handModel.Fingers[i].localEulerAngles.x;
            if (startAngles[i] > 180f) startAngles[i] -= 360f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < handModel.Fingers.Length; i++)
            {
                float angle = Mathf.Lerp(startAngles[i], targetAngles[i], t);
                handModel.Fingers[i].localEulerAngles = new Vector3(angle, 0f, 0f);
            }
            yield return null;
        }
        for (int i = 0; i < handModel.Fingers.Length; i++)
        {
            handModel.Fingers[i].localEulerAngles = new Vector3(targetAngles[i], 0f, 0f);
        }
        fingerFoldCoroutine = null;
    }

    /// <summary>손가락 접힘/펼침 애니메이션</summary>
    private void AnimateFingerFold(bool fold)
    {
        if (fingerFoldCoroutine != null)
            StopCoroutine(fingerFoldCoroutine);
        fingerFoldCoroutine = StartCoroutine(DoFingerFold(fold));
    }

    private IEnumerator DoFingerFold(bool fold)
    {
        if (handModel == null || handModel.Fingers == null) yield break;

        // X축 회전: 양수 = 화면 안쪽으로 말림 (주먹 쥐기)
        // 모든 손가락 동일 각도 (90도)
        float duration = 0.1f;
        float elapsed = 0f;

        float[] startAngles = new float[handModel.Fingers.Length];
        float[] targetAngles = new float[handModel.Fingers.Length];
        for (int i = 0; i < handModel.Fingers.Length; i++)
        {
            startAngles[i] = handModel.Fingers[i].localEulerAngles.x;
            if (startAngles[i] > 180f) startAngles[i] -= 360f;
            targetAngles[i] = fold ? FingerFoldX : 0f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < handModel.Fingers.Length; i++)
            {
                float angle = Mathf.Lerp(startAngles[i], targetAngles[i], t);
                handModel.Fingers[i].localEulerAngles = new Vector3(angle, 0f, 0f);
            }
            yield return null;
        }

        for (int i = 0; i < handModel.Fingers.Length; i++)
        {
            handModel.Fingers[i].localEulerAngles = new Vector3(targetAngles[i], 0f, 0f);
        }

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
        // 손가락 펼침 상태로 즉시 복원
        if (handModel != null && handModel.Fingers != null)
        {
            foreach (var finger in handModel.Fingers)
            {
                if (finger != null)
                    finger.localEulerAngles = new Vector3(finger.localEulerAngles.x, finger.localEulerAngles.y, 0f);
            }
        }

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
