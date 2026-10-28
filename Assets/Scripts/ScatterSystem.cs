using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FiveStones.Core;

/// <summary>
/// 뿌리기 시스템 (v14 코루틴 토스 재설계).
/// 게이지 왕복 → 놓는 순간의 세기로 산개 폭 결정.
/// ★핵심: 물리 임펄스가 아니라 "코루틴 애니메이션 토스"로 돌을 보드 좌표(원근) 목표까지 던진다.
///   - 화면 평면 물리(위/아래로 날아감) 문제 원천 제거 → CLAUDE.md "연출은 코루틴" 규칙 준수.
///   - 목표는 BoardBounds.QuadPoint(u,v)로 계산 → 사다리꼴 원근을 정확히 따름.
///   - 파워가 크면 일부 목표 uv가 [0,1] 밖 → QuadPoint 외삽으로 보드 밖 → 그 돌만 가장자리 너머로 낙.
/// </summary>
public class ScatterSystem : MonoBehaviour
{
    [Header("Gauge Settings")]
    [SerializeField] private float gaugeSpeed = 1.3f;
    [SerializeField] private float minScatterForce = 1.0f; // (v14 미사용 — 씬 직렬화 잔존, 무해)
    [SerializeField] private float maxScatterForce = 3.0f; // (v14 미사용)

    [Header("Scatter Settings")]
    [SerializeField] private float baseSpreadRadius = 0.9f;   // (v14 미사용)
    [SerializeField] private float minStoneSeparation = 0.8f; // (v14 미사용)
    [SerializeField] private float dropHeight = 1.5f;         // (v14 미사용)
    [SerializeField] private float settleSpeedThreshold = 0.1f; // (v14 미사용)
    [SerializeField] private float settleTimeout = 4f;       // (v14 미사용)

    [Header("Board (auto-resolved)")]
    [SerializeField] private Vector2 boardSize = new Vector2(9.6f, 6.1f);

    [Header("State")]
    [SerializeField] private float currentGaugeValue;
    [SerializeField] private bool isGaugeActive;

    // ── v14 토스 튜닝 상수 (씬 오버라이드 함정 회피 위해 코드 상수) ──
    // ── v15 뿌리기 밸런스 재설계 ("안 B 양끝위험 왕복") ──
    //   저게이지=뭉침(픽업실패) / 스윗스팟=안전 / 고게이지=낙. 100%=거의 확정 낙.
    // v17: 반경을 uv → **보드 단위**로. uv는 축마다 정규화라 uv 원이 보드에서는
    //   16:9로 가로로 늘어난 타원이 되고, 화면 원근이 더해져 4.4:1로 납작하게 퍼졌다
    //   (보드를 납작하게 만들면서 이 불균형이 드러남).
    //   보드 단위 원 = "돗자리 위에 사방으로 고르게 퍼진다"는 물리적 의미와 일치.
    //   보드 반너비 8 / 반깊이 4.5 → 4.5를 넘으면 앞뒤로 낙이 난다.
    //   v17-b 튜닝: Span 4.00 → 3.00. 플레이 결과 게이지 0.54에서도 낙이 나서
    //   성공 구간이 0.4 이하뿐이었다(게이지가 왕복하는 걸 감안하면 너무 좁다).
    //   3.00이면 g=0.5에서 반경 2.60 — 중심이 앞쪽이어도 앞변(4.5) 안에 들어온다.
    private const float RBoardBase = 1.10f;  // g=0 (뭉침)
    private const float RBoardSpan = 3.00f;  // g=1 → 4.10 (반깊이 4.5에 근접 = 고게이지 낙)
    // M1-S7(D9): 뿌리기 낙 마진은 BoardSpace.ScatterMargins(변별: Ledge 0 / Flat 0.2)로 옮겼다 — 상수는 BoardSpace 한 곳.

    /// <summary>뿌리기 낙의 실패 상한(놓은 순간부터, T5).</summary>
    private const float NakFailCapSeconds = 1.6f;

    /// <summary>게이지 → 뿌림 반경(보드 단위). 게이지바도 이 값으로 위험 색을 정한다
    /// (GaugeBarUI) — 링과 바가 서로 다른 식을 쓰면 두 개가 다른 말을 하게 된다.</summary>
    public static float RadiusBoard(float g) => RBoardBase + RBoardSpan * Mathf.Clamp01(g);

    private const float NakMargin    = 0.25f; // 이만큼 사다리꼴 밖으로 나가야 낙(경계 애매낙 방지)

    // ── M1 뿌리기 손맛 (계획서 §5-5). 씬 오버라이드 함정 회피 위해 전부 코드 상수 ──
    // 원칙: 단단한 돌 — 무게감은 변형이 아니라 소리·먼지·짧은 정지로 낸다(T1). 효과는 적게, 정확하게.
    /// <summary>단일 중력(보드 높이 단위/s²). 모든 돌이 같은 G → 무게의 일관성.</summary>
    public const float TossGravity = 45f;
    private const float TossTMin = 0.40f, TossTMax = 0.54f; // 체공: 가까우면 0.40, 4.4 이상이면 0.54
    private const float TossTRefDist = 4.4f;
    private const float TossTJitter = 0.025f;               // 돌마다 ±0.025 (fxRng)
    private const float StaggerMin = 0.02f, StaggerMax = 0.07f, StaggerSumMax = 0.18f;
    // Z축(화면 평면) 회전 — Y축은 정면 카메라에서 안 보였다.
    // 리뷰 반영: 돌 메시는 바닥이 평평한 구(StoneShape)라 방향이 있다. 회전이 남으면 옆으로 누운 채 놓이고,
    // 스쿼시가 돌의 로컬 축에 걸려 화면 세로가 아닌 방향으로 눌린다 → **정확히 한 바퀴** 돌아 첫 접촉에 똑바로 선다.
    private const float SpinTurnDeg = 360f;
    private const float StretchY = 0.04f, StretchX = 0.02f; // 비행 중 늘어남(높이 속도 비례)
    private const float LandLeadK = 0.11f, LandLeadMin = 0.08f, LandLeadMaxFrac = 0.30f; // 첫 접촉 선행 거리
    private const float Bounce1Base = 0.16f, Bounce2Ratio = 0.35f; // 튐 높이(T1 축소안)
    private const float Bounce1Frac = 0.67f, Bounce2Frac = 0.25f, SlideFrac = 0.08f; // 접촉 후 수평 이동 배분(합 1)
    private const float SlideDuration = 0.07f;
    private const float Squash1Y = 0.94f, Squash1XZ = 1.03f, Squash1Dur = 0.07f;   // 스쿼시 ≤6% (T1)
    private const float Squash2Y = 0.97f, Squash2XZ = 1.015f, Squash2Dur = 0.05f;
    private const float SquashRise = 0.30f;

    // 쥐기·달그락 (M1-S3, 계획서 §5-5 "쥐기")
    private const float GatherDuration = 0.12f, GatherXScale = 0.5f, GatherY = 0.04f; // 돌을 손 안으로 모은다
    private const float RattleIntervalSlow = 0.16f, RattleIntervalFast = 0.07f;      // g=0 → g=1
    private const float HandShakeMin = 0.004f, HandShakeMax = 0.030f;                // × g² — 손 모델 피벗만
    private const float StoneShakeMin = 0.003f, StoneShakeMax = 0.020f;              // × g
    private const float ShakeDecay = 30f;                                            // × exp(−30·dt)
    private const float RattlePitchMin = 1.30f, RattlePitchMax = 1.50f;
    /// <summary>놓는 순간 손·돌만 멈추는 시간. Time.timeScale은 건드리지 않는다(F10).</summary>
    private const float HitStopSeconds = 0.05f;

    private Stone[] gripStones;
    private Vector3[] gripBaseLocal;
    private Vector2[] stoneShake;
    private Vector2 handShake;
    private float gatherT, rattleTimer;

    /// <summary>연출 전용 난수(T6). 목표 계산(UnityEngine.Random)과 분리 — 연출을 바꿔도 목표가 그대로다.</summary>
    private System.Random fxRng = new System.Random();

