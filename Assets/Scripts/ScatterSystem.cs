using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private float gaugeSpeed = 2f;
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
    private const float RMinUV       = 0.14f; // 게이지 0: 손 근처 뭉침(uv 중심±0.14)
    private const float RMaxUV       = 0.60f; // 게이지 1: uv 중심±0.60 → 일부 [0,1] 밖 = 낙
    private const float TossDuration = 0.42f; // 돌 하나 비행 시간(초)
    private const float TossStagger  = 0.035f; // 돌 간 출발 지연(캐스케이드 느낌)
    private const float LiftHeight   = 0.55f; // 비행 중 아치 최고 높이(화면 Y)
    private const float TargetSep    = 0.7f;  // 안착 목표 최소 간격(world)
    private const float NakMargin    = 0.25f; // 이만큼 사다리꼴 밖으로 나가야 낙(경계 애매낙 방지)

    private bool gaugeGoingUp = true;
    private InputAction pressAction;
    private HandController handController;
    private ScatterRangeIndicator rangeIndicator;

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

        GaugeBarUI.Instance?.SetValue(currentGaugeValue);
        rangeIndicator?.UpdateRing(Mathf.Lerp(RMinUV, RMaxUV, currentGaugeValue));
        AudioManager.Instance?.PlayGaugeTick();
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
        if (GameManager.Instance.IsTransitioning) return;
        if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Scatter) return;

        if (waitingForPress)
        {
            waitingForPress = false;
            isGaugeActive = true;
            currentGaugeValue = 0f;
            gaugeGoingUp = true;
            rangeIndicator?.Show();
            Debug.Log("[ScatterSystem] Gauge started!");
        }
    }

    private void OnPressReleased(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsPaused) return;
        if (GameManager.Instance.IsTransitioning) return;
        if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Scatter) return;
        if (!isGaugeActive) return;

        isGaugeActive = false;
        GaugeBarUI.Instance?.Hide();
        rangeIndicator?.Hide();
        AudioManager.Instance?.PlayGaugeConfirm();
        // v14: 게이지 값을 그대로 넘김. 퍼짐/낙은 DoScatter에서 코루틴 토스로 결정.
        float gauge = currentGaugeValue;
        TestLogger.Instance?.LogScatter(gauge, currentGaugeValue);
        Debug.Log($"[ScatterSystem] Scatter gauge: {gauge:F2}");

        handController?.PlayScatterFlick();
        StartCoroutine(DoScatter(gauge));
    }

    /// <summary>v14: 코루틴 토스 산개. 손 중앙 → 보드 좌표 목표까지 아치 애니메이션.
    /// 목표 uv가 [0,1] 밖인 돌은 보드 밖으로 던져져 낙.</summary>
    private IEnumerator DoScatter(float gauge)
    {
        var stones = GameManager.Instance.Stones;
        int n = stones != null ? stones.Length : 0;
        if (n == 0)
        {
            GameManager.Instance.SetPhase(GameManager.GamePhase.PickThrowStone);
            yield break;
        }

        float spreadT = Mathf.Clamp01(gauge);

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
        //   게임 낙 감시(IsOutsideMat)와 "동일 폴리곤" → placement·낙·play감시 3자 완전 일치.
        //   QuadPoint로 4꼭짓점을 얻어 LerpUnclamped bilinear(TrapPoint)로 매핑 → uv>1은 보드 밖 외삽(낙 목표).
        Vector2 cBL = BoardBounds.QuadPoint(0f, 0f);
        Vector2 cBR = BoardBounds.QuadPoint(1f, 0f);
        Vector2 cFL = BoardBounds.QuadPoint(0f, 1f);
        Vector2 cFR = BoardBounds.QuadPoint(1f, 1f);

        for (int i = 0; i < n; i++)
        {
            float angle = (slotAngleStep * slots[i] + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(RMinUV, RMaxUV, spreadT) * Random.Range(0.82f, 1.18f);
            float u = 0.5f + Mathf.Cos(angle) * radius;
            float v = 0.5f + Mathf.Sin(angle) * radius;

            // provisional (분리 그룹핑용). 최종 낙은 아래에서 IsOutsideMat로 재판정.
            isNak[i] = (u < 0f || u > 1f || v < 0f || v > 1f);
            targets[i] = TrapPoint(u, v, cBL, cBR, cFL, cFR);

            startPos[i] = new Vector2(stones[i].transform.position.x, stones[i].transform.position.y);
        }

        // ── 2) 안착 목표 최소 간격 보장 (낙 목표는 건드리지 않음 — 어차피 보드 밖) ──
        for (int pass = 0; pass < 8; pass++)
        {
            bool adjusted = false;
            for (int a = 0; a < n; a++)
            {
                if (isNak[a]) continue;
                for (int b = a + 1; b < n; b++)
                {
                    if (isNak[b]) continue;
                    Vector2 diff = targets[a] - targets[b];
                    float dist = diff.magnitude;
                    if (dist < TargetSep && dist > 0.001f)
                    {
                        Vector2 push = diff.normalized * (TargetSep - dist) * 0.5f;
                        targets[a] += push;
                        targets[b] -= push;
                        adjusted = true;
                    }
                }
            }
            if (!adjusted) break;
        }

        // ── 2.5) 최종 낙 판정: 게임 감시와 "완전히 동일한" IsOutsideMat 사용 → scatter 낙 = play 낙 100% 일치.
        // 분리 패스가 밀어낸 것도 반영. IsOutsideMat 자체 마진(0.2)이 경계 애매낙을 흡수.
        for (int i = 0; i < n; i++)
            isNak[i] = BoardBounds.IsOutsideMat(targets[i], 0.2f);

        // ── 3) 준비: 모든 돌 InHand(kinematic) + 콜라이더 반경 + 랜덤 스핀 ──
        var spin = new float[n];
        for (int i = 0; i < n; i++)
        {
            stones[i].SetState(Stone.State.InHand); // kinematic, 콜라이더 off
            stones[i].transform.SetParent(null); // 손에서 분리 (world 위치 유지 → 손 위치가 출발점)
            var col = stones[i].GetComponent<SphereCollider>();
            if (col != null) col.radius = 0.9f;
            spin[i] = Random.Range(180f, 540f) * (Random.value < 0.5f ? -1f : 1f);
        }

        // ── 4) 코루틴 토스 애니메이션 (캐스케이드 stagger) ──
        var landed = new bool[n];
        var nakStones = new List<Stone>();
        float total = TossDuration + TossStagger * (n - 1);
        float elapsed = 0f;

        while (elapsed < total + 0.02f)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                if (landed[i]) continue;
                float st = elapsed - i * TossStagger;
                if (st <= 0f) continue;

                float t = Mathf.Clamp01(st / TossDuration);
                float te = 1f - (1f - t) * (1f - t); // ease-out (던진 뒤 감속)
                Vector2 p = Vector2.Lerp(startPos[i], targets[i], te);
                float lift = Mathf.Sin(t * Mathf.PI) * LiftHeight; // 아치

                stones[i].transform.position = new Vector3(p.x, p.y + lift, 0f);
                stones[i].transform.rotation = Quaternion.Euler(0f, spin[i] * t, 0f);

                if (t >= 1f)
                {
                    landed[i] = true;
                    stones[i].transform.position = new Vector3(targets[i].x, targets[i].y, 0f);
                    AudioManager.Instance?.PlayScatterHit(i);

                    if (isNak[i])
                    {
                        nakStones.Add(stones[i]); // 보드 밖 도달 → 아래 낙 처리
                    }
                    else
                    {
                        stones[i].SetState(Stone.State.OnBoard); // 상태만 OnBoard
                        stones[i].Rb.linearVelocity = Vector3.zero;
                        stones[i].Rb.angularVelocity = Vector3.zero;
                        // v14: 안착 후 kinematic 고정 → 콜라이더 겹침 물리 사출/드리프트 차단.
                        // (드리프트로 SafeZone 밖으로 밀려 플레이 중 낙나던 버그 해결. 줍힐 때 InHand로 전환됨.)
                        stones[i].Rb.isKinematic = true;
                    }
                }
            }
            yield return null;
        }

        // ── 5) 낙 처리 (보드 밖 목표에 도달한 돌을 가장자리 너머로 떨어뜨림) ──
        if (nakStones.Count > 0)
        {
            AudioManager.Instance?.PlayOutOfBounds();
            TestLogger.Instance?.LogFailure("scatter_out_of_bounds");

            var wall = GameObject.Find("BoardBottomWall");
            bool wallWasActive = wall != null && wall.activeSelf;
            if (wall != null) wall.SetActive(false);

            foreach (var s in nakStones)
            {
                s.SetState(Stone.State.InAir);       // 중력 ON
                s.Rb.linearVelocity = new Vector3(0f, -4f, 0f); // 아래로 떨어짐
            }

            yield return new WaitForSeconds(1.0f);

            if (wall != null) wall.SetActive(wallWasActive);

            GameManager.Instance.SetFailReason("낙!");
            GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
        }
        else
        {
            Debug.Log("[ScatterSystem] Scatter complete. All stones on board.");
            GameManager.Instance.CurrentGimmick?.OnScatterComplete(stones);
            GameManager.Instance.SetPhase(GameManager.GamePhase.PickThrowStone);
        }
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
