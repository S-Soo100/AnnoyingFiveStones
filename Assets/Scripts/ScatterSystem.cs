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
    private const float RUvBase = 0.12f;  // g=0 (뭉침)
    private const float RUvSpan = 0.60f;  // g=1 → 0.72 (경계 0.50 초과 = 전낙)
    private static float RadiusUV(float g) => RUvBase + RUvSpan * Mathf.Clamp01(g);

    private const float TossDuration = 0.50f; // 돌 하나 비행 시간(초) — v15 여유있는 아치
    private const float TossStagger  = 0.05f; // 돌 간 출발 지연(캐스케이드 느낌)
    private const float LiftHeight   = 0.55f; // (v15 미사용 — per-stone liftH로 대체)
    private const float NakMargin    = 0.25f; // 이만큼 사다리꼴 밖으로 나가야 낙(경계 애매낙 방지)

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

        GaugeBarUI.Instance?.SetValue(currentGaugeValue);
        // 링 중심도 커서(손) 위치 — DoScatter의 centerUV와 동일 계산/clamp로 프리뷰 정합.
        Vector2 ringCenter = new Vector2(0.5f, 0.5f);
        if (handController != null)
        {
            ringCenter = BoardUV(new Vector2(handController.transform.position.x, handController.transform.position.y));
            ringCenter.x = Mathf.Clamp(ringCenter.x, 0.12f, 0.88f);
            ringCenter.y = Mathf.Clamp(ringCenter.y, 0.12f, 0.88f);
        }
        rangeIndicator?.UpdateRing(RadiusUV(currentGaugeValue), ringCenter);
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
            gaugeActiveTime = 0f;
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

        // 딸깍 방지: 너무 짧게 눌렀다 놓으면 던지지 않고 재대기(다음 Press에 다시 게이지 시작)
        if (gaugeActiveTime < MinGaugeHold)
        {
            isGaugeActive = false;
            currentGaugeValue = 0f;
            gaugeGoingUp = true;
            waitingForPress = true;
            GaugeBarUI.Instance?.SetValue(0f); // 바는 유지(재대기), 값만 0으로 — Hide하면 다음 Press에 안 켜져 사라짐
            rangeIndicator?.Hide();
            return;
        }

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
        Vector2 cBL = BoardBounds.QuadPoint(0f, 0f);
        Vector2 cBR = BoardBounds.QuadPoint(1f, 0f);
        Vector2 cFL = BoardBounds.QuadPoint(0f, 1f);
        Vector2 cFR = BoardBounds.QuadPoint(1f, 1f);

        // ★산개 중심 = 커서(손) 위치. 손이 없으면 보드 중앙(0.5,0.5) 폴백.
        //   테이블 밖 산개로 전-낙되는 것 방지 위해 [0.12,0.88]로 clamp(플레이 가능성 보존).
        //   밸런스(반경·간격·낙 판정)는 전부 불변 — 링/목표의 원점만 이동.
        Vector2 centerUV = new Vector2(0.5f, 0.5f);
        if (handController != null)
        {
            Vector2 handPos = new Vector2(handController.transform.position.x, handController.transform.position.y);
            centerUV = BoardUV(handPos);
            centerUV.x = Mathf.Clamp(centerUV.x, 0.12f, 0.88f);
            centerUV.y = Mathf.Clamp(centerUV.y, 0.12f, 0.88f);
        }

        // v15: 각도만 유기화(반경 매핑·낙 파이프라인 불변). 매 throw 방향 랜덤 + 지터 확대 + 뭉침 클럼프.
        float baseRot = Random.Range(0f, 360f); // 매 throw 방향 랜덤(단조 방어)
        float[] angDeg = new float[n];
        float[] rads = new float[n];
        for (int i = 0; i < n; i++)
        {
            angDeg[i] = slotAngleStep * slots[i] + baseRot + Random.Range(-22f, 22f); // 지터 ±12→±22
            rads[i]   = RadiusUV(gauge) * Random.Range(0.92f, 1.08f);                 // 반경 매핑 불변
            float a = angDeg[i] * Mathf.Deg2Rad;
            float u = centerUV.x + Mathf.Cos(a) * rads[i];
            float v = centerUV.y + Mathf.Sin(a) * rads[i];
            isNak[i] = (u < 0f || u > 1f || v < 0f || v > 1f);   // provisional
            targets[i] = TrapPoint(u, v, cBL, cBR, cFL, cFR);
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
            float u = centerUV.x + Mathf.Cos(a) * rads[i], v = centerUV.y + Mathf.Sin(a) * rads[i];
            isNak[i] = (u < 0f || u > 1f || v < 0f || v > 1f);
            targets[i] = TrapPoint(u, v, cBL, cBR, cFL, cFR);
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
                    Vector2 diff = targets[a] - targets[b];
                    float dist = diff.magnitude;
                    if (dist < sep && dist > 0.001f)
                    {
                        Vector2 push = diff.normalized * (sep - dist) * 0.5f;
                        targets[a] += push;
                        targets[b] -= push;
                        adjusted = true;
                    }
                }
            }
            if (!adjusted) break;
        }

        // ── 2.4) 우발낙 방어 (donts/game.md #19 SOT): 간격 확대가 비-낙 돌을 경계 밖으로 밀 수 있음.
        //   원래 uv 내부(provisional isNak==false)였는데 밀려난 돌만 보드 중심 C 쪽으로 회수.
        //   의도된 낙(provisional true)은 건드리지 않음.
        Vector2 C = TrapPoint(centerUV.x, centerUV.y, cBL, cBR, cFL, cFR);
        for (int i = 0; i < n; i++)
        {
            if (isNak[i]) continue; // 의도된 낙은 그대로
            int guard = 0;
            while (BoardBounds.IsOutsideMatStrict(targets[i], 0.2f) && guard++ < 10)
                targets[i] = Vector2.Lerp(targets[i], C, 0.18f);
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
        for (int i = 0; i < n; i++)
            isNak[i] = BoardBounds.IsOutsideMatStrict(targets[i], 0.2f);

        // v16: 놓는 즉시 던지기 — 윈드업 대기 제거. 아래 준비 루프에서 즉시 detach + startPos(현재 손=커서 위치)
        //   재캡처 + 토스 시작 → 유저 릴리스와 돌 발사 타이밍 일치(체감 딜레이 제거).
        //   손 캐스트 연출(DoScatterThrow)은 돌과 무관하게 병렬 진행.

        // ── 3) 준비: 모든 돌 InHand(kinematic) + 콜라이더 반경 + 랜덤 스핀 ──
        var spin = new float[n];
        var liftH = new float[n]; // per-stone 아치 최고 높이(화면 Y)
        for (int i = 0; i < n; i++)
        {
            stones[i].SetState(Stone.State.InHand); // kinematic, 콜라이더 off
            stones[i].transform.SetParent(null); // 손에서 분리 (world 위치 유지 → 손 위치가 출발점)
            startPos[i] = new Vector2(stones[i].transform.position.x, stones[i].transform.position.y); // 캐스트 끝 손위치
            var col = stones[i].GetComponent<SphereCollider>();
            if (col != null) col.radius = 0.9f;
            spin[i] = Random.Range(180f, 540f) * (Random.value < 0.5f ? -1f : 1f);
            liftH[i] = Random.Range(1.2f, 1.8f);
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
                float lift = Mathf.Sin(Mathf.Pow(t, 0.8f) * Mathf.PI) * liftH[i]; // 아치(피크 t≈0.4, 1.2~1.8)

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
                        // v15: 착지 여운(hop+squash) 후 kinematic 고정. SettleStone이 최종 isKinematic=true.
                        // (드리프트로 SafeZone 밖으로 밀려 플레이 중 낙나던 버그 해결. 줍힐 때 InHand로 전환됨.)
                        StartCoroutine(SettleStone(stones[i], targets[i]));
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
            // v15: 착지 여운(SettleStone 0.16s)이 끝나 모든 돌이 안착된 뒤 픽업 페이즈로.
            yield return new WaitForSeconds(0.16f);
            Debug.Log("[ScatterSystem] Scatter complete. All stones on board.");
            GameManager.Instance.CurrentGimmick?.OnScatterComplete(stones);
            GameManager.Instance.SetPhase(GameManager.GamePhase.PickThrowStone);
        }
    }

    /// <summary>v15 착지 여운: 안착 지점에서 1회 hop + squash·stretch 후 kinematic 고정.
    /// 경계 근처(board-out 위험)면 hop 생략하고 squash만 → 안착 후 밀려나 낙나는 것 방지.</summary>
    private IEnumerator SettleStone(Stone s, Vector2 target)
    {
        Vector3 baseScale = s.transform.localScale;
        // 경계 근처면 hop 생략(스쿼시만) — board-out 방어
        // v16: 위 2.5)와 동일 사유로 Strict 사용. 안착 연출이므로 "하늘 면제"를 받으면 안 된다
        //      (뒷변 너머에 앉는 돌이 면제를 받아 hop까지 튀던 것 차단).
        float hop = BoardBounds.IsOutsideMatStrict(target, 0.5f) ? 0f : 0.09f;
        float dur = 0.16f, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime; float u = Mathf.Clamp01(e / dur);
            float y = target.y + Mathf.Sin(u * Mathf.PI) * hop * (1f - u); // 1회 튐 후 감쇠
            s.transform.position = new Vector3(target.x, y, 0f);
            float sq = Mathf.Sin(u * Mathf.PI); // 0→1→0
            s.transform.localScale = new Vector3(baseScale.x * (1f + 0.15f * sq), baseScale.y * (1f - 0.20f * sq), baseScale.z);
            yield return null;
        }
        s.transform.position = new Vector3(target.x, target.y, 0f);
        s.transform.localScale = baseScale;
        s.Rb.isKinematic = true; // 드리프트 방지 최종 고정
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
