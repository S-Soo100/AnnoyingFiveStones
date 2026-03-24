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
    [Header("Pick Up")]
    [SerializeField] private float pickupRadius = 0.8f;

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
    private SpriteRenderer spriteRenderer;
    private InputAction pointerAction;
    private InputAction clickAction;

    private List<Stone> pickedStones = new List<Stone>();
    private Stone throwStone;
    private bool isOnBoard;
    private bool isCatchMode;

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
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureHandSprite();

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
    }

    private void OnDisable()
    {
        clickAction.performed -= OnClick;
        pointerAction.Disable();
        clickAction.Disable();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (isCatchMode) return;

        UpdatePosition();
        UpdateVisibility();

        var phase = GameManager.Instance.CurrentPhase;

        if (phase == GameManager.GamePhase.PickThrowStone)
        {
            TryAutoPickThrowStone();
        }
        else if (phase == GameManager.GamePhase.PickStones)
        {
            TryAutoPickStones();
        }
    }

    private void UpdatePosition()
    {
        Vector2 screenPos = pointerAction.ReadValue<Vector2>();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        worldPos.z = -0.5f;
        transform.position = worldPos;
    }

    private void UpdateVisibility()
    {
        if (GameManager.Instance == null) return;

        Vector3 pos = transform.position;
        isOnBoard = pos.x >= boardMin.x && pos.x <= boardMax.x
                 && pos.y >= boardMin.y && pos.y <= boardMax.y;

        var phase = GameManager.Instance.CurrentPhase;
        bool visible = isOnBoard
                    || phase == GameManager.GamePhase.PickStones
                    || phase == GameManager.GamePhase.Catch
                    || phase == GameManager.GamePhase.Stage5Throw
                    || phase == GameManager.GamePhase.Stage5Catch;

        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
    }

    // === 던질 돌 고르기 ===

    private void TryAutoPickThrowStone()
    {
        if (throwStone != null) return;
        if (GameManager.Instance.IsTransitioning) return;

        var stonesInRange = GetStonesInRange();

        if (stonesInRange.Count == 1)
        {
            throwStone = stonesInRange[0];
            throwStone.SetState(Stone.State.InHand);
            throwStone.transform.SetParent(transform);
            throwStone.transform.localPosition = Vector3.zero;
            AudioManager.Instance?.PlayStonePickThrow();
            TestLogger.Instance?.LogStoneState(throwStone.StoneIndex, "picked_as_throw", throwStone.transform.position);
            GameManager.Instance.SetPhase(GameManager.GamePhase.Throw);
        }
        else if (stonesInRange.Count >= 2)
        {
            AudioManager.Instance?.PlayPickExcess();
            TestLogger.Instance?.LogFailure("pick_throw_excess");
            GameManager.Instance.SetFailReason("돌을 너무 많이 집었다!");
            GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
        }
    }

    // === 바닥 돌 줍기 ===

    private void TryAutoPickStones()
    {
        if (GameManager.Instance.IsTransitioning) return;
        var stonesInRange = GetStonesInRange();
        int required = GameManager.Instance.RequiredPickCount;

        foreach (var stone in stonesInRange)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;
            if (pickedStones.Contains(stone)) continue;

            pickedStones.Add(stone);
            stone.SetState(Stone.State.InHand);
            stone.transform.SetParent(transform);
            stone.transform.localPosition = Vector3.up * 0.3f * pickedStones.Count;
            AudioManager.Instance?.PlayStonePick(pickedStones.Count);

            TestLogger.Instance?.LogStoneState(stone.StoneIndex, "auto_picked", stone.transform.position);

            if (pickedStones.Count > required)
            {
                AudioManager.Instance?.PlayPickExcess();
                TestLogger.Instance?.LogFailure($"pick_excess_{pickedStones.Count}_of_{required}");
                GameManager.Instance.SetFailReason("돌을 너무 많이 집었다!");
                GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
                return;
            }

            if (pickedStones.Count == required)
            {
                // v3: 제자리에서 받기 모드 전환 (자동 상승 제거)
                isCatchMode = true;
                SetHandMode(true);
                Debug.Log("[Hand] Pick complete — catch mode ON (stay in place)");
                return;
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

        // 낙하 사운드
        AudioManager.Instance?.PlayThrowDown();

        // === 내려오기 (EaseIn — 가속 낙하) ===
        elapsed = 0f;
        while (elapsed < throwDownDuration)
        {
            if (catchSystem != null && !catchSystem.IsCatchPhase)
            {
                Debug.Log("[Hand] Stone caught!");
                isCatchMode = false;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwDownDuration);
            float eased = t * t;
            float y = Mathf.Lerp(throwPeakY, startY, eased);
            stone.transform.position = new Vector3(startX, y, 0f);
            yield return null;
        }

        // 못 받음 → 실패
        AudioManager.Instance?.PlayCatchFail();
        if (catchSystem != null) catchSystem.StopCatch();
        isCatchMode = false;
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

        // 5단 받기: stage5CatchActive일 때 Y=catchAreaY 고정, X만 커서 추종
        if (stage5CatchActive)
        {
            Vector2 screenPos = pointerAction.ReadValue<Vector2>();
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
            transform.position = new Vector3(worldPos.x, catchAreaY, -0.5f);

            if (spriteRenderer != null)
                spriteRenderer.enabled = true;
            return;
        }

        // 1~4단 받기: X/Y 모두 커서 추종, Y 하한 = boardMin.y
        Vector2 screenPos2 = pointerAction.ReadValue<Vector2>();
        Vector3 worldPos2 = mainCamera.ScreenToWorldPoint(new Vector3(screenPos2.x, screenPos2.y, 10f));
        float clampedY = Mathf.Max(worldPos2.y, boardMin.y);
        transform.position = new Vector3(worldPos2.x, clampedY, -0.5f);

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
    }

    // === 유틸리티 ===

    private Sprite boardSprite;      // 보드: 노란 반투명 원 (줍기 범위)
    private Sprite catchSprite;      // 하늘: 손바닥 모양 (받기)
    private Sprite backHandSprite;   // 5단: 손등 모양

    private void EnsureHandSprite()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.sortingOrder = 10;

        boardSprite = CreateCircleSprite();
        catchSprite = CreatePalmSprite();
        backHandSprite = CreateBackHandSprite();
        spriteRenderer.sprite = boardSprite;
    }

    /// <summary>
    /// 모드에 따라 스프라이트 전환
    /// </summary>
    private void SetHandMode(bool catching)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = catching ? catchSprite : boardSprite;
        spriteRenderer.color = catching
            ? new Color(1f, 0.85f, 0.6f, 0.7f)   // 캐치: 살색 불투명
            : new Color(1f, 0.92f, 0.3f, 0.35f);  // 보드: 노란 반투명
        // v3: 받기 모드 시 손가락이 왼쪽을 향하도록 +90도 회전
        transform.localEulerAngles = catching ? new Vector3(0, 0, 90f) : Vector3.zero;
    }

    /// <summary>보드용: 작은 반투명 원 (줍기 범위 표시)</summary>
    private Sprite CreateCircleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 2;

        for (int px = 0; px < size; px++)
            for (int py = 0; py < size; py++)
            {
                float dist = Vector2.Distance(new Vector2(px, py), new Vector2(center, center));
                // 가장자리만 보이는 링 형태
                if (dist <= radius && dist > radius - 3)
                    tex.SetPixel(px, py, new Color(1f, 0.92f, 0.3f, 0.6f));
                else if (dist <= radius)
                    tex.SetPixel(px, py, new Color(1f, 0.92f, 0.3f, 0.15f));
                else
                    tex.SetPixel(px, py, Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    /// <summary>캐치용: 손바닥을 위로 벌린 모양</summary>
    private Sprite CreatePalmSprite()
    {
        int w = 64, h = 80;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color skin = new Color(1f, 0.85f, 0.6f, 0.8f);
        Color outline = new Color(0.7f, 0.5f, 0.3f, 0.9f);

        // 투명 초기화
        for (int px = 0; px < w; px++)
            for (int py = 0; py < h; py++)
                tex.SetPixel(px, py, Color.clear);

        // 손바닥 (하단 타원)
        DrawEllipse(tex, w / 2, 20, 22, 18, skin, outline);

        // 손가락 5개 (상단으로 뻗음)
        int[] fingerX = { 10, 20, 32, 42, 50 };
        int[] fingerH = { 18, 26, 28, 24, 16 };
        int[] fingerW = { 5, 5, 5, 5, 5 };

        for (int i = 0; i < 5; i++)
        {
            int baseY = 34;
            for (int fy = 0; fy < fingerH[i]; fy++)
            {
                // 손가락 끝으로 갈수록 좁아짐
                float taper = 1f - (float)fy / fingerH[i] * 0.3f;
                int halfW = Mathf.Max(1, (int)(fingerW[i] * taper));
                for (int fx = -halfW; fx <= halfW; fx++)
                {
                    int px = fingerX[i] + fx;
                    int py = baseY + fy;
                    if (px >= 0 && px < w && py >= 0 && py < h)
                    {
                        bool edge = Mathf.Abs(fx) == halfW || fy == fingerH[i] - 1;
                        tex.SetPixel(px, py, edge ? outline : skin);
                    }
                }
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.25f), 48f);
    }

    private void DrawEllipse(Texture2D tex, int cx, int cy, int rx, int ry, Color fill, Color edge)
    {
        for (int px = cx - rx; px <= cx + rx; px++)
            for (int py = cy - ry; py <= cy + ry; py++)
            {
                if (px < 0 || px >= tex.width || py < 0 || py >= tex.height) continue;
                float dx = (float)(px - cx) / rx;
                float dy = (float)(py - cy) / ry;
                float d = dx * dx + dy * dy;
                if (d <= 1f)
                    tex.SetPixel(px, py, d > 0.85f ? edge : fill);
            }
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
        isCatchMode = false;
        SetHandMode(false);

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
        SetHandMode_BackHand();
        isCatchMode = false;              // 슬라이드 인 중 LateUpdate 차단
        stage5CatchActive = false;        // 명시적 초기화
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        bool success = false;
        yield return DoStage5Catch(allStones, stoneCount, (result) => success = result);
        if (!success)
        {
            isCatchMode = false;
            SetHandMode(false);
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
        isCatchMode = false;

        yield return new WaitForSeconds(0.5f);

        // === [2차 던지기] — 손등 자세 그대로 쳐올리기 ===
        AudioManager.Instance?.PlayStage5Toss();
        yield return DoStage5Toss(allStones, stoneCount);
        if (gm.CurrentPhase == GameManager.GamePhase.Failed) yield break;

        // === [손바닥 받기] ===
        SetHandMode(true); // 기존 손바닥 스프라이트
        isCatchMode = false;              // 슬라이드 인 중 LateUpdate 차단
        stage5CatchActive = false;
        gm.SetPhase(GameManager.GamePhase.Stage5Catch);

        success = false;
        yield return DoStage5Catch(allStones, stoneCount, (result) => success = result);
        if (!success)
        {
            isCatchMode = false;
            SetHandMode(false);
            if (gm.CurrentPhase != GameManager.GamePhase.Failed)
                gm.SetPhase(GameManager.GamePhase.Failed);
            yield break;
        }

        Debug.Log("[Stage5] Palm catch SUCCESS! ALL CLEAR!");
        TestLogger.Instance?.LogCatch(true, 0f);

        // === 전부 성공 → 클리어 ===
        isCatchMode = false;
        SetHandMode(false);
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
        if (spriteRenderer != null) spriteRenderer.enabled = true;

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
        isCatchMode = true;
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
                    isCatchMode = false;
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
                isCatchMode = false;
                onResult?.Invoke(true);
                yield break;
            }

            yield return null;
        }

        // 시간 초과
        stage5CatchActive = false;
        isCatchMode = false;
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

    /// <summary>5단 손등 스프라이트 전환</summary>
    private void SetHandMode_BackHand()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = backHandSprite;
        spriteRenderer.color = new Color(0.95f, 0.8f, 0.55f, 0.7f);
        // v3: 손등도 동일 방향 (손가락 왼쪽)
        transform.localEulerAngles = new Vector3(0, 0, 90f);
    }

    /// <summary>
    /// 손등 스프라이트: 손바닥과 비슷하지만 손톱 표시로 "등" 느낌
    /// </summary>
    private Sprite CreateBackHandSprite()
    {
        int w = 64, h = 80;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color skin = new Color(0.95f, 0.8f, 0.55f, 0.8f);     // 살짝 어두운 살색
        Color outline = new Color(0.65f, 0.45f, 0.25f, 0.9f);
        Color nail = new Color(1f, 0.9f, 0.85f, 0.9f);         // 손톱 색

        // 투명 초기화
        for (int px = 0; px < w; px++)
            for (int py = 0; py < h; py++)
                tex.SetPixel(px, py, Color.clear);

        // 손등 (하단 타원 — 약간 큼)
        DrawEllipse(tex, w / 2, 20, 24, 18, skin, outline);

        // 손가락 5개 + 손톱
        int[] fingerX = { 10, 20, 32, 42, 50 };
        int[] fingerH = { 18, 26, 28, 24, 16 };
        int[] fingerW = { 5, 5, 5, 5, 5 };

        for (int i = 0; i < 5; i++)
        {
            int baseY = 34;
            for (int fy = 0; fy < fingerH[i]; fy++)
            {
                float taper = 1f - (float)fy / fingerH[i] * 0.3f;
                int halfW = Mathf.Max(1, (int)(fingerW[i] * taper));
                for (int fx = -halfW; fx <= halfW; fx++)
                {
                    int px = fingerX[i] + fx;
                    int py = baseY + fy;
                    if (px >= 0 && px < w && py >= 0 && py < h)
                    {
                        bool edge = Mathf.Abs(fx) == halfW || fy == fingerH[i] - 1;
                        // 손톱: 손가락 끝 3픽셀
                        bool isNail = fy >= fingerH[i] - 3 && Mathf.Abs(fx) < halfW;
                        tex.SetPixel(px, py, isNail ? nail : (edge ? outline : skin));
                    }
                }
            }
        }

        // 관절 주름 (가로선 2개 — 손등 느낌)
        for (int px = w / 2 - 18; px <= w / 2 + 18; px++)
        {
            if (px >= 0 && px < w)
            {
                tex.SetPixel(px, 26, outline);
                tex.SetPixel(px, 30, outline);
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.25f), 48f);
    }

    private List<Stone> GetStonesInRange()
    {
        var result = new List<Stone>();
        var allStones = GameManager.Instance.Stones;
        if (allStones == null) return result;

        foreach (var stone in allStones)
        {
            if (stone.CurrentState != Stone.State.OnBoard) continue;

            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(stone.transform.position.x, stone.transform.position.y)
            );

            if (dist <= pickupRadius)
            {
                result.Add(stone);
            }
        }

        return result;
    }

    /// <summary>
    /// 캐치 성공 후: picked 클리어, throwStone 유지 (같은 돌로 다시 던지기).
    /// 던지기 돌은 손에 붙어있는 상태로 Throw 페이즈 직행.
    /// </summary>
    public void ClearPickedButKeepThrow()
    {
        pickedStones.Clear();
        isCatchMode = false;
        SetHandMode(false);
        // throwStone은 그대로 유지 (Caught 상태, 손에 부착)
    }

    /// <summary>
    /// 전체 리셋용 (실패/새 스테이지): throwStone도 null로 비움.
    /// </summary>
    public void ClearPickedOnly()
    {
        pickedStones.Clear();
        throwStone = null;
        isCatchMode = false;
        SetHandMode(false);
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

        isCatchMode = false;
        SetHandMode(false);

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