    // 진행 중인 뿌리기 상태 (ForceCompleteTosses가 스냅·정지에 쓴다)
    private Coroutine scatterRoutine;
    private int scatterGen;
    private TossPlan[] tossPlans;
    private Stone[] tossStones;
    private bool[] tossDone;
    private int activeTosses;
    private int tossSafeCount;
    private readonly List<Coroutine> tossRoutines = new List<Coroutine>();
    private float scatterReleaseTime, tossStartTime;
#if UNITY_EDITOR
    public float DebugLastTotal { get; private set; }
    public float DebugLastMaxDiff { get; private set; }
    public float DebugLastStaggerSum { get; private set; }
#endif

    // 저게이지=간격 좁음(손바닥폭~1.0 아래=뭉침 픽업실패), 스윗=간격 확보
    private static float TargetSepForGauge(float g)
    {
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.50f, g));
        return Mathf.Lerp(0.40f, 1.05f, t);
    }

    private bool gaugeGoingUp = true;
    // 딸깍 방지: 게이지가 활성된 시간이 이 값 미만이면 던지지 않고 재대기(캔슬)
    private float gaugeActiveTime;
    private const float MinGaugeHold = 0.2f;
    private InputAction pressAction;
    private HandController handController;
    private ScatterRangeIndicator rangeIndicator;
    private ScatterFx scatterFx;

    public float CurrentGaugeValue => currentGaugeValue;
    public bool IsGaugeActive => isGaugeActive;

    /// <summary>스테이지 전환 시 게이지 상태 강제 리셋</summary>
    public void ResetGauge()
    {
        isGaugeActive = false;
        waitingForPress = false;
        currentGaugeValue = 0f;
        GaugeBarUI.Instance?.Hide();
        rangeIndicator?.Hide();
        ForceCompleteTosses();   // M1-S1: 지난 뿌리기의 토스·완료 처리가 새 스테이지로 새지 않게
        CancelGrip(restoreStones: false);   // M1-S3: 돌은 이어서 ResetAllStones가 정리한다
#if UNITY_EDITOR
        debugGaugeFrozen = false;
#endif
    }

    /// <summary>쥐기 연출 취소 — 손가락 펼침·오프셋 0. restoreStones면 돌을 손바닥 원래 자리로.</summary>
    private void CancelGrip(bool restoreStones)
    {
        handShake = Vector2.zero;
        if (stoneShake != null) System.Array.Clear(stoneShake, 0, stoneShake.Length);
        handController?.CancelScatterGrip();
        if (!restoreStones || gripStones == null || gripBaseLocal == null || handController == null) return;
        for (int i = 0; i < gripStones.Length; i++)
            if (gripStones[i] != null && gripStones[i].transform.parent == handController.transform)
                gripStones[i].transform.localPosition = gripBaseLocal[i];
    }

    /// <summary>게이지 중 매 프레임 — 돌 모으기(0.12초) + 달그락(손 피벗·돌 개별 흔들림·소리).</summary>
    private void UpdateGrip(float dt, float g)
    {
        gatherT = Mathf.Min(1f, gatherT + dt / GatherDuration);
        float ge = 1f - (1f - gatherT) * (1f - gatherT);

        float decay = Mathf.Exp(-ShakeDecay * dt);
        handShake *= decay;
        if (stoneShake != null) for (int i = 0; i < stoneShake.Length; i++) stoneShake[i] *= decay;

        rattleTimer -= dt;
        if (rattleTimer <= 0f)
        {
            rattleTimer = Mathf.Lerp(RattleIntervalSlow, RattleIntervalFast, g) * FxRange(0.85f, 1.15f);
            handShake = FxDir() * Mathf.Lerp(HandShakeMin, HandShakeMax, g * g);
            if (stoneShake != null)
                for (int i = 0; i < stoneShake.Length; i++)
                    stoneShake[i] = FxDir() * Mathf.Lerp(StoneShakeMin, StoneShakeMax, g);
            AudioManager.Instance?.PlayScatterRattle(g, fxRng.Next(0, 5), FxRange(RattlePitchMin, RattlePitchMax));
        }

        handController?.SetScatterRattle(handShake);
        if (gripStones == null || gripBaseLocal == null || handController == null) return;
        for (int i = 0; i < gripStones.Length; i++)
        {
            var st = gripStones[i];
            if (st == null || st.transform.parent != handController.transform) continue;
            Vector3 b = gripBaseLocal[i];
            Vector3 gathered = new Vector3(b.x * GatherXScale, GatherY, b.z);
            Vector3 lp = Vector3.Lerp(b, gathered, ge);
            Vector2 sh = stoneShake != null && i < stoneShake.Length ? stoneShake[i] : Vector2.zero;
            st.transform.localPosition = lp + new Vector3(sh.x, sh.y, 0f);
        }
    }

    private Vector2 FxDir()
    {
        float a = (float)fxRng.NextDouble() * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    /// <summary>뿌리기 코루틴 시작 — 진행 중인 뿌리기가 있으면 먼저 끝낸다.</summary>
    private void StartScatterRoutine(float gauge)
    {
        if (scatterRoutine != null || activeTosses > 0) ForceCompleteTosses();
        scatterRoutine = StartCoroutine(DoScatter(gauge));
    }

    private void Awake()
    {
        pressAction = new InputAction("Press", InputActionType.Button);
        pressAction.AddBinding("<Mouse>/leftButton");
        pressAction.AddBinding("<Touchscreen>/primaryTouch/press");

        handController = FindFirstObjectByType<HandController>();

        // 뿌리기 범위 표시 링 (순수 시각 레이어)
        var go = new GameObject("ScatterRangeIndicator");
        rangeIndicator = go.AddComponent<ScatterRangeIndicator>();
        rangeIndicator.Hide();

        // M1-S2: 착지 먼지 풀
        scatterFx = new GameObject("ScatterFx").AddComponent<ScatterFx>();
    }

    private void OnEnable()
    {
        pressAction.Enable();
        pressAction.started += OnPressStarted;
        pressAction.canceled += OnPressReleased;
    }

    private void OnDisable()
    {
        pressAction.started -= OnPressStarted;
        pressAction.canceled -= OnPressReleased;
        pressAction.Disable();
    }

    private void Update()
    {
        if (!isGaugeActive) return;

        // 리뷰 반영: 일시정지 메뉴는 Time.timeScale을 멈추지 않는다 — 게이지·달그락도 같이 멈춘다.
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // 일시정지·전환 중에 손을 떼면 OnPressReleased가 무시된다. 재개했을 때 버튼이 이미 떨어져 있으면
        // 놓은 걸로 치지 않고 취소한다(플레이어가 보지 않은 순간에 뿌려지지 않게) — 짧게 뗌과 같은 재대기.
        bool pressed = pressAction.IsPressed();
#if UNITY_EDITOR
        pressed |= debugGaugeFrozen;   // 검증용 게이지 고정은 실제 버튼 없이 돈다
#endif
        if (!pressed)
        {
            CancelGaugeToWaiting();
            return;
        }

        gaugeActiveTime += Time.deltaTime;

        if (gaugeGoingUp)
        {
            currentGaugeValue += gaugeSpeed * Time.deltaTime;
            if (currentGaugeValue >= 1f) { currentGaugeValue = 1f; gaugeGoingUp = false; }
        }
        else
        {
            currentGaugeValue -= gaugeSpeed * Time.deltaTime;
            if (currentGaugeValue <= 0f) { currentGaugeValue = 0f; gaugeGoingUp = true; }
        }

#if UNITY_EDITOR
        if (debugGaugeFrozen) currentGaugeValue = debugFrozenGauge;   // 검증용 게이지 고정
#endif
        GaugeBarUI.Instance?.SetValue(currentGaugeValue);
        // 링 중심도 커서(손) 위치 — DoScatter의 centerUV와 동일 계산/clamp로 프리뷰 정합.
        rangeIndicator?.UpdateRing(RadiusBoard(currentGaugeValue),
                                   BoardSpace.Current.UvToBoard(ScatterCenterUV()));
        // M1-S3 (D4-A): 게이지 틱음(PlayGaugeTick)은 손 안 돌의 달그락으로 대체한다.
        UpdateGrip(Time.deltaTime, currentGaugeValue);
    }

    private bool waitingForPress;

    public void BeginScatter()
    {
        // 돌들을 손바닥 위에 소복이 쥐게 (손에서 던지는 출발점)
        var stones = GameManager.Instance.Stones;
        int idx = 0;
        foreach (var stone in stones)
        {
            stone.SetState(Stone.State.InHand);
            if (handController != null)
            {
                // 손 자식으로 붙여 손바닥 위 클러스터로 배치 (localPosition = 손 로컬)
                //   Z=-0.15: 팜 앞(카메라 쪽) → 손 위에 얹혀 보임. Y≈0.05: 손바닥 바로 위.
                //   X 지터로 5개가 서로 겹치지 않게 소폭 분산.
                stone.transform.SetParent(handController.transform);
                float jitterX = Mathf.Lerp(-0.25f, 0.25f, stones.Length > 1 ? (float)idx / (stones.Length - 1) : 0.5f);
                float jitterY = 0.05f + (idx % 2) * 0.04f;
                stone.transform.localPosition = new Vector3(jitterX, jitterY, -0.15f);
            }
            else
            {
                // 안전망: 손이 없으면 기존처럼 보드 중앙
                stone.transform.position = new Vector3(0f, BoardBounds.MatRect.center.y, 0f);
            }
            idx++;
        }

        // M1-S3: 쥐기 연출의 기준 — 손바닥 위 원래 자리(짧게 뗌 취소 시 여기로 되돌린다)
        gripStones = stones;
        gripBaseLocal = new Vector3[stones.Length];
        stoneShake = new Vector2[stones.Length];
        for (int i = 0; i < stones.Length; i++) gripBaseLocal[i] = stones[i].transform.localPosition;
        handShake = Vector2.zero;

        waitingForPress = true;
        isGaugeActive = false;
        currentGaugeValue = 0f;
        gaugeGoingUp = true;

        GaugeBarUI.Instance?.Show();
        Debug.Log("[ScatterSystem] Ready. Long-press to start gauge.");
    }

    private void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsPaused) return;
        if (GameManager.Instance.IsInputBlocked) return;
        if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Scatter) return;

        if (waitingForPress)
        {
            waitingForPress = false;
            isGaugeActive = true;
            currentGaugeValue = 0f;
            gaugeGoingUp = true;
            gaugeActiveTime = 0f;
            rangeIndicator?.Show();
            // M1-S3: 쥐기 — 손가락 0.55 + 돌을 손 안으로 모으기 시작
            gatherT = 0f;
            rattleTimer = RattleIntervalSlow;
            handController?.BeginScatterGrip();
            Debug.Log("[ScatterSystem] Gauge started!");
        }
    }

    private void OnPressReleased(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsPaused) return;
        if (GameManager.Instance.IsInputBlocked) return;
        if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Scatter) return;
        if (!isGaugeActive) return;

        // 딸깍 방지: 너무 짧게 눌렀다 놓으면 던지지 않고 재대기(다음 Press에 다시 게이지 시작)
        if (gaugeActiveTime < MinGaugeHold)
        {
            CancelGaugeToWaiting();
            return;
        }

        isGaugeActive = false;
        GaugeBarUI.Instance?.ConfirmPunch(); // v20: 확정 펀치 후 스스로 숨는다
        rangeIndicator?.Hide();
        AudioManager.Instance?.PlayGaugeConfirm();
        // v14: 게이지 값을 그대로 넘김. 퍼짐/낙은 DoScatter에서 코루틴 토스로 결정.
        float gauge = currentGaugeValue;
        TestLogger.Instance?.LogScatter(gauge, currentGaugeValue);
        Debug.Log($"[ScatterSystem] Scatter gauge: {gauge:F2}");

        handController?.PlayScatterFlick();
        StartScatterRoutine(gauge);
    }

    /// <summary>게이지를 접고 다음 누름을 기다린다 — 짧게 뗌, 그리고 일시정지 중 뗌을 재개 후 발견했을 때.</summary>
    private void CancelGaugeToWaiting()
    {
        isGaugeActive = false;
        currentGaugeValue = 0f;
        gaugeGoingUp = true;
        waitingForPress = true;
        GaugeBarUI.Instance?.SetValue(0f); // 바는 유지(재대기), 값만 0으로 — Hide하면 다음 Press에 안 켜져 사라짐
        rangeIndicator?.Hide();
        CancelGrip(restoreStones: true);   // M1-S3: 손가락 펼침, 오프셋 0, 돌 원위치
    }

    /// <summary>v14: 코루틴 토스 산개. 손 중앙 → 보드 좌표 목표까지 아치 애니메이션.
    /// 목표 uv가 [0,1] 밖인 돌은 보드 밖으로 던져져 낙.</summary>
