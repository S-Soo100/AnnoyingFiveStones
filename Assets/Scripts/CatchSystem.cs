using UnityEngine;

/// <summary>
/// 받기 시스템: 하강하는 돌과 손의 거리로 캐치 판정.
/// 기획서: "바닥 돌 남음 → 같은 돌로 다시 던지기 / 전부 제거 → 다음 단"
/// </summary>
public class CatchSystem : MonoBehaviour
{
    [Header("Catch Settings")]
    [SerializeField] private float catchRadius = 2f;

    [Header("State")]
    [SerializeField] private bool isCatchPhase;

    private HandController handController;
    private Stone thrownStone;
    private float lastStoneY;
    private bool stoneDescending;

    public bool IsCatchPhase => isCatchPhase;

    private void Start()
    {
        handController = FindFirstObjectByType<HandController>();
    }

    private void Update()
    {
        if (!isCatchPhase) return;
        if (thrownStone == null) return;

        float currentY = thrownStone.transform.position.y;

        if (currentY < lastStoneY - 0.01f)
            stoneDescending = true;
        lastStoneY = currentY;

        if (!stoneDescending) return;

        // 받기 판정
        float dist = Vector2.Distance(
            new Vector2(handController.transform.position.x, handController.transform.position.y),
            new Vector2(thrownStone.transform.position.x, thrownStone.transform.position.y)
        );

        if (dist <= catchRadius)
        {
            OnCatchSuccess();
        }
    }

    public void BeginCatch(Stone thrown)
    {
        thrownStone = thrown;
        isCatchPhase = true;
        stoneDescending = false;
        lastStoneY = thrown.transform.position.y;
        Debug.Log("[CatchSystem] Catch phase started!");
    }

    public void StopCatch()
    {
        isCatchPhase = false;
        thrownStone = null;
    }

    private void OnCatchSuccess()
    {
        // === B1+B2: 미달 검증 (v3) ===
        int picked = handController.PickedStones.Count;
        int required = GameManager.Instance.RequiredPickCount;

        // 보드에 남은 돌 수 (picked stones는 이미 InHand이므로 OnBoard 카운트에 포함 안 됨)
        int remainingOnBoard = 0;
        foreach (var stone in GameManager.Instance.Stones)
        {
            if (stone.CurrentState == Stone.State.OnBoard)
                remainingOnBoard++;
        }

        // 실제 필요 수량 = min(required, 줍기 시점의 보드 돌 수)
        int stonesAvailableAtPickTime = remainingOnBoard + picked;
        int actualRequired = Mathf.Min(required, stonesAvailableAtPickTime);

        if (picked < actualRequired)
        {
            StopCatch();
            AudioManager.Instance?.PlayCatchFail();
            Debug.Log($"[CatchSystem] UNDERPICK FAIL: picked={picked}, required={actualRequired} (req={required}, boardRemain={remainingOnBoard})");
            TestLogger.Instance?.LogFailure($"underpick_{picked}_of_{actualRequired}");
            GameManager.Instance.SetFailReason("돌을 덜 주웠다!");
            GameManager.Instance.SetPhase(GameManager.GamePhase.Failed);
            return;
        }
        // === 미달 검증 끝 ===

        isCatchPhase = false;
        AudioManager.Instance?.PlayCatchSuccess();
        TestLogger.Instance?.LogCatch(true, 0f);

        // 던진 돌 = 캐치 완료, 손에 복귀
        thrownStone.SetState(Stone.State.Caught);
        thrownStone.transform.SetParent(handController.transform);
        thrownStone.transform.localPosition = Vector3.zero;
        Debug.Log("[CatchSystem] CATCH SUCCESS!");

        // 주운 돌들은 "수거됨" — 비활성화 (보드에서 제거)
        foreach (var stone in handController.PickedStones)
        {
            stone.transform.SetParent(null);
            stone.SetState(Stone.State.Caught);
            stone.gameObject.SetActive(false);
        }

        // remainingOnBoard는 위에서 이미 계산됨
        Debug.Log($"[CatchSystem] Remaining on board: {remainingOnBoard}");

        if (remainingOnBoard == 0)
        {
            // 모든 돌 제거 → 단계 클리어
            GameManager.Instance.SetPhase(GameManager.GamePhase.StageComplete);
        }
        else
        {
            // 돌 남음 → 같은 던지기 돌로 다시 던지기 (Throw 직행)
            // picked만 클리어, throwStone은 손에 유지
            handController.ClearPickedButKeepThrow();

            GameManager.Instance.SetPhase(GameManager.GamePhase.Throw);
        }
    }
}
