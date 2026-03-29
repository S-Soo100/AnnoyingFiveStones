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
    [SerializeField] private float stage5CatchRadius = 1.8f;    // 5개 동시 캐치 반경
    [SerializeField] private float stage5SpreadRange = 2.5f;     // 돌 퍼짐 범위
    [SerializeField] private float stage5MinSpacing = 0.8f;      // 돌 최소 간격
    [SerializeField] private float stage5MissThreshold = 3.5f;   // catchAreaY - 이 값 아래로 지나가면 실패 (보드 근처까지 허용)

    private bool stage5ClickPending;
    private bool stage5CatchActive;                            // 슬라이드 인 완료 후 true — LateUpdate Y 고정 + X 추종 트리거
    private Coroutine stage5Coroutine;

    [Header("Stage 5 Height Settings")]
    [SerializeField] private float stage5HeightStep = 1.5f;   // 돌 간 높이 간격

    public List<Stone> PickedStones => pickedStones;
    public Stone ThrowStone => throwStone;
    public bool IsOnBoard => isOnBoard;

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
    }

    private void UpdatePosition()
    {
        Vector2 screenPos = pointerAction.ReadValue<Vector2>();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        worldPos.z = -0.5f;
        transform.position = worldPos;
        // Hitbox 위치 동기화 (회전 독립)
        handModel?.SyncHitboxPosition(worldPos);
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
                pickedStones.Add(stone);
                stone.SetState(Stone.State.InHand);
                stone.transform.SetParent(transform);
                stone.transform.localPosition = Vector3.up * 0.3f * pickedStones.Count;
                AudioManager.Instance?.PlayStonePick(pickedStones.Count);
                TestLogger.Instance?.LogStoneState(stone.StoneIndex, "hold_picked", stone.transform.position);

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
            StartCoroutine(DoThrow());
        }
        else if (phase == GameManager.GamePhase.Stage5Throw)
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
        elapsed = 0f;
        while (elapsed < throwDownDuration)
        {
            if (catchSystem != null && !catchSystem.IsCatchPhase)
            {
                Debug.Log("[Hand] Stone caught!");
                SetCatchMode(false);
                handModel?.SetCollidersEnabled(false);
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwDownDuration);
            float eased = t * t;
            float y = Mathf.Lerp(throwPeakY, startY, eased);

            // catchAreaY 도달: 코루틴 제어에서 물리 낙하로 전환
            if (y <= catchAreaY && stone.Rb.isKinematic)
            {
                stone.Rb.isKinematic = false;
                stone.Rb.useGravity = true;
                // 현재 코루틴 하강 속도 유지 (EaseIn t²에 의한 순간 속도)
                float instantSpeed = (throwPeakY - startY) / throwDownDuration * 2f * t;
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

        // 못 받음 → 실패
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
        Vector3 worldPos2 = mainCamera.ScreenToWorldPoint(new Vector3(screenPos2.x, screenPos2.y, 10f));
        float clampedY = Mathf.Max(worldPos2.y, boardMin.y);
        transform.position = new Vector3(worldPos2.x, clampedY, -0.5f);
        handModel?.SyncHitboxPosition(transform.position);
    }

    // === 5단 꺾기 ===

    /// <summary>
    /// GameManager에서 호출: 5단 시퀀스 시작
    /// </summary>
    public void BeginStage5Throw()
    {
        stage5ClickPending = false;
        stage5Coroutine = StartCoroutine(DoStage5Sequence());
    }

    /// <summary>
    /// 5단 꺾기 전체 시퀀스:
    /// 1차 던지기(클릭) → 손등 받기 → 2차 던지기(자동) → 손바닥 받기 → 클리어
    /// </summary>
    private IEnumerator DoStage5Sequence()
    {
        var gm = GameManager.Instance;
        Stone[] allStones = gm.Stones;
        int stoneCount = allStones.Length; // 5

        // === [1차 던지기 대기] — 클릭 대기 ===
        // 손을 화면 중앙 하단에 위치시키고 돌들을 모아 표시
        SetCatchMode(false);

        // 돌들을 손 위치에 모음 (시각적으로 쥐고 있는 느낌)
        Vector3 handStartPos = new Vector3(0f, catchAreaY - 2f, -0.5f);
        transform.position = handStartPos;

        for (int i = 0; i < stoneCount; i++)
        {
            float offsetX = (i - 2) * 0.4f;
            float offsetY = (i % 2) * 0.2f;
            allStones[i].transform.localPosition = new Vector3(offsetX, offsetY, 0f);
        }

        // 클릭 대기
        stage5ClickPending = false;
        yield return new WaitUntil(() => stage5ClickPending);
        stage5ClickPending = false;

        // === [1차 던지기] — 5개 동시에 하늘로 ===
        AudioManager.Instance?.PlayStage5Toss();
        yield return DoStage5Toss(allStones, stoneCount);
        if (gm.CurrentPhase == GameManager.GamePhase.Failed) yield break;

        // === [손등 받기] ===
        SetCatchMode(false);              // 슬라이드 인 중 LateUpdate 차단
        stage5CatchActive = false;        // 명시적 초기화
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        bool success = false;
        yield return DoStage5Catch(allStones, stoneCount, (result) => success = result);
        if (!success)
        {
            SetCatchMode(false);
            if (gm.CurrentPhase != GameManager.GamePhase.Failed)
                gm.SetPhase(GameManager.GamePhase.Failed);
            yield break;
        }

        Debug.Log("[Stage5] Back-hand catch SUCCESS! Preparing 2nd toss...");
        TestLogger.Instance?.LogCatch(true, 0f);

        // 모든 돌을 Caught → 손에 부착
        for (int i = 0; i < stoneCount; i++)
        {
            allStones[i].SetState(Stone.State.Caught);
            allStones[i].transform.SetParent(transform);
            allStones[i].transform.localPosition = new Vector3((i - 2) * 0.3f, 0f, 0f);
        }

        // === [2차 던지기 준비] — 0.5초 대기 후 자동 ===
        gm.AdvanceStage5Step(); // step → 1 (손바닥)
        SetCatchMode(false);

        yield return new WaitForSeconds(0.5f);

        // === [2차 던지기] — 손등 자세 그대로 쳐올리기 ===
        AudioManager.Instance?.PlayStage5Toss();
        yield return DoStage5Toss(allStones, stoneCount);
        if (gm.CurrentPhase == GameManager.GamePhase.Failed) yield break;

        // === [손바닥 받기] ===
        SetCatchMode(false);              // 슬라이드 인 중 LateUpdate 차단
        stage5CatchActive = false;
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        success = false;
        yield return DoStage5Catch(allStones, stoneCount, (result) => success = result);
        if (!success)
        {
            SetCatchMode(false);
            if (gm.CurrentPhase != GameManager.GamePhase.Failed)
                gm.SetPhase(GameManager.GamePhase.Failed);
            yield break;
        }

        Debug.Log("[Stage5] Palm catch SUCCESS! ALL CLEAR!");
        TestLogger.Instance?.LogCatch(true, 0f);

        // === 전부 성공 → 클리어 ===
        SetCatchMode(false);
        stage5Coroutine = null;

        // 돌들 정리
        for (int i = 0; i < stoneCount; i++)
        {
            allStones[i].SetState(Stone.State.Caught);
            allStones[i].transform.SetParent(null);
            allStones[i].gameObject.SetActive(false);
        }

        gm.SetPhase(GameManager.GamePhase.StageComplete);
    }

    /// <summary>
    /// 5개 돌을 동시에 하늘로 던지는 코루틴.
    /// 돌들을 손에서 분리하고 각각 랜덤 X 오프셋으로 올린다.
    /// </summary>
    private IEnumerator DoStage5Toss(Stone[] stones, int count)
    {
        float[] targetX = GenerateSpreadPositions(count);
        float[] peakOffset = new float[count];

        float startY = catchAreaY;

        for (int i = 0; i < count; i++)
        {
            peakOffset[i] = i * stage5HeightStep + Random.Range(-0.3f, 0.3f);
            stones[i].transform.SetParent(null);
            stones[i].Rb.isKinematic = true;
            stones[i].Rb.useGravity = false;
            stones[i].SetState(Stone.State.InAir);
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
                float peak = throwPeakY + peakOffset[i];
                float y = Mathf.Lerp(startY, peak, eased);
                float x = Mathf.Lerp(0f, targetX[i], eased);
                stones[i].transform.position = new Vector3(x, y, 0f);
            }
            yield return null;
        }

        // 최고점 고정
        for (int i = 0; i < count; i++)
        {
            stones[i].transform.position = new Vector3(
                targetX[i], throwPeakY + peakOffset[i], 0f);
        }

        Debug.Log("[Stage5] Toss complete — stones at peak.");
    }

    /// <summary>
    /// 5개 돌의 하강 + 캐치 판정 코루틴.
    /// 플레이어는 커서 좌우 이동으로 손을 움직여 받는다.
    /// </summary>
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

        float landY = catchAreaY - stage5MissThreshold; // 이 아래로 가면 놓침

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
                        stones[i].transform.position = new Vector3(stoneX[i], catchAreaY, 0f);
                        Debug.Log($"[Stage5] Caught stone {stones[i].StoneIndex}! ({caughtCount}/{count})");
                    }
                }

                // 놓침 판정: 손 아래로 지나감
                if (y < landY && !caught[i])
                {
                    Debug.Log($"[Stage5] MISSED stone {stones[i].StoneIndex}!");
                    TestLogger.Instance?.LogFailure($"stage5_miss_stone_{stones[i].StoneIndex}");
                    stage5CatchActive = false;
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
                SetCatchMode(false);
                onResult?.Invoke(true);
                yield break;
            }

            yield return null;
        }

        // 시간 초과
        stage5CatchActive = false;
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

            // 물리 hitbox는 회전과 무관하게 월드 기준 위치 설정
            // → hitbox를 hand의 자식에서 분리하여 월드 좌표로 직접 배치하면 회전 영향 안 받음
            // → 대신 매 프레임 LateUpdate에서 hand 위치를 따라가게 함
        }
        else
        {
            transform.localEulerAngles = Vector3.zero;
        }
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

        // 손가락별 접힘 방향 (안쪽으로 모이도록)
        // Thumb(-0.55) → +Z로 회전, Index(-0.2) → +Z, Middle(0) → +Z,
        // Ring(+0.2) → -Z, Pinky(+0.38) → -Z
        // 왼쪽 손가락(엄지~중지)은 시계방향(음수Z)으로 접혀야 안쪽
        // 오른쪽 손가락(약지~소지)은 반시계방향(양수Z)으로 접혀야 안쪽
        float[] foldAngles = { -70f, -50f, -40f, 50f, 70f };
        float duration = 0.1f;
        float elapsed = 0f;

        // 현재 각도 저장
        float[] startAngles = new float[handModel.Fingers.Length];
        float[] targetAngles = new float[handModel.Fingers.Length];
        for (int i = 0; i < handModel.Fingers.Length; i++)
        {
            startAngles[i] = handModel.Fingers[i].localEulerAngles.z;
            if (startAngles[i] > 180f) startAngles[i] -= 360f;
            targetAngles[i] = fold ? foldAngles[i] : 0f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < handModel.Fingers.Length; i++)
            {
                float angle = Mathf.Lerp(startAngles[i], targetAngles[i], t);
                var euler = handModel.Fingers[i].localEulerAngles;
                handModel.Fingers[i].localEulerAngles = new Vector3(euler.x, euler.y, angle);
            }
            yield return null;
        }

        // 최종 값 보정
        for (int i = 0; i < handModel.Fingers.Length; i++)
        {
            var euler = handModel.Fingers[i].localEulerAngles;
            handModel.Fingers[i].localEulerAngles = new Vector3(euler.x, euler.y, targetAngles[i]);
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
    public void ResetHand()
    {
        // 5단 코루틴이 실행 중이면 중단
        if (stage5Coroutine != null)
        {
            StopCoroutine(stage5Coroutine);
            stage5Coroutine = null;
        }
        stage5ClickPending = false;
        stage5CatchActive = false;

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