#if UNITY_EDITOR
    /// <summary>에디터 검증 전용 — 마우스 입력 없이 뿌리기를 실행한다.
    /// (MCP로 에디터를 조작할 땐 게임 뷰에 클릭을 넣을 수 없다)</summary>
    public void DebugScatterNow(float gauge)
    {
        waitingForPress = false;
        isGaugeActive = false;
        GaugeBarUI.Instance?.Hide();
        rangeIndicator?.Hide();
        StartScatterRoutine(gauge);
    }

    // ── M1-S7 검증용 강제 낙 (에디터 전용) ──
    private Vector2[] debugForcedTargets;

    /// <summary>돌 0..k-1의 목표를 보드 좌표로 덮어쓴 시드 뿌리기(간격 보장 뒤 덮어씀 — 2.4·2.5 판정은 그대로 거친다).</summary>
    public void DebugScatterForcedNak(float gauge, int seed, Vector2 centerUV, Vector2[] forcedBoardTargets)
    {
        debugForcedTargets = forcedBoardTargets;
        DebugScatterSeeded(gauge, seed, centerUV);
    }

    // ── M1-S3 검증용 게이지 고정 (에디터 전용) — 마우스 없이 실제 누름/놓음 경로를 태운다 ──
    private bool debugGaugeFrozen;
    private float debugFrozenGauge;

    /// <summary>누름과 같은 경로로 게이지를 시작하고 값을 g에 고정한다(쥐기·달그락 확인용).</summary>
    public void DebugGaugeHold(float g)
    {
        OnPressStarted(default);
        debugGaugeFrozen = isGaugeActive;
        debugFrozenGauge = g;
    }

    /// <summary>놓음과 같은 경로(짧게 뗌 판정·히트스톱·손 캐스트 포함).</summary>
    public void DebugGaugeRelease()
    {
        debugGaugeFrozen = false;
        OnPressReleased(default);
    }

    // ── M1-S0 기준값 캡처 (에디터 전용) ──────────────────────────────────────
    // 목표 계산(1~2.5단계)이 소비하는 UnityEngine.Random을 시드로 고정하고, 산개 중심을
    // 손 위치 대신 고정 uv로 바꿔 "같은 입력 → 같은 targets/isNak"을 기록한다.
    // 이후 단계(연출 재구성)가 목표 계산을 건드리지 않았는지 이 기록과 비교한다(계획서 T6).
    private bool debugHasCenterUV;
    private Vector2 debugCenterUV;
    private bool debugPlanOnly;                       // true면 2.5 판정 직후 종료(돌·상태 무변경)
    private System.Text.StringBuilder debugBaselineSink; // null이면 콘솔 로그
    private string debugBaselineTag;                  // "stage=.. gauge=.. seed=.."

    /// <summary>시드·중심 고정 뿌리기. planOnly=true면 목표 계산과 기준값 기록만 하고 끝낸다
    /// (돌을 움직이지 않으므로 수백 번 연속 호출해도 된다). sink가 있으면 콘솔 대신 거기에 쓴다.</summary>
    public void DebugScatterSeeded(float gauge, int seed, Vector2 centerUV,
                                   bool planOnly = false, System.Text.StringBuilder sink = null)
    {
        int loop = GameSession.Instance != null ? GameSession.Instance.CurrentLoop : 0;
        int age = GameSession.Instance != null ? GameSession.Instance.CurrentAge : 0;
        debugBaselineTag = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "stage={0} age={1} center=({4:F2},{5:F2}) gauge={2:F2} seed={3}", loop, age, gauge, seed,
            centerUV.x, centerUV.y);
        debugHasCenterUV = true;
        debugCenterUV = centerUV;
        debugPlanOnly = planOnly;
        debugBaselineSink = sink;
        if (!planOnly)
        {
            waitingForPress = false;
            isGaugeActive = false;
            GaugeBarUI.Instance?.Hide();
            rangeIndicator?.Hide();
        }
        if (planOnly)
        {
            Random.InitState(seed);
            StartCoroutine(DoScatter(gauge));   // 첫 yield 전에 끝나므로 여기서 동기 완료(돌·상태 무변경)
            DebugClearSeeded();
            return;
        }
        fxRng = new System.Random(seed);        // 검증 재현용: 연출 난수도 시드 고정
        if (scatterRoutine != null || activeTosses > 0) ForceCompleteTosses();
        Random.InitState(seed);                 // ForceComplete 뒤에 — 목표 계산 직전에 고정
        scatterRoutine = StartCoroutine(DoScatter(gauge));
    }

    private void DebugClearSeeded()
    {
        debugHasCenterUV = false;
        debugPlanOnly = false;
        debugBaselineSink = null;
        debugBaselineTag = null;
    }

    private void DebugWriteBaseline(Vector2[] boardTargets, bool[] isNak)
    {
        if (debugBaselineTag == null) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < boardTargets.Length; i++)
        {
            string line = string.Format(ci, "[ScatterBaseline] {0} i={1} target=({2:F6}, {3:F6}) isNak={4}",
                debugBaselineTag, i, boardTargets[i].x, boardTargets[i].y, isNak[i]);
            if (debugBaselineSink != null) debugBaselineSink.AppendLine(line);
            else Debug.Log(line);
        }
    }
