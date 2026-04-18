using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 뿌리기 시스템: 게이지 왕복 → 놓는 순간의 높이로 산개 세기 결정.
/// 돌을 보드 중앙 위에서 살짝 띄운 뒤 수평으로 흩뿌린다.
/// </summary>
public class ScatterSystem : MonoBehaviour
{
    [Header("Gauge Settings")]
    [SerializeField] private float gaugeSpeed = 2f;
    [SerializeField] private float minScatterForce = 1.0f;
    [SerializeField] private float maxScatterForce = 3.0f;

    [Header("Scatter Settings")]
    [SerializeField] private float baseSpreadRadius = 0.9f;  // 게이지 0%에서도 최소 퍼짐 반지름
    [SerializeField] private float minStoneSeparation = 0.8f; // 돌 사이 최소 간격 (units)
    [SerializeField] private float dropHeight = 1.5f;        // 보드 중앙 위로 이 높이에서 떨어뜨림
    [SerializeField] private float settleSpeedThreshold = 0.1f; // 이 속도 이하면 안착 간주
    [SerializeField] private float settleTimeout = 4f;       // 최대 안착 대기 시간

    [Header("Board (auto-resolved)")]
    [SerializeField] private Vector2 boardSize = new Vector2(9.6f, 6.1f);

    [Header("State")]
    [SerializeField] private float currentGaugeValue;
    [SerializeField] private bool isGaugeActive;

    private Transform boardTransform;
    private bool gaugeGoingUp = true;
    private InputAction pressAction;

    public float CurrentGaugeValue => currentGaugeValue;
    public bool IsGaugeActive => isGaugeActive;

    /// <summary>스테이지 전환 시 게이지 상태 강제 리셋</summary>
    public void ResetGauge()
    {
        isGaugeActive = false;
        waitingForPress = false;
        currentGaugeValue = 0f;
        GaugeBarUI.Instance?.Hide();
    }

    private void Awake()
    {
        pressAction = new InputAction("Press", InputActionType.Button);
        pressAction.AddBinding("<Mouse>/leftButton");
        pressAction.AddBinding("<Touchscreen>/primaryTouch/press");

        boardTransform = GameObject.Find("Cloth")?.transform;
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
        AudioManager.Instance?.PlayGaugeTick();
    }

    private bool waitingForPress;

    public void BeginScatter()
    {
        // 돌들을 보드 중앙에 모아놓기
        float boardCenterY = boardTransform != null ? boardTransform.position.y : -5.25f;
        var stones = GameManager.Instance.Stones;
        foreach (var stone in stones)
        {
            stone.SetState(Stone.State.InHand);
            stone.transform.position = new Vector3(0f, boardCenterY, 0f);
        }

        // 롱프레스 대기 (아직 게이지 시작 안 함)
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
        if (GameManager.Instance.IsPaused) return; // [P4]
        if (GameManager.Instance.IsTransitioning) return;
        if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Scatter) return;

