using System.Collections;
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
    [SerializeField] private float minScatterForce = 0.5f;
    [SerializeField] private float maxScatterForce = 2.5f;

    [Header("Scatter Settings")]
    [SerializeField] private float dropHeight = 1.5f;       // 보드 중앙 위로 이 높이에서 떨어뜨림
    [SerializeField] private float settleSpeedThreshold = 0.1f; // 이 속도 이하면 안착 간주
    [SerializeField] private float settleTimeout = 4f;       // 최대 안착 대기 시간

    [Header("Board (auto-resolved)")]
    [SerializeField] private Vector2 boardSize = new Vector2(8f, 8f);

    [Header("State")]
    [SerializeField] private float currentGaugeValue;
    [SerializeField] private bool isGaugeActive;

    private Transform boardTransform;
    private bool gaugeGoingUp = true;
    private InputAction pressAction;

    public float CurrentGaugeValue => currentGaugeValue;
    public bool IsGaugeActive => isGaugeActive;

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
    }

    private bool waitingForPress;

    public void BeginScatter()
    {
        // 돌들을 보드 중앙에 모아놓기
        float boardCenterY = boardTransform != null ? boardTransform.position.y : -4f;
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

        Debug.Log("[ScatterSystem] Ready. Long-press to start gauge.");
    }

    private void OnPressStarted(InputAction.CallbackContext ctx)
    {
        if (GameManager.Instance == null) return;
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
        if (GameManager.Instance.IsTransitioning) return;
        if (GameManager.Instance.CurrentPhase != GameManager.GamePhase.Scatter) return;
        if (!isGaugeActive) return;

        isGaugeActive = false;
        float power = Mathf.Lerp(minScatterForce, maxScatterForce, currentGaugeValue);
        TestLogger.Instance?.LogScatter(power, currentGaugeValue);
        Debug.Log($"[ScatterSystem] Scatter power: {power:F2} (gauge: {currentGaugeValue:F2})");

        StartCoroutine(DoScatter(power));
    }

    private IEnumerator DoScatter(float power)
    {
        var stones = GameManager.Instance.Stones;
        float boardCenterY = boardTransform != null ? boardTransform.position.y : -4f;

        for (int i = 0; i < stones.Length; i++)
        {
            var stone = stones[i];
            stone.SetState(Stone.State.OnBoard);

            // 보드 중앙에서 시작 (돌끼리 겹치지 않게 약간 오프셋)
            float offsetX = (i - 2) * 0.15f;
            float offsetY = ((i % 2) - 0.5f) * 0.15f;
            stone.transform.position = new Vector3(offsetX, boardCenterY + offsetY, 0f);
            stone.Rb.linearVelocity = Vector3.zero;
            stone.Rb.angularVelocity = Vector3.zero;

            // 탁자 위에서 X/Y 방향으로 퍼짐 (탑뷰 느낌, 중력 없음)
            float spreadX = Random.Range(-1f, 1f) * power;
            float spreadY = Random.Range(-0.6f, 0.6f) * power;

            Vector3 force = new Vector3(spreadX, spreadY, 0f);
            stone.Rb.AddForce(force, ForceMode.Impulse);
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

        // 장외 체크 (2D 기준: X, Y만 비교)
        Vector2 boardCenter = new Vector2(
            boardTransform != null ? boardTransform.position.x : 0f,
            boardTransform != null ? boardTransform.position.y : -4f
        );
        Vector2 halfSize = boardSize * 0.5f;

        bool anyOutOfBounds = false;
        foreach (var stone in stones)
        {
            Vector2 stonePos = new Vector2(stone.transform.position.x, stone.transform.position.y);
            bool outX = stonePos.x < boardCenter.x - halfSize.x || stonePos.x > boardCenter.x + halfSize.x;
            bool outY = stonePos.y < boardCenter.y - halfSize.y || stonePos.y > boardCenter.y + halfSize.y;

            if (outX || outY)
            {
                Debug.Log($"[ScatterSystem] Stone {stone.StoneIndex} out of bounds at ({stonePos.x:F2}, {stonePos.y:F2})");
                TestLogger.Instance?.LogPhysics("out_of_bounds",
                    $"stone={stone.StoneIndex} pos=({stonePos.x:F2},{stonePos.y:F2}) board_center=({boardCenter.x:F2},{boardCenter.y:F2}) board_half=({halfSize.x:F2},{halfSize.y:F2})");
                anyOutOfBounds = true;
            }
        }

        if (anyOutOfBounds)
        {
            TestLogger.Instance?.LogFailure("scatter_out_of_bounds");
            GameManager.Instance.SetFailReason("돌이 밖으로 나갔다!");
            GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
        }
        else
        {
            Debug.Log("[ScatterSystem] Scatter complete. All stones on board.");
            GameManager.Instance.SetPhase(GameManager.GamePhase.PickThrowStone);
        }
    }
}