#endif

    private IEnumerator DoScatter(float gauge)
    {
        scatterReleaseTime = Time.time;
#if UNITY_EDITOR
        DebugLastMaxDiff = 0f;
        DebugLastTotal = -1f;
#endif
        var stones = GameManager.Instance.Stones;
        int n = stones != null ? stones.Length : 0;
        if (n == 0)
        {
            GameManager.Instance.SetPhase(GameManager.GamePhase.PickThrowStone);
            yield break;
        }

        // ── 1) 목표 uv 계산 (셔플 슬롯으로 각도 균등 분포) ──
        int slotCount = Mathf.Max(10, n * 2);
        int[] slots = new int[slotCount];
        for (int i = 0; i < slotCount; i++) slots[i] = i;
        for (int i = slotCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }
        float slotAngleStep = 360f / slotCount;

        var targets = new Vector2[n];
        var isNak = new bool[n];
        var startPos = new Vector2[n];

        // 뿌리기 보드 = 게임 BoardBounds 실제 폴리곤 (quad=사다리꼴 = 보이는 노란 테이블 / 없으면 AABB).
        //   게임 낙 감시와 "동일 폴리곤" → placement·낙·play감시 3자 완전 일치.
        //   ⚠️ v16: 단, 뿌리기 판정은 IsOutsideMat이 아니라 **IsOutsideMatStrict**를 쓴다(2.5 참고).
        //      폴리곤은 같고 "하늘 면제"만 뺀 것 — 안착 돌엔 그 면제가 틀리기 때문. 되돌리지 말 것.
        //      뿌리기가 감시보다 엄격해야 안전하다(통과한 돌은 감시에서도 통과 → 안착 직후 오낙 없음).
        //   QuadPoint로 4꼭짓점을 얻어 LerpUnclamped bilinear(TrapPoint)로 매핑 → uv>1은 보드 밖 외삽(낙 목표).
        // v17: 목표를 **보드 좌표**로 계산하고 화면 위치는 투영으로 파생시킨다.
        //   uv로 계산하던 기존 구조를 그대로 살리되, 마지막 변환만 BoardSpace로 바꾼 것.
        //   이렇게 해야 안착한 돌이 자기 BoardPos를 갖고, 그림자가 실제 착지 지점에 찍힌다.
        var boardTargets = new Vector2[n];

        // ★산개 중심 = 커서(손) 위치 (ScatterCenterUV — 링 프리뷰와 동일 계산).
        Vector2 centerUV = ScatterCenterUV();
        Vector2 centerBoard = BoardSpace.Current.UvToBoard(centerUV);

        // v15: 각도만 유기화(반경 매핑·낙 파이프라인 불변). 매 throw 방향 랜덤 + 지터 확대 + 뭉침 클럼프.
        float baseRot = Random.Range(0f, 360f); // 매 throw 방향 랜덤(단조 방어)
        float[] angDeg = new float[n];
        float[] rads = new float[n];
        for (int i = 0; i < n; i++)
        {
            angDeg[i] = slotAngleStep * slots[i] + baseRot + Random.Range(-22f, 22f); // 지터 ±12→±22
            rads[i]   = RadiusBoard(gauge) * Random.Range(0.92f, 1.08f);
            float a = angDeg[i] * Mathf.Deg2Rad;
            boardTargets[i] = centerBoard + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rads[i];
            isNak[i] = !BoardSpace.Current.Contains(boardTargets[i]);   // provisional
        }
        // 클럼프: 비-낙 스톤 ~40%를 각도상 가장 가까운 다른 스톤 쪽으로 당겨 뭉침/빈틈 생성 (반경 불변)
        int clumpCount = Mathf.RoundToInt(n * 0.4f);
        for (int c = 0; c < clumpCount; c++)
        {
            int i = Random.Range(0, n);
            if (isNak[i]) continue;
            int best = -1; float bestD = 999f;
            for (int j = 0; j < n; j++)
            {
                if (j == i || isNak[j]) continue;
                float d = Mathf.Abs(Mathf.DeltaAngle(angDeg[i], angDeg[j]));
                if (d < bestD) { bestD = d; best = j; }
            }
            if (best < 0) continue;
            angDeg[i] = Mathf.LerpAngle(angDeg[i], angDeg[best], 0.35f);
            float a = angDeg[i] * Mathf.Deg2Rad;
            boardTargets[i] = centerBoard + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rads[i];
            isNak[i] = !BoardSpace.Current.Contains(boardTargets[i]);
        }

        // ── 2) 안착 목표 최소 간격 보장 (낙 목표는 건드리지 않음 — 어차피 보드 밖) ──
        // v15: 간격을 게이지 함수로 → 저게이지=뭉침(픽업실패), 스윗=간격 확보.
        float sep = TargetSepForGauge(gauge);
        for (int pass = 0; pass < 8; pass++)
        {
            bool adjusted = false;
            for (int a = 0; a < n; a++)
            {
                if (isNak[a]) continue;
                for (int b = a + 1; b < n; b++)
                {
                    if (isNak[b]) continue;
                    Vector2 diff = boardTargets[a] - boardTargets[b];
                    float dist = diff.magnitude;
                    if (dist < sep && dist > 0.001f)
                    {
                        Vector2 push = diff.normalized * (sep - dist) * 0.5f;
                        boardTargets[a] += push;
                        boardTargets[b] -= push;
                        adjusted = true;
                    }
                }
            }
            if (!adjusted) break;
        }

#if UNITY_EDITOR
        // 검증용 강제 목표(DebugScatterForcedNak): 간격 보장 뒤에 덮어쓴다. 잠정 isNak는 원래 코드와 같은 식(마진 0),
        // 이후 2.4·2.5 판정은 그대로 거친다(계획서 §7 "판정 식은 그대로 거친다").
        if (debugForcedTargets != null)
        {
            for (int k = 0; k < debugForcedTargets.Length && k < n; k++)
            {
                boardTargets[k] = debugForcedTargets[k];
                isNak[k] = !BoardSpace.Current.Contains(boardTargets[k]);
            }
            debugForcedTargets = null;
        }
#endif

        // ── 2.4) 우발낙 방어 (donts/game.md #19 SOT): 간격 확대가 비-낙 돌을 경계 밖으로 밀 수 있음.
        //   원래 uv 내부(provisional isNak==false)였는데 밀려난 돌만 보드 중심 C 쪽으로 회수.
        //   의도된 낙(provisional true)은 건드리지 않음.
        //   M1-S7(D9): 판정과 같은 변별 마진 식(Ledge 0 / Flat 0.2)으로 회수한다. 안 바꾸면 Ledge 0~0.2 띠에 남은
        //   안전 돌이 2.5에서 우발 낙이 된다.
        Vector2 C = centerBoard;
        float hw = BoardSpace.LogicalWidth * 0.5f, hd = BoardSpace.LogicalDepth * 0.5f;
        EdgeMargins margins = BoardSpace.ScatterMargins;
        for (int i = 0; i < n; i++)
        {
            if (isNak[i]) continue; // 의도된 낙은 그대로
            int guard = 0;
            while (!BoardEdgeMath.ContainsEdgeAware(boardTargets[i], hw, hd, margins) && guard++ < 10)
                boardTargets[i] = Vector2.Lerp(boardTargets[i], C, 0.18f);
        }

        // ── 2.5) 최종 낙 판정 ──
        // v16 수정: IsOutsideMat → IsOutsideMatStrict.
        //   IsOutsideMat에는 "뒷변 위(SkyFloorY 초과)는 하늘이라 outside 면제" 규칙이 있는데,
        //   그건 **던져서 날아가는 중인 돌**을 보호하려는 것이다. 뿌려서 **안착하는** 돌은
        //   날아가는 중이 아니므로 이 면제를 받으면 안 된다. 면제를 받는 바람에
        //   보이는 보드 뒷변 너머(예: 옆 테이블 위)에 앉아도 낙이 아니게 되어
        //   "판정 영역이 보이는 것보다 훨씬 커 보이는" 증상이 났다.
        //   Strict는 SkyFloor 면제 없이 사다리꼴 내부만 본다 — 마진(0.2)은 그대로라 경계 애매낙 흡수도 동일.
        //   (v12에서 Flee 돌이 같은 이유로 +Y로 새어 IsOutsideMatStrict를 만든 것과 동일 패턴.)
        // M1-S7(D9): Ledge 변 = 보이는 가장자리(마진 0), Flat 변 = 0.2. 의도된 판정 변경 — Ledge 0~0.2 띠는 이제 낙.
        for (int i = 0; i < n; i++)
        {
            isNak[i] = !BoardEdgeMath.ContainsEdgeAware(boardTargets[i], hw, hd, margins);
            targets[i] = BoardSpace.ToScreen(boardTargets[i], 0f); // 애니메이션용 화면 좌표(파생)
        }

#if UNITY_EDITOR
        // [S0 기준값 로그 지점] — 목표 계산이 끝난 직후, 연출이 시작되기 전.
        DebugWriteBaseline(boardTargets, isNak);
        if (debugPlanOnly) yield break;
        DebugClearSeeded();   // 실제 뿌리기는 계속 진행 — 다음 일반 뿌리기에 고정값이 남지 않게
#endif

        // v16: 놓는 즉시 던지기 — 윈드업 대기 제거. 아래 준비 루프에서 즉시 detach + startPos(현재 손=커서 위치)
        //   재캡처 + 토스 시작 → 유저 릴리스와 돌 발사 타이밍 일치(체감 딜레이 제거).
        //   손 캐스트 연출(DoScatterThrow)은 돌과 무관하게 병렬 진행.

        // ── 히트스톱 (M1-S3): 놓은 뒤 0.05초 손·돌만 멈춘다. Time.timeScale은 건드리지 않는다(F10 —
        //    30살 방해물 공 등 다른 것은 계속 움직인다). 손은 DoScatterThrow가 신호를 기다리며 정지해 있다.
#if UNITY_EDITOR
        var hsObstacles = FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
        var hsObsPos = new Vector3[hsObstacles.Length];
        for (int k = 0; k < hsObstacles.Length; k++) hsObsPos[k] = hsObstacles[k].transform.position;
        float hsStart = Time.time;
#endif
        for (float hs = 0f; hs < HitStopSeconds; )
        {
            yield return null;
            hs += Time.deltaTime;
        }
#if UNITY_EDITOR
        float hsMove = 0f;
        for (int k = 0; k < hsObstacles.Length; k++)
            if (hsObstacles[k] != null) hsMove = Mathf.Max(hsMove, (hsObstacles[k].transform.position - hsObsPos[k]).magnitude);
        Debug.Log($"[Scatter] hitstop {Time.time - hsStart:F3}s timeScale={Time.timeScale:F2} obstacles={hsObstacles.Length} obstacleMove={hsMove:F4}");
#endif

        // ── 3) 준비: 모든 돌 InHand(kinematic) + 콜라이더 반경 ──
        // M1-S1(T6): 여기서부터의 무작위(간격·체공 지터·회전)는 fxRng만 쓴다.
        //   UnityEngine.Random은 위 1)~2.3) 목표 계산 전용 — 연출을 바꿔도 목표가 안 바뀌게.
        for (int i = 0; i < n; i++)
        {
            stones[i].SetState(Stone.State.InHand); // kinematic, 콜라이더 off
            stones[i].transform.SetParent(null); // 손에서 분리 (world 위치 유지 → 손 위치가 출발점)
            startPos[i] = new Vector2(stones[i].transform.position.x, stones[i].transform.position.y); // 캐스트 끝 손위치
            var col = stones[i].GetComponent<SphereCollider>();
            if (col != null) col.radius = 0.9f;
        }

        // 히트스톱 해제 신호 — 돌을 손에서 떼어 낸 **뒤에** 보낸다(같은 프레임). 손 캐스트가 먼저 움직이면
        // 아직 손 자식인 돌의 출발점이 같이 끌려간다.
        handController?.ReleaseScatterHitStop();

        // 출발점도 보드 좌표로 — 토스 전체를 보드 공간에서 계산해야 원근 스케일이 따라온다.
        var startBoard = new Vector2[n];
        for (int i = 0; i < n; i++)
            startBoard[i] = BoardSpace.ClampToBoard(BoardSpace.ToBoard(startPos[i]));

        // ── 4) 토스 계획 + 돌별 코루틴 (M1-S1, 계획서 §5-5) ──
        int gen = ++scatterGen;
        tossPlans = BuildTossPlans(startBoard, boardTargets, isNak);
        tossStones = stones;
        tossDone = new bool[n];
        tossContact = new bool[n];
        tossStartTime = Time.time;
        activeTosses = n;

        // ── 낙 (M1-S7, 계획서 §5-5 "완료·실패"): 놓는 순간 판정이 끝났으므로 실패 대기를 바로 건다.
        //   실패 시각 = max(낙 돌 종료, 모든 토스 종료) + 0.3초, 상한 1.6초(놓은 순간 기준). 게이트는 GameManager 하나.
        bool anyNak = false;
        for (int i = 0; i < n; i++) anyNak |= isNak[i];
        if (anyNak)
        {
            TestLogger.Instance?.LogFailure("scatter_out_of_bounds");
            GameManager.Instance.BeginNakWait("낙!", scatterReleaseTime + NakFailCapSeconds);
            GameManager.Instance.AddNakBusy();   // 토스 전체분 1회
        }

        tossRoutines.Clear();
        for (int i = 0; i < n; i++)
            tossRoutines.Add(StartCoroutine(TossStone(i, gen)));

        while (activeTosses > 0) yield return null;
        tossRoutines.Clear();
        if (gen != scatterGen) yield break; // ForceCompleteTosses가 이 뿌리기를 끝냈다

        if (anyNak)
        {
            // 실패는 게이트가 건다(낙하 연출이 끝나고 0.3초 뒤). 완료 신호(OnScatterComplete)는 보내지 않는다.
            Debug.Log($"[Scatter] tosses done (nak) t={Time.time - scatterReleaseTime:F3}s");
            scatterRoutine = null;
            GameManager.Instance.ReleaseNakBusy();
        }
        else
        {
            // M1-S1: 고정 0.16초 대기 삭제 — 완료 카운터가 0이면 모든 돌이 이미 정지·고정됐다.
            float total = Time.time - scatterReleaseTime;
#if UNITY_EDITOR
            DebugLastTotal = total;
#endif
            Debug.Log($"[Scatter] complete total={total:F3}s n={n}");
            scatterRoutine = null;
            AudioManager.Instance?.PlayScatterFinishCue();   // D6 — 꺼져 있으면 no-op. 같은 프레임(시간 불변)
            GameManager.Instance.CurrentGimmick?.OnScatterComplete(stones);
            GameManager.Instance.SetPhase(GameManager.GamePhase.PickThrowStone);
        }
    }

    // ── M1-S1 토스 계획 ────────────────────────────────────────────────────────

    /// <summary>돌 하나의 비행·착지 계획. 보드 좌표 S(출발) → A(첫 접촉) → F(최종 = boardTargets[i]).</summary>
    private struct TossPlan
    {
        public Vector2 S, A, F, Dir;
        public bool IsNak;
        public float Delay;   // 출발 지연(초)
        public float T, H;    // 체공 시간, 최고 높이
        public float DPost;   // 첫 접촉 후 수평 이동 거리 (A→F)
        public float B1, T1, B2, T2, Slide;
        public float SpinDeg; // 전체 Z 회전량(부호 포함)
        public int Order;     // 안착 돌의 첫 접촉 순서(착지음 음계). 낙 돌은 -1
        public EdgeKind Kind; // 낙 돌: Ledge(허공으로 떨어짐) / Flat(바깥 면에 앉음). ClassifyOutside(F)
        public BoardEdge Edge;// 낙 돌: Ledge = 처음 넘는 Ledge 선, Flat = 대표 변
        public float TCross;  // Ledge 낙: Ledge 선을 넘는 시각(출발 기준, 초) → 낙하 연출로 넘긴다
        public bool IsLedgeNak => IsNak && Kind == EdgeKind.Ledge;
        public float End => IsLedgeNak ? T : T + Mathf.Max(T1 + T2 + Slide, Squash1Dur);
    }

    private TossPlan[] BuildTossPlans(Vector2[] startBoard, Vector2[] boardTargets, bool[] isNak)
    {
        int n = boardTargets.Length;
        var plans = new TossPlan[n];

        // 출발 간격: 각 0.02~0.07, 합이 0.18을 넘으면 비례 축소 (n=10에서도 합 ≤ 0.18)
        var gaps = new float[n];
        float gapSum = 0f;
        for (int i = 1; i < n; i++) { gaps[i] = FxRange(StaggerMin, StaggerMax); gapSum += gaps[i]; }
        float gapScale = gapSum > StaggerSumMax ? StaggerSumMax / gapSum : 1f;
        float delay = 0f;
        float hw = BoardSpace.LogicalWidth * 0.5f, hd = BoardSpace.LogicalDepth * 0.5f;
        BoardEdges kinds = BoardSpace.Edges;
        EdgeMargins judge = BoardSpace.ScatterMargins;            // D9 판정 마진 (Flat 0.2 / Ledge 0)
        EdgeMargins lines = EdgeMargins.For(kinds, 0f, 0f);      // 보이는 가장자리

        for (int i = 0; i < n; i++)
        {
            if (i > 0) delay += gaps[i] * gapScale;
            var p = new TossPlan { S = startBoard[i], F = boardTargets[i], IsNak = isNak[i], Delay = delay };

            Vector2 sf = p.F - p.S;
            float L = sf.magnitude;
            p.Dir = L > 1e-4f ? sf / L : Vector2.zero;

            // 첫 접촉점 계산 전용 체공(지터 없음) → 접근 속도 → 선행 거리
            float tNominal = Mathf.Lerp(TossTMin, TossTMax, Mathf.Clamp01(L / TossTRefDist));
            float v0 = L / tNominal;
            // 0.30·L 상한이 우선: 아주 가까운 목표에서 A가 S 뒤로 넘어가지 않게.
            float dNom = Mathf.Min(Mathf.Max(LandLeadK * v0, LandLeadMin), LandLeadMaxFrac * L);
            if (p.IsNak)
            {
                // 모서리 통일 규칙: Ledge 선을 하나라도 넘었으면 Ledge (계획서 §5-1 ③)
                p.Kind = BoardEdgeMath.ClassifyOutside(p.F, hw, hd, kinds, out BoardEdge rep);
                p.Edge = rep;
                if (p.Kind == EdgeKind.Ledge)
                    p.DPost = 0f;   // 착지하지 않는다 — 포물선 그대로 F까지(그 전에 Ledge 선에서 넘겨진다)
                else
                {
                    // Flat 낙: 첫 접촉점 A가 항상 낙 영역 안(판정 경계 교차점 이상)에 있도록 (§5-5, F12)
                    Vector2 xc = p.F;
                    if (BoardEdgeMath.TryFirstCrossing(p.S, p.F, hw, hd, judge, kinds, null, out float tx, out _))
                        xc = p.S + (p.F - p.S) * tx;
                    p.DPost = Mathf.Min(dNom, (p.F - xc).magnitude);
                }
            }
            else p.DPost = dNom;
            p.A = p.F - p.Dir * p.DPost;

            float flightDist = (p.A - p.S).magnitude;
            p.T = Mathf.Lerp(TossTMin, TossTMax, Mathf.Clamp01(flightDist / TossTRefDist))
                + FxRange(-TossTJitter, TossTJitter);
            p.H = TossGravity * p.T * p.T / 8f;

            if (p.IsLedgeNak)
            {
                // Flat 바깥을 지나다 Ledge 선을 넘는 경우도 이 교차가 잡는다 (Ledge 필터, 마진 0 = 보이는 가장자리)
                float tc = 1f;
                if (BoardEdgeMath.TryFirstCrossing(p.S, p.F, hw, hd, lines, kinds, EdgeKind.Ledge, out float t0, out BoardEdge ce))
                { tc = t0; p.Edge = ce; }
                p.TCross = tc * p.T;
            }

            if (!p.IsLedgeNak && p.DPost > 0f)
            {
                float ratio = dNom > 0f ? p.DPost / dNom : 0f;
                p.B1 = Bounce1Base * Mathf.Clamp(p.H / 1.2f, 0.8f, 1.2f) * ratio;
                p.T1 = 2f * Mathf.Sqrt(2f * p.B1 / TossGravity);
                p.B2 = Bounce2Ratio * p.B1;
                p.T2 = 2f * Mathf.Sqrt(2f * p.B2 / TossGravity);
                p.Slide = SlideDuration;
            }

            p.SpinDeg = fxRng.NextDouble() < 0.5 ? -SpinTurnDeg : SpinTurnDeg;
            p.Order = -1;
            plans[i] = p;
        }

        // 착지음 순서 = 첫 접촉 시각(출발 지연 + 체공) 순. 낙 돌은 순서에서 빠진다(§5-6).
        var safe = new List<int>();
        for (int i = 0; i < n; i++) if (!plans[i].IsNak) safe.Add(i);
        safe.Sort((a, b) => (plans[a].Delay + plans[a].T).CompareTo(plans[b].Delay + plans[b].T));
        for (int k = 0; k < safe.Count; k++) plans[safe[k]].Order = k;
        tossSafeCount = safe.Count;

        for (int i = 0; i < n; i++)
        {
            var p = plans[i];
            EditorLog($"[Scatter] plan i={i} nak={p.IsNak} kind={(p.IsNak ? p.Kind.ToString() : "-")} " +
                      $"edge={(p.IsNak ? p.Edge.ToString() : "-")} S={p.S:F2} A={p.A:F2} F={p.F:F2} " +
                      $"T={p.T:F3} H={p.H:F2} D={p.DPost:F3} B1={p.B1:F3} tc={(p.IsLedgeNak ? p.TCross : -1f):F3} " +
                      $"delay={p.Delay:F3} end={p.Delay + p.End:F3} order={p.Order}");
        }
#if UNITY_EDITOR
        DebugLastStaggerSum = n > 0 ? plans[n - 1].Delay : 0f;
#endif
        return plans;
    }

    /// <summary>돌 하나의 토스. 비행(단일 중력 포물선) → 첫 접촉 → 작게 두 번 튐 → 미끄러져 정확히 F에 정지.
    /// Flat 낙 돌도 같은 착지 패키지(바깥 면). Ledge 낙 돌은 Ledge 선을 넘는 프레임에 NakFallPresenter로 넘긴다.</summary>
    private IEnumerator TossStone(int i, int gen)
    {
        var p = tossPlans[i];
        var s = tossStones[i];
        float tau = -p.Delay;
        bool contact1 = false, contact2 = false, departed = false;

        while (true)
        {
            if (gen != scatterGen) yield break;
            tau += Time.deltaTime;
            if (tau < 0f) { yield return null; continue; }   // 아직 출발 전 — 손에서 떨어진 자리에 대기

            if (p.IsLedgeNak && tau >= p.TCross)
            {
                // Ledge 선을 넘는 프레임 — 포물선을 그대로 이어 판 아래로 (착지음 없음, 그림자 0.08초 소멸)
                // 넘기는 자세는 **정확히 교차 시각 τc**의 것(선 위), 이 프레임에 이미 지난 시간은 연출이 이어서 센다.
                // (프레임이 크면 선을 한참 지나서 넘기게 되어 그림자 소멸 위치가 밀린다)
                float tc = p.TCross;
                Vector2 b = Vector2.LerpUnclamped(p.S, p.A, tc / p.T);
                float hh = TossGravity * tc * (p.T - tc) * 0.5f;
                Vector2 vB = (p.F - p.S) / p.T;
                float vH = TossGravity * (p.T * 0.5f - tc);
                HandOffLedge(i, s, b, vB, hh, vH, p.Edge, tau - tc);
                yield break;
            }

            if (tau >= p.End) break;

            Vector2 bp; float h; Vector3 mul = Vector3.one; float rot;
            if (tau < p.T)
            {
                // 비행: 수평 등속 + 높이 h = G·τ·(T−τ)/2 (최고 G·T²/8, 단일 G = 무게의 일관성)
                float u = tau / p.T;
                bp = Vector2.Lerp(p.S, p.A, u);
                h = TossGravity * tau * (p.T - tau) * 0.5f;
                float k = Mathf.Abs(1f - 2f * u);   // |높이속도| / (G·T/2)
                mul = new Vector3(1f - StretchX * k, 1f + StretchY * k, 1f - StretchX * k);
                rot = p.SpinDeg * u;   // 첫 접촉(u=1)에 한 바퀴 = 똑바로
            }
            else
            {
                if (!contact1) { contact1 = true; OnTossContact(i, p, false); }
                float post = tau - p.T;
                rot = 0f;   // 접촉 후엔 똑바로 — 스쿼시가 화면 세로로 눌린다

                if (post < p.T1)
                {
                    float u = post / p.T1;
                    bp = p.A + p.Dir * (p.DPost * Bounce1Frac * u);
                    h = 4f * p.B1 * u * (1f - u);
                }
                else if (post < p.T1 + p.T2)
                {
                    if (!contact2) { contact2 = true; OnTossContact(i, p, true); }
                    float u = (post - p.T1) / p.T2;
                    bp = p.A + p.Dir * (p.DPost * (Bounce1Frac + Bounce2Frac * u));
                    h = 4f * p.B2 * u * (1f - u);
                }
                else
                {
                    float u = p.Slide > 0f ? Mathf.Clamp01((post - p.T1 - p.T2) / p.Slide) : 1f;
                    float e = 1f - (1f - u) * (1f - u);   // ease-out
                    bp = p.A + p.Dir * (p.DPost * (Bounce1Frac + Bounce2Frac + SlideFrac * e));
                    h = 0f;
                }

                // 스쿼시: 접촉 순간부터 짧게 (앞 30% 눌림 → 복귀). ≤6% (T1 확정)
                float sq1 = SquashCurve(post / Squash1Dur);
                float sq2 = p.T1 > 0f ? SquashCurve((post - p.T1) / Squash2Dur) : 0f;
                float dy = (1f - Squash1Y) * sq1 + (1f - Squash2Y) * sq2;
                float dxz = (Squash1XZ - 1f) * sq1 + (Squash2XZ - 1f) * sq2;
                mul = new Vector3(1f + dxz, 1f - dy, 1f + dxz);
            }

            s.SetBoardMotion(bp, h);   // 위치·원근 크기 파생
            if (!departed) { departed = true; s.SetFlightShadow(true); }   // M1-S6 (D2): 비행 그림자 — 보드 좌표가 생긴 뒤 켠다
            s.transform.localScale = Vector3.Scale(s.transform.localScale, mul); // 매 프레임 원근 크기에서 다시 곱함 → 누적 없음
            s.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            yield return null;
        }

        if (gen != scatterGen) yield break;
        FinishToss(i, natural: true);
    }

    /// <summary>Ledge 낙 돌을 낙하 연출로 넘긴다. 토스는 여기서 끝난다.</summary>
    private void HandOffLedge(int i, Stone s, Vector2 b, Vector2 vB, float h, float vH, BoardEdge edge, float startTau)
    {
        if (tossDone == null || tossDone[i]) return;
        tossDone[i] = true;
        EditorLog($"[Scatter] handoff i={i} edge={edge} b={b:F2} h={h:F2} t={Time.time - tossStartTime:F3}");
        var presenter = GameManager.Instance != null ? GameManager.Instance.NakPresenter : null;
        if (presenter != null) presenter.FallFromBoard(s, b, vB, h, vH, TossGravity, edge, startTau);
        activeTosses = Mathf.Max(0, activeTosses - 1);
    }

    /// <summary>토스 종료 처리 — 정상 종료와 ForceCompleteTosses가 같은 경로를 쓴다(최종 위치 = F 직접 대입).
    /// natural=true(정상 종료)일 때만 소리를 보충한다 — 큰 프레임 튐으로 접촉 구간을 건너뛰어도 첫 접촉음은 난다.
    /// 강제 종료(스테이지 전환·실패 확정)에서는 소리를 내지 않는다.</summary>
    private void FinishToss(int i, bool natural)
    {
        if (tossDone == null || tossDone[i]) return;
        tossDone[i] = true;
        var p = tossPlans[i];
        var s = tossStones[i];
        if (s != null)
        {
            if (p.IsNak && p.Kind == EdgeKind.Flat)
            {
                // Flat 낙: 바깥 면에 앉는다 — InHand·키네마틱 유지, 그림자 유지(그 면에 깔림)
                if (natural && !TossContactPlayed(i)) OnTossContact(i, p, false);
                s.SetBoardMotion(p.F, 0f);
                s.transform.rotation = Quaternion.identity;
                EditorLog($"[Scatter] final i={i} kind=Flat diff={(s.BoardPos - p.F).magnitude:F4} t={Time.time - tossStartTime:F3}");
                if (natural) GameManager.Instance?.NotifyNakStoneEnded();
            }
            else if (p.IsNak)
            {
                // Ledge 낙인데 넘기기 전에 강제 종료된 경우만 여기 온다(실패 확정·스테이지 전환)
                s.SetBoardMotion(p.F, 0f);
                s.SetFlightShadow(false);
            }
            else
            {
                if (natural && !TossContactPlayed(i)) OnTossContact(i, p, false);
                s.SetBoardMotion(p.F, 0f);   // 정지: 직접 대입 → boardTargets와 diff 0
                s.transform.rotation = Quaternion.identity;
                s.SetState(Stone.State.OnBoard);
                s.SetFlightShadow(false);   // (SetState가 이미 초기화 — 명시)
                s.Rb.linearVelocity = Vector3.zero;
                s.Rb.angularVelocity = Vector3.zero;
                s.Rb.isKinematic = true;   // 드리프트 방지 최종 고정 (줍힐 때 InHand로 전환됨)
                float diff = (s.BoardPos - p.F).magnitude;
#if UNITY_EDITOR
                DebugLastMaxDiff = Mathf.Max(DebugLastMaxDiff, diff);
#endif
                EditorLog($"[Scatter] final i={i} diff={diff:F4} t={Time.time - tossStartTime:F3}");
            }
        }
        activeTosses = Mathf.Max(0, activeTosses - 1);
    }

    private bool[] tossContact;
    private bool TossContactPlayed(int i) => tossContact != null && tossContact[i];

    /// <summary>접촉 순간 — 안착 돌: 첫 접촉 = 음계 착지음 + 먼지, 둘째 접촉 = 같은 음 작게(n ≤ 5일 때만).
    /// Flat 낙 돌: 둔탁음(첫 접촉 + 둘째는 n ≤ 5만) + 먼지. Ledge 낙 돌은 접촉하지 않는다(착지음 없음).</summary>
    private void OnTossContact(int i, TossPlan p, bool second)
    {
        var audio = AudioManager.Instance;
        if (second)
        {
            if (p.IsNak)
            {
                if (audio != null) audio.PlayScatterThud(true, tossPlans.Length);   // n>5면 내부에서 무음
                return;
            }
            if (tossPlans.Length > 5) return;   // 보이스 예산: n=10이면 둘째 접촉음 없음
            float p2 = audio != null ? audio.PlayScatterLand(p.Order, tossSafeCount, true) : 0f;
            EditorLog($"[Scatter] land i={i} contact=2 order={p.Order} pitch={p2:F3}");
            return;
        }
        if (tossContact == null || tossContact.Length != tossPlans.Length) tossContact = new bool[tossPlans.Length];
        if (tossContact[i]) return;
        tossContact[i] = true;

        if (p.IsNak)
        {
            // Flat 낙만 접촉한다(Ledge는 선에서 넘겨짐). 둔탁음 + 먼지
            float tp = audio != null ? audio.PlayScatterThud(false, tossPlans.Length) : 0f;
            scatterFx?.SpawnDust(p.A, fxRng);
            EditorLog($"[Scatter] land i={i} contact=1 kind=Flat pitch={tp:F3} t={Time.time - tossStartTime:F3}");
            return;
        }
        float pitch = audio != null ? audio.PlayScatterLand(p.Order, tossSafeCount, false) : 0f;
        scatterFx?.SpawnDust(p.A, fxRng);   // 먼지는 첫 접촉에만 (T1)
        EditorLog($"[Scatter] land i={i} contact=1 order={p.Order} pitch={pitch:F3} t={Time.time - tossStartTime:F3}");
    }

    /// <summary>돌 하나마다 찍는 검증 로그 — 에디터에서만. Conditional이라 빌드에선 호출과 문자열 생성이 함께 빠진다.</summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void EditorLog(string msg) => Debug.Log(msg);

    private static float SquashCurve(float x)
    {
        if (x <= 0f || x >= 1f) return 0f;
        return x < SquashRise ? x / SquashRise : 1f - Mathf.SmoothStep(0f, 1f, (x - SquashRise) / (1f - SquashRise));
    }

    private float FxRange(float a, float b) => a + (float)fxRng.NextDouble() * (b - a);

    /// <summary>미완료 토스를 전부 최종 자세(F, h=0)로 맞추고 뿌리기 코루틴을 멈춘다.
    /// 스테이지 전환(ResetGauge)과 실패 확정(S7 FinishNakWait)이 부른다 — 리셋된 돌을
    /// 남은 코루틴이 다시 움직이거나, 지난 뿌리기가 새 스테이지에 SetPhase를 거는 것을 막는다.</summary>
    public void ForceCompleteTosses()
    {
        bool hadWork = scatterRoutine != null || activeTosses > 0;
        scatterGen++;   // 남은 코루틴은 다음 프레임에 스스로 빠진다(아래 Stop과 이중 안전)
        if (scatterRoutine != null) { StopCoroutine(scatterRoutine); scatterRoutine = null; }
        foreach (var r in tossRoutines) if (r != null) StopCoroutine(r);
        tossRoutines.Clear();
        if (tossDone != null && tossPlans != null && tossStones != null)
        {
            for (int i = 0; i < tossDone.Length; i++)
            {
                if (tossDone[i]) continue;
                var s = tossStones[i];
                if (s == null) { tossDone[i] = true; continue; }
                FinishToss(i, natural: false);
            }
        }
        activeTosses = 0;
        scatterFx?.ClearAll();
        if (hadWork) Debug.Log("[Scatter] ForceCompleteTosses");
    }

    /// <summary>
    /// 산개 중심 uv — 링 프리뷰(UpdateRing)와 실제 목표(DoScatter)가 **반드시 이 하나**를 쓴다.
    ///
    /// 손이 보드 **안**이면: 기존대로 [0.12, 0.88] 가장자리 클램프(가장자리 조준의 낙 위험은
    /// 게이지 선택의 의미이므로 보존 — 밸런스 불변).
    /// 손이 보드 **밖**이면(특히 하늘 — 스토리 멘트를 클릭한 직후가 딱 이 상황): 그 축만
    /// 중앙(0.5)으로 되돌린다. 예전처럼 가장자리 줄(0.12)로 클램프하면 중심이 뒷변에 박혀
    /// 반경 대부분이 보드 밖 = 낙 확률 급등 — 조준한 적도 없는 자리로 뿌려져 죽는 함정이었다
    /// (2026-10-17 직접 플레이 검증). 링이 중앙으로 점프하므로 플레이어에게도 즉시 보인다.
    /// </summary>
    private Vector2 ScatterCenterUV()
    {
#if UNITY_EDITOR
        if (debugHasCenterUV) return debugCenterUV;   // S0 기준값 캡처 전용
#endif
        if (handController == null) return new Vector2(0.5f, 0.5f);
        Vector2 raw = BoardUV(new Vector2(handController.transform.position.x,
                                          handController.transform.position.y));
        return new Vector2(
            (raw.x < 0f || raw.x > 1f) ? 0.5f : Mathf.Clamp(raw.x, 0.12f, 0.88f),
            (raw.y < 0f || raw.y > 1f) ? 0.5f : Mathf.Clamp(raw.y, 0.12f, 0.88f));
    }

    /// <summary>보드 사다리꼴 역매핑 (앞/뒤 변 수평 가정 — 이 게임 보드 구조). world → uv.
    /// 산개 중심을 커서(손) 위치로 옮기기 위한 순정 역함수. clamp는 호출부에서.</summary>
    private static Vector2 BoardUV(Vector2 p)
    {
        Vector2 bl = BoardBounds.QuadPoint(0f, 0f), fl = BoardBounds.QuadPoint(0f, 1f);
        Vector2 br = BoardBounds.QuadPoint(1f, 0f), fr = BoardBounds.QuadPoint(1f, 1f);
        float v = Mathf.Approximately(fl.y, bl.y) ? 0.5f : (p.y - bl.y) / (fl.y - bl.y);
        float leftX  = Mathf.Lerp(bl.x, fl.x, v);
        float rightX = Mathf.Lerp(br.x, fr.x, v);
        float u = Mathf.Approximately(rightX, leftX) ? 0.5f : (p.x - leftX) / (rightX - leftX);
        return new Vector2(u, v);
    }

    /// <summary>보드 폴리곤 bilinear 매핑. u,v∈[0,1]=내부. LerpUnclamped라 밖이면 외삽(낙 목표).
    /// 코너는 BoardBounds.QuadPoint(0/1,0/1)에서 얻어 게임의 실제 보드(quad/AABB)를 그대로 따름.</summary>
    private static Vector2 TrapPoint(float u, float v, Vector2 bl, Vector2 br, Vector2 fl, Vector2 fr)
    {
        Vector2 back  = Vector2.LerpUnclamped(bl, br, u);
        Vector2 front = Vector2.LerpUnclamped(fl, fr, u);
        return Vector2.LerpUnclamped(back, front, v);
    }
}