        if (waitingForPress)
        {
            // 롱프레스 시작 = 게이지 왕복 시작
            waitingForPress = false;
            isGaugeActive = true;
            currentGaugeValue = 0f;
            gaugeGoingUp = true;
            Debug.Log("[ScatterSystem] Gauge started!");
        }
    }

    private void OnPressReleased(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsPaused) return; // [P4]
        if (GameManager.Instance.IsTransitioning) return;
        if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Scatter) return;
        if (!isGaugeActive) return;

        isGaugeActive = false;
        GaugeBarUI.Instance?.Hide();
        AudioManager.Instance?.PlayGaugeConfirm();
        // 제곱 커브: 낮은 게이지에서 미세 조절 가능, 상위에서 급격히 증가
        float curved = currentGaugeValue * currentGaugeValue;
        float power = Mathf.Lerp(minScatterForce, maxScatterForce, curved);
        TestLogger.Instance?.LogScatter(power, currentGaugeValue);
        Debug.Log($"[ScatterSystem] Scatter power: {power:F2} (gauge: {currentGaugeValue:F2})");

        StartCoroutine(DoScatter(power));
    }

    private IEnumerator DoScatter(float power)
    {
        var stones = GameManager.Instance.Stones;
        int stoneCount = stones.Length;
        float boardCenterY = boardTransform != null ? boardTransform.position.y : -5.25f;

        // 돌 개수에 따라 최소 간격 + 세기 동적 조정
        float dynamicMinSeparation = stoneCount <= 5 ? minStoneSeparation : minStoneSeparation * 0.4f;
        // 많은 돌: 힘을 줄여 보드 밖 낙 방지 (5개=1.0, 18개=0.45, 20개=0.4)
        float forceScale = stoneCount <= 5 ? 1f : Mathf.Lerp(0.6f, 0.35f, (stoneCount - 6f) / 14f);

        // 슬롯 개수: 돌의 2배 (최소 10)
        int slotCount = Mathf.Max(10, stoneCount * 2);
        int[] slots = new int[slotCount];
        for (int i = 0; i < slotCount; i++) slots[i] = i;
        // Fisher-Yates 셔플 후 앞 stoneCount개만 사용
        for (int i = slotCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        float slotAngleStep = 360f / slotCount;
        float ellipseRatio = boardSize.x / boardSize.y; // 가로로 긴 타원형 (9.6/7 ≈ 1.37)
        Vector2[] baseOffsets = new Vector2[stones.Length];
        for (int i = 0; i < stones.Length; i++)
        {
            float angle = (slotAngleStep * slots[i] + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            float radius = baseSpreadRadius * Random.Range(0.8f, 1.2f);
            baseOffsets[i] = new Vector2(Mathf.Cos(angle) * ellipseRatio, Mathf.Sin(angle)) * radius;
        }

        // 최소 간격 보장: 너무 가까운 돌 쌍이 있으면 밀어냄
        for (int pass = 0; pass < 10; pass++)
        {
            bool adjusted = false;
            for (int a = 0; a < stones.Length; a++)
            {
                for (int b = a + 1; b < stones.Length; b++)
                {
                    Vector2 diff = baseOffsets[a] - baseOffsets[b];
                    float dist = diff.magnitude;
                    if (dist < dynamicMinSeparation && dist > 0.001f)
                    {
                        Vector2 push = diff.normalized * (dynamicMinSeparation - dist) * 0.5f;
                        baseOffsets[a] += push;
                        baseOffsets[b] -= push;
                        adjusted = true;
                    }
                }
            }
            if (!adjusted) break;
        }

        for (int i = 0; i < stones.Length; i++)
        {
            var stone = stones[i];
            stone.SetState(Stone.State.OnBoard);

            // 보드 중앙 + 기본 오프셋에서 시작
            stone.transform.position = new Vector3(
                baseOffsets[i].x,
                boardCenterY + baseOffsets[i].y,
                0f
            );
            // Y축만 랜덤 회전 (X/Z 틸트는 물리가 자연스럽게 처리)
            // air_rock이 비대칭이라 Y 회전만으로도 다양한 모양
            stone.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // SphereCollider를 air_rock mesh에 맞게 확대 (시각 mesh가 바닥 아래로 돌출 방지)
            var col = stone.GetComponent<SphereCollider>();
            if (col != null) col.radius = 0.9f;

            stone.Rb.linearVelocity = Vector3.zero;
            stone.Rb.angularVelocity = Vector3.zero;

            // X/Y 방향으로 퍼짐 + 기본 오프셋 방향으로 약간 밀어줌
            float scaledPower = power * forceScale;
            float spreadX = Random.Range(-1f, 1f) * scaledPower + baseOffsets[i].x * 0.5f * forceScale;
            float spreadY = Random.Range(-0.6f, 0.6f) * scaledPower + baseOffsets[i].y * 0.5f * forceScale;

            Vector3 force = new Vector3(spreadX, spreadY, 0f);
            stone.Rb.AddForce(force, ForceMode.Impulse);

            // 회전 부여 (퍼지면서 굴러가는 느낌)
            float torqueScale = forceScale;
            stone.Rb.AddTorque(
                new Vector3(Random.Range(-3f, 3f) * torqueScale, Random.Range(-5f, 5f) * torqueScale, Random.Range(-3f, 3f) * torqueScale),
                ForceMode.Impulse
            );

            AudioManager.Instance?.PlayScatterHit(i);
        }

        // 안착 판정: 모든 돌의 속도가 threshold 이하가 될 때까지 대기
        float elapsed = 0f;
        while (elapsed < settleTimeout)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            bool allSettled = true;
            foreach (var stone in stones)
            {
                if (stone.Rb.linearVelocity.magnitude > settleSpeedThreshold)
                {
                    allSettled = false;
                    break;
                }
            }

            if (allSettled && elapsed > 0.5f) // 최소 0.5초는 대기
                break;
        }

        // 장외 체크 (2D 기준: X, Y만 비교) — SafeZone 기반
        var outOfBoundsStones = new List<Stone>();
        foreach (var stone in stones)
        {
            Vector2 stonePos = new Vector2(stone.transform.position.x, stone.transform.position.y);
            bool outX = stonePos.x < GameManager.SafeZoneMin.x || stonePos.x > GameManager.SafeZoneMax.x;
            bool outY = stonePos.y < GameManager.SafeZoneMin.y || stonePos.y > GameManager.SafeZoneMax.y;

            if (outX || outY)
            {
                Debug.Log($"[ScatterSystem] Stone {stone.StoneIndex} out of bounds at ({stonePos.x:F2}, {stonePos.y:F2})");
                TestLogger.Instance?.LogPhysics("out_of_bounds",
                    $"stone={stone.StoneIndex} pos=({stonePos.x:F2},{stonePos.y:F2}) safe_min=({GameManager.SafeZoneMin.x:F2},{GameManager.SafeZoneMin.y:F2}) safe_max=({GameManager.SafeZoneMax.x:F2},{GameManager.SafeZoneMax.y:F2})");
                outOfBoundsStones.Add(stone);
            }
        }

        if (outOfBoundsStones.Count > 0)
        {
            AudioManager.Instance?.PlayOutOfBounds();
            TestLogger.Instance?.LogFailure("scatter_out_of_bounds");

            // 낙 연출: InAir 상태 전환 + 하강 속도 → 허공 정지 방지
            var wall = GameObject.Find("BoardBottomWall");
            bool wallWasActive = wall != null && wall.activeSelf;
            if (wall != null) wall.SetActive(false);

            foreach (var s in outOfBoundsStones)
            {
                s.SetState(Stone.State.InAir);
                var v = s.Rb.linearVelocity;
                s.Rb.linearVelocity = new Vector3(v.x * 0.5f, Mathf.Min(v.y, -4f), 0f);
            }

            yield return new WaitForSeconds(1.0f);

            if (wall != null) wall.SetActive(wallWasActive);

            GameManager.Instance.SetFailReason("낙!");
            GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
        }
        else
        {
            Debug.Log("[ScatterSystem] Scatter complete. All stones on board.");
            // 기믹에 산란 완료 알림 (장애물과 겹치는 돌 보정 등)
            GameManager.Instance.CurrentGimmick?.OnScatterComplete(stones);
            GameManager.Instance.SetPhase(GameManager.GamePhase.PickThrowStone);
        }
    }
}
